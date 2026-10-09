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

        /// <summary>Feed notification. Script thread.</summary>
        public static void Notify(string message) =>
            GTA.UI.Notification.PostTicker("StreamEmber: " + Plain(message), false);

        /// <summary>Short on-screen line for an incoming action. Script thread.</summary>
        public static void Announce(string message) => GTA.UI.Screen.ShowSubtitle(Plain(message), 2500);

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

        /// <summary>Viewer names must not inject GTA text formatting (~r~, ~n~ …).</summary>
        private static string Plain(string text) => (text ?? string.Empty).Replace("~", string.Empty);
    }
}
