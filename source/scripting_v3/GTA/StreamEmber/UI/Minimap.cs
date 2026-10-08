//
// StreamEmber: minimap layout (position / size), ChaosModV SetRadarOffset / MultiplyRadarSize.
//

namespace GTA.UI
{
    /// <summary>
    /// StreamEmber: moves and resizes the minimap (all three layers: minimap, mask and blur) by editing the game's
    /// minimap layout data and asking the game to refresh it. The original layout is remembered the first time and
    /// restored by <see cref="Reset"/> and when the scripts are unloaded.
    /// </summary>
    public static class Minimap
    {
        /// <summary>Whether the layout data was found in this game build.</summary>
        public static bool IsSupported => SHVDN.StreamEmberMemory.MinimapAvailable;

        /// <summary>Moves the minimap by an offset in screen units (0..1) from its original position.</summary>
        public static void SetOffset(float x, float y)
            => SHVDN.StreamEmberMemory.SetMinimap(1f, x, y, false);

        /// <summary>
        /// Scales the minimap (size and position) by <paramref name="multiplier"/> and then moves it by the offset
        /// (screen units).
        /// </summary>
        public static void SetScale(float multiplier, float offsetX = 0f, float offsetY = 0f)
            => SHVDN.StreamEmberMemory.SetMinimap(multiplier, offsetX, offsetY, true);

        /// <summary>Restores the original layout.</summary>
        public static void Reset() => SHVDN.StreamEmberMemory.ResetMinimap();
    }
}
