//
// StreamEmber: global audio pitch / filter / volume override (ChaosModV's rage::audSound::CombineBuffers hook).
//

namespace GTA
{
    /// <summary>
    /// StreamEmber (experimental): changes every sound the game plays: pitch, low-pass and high-pass cutoff and
    /// volume. Implemented with a detour of the game's <c>rage::audSound::CombineBuffers</c>; the hook code is
    /// native (no managed code on the audio thread). Every override is reset when the scripts are unloaded.
    /// A property set to <see langword="null"/> means "no override".
    /// </summary>
    public static class AudioOverride
    {
        /// <summary>Whether the hook could be installed in this game build.</summary>
        public static bool IsSupported => SHVDN.StreamEmberHooks.AudioHookSupported;

        /// <summary>
        /// Pitch offset added to every sound, in cents (-5000..5000; +1200 = one octave up), or <see langword="null"/>.
        /// </summary>
        public static int? Pitch
        {
            get => SHVDN.StreamEmberHooks.GetAudioOverride(0);
            set => SHVDN.StreamEmberHooks.SetAudioOverride(0, value);
        }

        /// <summary>Low-pass filter cutoff in Hz (lower = muffled), or <see langword="null"/>.</summary>
        public static int? LowPassCutoff
        {
            get => SHVDN.StreamEmberHooks.GetAudioOverride(1);
            set => SHVDN.StreamEmberHooks.SetAudioOverride(1, value);
        }

        /// <summary>High-pass filter cutoff in Hz (higher = thin / tinny), or <see langword="null"/>.</summary>
        public static int? HighPassCutoff
        {
            get => SHVDN.StreamEmberHooks.GetAudioOverride(2);
            set => SHVDN.StreamEmberHooks.SetAudioOverride(2, value);
        }

        /// <summary>Raw volume value written to every sound (game units, millibels), or <see langword="null"/>.</summary>
        public static int? Volume
        {
            get => SHVDN.StreamEmberHooks.GetAudioOverride(3);
            set => SHVDN.StreamEmberHooks.SetAudioOverride(3, value);
        }

        /// <summary>
        /// Sets <see cref="Pitch"/> so the audio sounds like the game speed multiplier (ChaosModV:
        /// <c>400 * log2(speed)</c>, e.g. 0.5 = deeper, 2 = higher).
        /// </summary>
        public static void SetPitchFromSpeedMultiplier(float speedMultiplier)
        {
            if (speedMultiplier <= 0f)
            {
                return;
            }

            Pitch = (int)(400.0 * System.Math.Log(speedMultiplier, 2.0));
        }

        /// <summary>Removes every override.</summary>
        public static void ResetAll()
        {
            Pitch = null;
            LowPassCutoff = null;
            HighPassCutoff = null;
            Volume = null;
        }
    }
}
