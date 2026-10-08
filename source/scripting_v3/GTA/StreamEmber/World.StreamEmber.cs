//
// StreamEmber: world-level game patches found while porting ChaosModV (see docs/StreamEmber-API.md).
//

namespace GTA
{
    public static partial class World
    {
        /// <summary>
        /// StreamEmber: gets or sets whether the ground and roads are covered with snow regardless of the weather
        /// (the game's "XMAS" snow, patched in like Menyoo / ChaosModV). Changing it costs a code patch; the original
        /// code is restored when set to <see langword="false"/> and when the scripts are unloaded.
        /// </summary>
        /// <remarks>Does nothing when the code pattern is not found in this game build (see <see cref="SHVDN.StreamEmberMemory.IsSnowPatchAvailable"/>).</remarks>
        public static bool SnowOnGround
        {
            get => SHVDN.StreamEmberMemory.SnowPatched;
            set => SHVDN.StreamEmberMemory.SetSnow(value);
        }

        /// <summary>
        /// StreamEmber: gets or sets whether the sky (sky dome, clouds, sun and moon) is not rendered. Restored when
        /// set to <see langword="false"/> and when the scripts are unloaded.
        /// </summary>
        public static bool SkyDisabled
        {
            get => SHVDN.StreamEmberMemory.SkyDisabled;
            set => SHVDN.StreamEmberMemory.SetSkyDisabled(value);
        }

        /// <summary>
        /// StreamEmber: free physics collider slots of the physics simulator (<c>phSimulator</c>), or -1 when unknown.
        /// The game can crash when an entity gets activated while there is no free slot.
        /// </summary>
        public static int FreeColliderSlots => SHVDN.StreamEmberMemory.FreeColliderSlots;

        /// <summary>
        /// StreamEmber: <see langword="true"/> when more than 50 collider slots are free (or the count is unknown);
        /// ChaosModV's rule before activating physics on many entities.
        /// </summary>
        public static bool IsPhysicsBudgetAvailable => SHVDN.StreamEmberMemory.IsFreeToActivatePhysics;
    }
}
