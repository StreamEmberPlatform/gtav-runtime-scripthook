//
// StreamEmber Live: the GTA V specific part. Everything under StreamEmber.Live/ is shared with
// rdr2-runtime-scripthook and must stay identical; only this file differs per game.
//

namespace StreamEmber.Live
{
    public abstract partial class LiveScript : GTA.Script
    {
    }
}

namespace StreamEmber.Live.Internal
{
    internal static class GameBridge
    {
        /// <summary>Presence source sent to EventFabric.</summary>
        public const string Source = "gtav";

        public static string ConfigFile => SHVDN.StreamEmberLayout.ConfigFile;

        public static string ProductVersion => SHVDN.StreamEmberLayout.ProductVersion;

        /// <summary>GET_CURRENT_LANGUAGE (0 English … 12 Simplified Chinese). Script thread.</summary>
        public static int LanguageId() => (int)GTA.Game.Language;

        /// <summary>System message: F4 console + small bottom-left status line (warning = orange, error = red). Script thread.</summary>
        public static void Notify(string message, LiveLogLevel level = LiveLogLevel.Info) =>
            SHVDN.Console.Status(level == LiveLogLevel.Error ? 3 : level == LiveLogLevel.Warning ? 2 : 0, message);

        /// <summary>Incoming live action: F4 console + bottom-left status line instead of a large subtitle. Script thread.</summary>
        public static void Announce(string message) => SHVDN.Console.Status(1, message);

        public static void Log(LiveLogLevel level, string message)
        {
            SHVDN.Log.Level mapped;
            switch (level)
            {
                case LiveLogLevel.Error: mapped = SHVDN.Log.Level.Error; break;
                case LiveLogLevel.Warning: mapped = SHVDN.Log.Level.Warning; break;
                case LiveLogLevel.Debug: mapped = SHVDN.Log.Level.Debug; break;
                default: mapped = SHVDN.Log.Level.Info; break;
            }
            SHVDN.Log.Message(mapped, message);
        }
    }
}
