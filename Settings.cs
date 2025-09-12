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

        [Menu("Auto corrupt", "Use Vaal Orb on the Essence")]
        public ToggleNode AutoCorrupt { get; set; } = new(false);

        [Menu("Maximum price in exalts to auto corrupt")]
        public RangeNode<int> MaximumPriceToAutoCorrupt { get; set; } = new(1, 5, 1000);

        [Menu("Minimum distance to essence to auto corrupt")]
        public RangeNode<int> MinimumDistanceToEssenceToAutoCorrupt { get; set; } = new(0, 50, 1000);

        [Menu("Restore mouse to original position", "Restore mouse to original position after auto corrupt")]
        public ToggleNode RestoreMouseToOriginalPosition { get; set; } = new(true);
    }
}