//
// StreamEmber: game memory features that SHVDN does not have, found while porting ChaosModV (C++, GPL-3.0) to C#.
// Every pattern below comes from ChaosModV's Memory/*.h (GTA V Legacy patterns; this runtime does not run on
// Enhanced). Each feature is resolved lazily the first time it is used, on its own: a pattern that is not found
// only disables that feature (logged once), never the others. Code patches keep the original bytes and are undone
// when the script domain unloads.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SHVDN
{
    /// <summary>
    /// StreamEmber: low level game memory features (snow, sky, minimap, vehicle state, model spawn bypass, script
    /// code patches, 2D lines). Use the <c>GTA</c> API wrappers instead of calling this directly.
    /// </summary>
    public static unsafe class StreamEmberMemory
    {
        #region Helpers

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        private const uint PageExecuteReadWrite = 0x40;

        private static readonly HashSet<string> s_loggedOnce = new HashSet<string>();
        private static bool s_unloadHooked;

        internal static void LogOnce(string key, string message)
        {
            lock (s_loggedOnce)
            {
                if (!s_loggedOnce.Add(key))
                {
                    return;
                }
            }

            Log.Message(Log.Level.Warning, "[StreamEmber] ", message);
        }

        internal static void LogInfo(string message) => Log.Message(Log.Level.Info, "[StreamEmber] ", message);

        /// <summary>Converts an IDA-style pattern ("48 8B ? ?? 05") to the pattern / mask pair of <see cref="MemScanner"/>.</summary>
        internal static void ParsePattern(string ida, out string pattern, out string mask)
        {
            var p = new StringBuilder();
            var m = new StringBuilder();
            foreach (string token in ida.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token[0] == '?')
                {
                    p.Append('\0');
                    m.Append('?');
                }
                else
                {
                    p.Append((char)byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    m.Append('x');
                }
            }

            pattern = p.ToString();
            mask = m.ToString();
        }

        /// <summary>First match of an IDA-style pattern in the game module, or null.</summary>
        internal static byte* Find(string ida)
        {
            try
            {
                ParsePattern(ida, out string pattern, out string mask);
                return MemScanner.FindPatternBmh(pattern, mask);
            }
            catch (Exception ex)
            {
                LogOnce("find:" + ida, "Pattern scan failed for \"" + ida + "\": " + ex.Message);
                return null;
            }
        }

        /// <summary>First match of an IDA-style pattern in [start, start + size), or null.</summary>
        internal static byte* Find(string ida, IntPtr start, ulong size)
        {
            ParsePattern(ida, out string pattern, out string mask);
            if (size < (ulong)pattern.Length)
            {
                return null;
            }

            return MemScanner.FindPatternBmh(pattern, mask, start, size);
        }

        /// <summary>ChaosModV Handle::Into: target of the rel32 operand at <c>p + 1</c> (call / jmp / lea rip).</summary>
        internal static byte* Into(byte* p) => p == null ? null : p + 5 + *(int*)(p + 1);

        /// <summary>Writes bytes into code / read-only memory (VirtualProtect around the write).</summary>
        internal static bool WriteProtected(byte* address, byte[] bytes)
        {
            if (address == null || bytes == null || bytes.Length == 0)
            {
                return false;
            }

            return ReplaceCode(address, Read(address, bytes.Length), bytes);
        }

        [DllImport(StreamEmberLayout.RuntimeAssemblyName + ".asi", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SE_ReplaceCode(byte* address, byte[] expected, byte[] replacement, int size);

        internal static bool ReplaceCode(byte* address, byte[] expected, byte[] bytes)
            => address != null && expected != null && bytes != null && expected.Length == bytes.Length &&
               SE_ReplaceCode(address, expected, bytes, bytes.Length);

        internal static byte[] Read(byte* address, int count)
        {
            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                bytes[i] = address[i];
            }

            return bytes;
        }

        /// <summary>A code patch with its original bytes; undone on script domain unload.</summary>
        private sealed class Patch
        {
            public byte* Address;
            public byte[] Original;
            public bool Applied;
            private byte[] _installed;

            public bool Apply(byte[] bytes)
            {
                if (Address == null)
                {
                    return false;
                }

                if (Original == null || !Applied)
                {
                    Original = Read(Address, bytes.Length);
                }

                if (ReplaceCode(Address, Applied ? _installed : Original, bytes))
                {
                    _installed = (byte[])bytes.Clone();
                    Applied = true;
                    EnsureUnloadHook();
                    return true;
                }

                return false;
            }

            public void Restore()
            {
                if (Applied && Original != null)
                {
                    if (!ReplaceCode(Address, _installed, Original)) return;
                }

                Applied = false;
            }
        }

        private static readonly List<Patch> s_patches = new List<Patch>();

        private static Patch NewPatch(byte* address)
        {
            var patch = new Patch { Address = address };
            s_patches.Add(patch);
            return patch;
        }

        private static void EnsureUnloadHook()
        {
            if (s_unloadHooked)
            {
                return;
            }

            s_unloadHooked = true;
            AppDomain.CurrentDomain.DomainUnload += (s, e) => RestoreAll();
        }

        /// <summary>Undoes every code patch and memory override of this script domain.</summary>
        public static void RestoreAll()
        {
            try
            {
                ResetMinimap();
            }
            catch
            {
            }

            try
            {
                RestoreWaterQuads();
            }
            catch
            {
            }

            for (int i = 0; i < 13; i++)
            {
                try
                {
                    OverrideXenonColor(i, false, 0, 0, 0);
                }
                catch
                {
                }
            }

            foreach (Patch patch in s_patches)
            {
                try
                {
                    patch.Restore();
                }
                catch
                {
                }
            }

            s_snowPatched = false;
            s_skyDisabled = false;
            s_modelSpawnBypass = false;
        }

        #endregion

        #region Snow on ground (Menyoo / ChaosModV Memory::SetSnow)

        private static bool s_snowResolved;
        private static Patch s_snow1;
        private static Patch s_snow2;
        private static bool s_snowPre3095;
        private static bool s_snowPatched;

        public static bool IsSnowPatchAvailable
        {
            get
            {
                ResolveSnow();
                return s_snow1 != null && s_snow2 != null;
            }
        }

        public static bool SnowPatched => s_snowPatched;

        private static void ResolveSnow()
        {
            if (s_snowResolved)
            {
                return;
            }

            s_snowResolved = true;
            s_snowPre3095 = NativeMemory.GetGameVersion() < 85; // v1_0_3095_0
            byte* p1 = Find(s_snowPre3095
                ? "75 17 80 3D ?? ?? ?? ?? ?? 74 25"
                : "75 ? 44 38 3d ? ? ? ? 74 ? b9 ? ? ? ? e8 ? ? ? ? 84 c0 74 ? 84 db");
            byte* p2 = Find("75 17 44 38 3D ?? ?? ?? ?? 74 1D");
            if (p1 == null || p2 == null)
            {
                LogOnce("snow", "Snow patch not available in this game build (pattern not found).");
                return;
            }

            s_snow1 = NewPatch(p1);
            s_snow2 = NewPatch(p2);
        }

        public static void SetSnow(bool state)
        {
            ResolveSnow();
            if (s_snow1 == null || s_snow2 == null || state == s_snowPatched)
            {
                return;
            }

            if (state)
            {
                // jne -> jmp over the "is it snowing" checks
                s_snow1.Apply(new byte[] { 0xEB, s_snowPre3095 ? (byte)0x20 : (byte)0x25 });
                s_snow2.Apply(new byte[] { 0xEB, 0x1C });
            }
            else
            {
                s_snow1.Restore();
                s_snow2.Restore();
            }

            s_snowPatched = state;
        }

        #endregion

        #region Sky (ChaosModV Memory::SetSkyDisabled)

        private static bool s_skyResolved;
        private static Patch s_sky;
        private static bool s_skyDisabled;

        public static bool SkyDisabled => s_skyDisabled;

        public static void SetSkyDisabled(bool state)
        {
            if (!s_skyResolved)
            {
                s_skyResolved = true;
                byte* p = Find("E8 ? ? ? ? C6 05 ? ? ? ? ? 48 83 C4 58");
                if (p == null)
                {
                    LogOnce("sky", "Sky patch not available in this game build (pattern not found).");
                }
                else
                {
                    s_sky = NewPatch(Into(p));
                }
            }

            if (s_sky == null || state == s_skyDisabled)
            {
                return;
            }

            if (state)
            {
                s_sky.Apply(new byte[] { 0xC3 }); // the sky render function returns at once
            }
            else
            {
                s_sky.Restore();
            }

            s_skyDisabled = state;
        }

        #endregion

        #region Physics budget (ChaosModV Memory/Physics.h)

        private static bool s_colliderFuncResolved;
        private static delegate* unmanaged[Stdcall]<IntPtr, IntPtr> s_getColliderNonConst;

        public static int FreeColliderSlots
        {
            get
            {
                try
                {
                    int capacity = NativeMemory.GetEntityColliderCapacity();
                    int count = NativeMemory.GetEntityColliderCount();
                    if (capacity <= 0)
                    {
                        return -1;
                    }

                    return capacity - count;
                }
                catch
                {
                    return -1;
                }
            }
        }

        public static bool IsFreeToActivatePhysics
        {
            get
            {
                int free = FreeColliderSlots;
                return free < 0 || free > 50;
            }
        }

        public static bool EntityHasCollider(IntPtr entityAddress)
        {
            if (entityAddress == IntPtr.Zero)
            {
                return false;
            }

            if (!s_colliderFuncResolved)
            {
                s_colliderFuncResolved = true;
                byte* p = Find("? 85 C0 74 ? ? 3B ? ? ? ? ? 75 ? ? 8B CF E8 ? ? ? ? ? 8D");
                if (p == null)
                {
                    LogOnce("collider", "CEntity::GetColliderNonConst not found.");
                }
                else
                {
                    s_getColliderNonConst = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)Into(p + 17);
                }
            }

            if (s_getColliderNonConst == null)
            {
                return false;
            }

            return s_getColliderNonConst(entityAddress) != IntPtr.Zero;
        }

        #endregion

        #region Vehicle state (ChaosModV Memory/Vehicle.h)

        private static bool s_vehOffsetsResolved;
        private static int s_outOfControlOffset;
        private static int s_brakeInputOffset;

        private static void ResolveVehicleOffsets()
        {
            if (s_vehOffsetsResolved)
            {
                return;
            }

            s_vehOffsetsResolved = true;
            byte* p = Find("FF 90 ? ? 00 00 80 A3 ? ? 00 00 FE 40 80 E7 01");
            if (p != null)
            {
                s_outOfControlOffset = *(ushort*)(p + 8);
            }
            else
            {
                LogOnce("ooc", "Vehicle out-of-control state offset not found.");
            }

            p = Find("F3 0F 11 80 ? ? 00 00 48 83 C4 20 5B C3 ? ? 40 53");
            if (p != null)
            {
                s_brakeInputOffset = *(ushort*)(p + 4);
            }
            else
            {
                LogOnce("brake", "Vehicle brake input offset not found.");
            }
        }

        public static bool GetVehicleOutOfControl(IntPtr vehicleAddress)
        {
            ResolveVehicleOffsets();
            if (s_outOfControlOffset == 0 || vehicleAddress == IntPtr.Zero)
            {
                return false;
            }

            return (*((byte*)vehicleAddress + s_outOfControlOffset) & 1) != 0;
        }

        public static void SetVehicleOutOfControl(IntPtr vehicleAddress, bool state)
        {
            ResolveVehicleOffsets();
            if (s_outOfControlOffset == 0 || vehicleAddress == IntPtr.Zero)
            {
                return;
            }

            byte* flags = (byte*)vehicleAddress + s_outOfControlOffset;
            *flags = (byte)((*flags & 0xFE) | (state ? 1 : 0));
        }

        public static float GetVehicleBrakeInput(IntPtr vehicleAddress)
        {
            ResolveVehicleOffsets();
            if (s_brakeInputOffset == 0 || vehicleAddress == IntPtr.Zero)
            {
                return 0f;
            }

            return *(float*)((byte*)vehicleAddress + s_brakeInputOffset);
        }

        /// <summary>Multiplies the 3x3 part of the entity's render matrix (+0x60) and its physics instance matrix.</summary>
        public static void ScaleEntityMatrices(IntPtr entityAddress, float multiplier)
        {
            if (entityAddress == IntPtr.Zero)
            {
                return;
            }

            byte* baseAddr = (byte*)entityAddress;
            ScaleRows((float*)(baseAddr + 0x60), multiplier);
            byte* inst = *(byte**)(baseAddr + 0x30);
            if (inst != null)
            {
                ScaleRows((float*)(inst + 0x20), multiplier);
            }
        }

        private static void ScaleRows(float* matrix, float multiplier)
        {
            // Three rows (right, forward, up) of 4 floats; the 4th column is padding
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    matrix[row * 4 + col] *= multiplier;
                }
            }
        }

        #endregion

        #region Xenon colour table (ChaosModV OverrideVehicleHeadlightColor)

        private static bool s_xenonResolved;
        private static ulong* s_xenonHolder;
        private static readonly uint[] s_xenonOriginal = new uint[13];
        private static bool s_xenonBackedUp;

        public static bool XenonColorTableAvailable
        {
            get
            {
                ResolveXenon();
                return s_xenonHolder != null;
            }
        }

        private static void ResolveXenon()
        {
            if (s_xenonResolved)
            {
                return;
            }

            s_xenonResolved = true;
            byte* p = Find("48 89 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 4D C8");
            if (p == null)
            {
                LogOnce("xenon", "Xenon colour table not found.");
                return;
            }

            s_xenonHolder = (ulong*)(p + 7 + *(int*)(p + 3));
        }

        public static bool OverrideXenonColor(int index, bool overrideColor, byte r, byte g, byte b)
        {
            if (index < 0 || index >= 13)
            {
                return false;
            }

            if (!overrideColor && !s_xenonBackedUp)
            {
                return true; // nothing changed yet
            }

            ResolveXenon();
            if (s_xenonHolder == null || *s_xenonHolder == 0)
            {
                return false;
            }

            uint* colors = *(uint**)(*s_xenonHolder + 328);
            if (colors == null)
            {
                return false;
            }

            if (!s_xenonBackedUp)
            {
                for (int i = 0; i < 13; i++)
                {
                    s_xenonOriginal[i] = colors[i * 4];
                }

                s_xenonBackedUp = true;
                EnsureUnloadHook();
            }

            uint value = overrideColor ? ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | 0xFF : s_xenonOriginal[index];
            colors[index * 4] = value;
            colors[index * 4 + 1] = value;
            return true;
        }

        #endregion

        #region Minimap layout (ChaosModV Memory/UI.h)

        // struct MinimapData { char Name[100]; float PosX, PosY, SizeX, SizeY; char AlignX, AlignY; } (0x78 bytes) x3
        private const int MinimapEntrySize = 0x78;
        private const int MinimapPosX = 100;

        private static bool s_minimapResolved;
        private static byte* s_minimapData;
        private static delegate* unmanaged[Stdcall]<void> s_refreshMinimap;
        private static float[] s_minimapDefault;

        public static bool MinimapAvailable
        {
            get
            {
                ResolveMinimap();
                return s_minimapData != null && s_refreshMinimap != null;
            }
        }

        private static void ResolveMinimap()
        {
            if (s_minimapResolved)
            {
                return;
            }

            s_minimapResolved = true;
            byte* p = Find("?? 8D 15 ?? ?? ?? ?? ?? 6B C9 78 8B 44 ?? ?? 89 03 8B 44 ?? ?? 89 43 04 8A 4C ?? ??");
            byte* refresh = Find("?? 89 5C ?? ?? 57 ?? 83 EC ?? ?? 8D 3D ?? ?? ?? ?? ?? 8D ?? ?? ?? E8");
            if (p == null || refresh == null)
            {
                LogOnce("minimap", "Minimap layout data not found.");
                return;
            }

            s_minimapData = Into(p + 2);
            s_refreshMinimap = (delegate* unmanaged[Stdcall]<void>)refresh;
            s_minimapDefault = new float[12];
            for (int i = 0; i < 3; i++)
            {
                float* entry = (float*)(s_minimapData + i * MinimapEntrySize + MinimapPosX);
                for (int j = 0; j < 4; j++)
                {
                    s_minimapDefault[i * 4 + j] = entry[j];
                }
            }
        }

        public static void SetMinimap(float multiplier, float offsetX, float offsetY, bool scale)
        {
            ResolveMinimap();
            if (s_minimapData == null || s_refreshMinimap == null)
            {
                return;
            }

            EnsureUnloadHook();
            for (int i = 0; i < 3; i++)
            {
                float* entry = (float*)(s_minimapData + i * MinimapEntrySize + MinimapPosX);
                float posX = s_minimapDefault[i * 4], posY = s_minimapDefault[i * 4 + 1];
                float sizeX = s_minimapDefault[i * 4 + 2], sizeY = s_minimapDefault[i * 4 + 3];
                if (scale)
                {
                    entry[2] = sizeX * multiplier;
                    entry[3] = sizeY * multiplier;
                    entry[0] = posX * multiplier + offsetX;
                    entry[1] = posY * multiplier + offsetY;
                }
                else
                {
                    entry[0] = posX + offsetX;
                    entry[1] = posY + offsetY;
                }
            }

            s_refreshMinimap();
        }

        public static void ResetMinimap()
        {
            if (s_minimapData == null || s_refreshMinimap == null || s_minimapDefault == null)
            {
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                float* entry = (float*)(s_minimapData + i * MinimapEntrySize + MinimapPosX);
                for (int j = 0; j < 4; j++)
                {
                    entry[j] = s_minimapDefault[i * 4 + j];
                }
            }

            s_refreshMinimap();
        }

        #endregion

        #region 2D lines and projection (ChaosModV Memory/Drawing.h, WorldToScreen.h; CitizenFX Draw2dNatives)

        private static bool s_drawResolved;
        private static delegate* unmanaged[Stdcall]<byte*, byte*> s_allocateDrawRect;
        private static delegate* unmanaged[Stdcall]<byte*, float, float, float, float, void> s_setDrawRectCoords;
        private static byte* s_drawRects;
        private static int s_drawRectsSize;
        private static int* s_mainThreadFrameIndex;
        private static Patch s_drawLimit;

        public static bool DrawLineAvailable
        {
            get
            {
                ResolveDraw();
                return s_allocateDrawRect != null;
            }
        }

        private static void ResolveDraw()
        {
            if (s_drawResolved)
            {
                return;
            }

            s_drawResolved = true;
            // DRAW_RECT's implementation
            byte* p = Find("48 8B C4 48 89 58 08 57 48 83 EC 70 48 63");
            // 'cmp edx, 500' in AllocateDrawRect
            byte* limit = Find("81 FA F4 01 00 00 73 13");
            if (p == null || limit == null)
            {
                LogOnce("drawline", "2D line drawing not available (pattern not found).");
                return;
            }

            int offsetSetCoords = NativeMemory.GetGameVersion() < 92 ? 0x92 : 0x97; // v1_0_3407_0
            s_drawRects = Into(p + 0x32);
            s_drawRectsSize = *(int*)(p + 0x2C);
            s_mainThreadFrameIndex = (int*)Into(p + 0x0E);
            s_setDrawRectCoords = (delegate* unmanaged[Stdcall]<byte*, float, float, float, float, void>)Into(p + offsetSetCoords);
            // More than 500 rects per frame (CitizenFX raises it the same way)
            s_drawLimit = NewPatch(limit + 2);
            s_drawLimit.Apply(BitConverter.GetBytes(5000));
            s_allocateDrawRect = (delegate* unmanaged[Stdcall]<byte*, byte*>)Into(p + 0x56);
        }

        /// <summary>Queues a 2D line for this frame. Colour is ARGB (A &lt;&lt; 24 | R &lt;&lt; 16 | G &lt;&lt; 8 | B).</summary>
        public static bool DrawLine(float x1, float y1, float x2, float y2, float width, uint argb)
        {
            ResolveDraw();
            if (s_allocateDrawRect == null || s_setDrawRectCoords == null)
            {
                return false;
            }

            byte* rect = s_allocateDrawRect(s_drawRects + s_drawRectsSize * *s_mainThreadFrameIndex);
            if (rect == null)
            {
                return false;
            }

            s_setDrawRectCoords(rect, x1, y1, x2, y2);
            *(uint*)(rect + 0x34) &= 0xFA;
            *(uint*)(rect + 0x34) |= 0x8A;
            *(float*)(rect + 0x1C) = width;
            *(uint*)(rect + 0x28) = argb;
            return true;
        }

        private static bool s_w2sResolved;
        private static delegate* unmanaged[Stdcall]<float*, float*, float*, byte> s_worldToScreen;

        public static bool WorldToScreen(float x, float y, float z, out float screenX, out float screenY)
        {
            screenX = 0f;
            screenY = 0f;
            if (!s_w2sResolved)
            {
                s_w2sResolved = true;
                byte* p = Find("48 89 5C 24 ? 55 56 57 48 83 EC 70 65 4C 8B 0C 25");
                if (p == null)
                {
                    LogOnce("w2s", "WorldToScreen function not found.");
                }
                else
                {
                    s_worldToScreen = (delegate* unmanaged[Stdcall]<float*, float*, float*, byte>)p;
                }
            }

            if (s_worldToScreen == null)
            {
                return false;
            }

            // The position is a 12-byte struct, passed by reference in the x64 calling convention
            float* pos = stackalloc float[4];
            pos[0] = x;
            pos[1] = y;
            pos[2] = z;
            pos[3] = 0f;
            float sx, sy;
            bool ok = s_worldToScreen(pos, &sx, &sy) != 0;
            screenX = sx;
            screenY = sy;
            return ok;
        }

        #endregion

        #region Model spawn bypass (ChaosModV Hooks/ModelSpawnBypass.cpp)

        private static bool s_modelSpawnResolved;
        private static Patch s_modelSpawn;
        private static bool s_modelSpawnBypass;

        public static bool ModelSpawnBypassActive => s_modelSpawnBypass;

        public static void SetModelSpawnBypass(bool state)
        {
            if (!s_modelSpawnResolved)
            {
                s_modelSpawnResolved = true;
                byte* p = Find("48 85 C0 0F 84 ? ? ? ? 8B 48 50");
                if (p == null)
                {
                    LogOnce("modelspawn", "Model spawn bypass not available (pattern not found).");
                }
                else
                {
                    s_modelSpawn = NewPatch(p);
                }
            }

            if (s_modelSpawn == null || state == s_modelSpawnBypass)
            {
                return;
            }

            if (state)
            {
                var nops = new byte[24];
                for (int i = 0; i < nops.Length; i++)
                {
                    nops[i] = 0x90;
                }

                s_modelSpawn.Apply(nops);
            }
            else
            {
                s_modelSpawn.Restore();
            }

            s_modelSpawnBypass = state;
        }

        #endregion

        #region Water quads (ChaosModV MiscNoWater.cpp)

        // CWaterQuad (0x1C bytes): short MinX, MinY, MaxX, MaxY; uint Color; 8 unknown bytes; float Z (+0x14); uint Flags
        public const int WaterQuadCount = 821;
        private const int WaterQuadSize = 0x1C;
        private const int WaterQuadZ = 0x14;

        private static bool s_waterResolved;
        private static byte** s_waterQuadsGlobal;
        private static float[] s_waterOriginal;

        private static byte* WaterQuads
        {
            get
            {
                if (!s_waterResolved)
                {
                    s_waterResolved = true;
                    byte* p = Find("? 6B C9 1C ? 03 0D ? ? ? ? 66 ? 03 C5 66 89 05 ? ? ? ?");
                    if (p == null)
                    {
                        LogOnce("water", "Water quad array not found.");
                    }
                    else
                    {
                        s_waterQuadsGlobal = (byte**)Into(p + 6);
                    }
                }

                return s_waterQuadsGlobal == null ? null : *s_waterQuadsGlobal;
            }
        }

        public static bool WaterQuadsAvailable => WaterQuads != null;

        public static float GetWaterQuadHeight(int index)
        {
            byte* quads = WaterQuads;
            if (quads == null || index < 0 || index >= WaterQuadCount)
            {
                return float.NaN;
            }

            return *(float*)(quads + index * WaterQuadSize + WaterQuadZ);
        }

        public static bool SetWaterQuadHeight(int index, float z)
        {
            byte* quads = WaterQuads;
            if (quads == null || index < 0 || index >= WaterQuadCount)
            {
                return false;
            }

            if (s_waterOriginal == null)
            {
                s_waterOriginal = new float[WaterQuadCount];
                for (int i = 0; i < WaterQuadCount; i++)
                {
                    s_waterOriginal[i] = *(float*)(quads + i * WaterQuadSize + WaterQuadZ);
                }

                EnsureUnloadHook();
            }

            *(float*)(quads + index * WaterQuadSize + WaterQuadZ) = z;
            return true;
        }

        public static void RestoreWaterQuads()
        {
            byte* quads = WaterQuads;
            if (quads == null || s_waterOriginal == null)
            {
                return;
            }

            for (int i = 0; i < WaterQuadCount; i++)
            {
                *(float*)(quads + i * WaterQuadSize + WaterQuadZ) = s_waterOriginal[i];
            }

            s_waterOriginal = null;
        }

        #endregion

        #region Script program code patches (ChaosModV Memory/Script.h, Rainbomizer)

        // rage::scrProgram (Legacy): +0x10 m_CodeBlocks (ulong*), +0x1C m_CodeSize (uint); code pages of 0x4000 bytes
        private const int ScrProgramCodeBlocks = 0x10;
        private const int ScrProgramCodeSize = 0x1C;
        private const int ScrProgramPageSize = 0x4000;

        private static bool s_programRegistryResolved;
        private static delegate* unmanaged[Stdcall]<ulong, uint, byte*> s_findProgramByHash;
        private static ulong s_programDirectory;
        private static readonly Dictionary<string, ulong> s_patchedPrograms = new Dictionary<string, ulong>();

        private static void ResolveProgramRegistry()
        {
            if (s_programRegistryResolved)
            {
                return;
            }

            s_programRegistryResolved = true;
            byte* p = Find("48 8D 0D ? ? ? ? ? 89 2D ? ? ? ? E8 ? ? ? ? 48 8B 8D B0 00 00 00");
            if (p == null)
            {
                LogOnce("scrprogram", "scrProgramRegistry::FindProgramByHash not found.");
                return;
            }

            s_findProgramByHash = (delegate* unmanaged[Stdcall]<ulong, uint, byte*>)Into(p + 14);
            s_programDirectory = (ulong)(p + 7 + *(int*)(p + 3));
        }

        /// <summary>The loaded rage::scrProgram of a script, or null.</summary>
        public static IntPtr FindScriptProgram(string scriptName)
        {
            ResolveProgramRegistry();
            if (s_findProgramByHash == null)
            {
                return IntPtr.Zero;
            }

            return (IntPtr)s_findProgramByHash(s_programDirectory, Joaat(scriptName));
        }

        public static bool PatchScriptCode(string scriptName, string pattern, int offset, byte[] bytes)
        {
            if (string.IsNullOrEmpty(scriptName) || string.IsNullOrEmpty(pattern) || bytes == null || bytes.Length == 0)
            {
                return false;
            }

            byte* program = (byte*)FindScriptProgram(scriptName);
            if (program == null)
            {
                return false; // script not running / not loaded
            }

            string key = scriptName.ToLowerInvariant() + "|" + pattern + "|" + offset;
            ulong codeBlocks = *(ulong*)(program + ScrProgramCodeBlocks);
            if (codeBlocks == 0)
            {
                return false;
            }

            // Patched already for this program instance (the code blocks pointer changes when it is reloaded)
            if (s_patchedPrograms.TryGetValue(key, out ulong patchedBlocks) && patchedBlocks == codeBlocks)
            {
                return true;
            }

            uint codeSize = *(uint*)(program + ScrProgramCodeSize);
            uint blockCount = (codeSize + ScrProgramPageSize - 1) >> 14;
            for (uint i = 0; i < blockCount; i++)
            {
                ulong block = ((ulong*)codeBlocks)[i];
                if (block == 0)
                {
                    continue;
                }

                ulong size = i == blockCount - 1 ? codeSize - i * (ulong)ScrProgramPageSize : ScrProgramPageSize;
                byte* match = Find(pattern, (IntPtr)(long)block, size);
                if (match == null)
                {
                    continue;
                }

                // Script bytecode lives in ordinary heap memory: a plain write is enough
                for (int j = 0; j < bytes.Length; j++)
                {
                    match[offset + j] = bytes[j];
                }

                s_patchedPrograms[key] = codeBlocks;
                LogInfo("Patched script " + scriptName + " (" + bytes.Length + " bytes at +" + offset + ").");
                return true;
            }

            LogOnce("scrpatch:" + key, "Script code pattern not found in " + scriptName + ": " + pattern);
            return false;
        }

        internal static uint Joaat(string text)
        {
            uint hash = 0;
            foreach (char c0 in text)
            {
                char c = c0 >= 'A' && c0 <= 'Z' ? (char)(c0 + 32) : c0;
                hash += (byte)c;
                hash += hash << 10;
                hash ^= hash >> 6;
            }

            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;
            return hash;
        }

        #endregion
    }
}
