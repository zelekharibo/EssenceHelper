using System;
using System.Drawing;
using System.Numerics;
using ImGuiNET;

namespace EssenceHelper.Utils
{
    public static class ImGuiExt
    {
        public static Vector4 ToVector4(this Color color)
        {
            return new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        public static bool? DrawDeleteConfirmationPopup(string popupId, string itemName)
        {
            bool isOpen = true;
            if (!ImGui.BeginPopupModal(popupId, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                return null;
            }

            // display confirmation message
            var message = !string.IsNullOrEmpty(itemName)
                ? $"Are you sure you want to delete {itemName}?"
                : "Are you sure you want to delete this item?";
            
            ImGui.Text(message);
            ImGui.Separator();
            
            bool? result = null;
            const float buttonWidth = 120f;
            
            // yes button
            if (ImGui.Button("Yes", new Vector2(buttonWidth, 0)))
            {
                result = true;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.SameLine();
            
            // no button
            if (ImGui.Button("No", new Vector2(buttonWidth, 0)))
            {
                result = false;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
            return result;
        }
    }
}
