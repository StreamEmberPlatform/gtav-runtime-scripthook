//
// StreamEmber: game-level patches and hooks found while porting ChaosModV (see docs/StreamEmber-API.md).
//

namespace GTA
{
    public static partial class Game
    {
        /// <summary>
        /// StreamEmber: gets or sets whether models that are normally restricted in single player (online-only
        /// vehicles, peds and props) can be created by scripts (ChaosModV's model spawn bypass). The original code is
        /// restored when set to <see langword="false"/> and when the scripts are unloaded.
        /// </summary>
        public static bool AllowRestrictedModelSpawning
        {
            get => SHVDN.StreamEmberMemory.ModelSpawnBypassActive;
            set => SHVDN.StreamEmberMemory.SetModelSpawnBypass(value);
        }

        /// <summary>
        /// StreamEmber: patches the running <c>shop_controller</c> script so online-only vehicles spawned in single
        /// player are not removed by it (ChaosModV / Rainbomizer). Safe to call repeatedly: it patches once per
        /// loaded script program. Returns <see langword="true"/> when the patch is in place.
        /// </summary>
        public static bool DisableOnlineVehicleDespawn()
            => SHVDN.StreamEmberMemory.PatchScriptCode("shop_controller",
                "2D ? ? 00 ? 38 00 5D ? ? ? 06 56 ? ? 2E 01 00", 12, new byte[] { 0, 0, 0 });

        /// <summary>
        /// StreamEmber: finds an IDA-style byte pattern (e.g. <c>"2D ? ? 00 38"</c>) in the bytecode of a loaded game
        /// script (by name, e.g. <c>"shop_controller"</c>) and writes <paramref name="bytes"/> at
        /// <c>match + offset</c>. A program is patched only once. Returns <see langword="true"/> when patched
        /// (now or before).
        /// </summary>
        public static bool PatchScriptCode(string scriptName, string pattern, int offset, byte[] bytes)
            => SHVDN.StreamEmberMemory.PatchScriptCode(scriptName, pattern, offset, bytes);

        /// <summary>
        /// StreamEmber (experimental): gets or sets whether the game's own scripts (missions, ambient, shops) are
        /// paused. Only <c>main</c>, <c>main_persistent</c> and <c>control_thread</c> keep running (Script Hook V
        /// and the .NET scripts run through them). ChaosModV uses it around teleports, fake deaths and the delete of
        /// mission vehicles. Implemented with a detour of <c>rage::scrThread::Run</c>; it is reset when the scripts
        /// are unloaded. Check <see cref="ScriptThreadBlockSupported"/>.
        /// </summary>
        public static bool ScriptThreadsBlocked
        {
            get => SHVDN.StreamEmberHooks.ScriptThreadsBlocked;
            set => SHVDN.StreamEmberHooks.SetScriptThreadsBlocked(value);
        }

        /// <summary>StreamEmber: whether <see cref="ScriptThreadsBlocked"/> can work in this game build.</summary>
        public static bool ScriptThreadBlockSupported => SHVDN.StreamEmberHooks.ScriptThreadBlockSupported;
    }
}
