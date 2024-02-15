using System.Text.Json.Serialization;

namespace CS2_AutoAccept.Models
{
    /// <summary>
    /// Represents user settings
    /// </summary>
    internal class SettingsModel
    {
        [JsonPropertyName("window_width")]
        public double? WindowWidth { get; set; }
        [JsonPropertyName("window_height")]
        public double? WindowHeight { get; set; }

        public SettingsModel()
        {

        }

        public SettingsModel(double windowWidth, double windowHeight)
        {
            WindowWidth = windowWidth;
            WindowHeight = windowHeight;
        }
    }
}
