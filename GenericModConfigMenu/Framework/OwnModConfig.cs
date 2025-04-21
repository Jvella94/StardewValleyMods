using StardewModdingAPI.Utilities;

namespace GenericModConfigMenu.Framework
{
    /// <summary>The mod configuration for Generic Mod Config Menu itself.</summary>
    internal class OwnModConfig
    {
        /*********
        ** Accessors
        *********/
        /// <summary>A keybind which opens the menu.</summary>
        public KeybindList OpenMenuKey = new(StardewModdingAPI.SButton.None);

        /// <summary>The number of field rows to offset when scrolling a config menu.</summary>
        public int ScrollSpeed { get; set; } = 120;

        /// <summary>
        /// The number of pixels to offset the filter text box from the left of the screen.
        /// </summary>
        public int FilterOffsetX { get; set; } = 0;

        /// <summary>
        /// The number of pixels to offset the filter text box from the top of the screen.
        /// </summary>
        public int FilterOffsetY { get; set; } = 0;

        /// <summary>
        /// Whether the filter text box should be auto focused upon loading the Config Menu.
        /// </summary>
        public bool SearchBarAutoFocus { get; set; } = true;
    }
}
