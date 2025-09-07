using System.Collections.Generic;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace EssenceHelper
{
    public class Settings : ISettings
    {
        public ToggleNode Enable { get; set; } = new(true);

        [Menu("League Name", "Current PoE2 league name for API requests")]
        public TextNode LeagueName { get; set; } = new("Rise of the Abyssal");

        [Menu("Auto-Update Interval (minutes)", "How often to fetch new data from API")]
        public RangeNode<int> ApiUpdateInterval { get; set; } = new(30, 5, 180);

        [Menu("Use NinjaPricer Data", "Use local NinjaPricer data instead of API calls")]
        public ToggleNode UseNinjaPricerData { get; set; } = new(false);

        public string LastApiUpdateTime { get; set; } = string.Empty;
    }
}