using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EssenceHelper
{
    public class PoE2ScoutItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("itemId")]
        public int ItemId { get; set; }
        
        [JsonPropertyName("currencyCategoryId")]
        public int CurrencyCategoryId { get; set; }
        
        [JsonPropertyName("apiId")]
        public string ApiId { get; set; } = string.Empty;
        
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
        
        [JsonPropertyName("categoryApiId")]
        public string CategoryApiId { get; set; } = string.Empty;
        
        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("currentPrice")]
        public decimal CurrentPrice { get; set; }
        
        [JsonPropertyName("priceLogs")]
        public List<object> PriceLogs { get; set; } = new();

        public decimal GetExaltedValue()
        {
            return CurrentPrice;
        }

        public string GetName()
        {
            return Text ?? string.Empty;
        }
    }

    public class PoE2ScoutApiResponse
    {
        [JsonPropertyName("currentPage")]
        public int CurrentPage { get; set; }
        
        [JsonPropertyName("pages")]
        public int Pages { get; set; }
        
        [JsonPropertyName("total")]
        public int Total { get; set; }
        
        [JsonPropertyName("items")]
        public List<PoE2ScoutItem> Items { get; set; } = new();
    }

    public class Category
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("apiId")]
        public string ApiId { get; set; } = string.Empty;
        
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;
        
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;
    }

    public class CategoriesResponse
    {
        [JsonPropertyName("unique_categories")]
        public List<Category> UniqueCategories { get; set; } = new();
        
        [JsonPropertyName("currency_categories")]
        public List<Category> CurrencyCategories { get; set; } = new();
    }
}
