//
// StreamEmber: positional ambient speech (no ped needed).
//

using GTA.Math;
using GTA.Native;

namespace GTA
{
    public static partial class Audio
    {
        /// <summary>
        /// StreamEmber: plays an ambient speech line with a voice at a world position, without a ped
        /// (<c>PLAY_AMBIENT_SPEECH_FROM_POSITION_NATIVE</c>). <paramref name="speechParam"/> is a speech parameter
        /// such as <c>"SPEECH_PARAMS_FORCE_SHOUTED"</c>.
        /// </summary>
        public static void PlayAmbientSpeechAtPosition(string speechName, string voiceName, Vector3 position,
            string speechParam = "SPEECH_PARAMS_FORCE_NORMAL")
        {
            Function.Call(Hash.PLAY_AMBIENT_SPEECH_FROM_POSITION_NATIVE, speechName, voiceName, position.X, position.Y,
                position.Z, speechParam);
        }
    }
}
