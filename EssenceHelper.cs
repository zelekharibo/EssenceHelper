using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.Shared;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;

namespace EssenceHelper
{
    public class EssenceHelper : BaseSettingsPlugin<Settings>
    {
        private readonly ConcurrentDictionary<RectangleF, bool?> _mouseStateForRect = new();
        private PoE2ScoutApiService? _apiService;
        private DateTime _lastApiUpdate = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, decimal> _essencePriceCache = new();
        private DateTime _lastEssenceCacheUpdate = DateTime.MinValue;
        private bool _isUpdatingEssencePrices = false;
        private readonly List<(string, Vector2)> _reusableEssencesList = new();
        private readonly HashSet<string> _reusableEssenceNames = new();
        private readonly List<(string name, decimal price)> _reusableEssencesWithPrices = new();
        
        private readonly StringComparison _essenceComparison = StringComparison.OrdinalIgnoreCase;
        
        public override bool Initialise()
        {
            try
            {
                // reset last update time to force fresh price fetch on every plugin start
                _lastEssenceCacheUpdate = DateTime.MinValue;
                
                // API integration is always enabled
                _apiService = new PoE2ScoutApiService(
                    Settings.LeagueName.Value,
                    LogMessage,
                    LogError
                );
                LogMessage("API service initialized successfully");
                
                // trigger immediate price update on plugin start
                _ = Task.Run(UpdateEssencePrices);
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize EssenceHelper: {ex.Message}");
                return false;
            }
        }


        public override void DrawSettings()
        {
            base.DrawSettings();
            
            if (ImGui.Button("Update Essence Prices Now"))
            {
                _ = Task.Run(UpdateEssencePrices);
            }
            
            ImGui.SameLine();
            var lastUpdateText = _lastEssenceCacheUpdate == DateTime.MinValue 
                ? "Never updated" 
                : $"Last updated: {_lastEssenceCacheUpdate:HH:mm:ss}";
            ImGui.Text(lastUpdateText);
            
            // show current data source
            var dataSource = Settings.UseNinjaPricerData.Value ? "NinjaPricer (Local)" : "PoE2Scout API";
            ImGui.Text($"Data source: {dataSource}");
            
            if (_essencePriceCache.Count > 0)
            {
                ImGui.Text($"Cached essences: {_essencePriceCache.Count}");
                ImGui.Separator();
                DrawEssencePriceList();
            }
        }

        private void DrawEssencePriceList()
        {
            if (ImGui.CollapsingHeader("Essence Prices", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // create a copy of the essence prices to avoid concurrent modification issues
                var essencesCopy = _essencePriceCache?.ToList() ?? new List<KeyValuePair<string, decimal>>();
                
                if (essencesCopy.Count == 0)
                {
                    ImGui.Text("No essence prices loaded");
                    return;
                }
                
                // sort by price descending for better usability
                essencesCopy.Sort((x, y) => y.Value.CompareTo(x.Value));
                
                // table headers
                if (ImGui.BeginTable("EssencePricesTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    ImGui.TableSetupColumn("Essence Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Price (exalts)", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableHeadersRow();
                    
                    for (var i = 0; i < essencesCopy.Count; i++)
                    {
                        // double-check that the item still exists in the original cache
                        if (!_essencePriceCache.ContainsKey(essencesCopy[i].Key))
                        {
                            break; // cache was modified, stop rendering
                        }
                        
                        ImGui.PushID($"EssencePrice{i}");
                        
                        try
                        {
                            ImGui.TableNextRow();
                            
                            // essence name column
                            ImGui.TableNextColumn();
                            ImGui.Text(essencesCopy[i].Key);
                            
                            // price column
                            ImGui.TableNextColumn();
                            ImGui.Text($"{essencesCopy[i].Value:F3}");
                        }
                        finally
                        {
                            ImGui.PopID();
                        }
                    }
                    
                    ImGui.EndTable();
                }
            }
        }


        public override void Render()
        {
            if (!Settings.Enable) return;

            // update essence prices if needed
            if (ShouldUpdateEssencePrices())
            {
                _ = Task.Run(UpdateEssencePrices);
            }

            // detect and display essence prices
            var essencesOnGround = GetEssencesOnGround();
            if (essencesOnGround.Any())
            {
                DisplayEssencePrices(essencesOnGround);
            }
        }

        private bool ShouldUpdateFromApi()
        {
            
            // try to get last update time from persistent settings
            var lastUpdate = GetLastApiUpdateFromSettings();
            
            var updateInterval = TimeSpan.FromMinutes(Settings.ApiUpdateInterval.Value);
            return DateTime.Now - lastUpdate >= updateInterval;
        }

        private DateTime GetLastApiUpdateFromSettings()
        {
            if (string.IsNullOrEmpty(Settings.LastApiUpdateTime))
            {
                // first time ever, force immediate update
                return DateTime.MinValue;
            }
            
            if (DateTime.TryParse(Settings.LastApiUpdateTime, out var lastUpdate))
            {
                _lastApiUpdate = lastUpdate; // sync the in-memory value
                return lastUpdate;
            }
            
            return DateTime.MinValue;
        }

        private void SaveLastApiUpdateTime()
        {
            _lastApiUpdate = DateTime.Now;
            Settings.LastApiUpdateTime = _lastApiUpdate.ToString("O"); // use ISO 8601 format
        }
        private bool ValidateApiSettings()
        {
            if (Settings?.LeagueName?.Value == null)
            {
                LogError("league name is not configured");
                return false;
            }
            
            return true;
        }

        private bool ShouldUpdateEssencePrices()
        {
            if (_isUpdatingEssencePrices) return false; // prevent multiple concurrent updates
            
            var updateInterval = TimeSpan.FromMinutes(Settings.ApiUpdateInterval.Value);
            return DateTime.Now - _lastEssenceCacheUpdate >= updateInterval;
        }

        private async Task UpdateEssencePrices()
        {
            if (Settings.UseNinjaPricerData.Value)
            {
                await UpdateEssencePricesFromNinjaPricer();
            }
            else
            {
                await UpdateEssencePricesFromApi();
            }
        }

        private async Task UpdateEssencePricesFromApi()
        {
            if (_isUpdatingEssencePrices) return; // prevent concurrent updates
            
            try
            {
                _isUpdatingEssencePrices = true;
                
                if (_apiService == null)
                {
                    _apiService = new PoE2ScoutApiService(
                        Settings.LeagueName.Value,
                        LogMessage,
                        LogError
                    );
                }

                var essences = await _apiService.GetEssenceDataAsync();
                
                // clear existing cache and populate with new data
                _essencePriceCache.Clear();
                foreach (var essence in essences)
                {
                    if (!string.IsNullOrEmpty(essence.Text))
                    {
                        _essencePriceCache.TryAdd(essence.Text, essence.CurrentPrice);
                    }
                }

                _lastEssenceCacheUpdate = DateTime.Now;
                _lastApiUpdate = DateTime.Now; // sync both timestamps
                SaveLastApiUpdateTime(); // save to persistent settings
                LogMessage($"Updated essence prices from API: {_essencePriceCache.Count} essences cached");
            }
            catch (Exception ex)
            {
                LogError($"Failed to update essence prices from API: {ex.Message}");
            }
            finally
            {
                _isUpdatingEssencePrices = false;
            }
        }

        private List<(string EssenceName, Vector2 Position)> GetEssencesOnGround()
        {
            return DetectEssencesOnGround();
        }

        private List<(string EssenceName, Vector2 Position)> DetectEssencesOnGround()
        {
            // reuse collections for better performance
            _reusableEssencesList.Clear();
            _reusableEssenceNames.Clear();

            try
            {
                var itemLabels = GameController.IngameState.IngameUi.ItemsOnGroundLabelElement;
                var labelsVisible = itemLabels?.LabelsOnGroundVisible;
                if (labelsVisible?.Any() != true) 
                {
                    return _reusableEssencesList;
                }
                foreach (var label in labelsVisible)
                {
                    try
                    {
                        var itemOnGround = label?.ItemOnGround;
                        var metadata = itemOnGround?.Metadata;
                        if (metadata?.Contains("Monolith") != true) continue;

                        var labelElement = label?.Label;
                        if (labelElement == null) continue;

                        // cache rect calculation for reuse
                        var labelRect = labelElement.GetClientRectCache;
                        var position = new Vector2(labelRect.Center.X, labelRect.Center.Y);

                        // check if label itself has essence text
                        var labelText = labelElement.Text;
                        if (!string.IsNullOrEmpty(labelText) && 
                            labelText.Contains("Essence", _essenceComparison) &&
                            _reusableEssenceNames.Add(labelText))
                        {
                            _reusableEssencesList.Add((labelText, position));
                        }
                        
                        // check label's children for essence text - find ALL essences in this monolith
                        var children = labelElement.Children;
                        if (children != null && children.Count > 0)
                        {
                            foreach (var child in children)
                            {
                                try
                                {
                                    var childText = child?.Text;
                                    if (!string.IsNullOrEmpty(childText) && 
                                        childText.Contains("Essence", _essenceComparison) &&
                                        _reusableEssenceNames.Add(childText))
                                    {
                                        _reusableEssencesList.Add((childText, position));
                                    }
                                    
                                    // check grandchildren too
                                    var grandChildren = child.Children;
                                    if (grandChildren != null && grandChildren.Count > 0)
                                    {
                                        foreach (var grandChild in grandChildren)
                                        {
                                            try
                                            {
                                                var grandChildText = grandChild?.Text;
                                                if (!string.IsNullOrEmpty(grandChildText) && 
                                                    grandChildText.Contains("Essence", _essenceComparison) &&
                                                    _reusableEssenceNames.Add(grandChildText))
                                                {
                                                    _reusableEssencesList.Add((grandChildText, position));
                                                }
                                            }
                                            catch
                                            {
                                                continue;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error detecting essences: {ex.Message}");
            }

            return _reusableEssencesList;
        }

        private void DisplayEssencePrices(List<(string EssenceName, Vector2 Position)> essences)
        {
            if (essences.Count == 0) return;

            try
            {
                var itemLabels = GameController.IngameState.IngameUi.ItemsOnGroundLabelElement;
                var labelsVisible = itemLabels?.LabelsOnGroundVisible;
                if (labelsVisible?.Any() != true) return;

                // reuse collection for better performance
                _reusableEssencesWithPrices.Clear();
                decimal totalPrice = 0;

                // calculate total price and prepare essence data
                for (int i = 0; i < essences.Count; i++)
                {
                    var (essenceName, _) = essences[i];
                    var price = GetEssencePrice(essenceName);
                    if (price > 0)
                    {
                        totalPrice += price;
                        _reusableEssencesWithPrices.Add((essenceName, price));
                    }
                }


                if (totalPrice <= 0) return;

                // find and draw on the relevant monolith
                foreach (var label in labelsVisible)
                {
                    try
                    {
                        var itemOnGround = label?.ItemOnGround;
                        var metadata = itemOnGround?.Metadata;
                        if (metadata?.Contains("Monolith") != true) continue;

                        if (!IsMonolithWithEssences(label, essences)) continue;

                        var labelElement = label?.Label;
                        if (labelElement == null) continue;

                        DrawTotalPrice(labelElement, totalPrice);
                        DrawIndividualPrices(labelElement, _reusableEssencesWithPrices);
                        break;
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error displaying essence prices: {ex.Message}");
            }
        }

        private bool IsMonolithWithEssences(dynamic label, List<(string EssenceName, Vector2 Position)> essences)
        {
            var itemOnGround = label?.ItemOnGround;
            if (itemOnGround?.Metadata?.Contains("Monolith") != true) return false;

            var labelElement = label?.Label;
            var children = labelElement?.Children;
            if (children == null || children.Count == 0) return false;

            foreach (var child in children)
            {
                if (HasMatchingEssence(child, essences)) return true;

                var grandChildren = child.Children;
                if (grandChildren != null && grandChildren.Count > 0)
                {
                    foreach (var grandChild in grandChildren)
                    {
                        if (HasMatchingEssence(grandChild, essences)) return true;
                    }
                }
            }
            return false;
        }

        private bool HasMatchingEssence(dynamic element, List<(string EssenceName, Vector2 Position)> essences)
        {
            var text = element?.Text as string;
            if (string.IsNullOrEmpty(text)) return false;
            
            // optimized loop instead of LINQ for better performance
            for (int i = 0; i < essences.Count; i++)
            {
                if (essences[i].EssenceName.Equals(text, _essenceComparison))
                    return true;
            }
            return false;
        }

        private void DrawTotalPrice(dynamic labelElement, decimal totalPrice)
        {
            try
            {
                var mainLabelRect = labelElement.GetClientRectCache;
                var totalText = $"Total: {totalPrice:F2} exalts";
                var totalTextSize = Graphics.MeasureText(totalText);
                
                var totalPos = new Vector2(
                    mainLabelRect.Center.X - totalTextSize.X / 2,
                    mainLabelRect.Top - totalTextSize.Y - 5
                );

                var totalRect = new RectangleF(
                    totalPos.X - 5, totalPos.Y - 2,
                    totalTextSize.X + 10, totalTextSize.Y + 4
                );
                
                Graphics.DrawBox(totalRect, System.Drawing.Color.FromArgb(200, 0, 0, 0));
                Graphics.DrawText(totalText, totalPos, System.Drawing.Color.Gold);
            }
            catch (Exception ex)
            {
                LogError($"Error drawing total price: {ex.Message}");
            }
        }

        private void DrawIndividualPrices(dynamic labelElement, List<(string name, decimal price)> essencesWithPrices)
        {
            var children = labelElement.Children;
            if (children == null || children.Count == 0) return;

            foreach (var child in children)
            {
                try
                {
                    DrawPriceForElement(child, essencesWithPrices);

                    var grandChildren = child.Children;
                    if (grandChildren != null && grandChildren.Count > 0)
                    {
                        foreach (var grandChild in grandChildren)
                        {
                            DrawPriceForElement(grandChild, essencesWithPrices);
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        private void DrawPriceForElement(dynamic element, List<(string name, decimal price)> essencesWithPrices)
        {
            var text = element?.Text as string;
            if (string.IsNullOrEmpty(text)) return;

            // optimized search instead of LINQ for better performance
            (string name, decimal price) matchingEssence = default;
            bool found = false;
            
            for (int i = 0; i < essencesWithPrices.Count; i++)
            {
                if (essencesWithPrices[i].name.Equals(text, _essenceComparison))
                {
                    matchingEssence = essencesWithPrices[i];
                    found = true;
                    break;
                }
            }
            
            if (!found) return;

            var elementRect = element.GetClientRectCache;
            var priceText = $"{matchingEssence.price:F2}ex";
            var priceTextSize = Graphics.MeasureText(priceText);
            
            var pricePos = new Vector2(
                elementRect.Right + 5,
                elementRect.Center.Y - priceTextSize.Y / 2
            );

            var priceRect = new RectangleF(
                pricePos.X - 3, pricePos.Y - 2,
                priceTextSize.X + 6, priceTextSize.Y + 4
            );
            
            Graphics.DrawBox(priceRect, System.Drawing.Color.FromArgb(180, 0, 0, 0));
            Graphics.DrawText(priceText, pricePos, System.Drawing.Color.Lime);
        }

        private decimal GetEssencePrice(string essenceName)
        {
            if (string.IsNullOrEmpty(essenceName)) return 0;

            // exact match - fastest path
            if (_essencePriceCache.TryGetValue(essenceName, out var exactPrice))
            {
                return exactPrice;
            }

            // optimized partial match - avoid LINQ allocation
            foreach (var kvp in _essencePriceCache)
            {
                if (kvp.Key.Contains(essenceName, _essenceComparison) ||
                    essenceName.Contains(kvp.Key, _essenceComparison))
                {
                    return kvp.Value;
                }
            }

            return 0;
        }

        private async Task UpdateEssencePricesFromNinjaPricer()
        {
            if (_isUpdatingEssencePrices) return; // prevent concurrent updates
            
            try
            {
                _isUpdatingEssencePrices = true;
                
                var ninjaPricerDataPath = GetNinjaPricerDataPath("Essences");
                if (!File.Exists(ninjaPricerDataPath))
                {
                    LogError($"NinjaPricer data file not found: {ninjaPricerDataPath}");
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(ninjaPricerDataPath);
                var ninjaPricerEssences = JsonSerializer.Deserialize<List<NinjaPricerEssenceItem>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (ninjaPricerEssences == null)
                {
                    LogError("Failed to deserialize NinjaPricer essence data");
                    return;
                }

                // clear existing cache and populate with new data
                _essencePriceCache.Clear();
                foreach (var essence in ninjaPricerEssences)
                {
                    if (!string.IsNullOrEmpty(essence.Text))
                    {
                        var price = essence.GetCurrentPrice();
                        if (price > 0)
                        {
                            _essencePriceCache.TryAdd(essence.Text, price);
                        }
                    }
                }

                _lastEssenceCacheUpdate = DateTime.Now;
                LogMessage($"Updated essence prices from NinjaPricer: {_essencePriceCache.Count} essences cached");
            }
            catch (Exception ex)
            {
                LogError($"Failed to update essence prices from NinjaPricer: {ex.Message}");
            }
            finally
            {
                _isUpdatingEssencePrices = false;
            }
        }

        private string GetNinjaPricerDataPath(string categoryName)
        {
            // build path: Plugins/Temp/NinjaPricer/poescoutdata/LEAGUE_NAME/CATEGORY_NAME.json
            var pluginsPath = Path.GetDirectoryName(Path.GetDirectoryName(DirectoryFullName)); // go up from Source/EssenceHelper to Plugins
            var ninjaPricerTempPath = Path.Combine(pluginsPath, "Temp", "NinjaPricer", "poescoutdata", Settings.LeagueName.Value, $"{categoryName}.json");
            return ninjaPricerTempPath;
        }

        private async Task UpdateDeferListFromApi()
        {
            await UpdateEssencePrices();
        }
    }
}