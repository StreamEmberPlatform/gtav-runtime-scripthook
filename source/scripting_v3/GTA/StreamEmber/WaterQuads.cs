//
// StreamEmber: the game's water quads (sea, lakes, rivers), ChaosModV "Drought".
//

namespace GTA
{
    /// <summary>
    /// StreamEmber: the heights of the game's 821 water quads (the rectangles that make the sea, lakes and rivers).
    /// Lowering every quad removes the water from the map. The original heights are remembered on the first change
    /// and restored by <see cref="RestoreAll"/> and when the scripts are unloaded.
    /// </summary>
    public static class WaterQuads
    {
        /// <summary>Number of water quads.</summary>
        public const int Count = SHVDN.StreamEmberMemory.WaterQuadCount;

        /// <summary>Whether the water quad array was found in this game build.</summary>
        public static bool IsSupported => SHVDN.StreamEmberMemory.WaterQuadsAvailable;

        /// <summary>Height (Z) of a quad, or NaN when unsupported / out of range.</summary>
        public static float GetHeight(int index) => SHVDN.StreamEmberMemory.GetWaterQuadHeight(index);

        /// <summary>Sets the height (Z) of a quad.</summary>
        public static bool SetHeight(int index, float height) => SHVDN.StreamEmberMemory.SetWaterQuadHeight(index, height);

        /// <summary>Sets every quad to <paramref name="height"/> (e.g. -1000 removes all water).</summary>
        public static bool SetAllHeights(float height)
        {
            if (!IsSupported)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                SetHeight(i, height);
            }

            return true;
        }

        /// <summary>Restores the original heights.</summary>
        public static void RestoreAll() => SHVDN.StreamEmberMemory.RestoreWaterQuads();
    }
}
