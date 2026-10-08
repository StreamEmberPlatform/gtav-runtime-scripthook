//
// StreamEmber: the game's global xenon headlight colour table (ChaosModV OverrideVehicleHeadlightColor).
//

using System.Drawing;

namespace GTA
{
    /// <summary>
    /// StreamEmber: the colour table behind <c>SET_VEHICLE_XENON_LIGHT_COLOR_INDEX</c> (13 entries, shared by every
    /// vehicle). Overriding an entry recolours the xenon headlights of every vehicle that uses that index.
    /// Originals are restored by <see cref="RestoreAll"/> and when the scripts are unloaded.
    /// </summary>
    public static class VehicleXenonColorTable
    {
        /// <summary>Number of entries (xenon colour indices 0..12).</summary>
        public const int Count = 13;

        /// <summary>Whether the table was found in this game build.</summary>
        public static bool IsSupported => SHVDN.StreamEmberMemory.XenonColorTableAvailable;

        /// <summary>Overrides one entry. Returns <see langword="false"/> when unsupported or out of range.</summary>
        public static bool Override(int index, Color color)
            => SHVDN.StreamEmberMemory.OverrideXenonColor(index, true, color.R, color.G, color.B);

        /// <summary>Restores one entry.</summary>
        public static bool Restore(int index)
            => SHVDN.StreamEmberMemory.OverrideXenonColor(index, false, 0, 0, 0);

        /// <summary>Restores every entry.</summary>
        public static void RestoreAll()
        {
            for (int i = 0; i < Count; i++)
            {
                SHVDN.StreamEmberMemory.OverrideXenonColor(i, false, 0, 0, 0);
            }
        }
    }
}
