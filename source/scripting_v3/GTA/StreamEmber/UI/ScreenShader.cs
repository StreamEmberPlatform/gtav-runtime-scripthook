//
// StreamEmber: replace one of the game's post-processing pixel shaders with your own HLSL (ChaosModV shader effects).
//

namespace GTA.UI
{
    /// <summary>Which game pixel shader <see cref="ScreenShader.Override"/> replaces.</summary>
    public enum ScreenShaderTarget
    {
        /// <summary><c>PS_LensDistortion</c> of the postfx shader: runs on the whole frame (full-screen effects).</summary>
        LensDistortion,
        /// <summary><c>PS_snow</c>: the snow-on-ground shader.</summary>
        Snow,
    }

    /// <summary>
    /// StreamEmber (experimental): compiles HLSL (<c>ps_4_0</c>, entry point <c>main</c>) with the system's
    /// d3dcompiler_47.dll and makes the game use it instead of one of its own pixel shaders, then reloads the
    /// game's postfx shaders on the render thread. Implemented with a detour of the game's shader creation
    /// (<c>rage::CreateShader</c>) and Script Hook V's present callback. Reset when the scripts are unloaded.
    /// </summary>
    public static class ScreenShader
    {
        /// <summary>Whether the hook and the shader reload functions were found in this game build.</summary>
        public static bool IsSupported => SHVDN.StreamEmberHooks.ShaderHookSupported;

        /// <summary>The last compile or install error, or <see langword="null"/>.</summary>
        public static string LastError => SHVDN.StreamEmberHooks.ShaderLastError;

        /// <summary>
        /// Compiles <paramref name="hlsl"/> and makes the game use it for <paramref name="target"/>. Compiled
        /// shaders are cached by source. Returns <see langword="false"/> on a compile error (see
        /// <see cref="LastError"/>) or when unsupported. The change becomes visible within a few frames.
        /// </summary>
        public static bool Override(ScreenShaderTarget target, string hlsl)
            => SHVDN.StreamEmberHooks.OverrideShader((int)target, hlsl);

        /// <summary>Goes back to the game's own shaders.</summary>
        public static void Reset() => SHVDN.StreamEmberHooks.ResetShader();
    }
}
