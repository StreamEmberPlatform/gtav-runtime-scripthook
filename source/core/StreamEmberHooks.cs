//
// StreamEmber: game function hooks found while porting ChaosModV (C++, GPL-3.0) to C#.
//
//   Script thread block   rage::scrThread::Run          ChaosModV Hooks/ScriptThreadRunHook.cpp
//   Audio override        rage::audSound::CombineBuffers ChaosModV Hooks/AudioSettingsHook.cpp
//   Screen shader         rage::CreateShader + shader cache reload on present
//                                                      ChaosModV Hooks/ShaderHook.cpp, Memory/Shader.h
//
// Each hook is installed the first time a script uses the feature, never at startup. Hook bodies are native code
// (StreamEmberNativeCode.cs); scripts only change a native control block. When the script domain unloads every
// control block is switched off and every detour removed, so nothing of a reloaded script keeps acting on the game.
//

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SHVDN
{
    /// <summary>
    /// StreamEmber: game hooks (script thread block, global audio override, screen shader override). Use the
    /// <c>GTA</c> API wrappers (<c>Game.ScriptThreadsBlocked</c>, <c>AudioOverride</c>, <c>GTA.UI.ScreenShader</c>).
    /// </summary>
    public static unsafe class StreamEmberHooks
    {
        #region Windows memory

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        private sealed class WindowsCodeMemory : INativeCodeMemory
        {
            private byte* _page;
            private int _used;
            private const int PageSize = 0x1000;

            public byte* AllocateExecutable(int size)
            {
                size = (size + 15) & ~15;
                if (_page == null || _used + size > PageSize)
                {
                    _page = (byte*)VirtualAlloc(IntPtr.Zero, (UIntPtr)(uint)Math.Max(PageSize, size), 0x3000 /* commit | reserve */, 0x40 /* rwx */);
                    _used = 0;
                    if (_page == null)
                    {
                        return null;
                    }
                }

                byte* result = _page + _used;
                _used += size;
                return result;
            }

            public bool WriteCode(byte* address, byte[] bytes)
            {
                if (!VirtualProtect((IntPtr)address, (UIntPtr)(uint)bytes.Length, 0x40, out uint old))
                {
                    return false;
                }

                for (int i = 0; i < bytes.Length; i++)
                {
                    address[i] = bytes[i];
                }

                VirtualProtect((IntPtr)address, (UIntPtr)(uint)bytes.Length, old, out _);
                FlushInstructionCache(GetCurrentProcess(), (IntPtr)address, (UIntPtr)(uint)bytes.Length);
                return true;
            }
        }

        private static readonly WindowsCodeMemory s_memory = new WindowsCodeMemory();
        private static readonly List<NativeDetour> s_detours = new List<NativeDetour>();
        private static bool s_unloadHooked;

        /// <summary>Zeroed native memory for a control block (never freed).</summary>
        private static byte* AllocateControl(int size)
        {
            byte* block = s_memory.AllocateExecutable(size);
            if (block != null)
            {
                for (int i = 0; i < size; i++)
                {
                    block[i] = 0;
                }
            }

            return block;
        }

        private static byte* WriteStub(byte[] code)
        {
            byte* stub = s_memory.AllocateExecutable(code.Length);
            if (stub == null || !s_memory.WriteCode(stub, code))
            {
                return null;
            }

            return stub;
        }

        /// <summary>Creates a detour, a stub built from its trampoline, and installs it. Null on any failure.</summary>
        private static NativeDetour Hook(string name, byte* target, Func<ulong, byte[]> buildStub)
        {
            NativeDetour detour = NativeDetour.Create(s_memory, target, out string error);
            if (detour == null)
            {
                StreamEmberMemory.LogOnce("hook:" + name, "Cannot hook " + name + ": " + error);
                return null;
            }

            byte* stub = WriteStub(buildStub((ulong)detour.Trampoline));
            if (stub == null || !detour.Install(stub))
            {
                StreamEmberMemory.LogOnce("hook:" + name, "Cannot hook " + name + ": install failed.");
                return null;
            }

            s_detours.Add(detour);
            EnsureUnloadHook();
            StreamEmberMemory.LogInfo("Hooked " + name + ".");
            return detour;
        }

        private static void EnsureUnloadHook()
        {
            if (s_unloadHooked)
            {
                return;
            }

            s_unloadHooked = true;
            AppDomain.CurrentDomain.DomainUnload += (s, e) => Shutdown();
        }

        /// <summary>Switches every control block off and removes the detours (script domain unload).</summary>
        public static void Shutdown()
        {
            try
            {
                if (s_threadControl != null)
                {
                    s_threadControl[0] = 0;
                }

                if (s_audioControl != null)
                {
                    for (int i = 0; i < 32; i++)
                    {
                        s_audioControl[i] = 0;
                    }
                }

                if (s_shaderControl != null && s_shaderControl[0] != 0)
                {
                    s_shaderControl[0] = 0;
                    if (s_refreshControl != null)
                    {
                        s_refreshControl[0] = 1; // back to the game's shaders on the next frame
                    }
                }
            }
            catch
            {
            }

            foreach (NativeDetour detour in s_detours)
            {
                try
                {
                    detour.Uninstall();
                }
                catch
                {
                }
            }

            s_detours.Clear();
            // The present callback stays registered for one more refresh (it only runs native code). It is removed
            // when the game exits; registering it again after a reload is harmless (Script Hook V keeps a set).
        }

        #endregion

        #region Script thread block

        private static bool s_threadResolved;
        private static byte* s_threadControl;

        public static bool ScriptThreadBlockSupported
        {
            get
            {
                ResolveThreadBlock();
                return s_threadControl != null;
            }
        }

        public static bool ScriptThreadsBlocked => s_threadControl != null && s_threadControl[0] != 0;

        private static void ResolveThreadBlock()
        {
            if (s_threadResolved)
            {
                return;
            }

            s_threadResolved = true;
            byte* target = StreamEmberMemory.Find(
                "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 48 8D 81 ? 00 00 00");
            if (target == null)
            {
                StreamEmberMemory.LogOnce("thread", "rage::scrThread::Run not found.");
                return;
            }

            byte* control = AllocateControl(16);
            if (control == null)
            {
                return;
            }

            uint[] allowed =
            {
                StreamEmberMemory.Joaat("main"),
                StreamEmberMemory.Joaat("main_persistent"),
                StreamEmberMemory.Joaat("control_thread"),
            };
            // rage::scrThread (Legacy): vftable, m_ThreadId (+0x8), m_ScriptHash (+0xC)
            if (Hook("rage::scrThread::Run", target, tramp => NativeStubs.ScriptThreadBlock((ulong)control, tramp, 0x0C, allowed)) != null)
            {
                s_threadControl = control;
            }
        }

        public static void SetScriptThreadsBlocked(bool blocked)
        {
            if (!blocked && s_threadControl == null)
            {
                return;
            }

            ResolveThreadBlock();
            if (s_threadControl == null)
            {
                return;
            }

            s_threadControl[0] = (byte)(blocked ? 1 : 0);
            if (blocked)
            {
                // ChaosModV: stop the player switch / slow motion effects the blocked scripts may have left on
                NativeFunc.Invoke(0xB4EDDC19532BFB85 /* ANIMPOSTFX_STOP_ALL */, new ulong[0]);
                NativeFunc.Invoke(0x1D408577D440E81E /* SET_TIME_SCALE */, new ulong[] { BitConverter.ToUInt32(BitConverter.GetBytes(1f), 0) });
            }
        }

        #endregion

        #region Audio override

        private static bool s_audioResolved;
        private static int* s_audioControl;

        public static bool AudioHookSupported
        {
            get
            {
                ResolveAudio();
                return s_audioControl != null;
            }
        }

        private static void ResolveAudio()
        {
            if (s_audioResolved)
            {
                return;
            }

            s_audioResolved = true;
            byte* call = StreamEmberMemory.Find("E8 ? ? ? ? 48 8B CE 44 89 7E 54");
            if (call == null)
            {
                StreamEmberMemory.LogOnce("audio", "rage::audSound::CombineBuffers not found.");
                return;
            }

            byte* control = AllocateControl(32);
            if (control == null)
            {
                return;
            }

            if (Hook("rage::audSound::CombineBuffers", StreamEmberMemory.Into(call),
                tramp => NativeStubs.AudioCombineBuffers((ulong)control, tramp)) != null)
            {
                s_audioControl = (int*)control;
            }
        }

        /// <summary>Slot 0 pitch, 1 low-pass cutoff, 2 high-pass cutoff, 3 volume.</summary>
        public static int? GetAudioOverride(int slot)
        {
            if (s_audioControl == null || slot < 0 || slot > 3 || s_audioControl[slot * 2] == 0)
            {
                return null;
            }

            return s_audioControl[slot * 2 + 1];
        }

        public static void SetAudioOverride(int slot, int? value)
        {
            if (slot < 0 || slot > 3 || (value == null && s_audioControl == null))
            {
                return;
            }

            ResolveAudio();
            if (s_audioControl == null)
            {
                return;
            }

            if (value == null)
            {
                s_audioControl[slot * 2] = 0;
                return;
            }

            int v = value.Value;
            if (slot != 3)
            {
                v = Math.Max(short.MinValue, Math.Min(short.MaxValue, v));
            }

            // Value first, then the flag: the audio thread never sees a stale value with the flag on
            s_audioControl[slot * 2 + 1] = v;
            s_audioControl[slot * 2] = 1;
        }

        #endregion

        #region Screen shader override

        [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int D3DCompile(byte[] srcData, UIntPtr srcDataSize, string sourceName, IntPtr defines,
            IntPtr include, string entryPoint, string target, uint flags1, uint flags2, out IntPtr code, out IntPtr errorMsgs);

        [DllImport("ScriptHookV.dll", ExactSpelling = true, EntryPoint = "?presentCallbackRegister@@YAXP6AXPEAX@Z@Z")]
        private static extern void PresentCallbackRegister(IntPtr callback);

        private static bool s_shaderResolved;
        private static byte* s_shaderControl;
        private static byte* s_refreshControl;
        private static string s_shaderError;
        private static readonly Dictionary<string, KeyValuePair<IntPtr, int>> s_shaderCache = new Dictionary<string, KeyValuePair<IntPtr, int>>();
        private static byte* s_filterLensDistortion;
        private static byte* s_filterSnow;

        public static bool ShaderHookSupported
        {
            get
            {
                ResolveShader();
                return s_shaderControl != null;
            }
        }

        public static string ShaderLastError => s_shaderError;

        private static byte* NativeString(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text + "\0");
            byte* p = AllocateControl(bytes.Length);
            if (p != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    p[i] = bytes[i];
                }
            }

            return p;
        }

        private static void ResolveShader()
        {
            if (s_shaderResolved)
            {
                return;
            }

            s_shaderResolved = true;
            byte* createCall = StreamEmberMemory.Find("E8 ? ? ? ? 48 89 83 28 02 00 00 8B 44 24 30 89 83 30 02 00 00 EB 0F");
            byte* resolve = StreamEmberMemory.Find("E8 ? ? ? ? 8B C8 E8 ? ? ? ? ? 85 C0 75 ? 81 3D ? ? ? ? 00 02 00 00");
            byte* destroy = StreamEmberMemory.Find("8B CB 84 C0 74 ? E8 ? ? ? ? EB ? E8 ? ? ? ? ? 8B CB E8");
            byte* reload = StreamEmberMemory.Find("89 ? ? ? ? ? E8 ? ? ? ? ? 8B ? E8 ? ? ? ? ? 84 FF 74 ? ? 8B ? E8");
            if (createCall == null || resolve == null || destroy == null || reload == null)
            {
                s_shaderError = "shader functions not found in this game build";
                StreamEmberMemory.LogOnce("shader", "Screen shader override not available: " + s_shaderError + ".");
                return;
            }

            IntPtr crt = LoadLibraryW("ucrtbase.dll");
            if (crt == IntPtr.Zero)
            {
                crt = LoadLibraryW("msvcrt.dll");
            }

            IntPtr strstr = crt == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(crt, "strstr");
            if (strstr == IntPtr.Zero)
            {
                s_shaderError = "strstr not found";
                return;
            }

            s_filterLensDistortion = NativeString("PS_LensDistortion");
            s_filterSnow = NativeString("PS_snow");

            byte* refreshControl = AllocateControl(16);
            byte* control = AllocateControl(32);
            if (control == null || refreshControl == null || s_filterLensDistortion == null || s_filterSnow == null)
            {
                return;
            }

            uint[] shaders =
            {
                StreamEmberMemory.Joaat("postfx"), StreamEmberMemory.Joaat("postfxms"), StreamEmberMemory.Joaat("postfxms0"),
                StreamEmberMemory.Joaat("deferred_lighting"), StreamEmberMemory.Joaat("deferred_lightingms"),
                StreamEmberMemory.Joaat("deferred_lightingms0"),
            };
            byte[] refreshCode = NativeStubs.ShaderRefreshOnPresent((ulong)refreshControl,
                (ulong)StreamEmberMemory.Into(resolve + 7), (ulong)StreamEmberMemory.Into(destroy + 13),
                (ulong)StreamEmberMemory.Into(reload + 27), shaders);
            byte* refreshStub = WriteStub(refreshCode);
            if (refreshStub == null)
            {
                return;
            }

            if (Hook("rage::CreateShader", StreamEmberMemory.Into(createCall),
                tramp => NativeStubs.CreateShader((ulong)control, tramp, (ulong)strstr)) == null)
            {
                s_shaderError = "cannot hook the shader creation";
                return;
            }

            try
            {
                PresentCallbackRegister((IntPtr)refreshStub);
            }
            catch (Exception ex)
            {
                s_shaderError = "present callback: " + ex.Message;
                StreamEmberMemory.LogOnce("shaderpresent", "Screen shader override: " + s_shaderError);
                return;
            }

            s_refreshControl = refreshControl;
            s_shaderControl = control;
        }

        /// <summary>Compiles HLSL (ps_4_0, entry "main") into native memory; cached by source.</summary>
        private static bool Compile(string hlsl, out IntPtr bytecode, out int size)
        {
            if (s_shaderCache.TryGetValue(hlsl, out KeyValuePair<IntPtr, int> cached))
            {
                bytecode = cached.Key;
                size = cached.Value;
                return true;
            }

            bytecode = IntPtr.Zero;
            size = 0;
            byte[] source = Encoding.UTF8.GetBytes(hlsl);
            IntPtr code, errors;
            int hr;
            try
            {
                hr = D3DCompile(source, (UIntPtr)(uint)source.Length, null, IntPtr.Zero, IntPtr.Zero, "main", "ps_4_0", 0, 0,
                    out code, out errors);
            }
            catch (Exception ex)
            {
                s_shaderError = "d3dcompiler_47.dll: " + ex.Message;
                return false;
            }

            if (hr < 0 || code == IntPtr.Zero)
            {
                s_shaderError = "compile error 0x" + hr.ToString("X8") + ": " + BlobString(errors);
                Release(errors);
                return false;
            }

            Release(errors);
            byte* data = BlobPointer(code);
            int length = (int)BlobSize(code);
            // Copy into memory that is never freed: the render thread may still read the previous bytecode
            byte* copy = AllocateControl(length);
            if (copy == null)
            {
                Release(code);
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                copy[i] = data[i];
            }

            Release(code);
            bytecode = (IntPtr)copy;
            size = length;
            if (s_shaderCache.Count < 32)
            {
                s_shaderCache[hlsl] = new KeyValuePair<IntPtr, int>(bytecode, size);
            }

            return true;
        }

        // ID3DBlob: QueryInterface, AddRef, Release, GetBufferPointer, GetBufferSize
        private static byte* BlobPointer(IntPtr blob) => ((delegate* unmanaged[Stdcall]<IntPtr, byte*>)(*(*(void***)blob + 3)))(blob);

        private static ulong BlobSize(IntPtr blob) => ((delegate* unmanaged[Stdcall]<IntPtr, ulong>)(*(*(void***)blob + 4)))(blob);

        private static void Release(IntPtr blob)
        {
            if (blob != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, uint>)(*(*(void***)blob + 2)))(blob);
            }
        }

        private static string BlobString(IntPtr blob)
        {
            if (blob == IntPtr.Zero)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringAnsi((IntPtr)BlobPointer(blob), (int)BlobSize(blob)).TrimEnd('\0', '\n', '\r');
        }

        /// <summary>Target 0 = PS_LensDistortion (full screen), 1 = PS_snow.</summary>
        public static bool OverrideShader(int target, string hlsl)
        {
            s_shaderError = null;
            ResolveShader();
            if (s_shaderControl == null)
            {
                s_shaderError = s_shaderError ?? "not supported";
                return false;
            }

            if (!Compile(hlsl, out IntPtr bytecode, out int size))
            {
                StreamEmberMemory.LogOnce("shadercompile:" + hlsl.GetHashCode(), "Shader compile failed: " + s_shaderError);
                return false;
            }

            // Off, change the fields, on: the render thread never sees a half-updated block
            s_shaderControl[0] = 0;
            *(ulong*)(s_shaderControl + 8) = (ulong)(target == 1 ? s_filterSnow : s_filterLensDistortion);
            *(ulong*)(s_shaderControl + 16) = (ulong)bytecode;
            *(uint*)(s_shaderControl + 24) = (uint)size;
            s_shaderControl[0] = 1;
            s_refreshControl[0] = 1;
            return true;
        }

        public static void ResetShader()
        {
            if (s_shaderControl == null)
            {
                return;
            }

            s_shaderControl[0] = 0;
            s_refreshControl[0] = 1;
        }

        #endregion
    }
}
