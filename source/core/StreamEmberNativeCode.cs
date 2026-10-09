//
// StreamEmber: a small x64 detour engine and the native hook stubs used by StreamEmberHooks.
//
// Why native stubs: the hooked game functions run on the game's audio and render threads and outlive a script
// domain (scripts can be reloaded). Managed callbacks there would stall those threads on GC and dangle after an
// AppDomain unload. So the hook bodies are tiny x64 routines generated here; scripts only change values in a native
// "control block" the routines read. Generated code and control blocks are never freed (a thread may still be
// inside them after an unhook), they are a few hundred bytes.
//
// Detours only relocate prologue instructions this decoder fully understands and that are position independent
// (no RIP-relative operands, no branches). Anything else (or a function another mod already hooked) is refused, the
// feature then reports itself as unsupported. This file has no Windows dependencies (memory access goes through
// INativeCodeMemory) so it is unit tested on Linux against Windows-x64 (ms_abi) functions.
//

using System;
using System.Collections.Generic;

namespace SHVDN
{
    /// <summary>Executable memory and code writes; Windows implementation in StreamEmberHooks.</summary>
    public unsafe interface INativeCodeMemory
    {
        /// <summary>Allocates readable, writable and executable memory (never freed).</summary>
        byte* AllocateExecutable(int size);

        /// <summary>Writes bytes to code memory (changing the protection around the write).</summary>
        bool WriteCode(byte* address, byte[] bytes);
        bool ReplaceCode(byte* address, byte[] expected, byte[] bytes);
    }

    /// <summary>Byte emitter with labels and short / near jump fix-ups.</summary>
    public sealed class CodeBuffer
    {
        private readonly List<byte> _bytes = new List<byte>(256);
        private readonly Dictionary<string, int> _labels = new Dictionary<string, int>();
        private readonly List<(int at, string label, int size)> _fixups = new List<(int, string, int)>();

        public int Length => _bytes.Count;

        public CodeBuffer Emit(params byte[] bytes)
        {
            _bytes.AddRange(bytes);
            return this;
        }

        public CodeBuffer Imm32(int value) => Emit(BitConverter.GetBytes(value));

        public CodeBuffer Imm64(ulong value) => Emit(BitConverter.GetBytes(value));

        public CodeBuffer Label(string name)
        {
            _labels[name] = _bytes.Count;
            return this;
        }

        /// <summary>Short conditional / unconditional jump (opcode 0x74 je, 0x75 jne, 0x7E jle, 0x7D jge, 0xEB jmp).</summary>
        public CodeBuffer Jump8(byte opcode, string label)
        {
            _bytes.Add(opcode);
            _fixups.Add((_bytes.Count, label, 1));
            _bytes.Add(0);
            return this;
        }

        /// <summary>mov rax, imm64</summary>
        public CodeBuffer MovRaxImm64(ulong value) => Emit(0x48, 0xB8).Imm64(value);

        /// <summary>mov r10, imm64 ; call r10</summary>
        public CodeBuffer CallAbs(ulong target) => Emit(0x49, 0xBA).Imm64(target).Emit(0x41, 0xFF, 0xD2);

        /// <summary>jmp qword ptr [rip+0] ; dq target (14 bytes, any distance)</summary>
        public CodeBuffer JmpAbs(ulong target) => Emit(0xFF, 0x25, 0, 0, 0, 0).Imm64(target);

        public byte[] ToArray()
        {
            byte[] code = _bytes.ToArray();
            foreach ((int at, string label, int size) in _fixups)
            {
                if (!_labels.TryGetValue(label, out int target))
                {
                    throw new InvalidOperationException("Undefined label " + label);
                }

                int rel = target - (at + size);
                if (size == 1)
                {
                    if (rel < sbyte.MinValue || rel > sbyte.MaxValue)
                    {
                        throw new InvalidOperationException("Short jump out of range to " + label);
                    }

                    code[at] = unchecked((byte)(sbyte)rel);
                }
            }

            return code;
        }
    }

    /// <summary>Length decoder for the position independent instructions found in MSVC prologues.</summary>
    public static unsafe class PrologueDecoder
    {
        /// <summary>Length of the instruction at <paramref name="p"/>, or 0 when it is not a supported, relocatable one.</summary>
        public static int InstructionLength(byte* p)
        {
            int i = 0;
            bool operandSize16 = false;

            // One legacy prefix (operand size / SSE)
            if (p[i] == 0x66 || p[i] == 0xF2 || p[i] == 0xF3)
            {
                operandSize16 = p[i] == 0x66;
                i++;
            }

            bool rexW = false;
            if (p[i] >= 0x40 && p[i] <= 0x4F)
            {
                rexW = (p[i] & 0x08) != 0;
                i++;
            }

            byte op = p[i++];
            switch (op)
            {
                // push / pop r64
                case byte b when b >= 0x50 && b <= 0x5F:
                    return i;
                case 0x90: // nop
                    return i;
                // ALU / mov / lea / test with ModRM, no immediate
                case 0x01: case 0x03: case 0x09: case 0x0B: case 0x21: case 0x23: case 0x29: case 0x2B:
                case 0x31: case 0x33: case 0x39: case 0x3B: case 0x85: case 0x88: case 0x89: case 0x8A:
                case 0x8B: case 0x8D:
                    return ModRm(p, i, 0);
                case 0x83: // group 1 r/m, imm8
                case 0xC6: // mov r/m8, imm8
                    return ModRm(p, i, 1);
                case 0x81: // group 1 r/m, imm32
                case 0xC7: // mov r/m, imm32
                    return ModRm(p, i, operandSize16 ? 2 : 4);
                case byte b when b >= 0xB8 && b <= 0xBF: // mov r, imm32 / imm64
                    return i + (rexW ? 8 : operandSize16 ? 2 : 4);
                case 0x0F:
                {
                    byte op2 = p[i++];
                    switch (op2)
                    {
                        case 0x10: case 0x11: case 0x28: case 0x29: // movups / movss / movaps
                        case 0xB6: case 0xB7: case 0xBE: case 0xBF: // movzx / movsx
                        case 0x57: // xorps
                            return ModRm(p, i, 0);
                        default:
                            return 0;
                    }
                }
                default:
                    return 0;
            }
        }

        /// <summary>Length through the ModRM operand plus an immediate; 0 for RIP-relative operands.</summary>
        private static int ModRm(byte* p, int i, int immediateSize)
        {
            byte modrm = p[i++];
            int mod = modrm >> 6;
            int rm = modrm & 7;
            if (mod != 3)
            {
                if (rm == 4)
                {
                    byte sib = p[i++];
                    if (mod == 0 && (sib & 7) == 5)
                    {
                        i += 4; // [index*scale + disp32]
                    }
                }
                else if (mod == 0 && rm == 5)
                {
                    return 0; // RIP-relative: would need relocation
                }

                if (mod == 1)
                {
                    i += 1;
                }
                else if (mod == 2)
                {
                    i += 4;
                }
            }

            return i + immediateSize;
        }

        /// <summary>Bytes (whole instructions) that cover at least <paramref name="needed"/> bytes, or 0.</summary>
        public static int StealLength(byte* p, int needed)
        {
            int total = 0;
            while (total < needed)
            {
                int len = InstructionLength(p + total);
                if (len == 0)
                {
                    return 0;
                }

                total += len;
            }

            return total;
        }
    }

    /// <summary>An x64 function detour: the target jumps to a detour, the trampoline runs the original.</summary>
    public sealed unsafe class NativeDetour
    {
        private const int JumpSize = 14;

        private readonly INativeCodeMemory _memory;
        private readonly byte[] _original;
        private byte[] _installed;

        public byte* Target { get; }

        /// <summary>Calls the original function (relocated prologue + jump back).</summary>
        public byte* Trampoline { get; }

        public bool Installed { get; private set; }

        private NativeDetour(INativeCodeMemory memory, byte* target, byte* trampoline, byte[] original)
        {
            _memory = memory;
            Target = target;
            Trampoline = trampoline;
            _original = original;
        }

        /// <summary>Prepares a detour of <paramref name="target"/> (not installed yet). Null with an error when the
        /// prologue cannot be relocated.</summary>
        public static NativeDetour Create(INativeCodeMemory memory, byte* target, out string error)
        {
            error = null;
            if (target == null)
            {
                error = "target not found";
                return null;
            }

            int steal = PrologueDecoder.StealLength(target, JumpSize);
            if (steal == 0)
            {
                error = "unsupported prologue: " + BitConverter.ToString(Copy(target, 16));
                return null;
            }

            byte[] original = Copy(target, steal);
            var tramp = new CodeBuffer();
            tramp.Emit(original);
            tramp.JmpAbs((ulong)(target + steal));
            byte[] trampolineCode = tramp.ToArray();
            byte* trampoline = memory.AllocateExecutable(trampolineCode.Length);
            if (trampoline == null || !memory.WriteCode(trampoline, trampolineCode))
            {
                error = "cannot allocate the trampoline";
                return null;
            }

            return new NativeDetour(memory, target, trampoline, original);
        }

        /// <summary>Points the target at <paramref name="detour"/> using the platform's guarded replacement transaction.
        /// Refuses to overwrite another mod's bytes or a prologue currently being executed.</summary>
        public bool Install(byte* detour)
        {
            if (Installed)
            {
                return true;
            }

            var jump = new CodeBuffer().JmpAbs((ulong)detour).ToArray();
            byte[] patch = new byte[_original.Length];
            Array.Copy(jump, patch, jump.Length);
            for (int i = jump.Length; i < patch.Length; i++)
            {
                patch[i] = 0x90;
            }

            if (!_memory.ReplaceCode(Target, _original, patch))
            {
                return false;
            }

            _installed = patch;
            Installed = true;
            return true;
        }

        /// <summary>Restores the original prologue.</summary>
        public bool Uninstall()
        {
            if (!Installed)
            {
                return true;
            }

            if (!_memory.ReplaceCode(Target, _installed, _original))
            {
                return false;
            }

            Installed = false;
            return true;
        }

        private static byte[] Copy(byte* p, int count)
        {
            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                bytes[i] = p[i];
            }

            return bytes;
        }
    }

    /// <summary>Generators of the native hook bodies (see the layout of each control block).</summary>
    public static class NativeStubs
    {
        /// <summary>
        /// rage::scrThread::Run(scrThread* thread) -> __int64. Control block: byte enabled at +0.
        /// When enabled, threads other than the allowed script hashes return 0 without running (ChaosModV
        /// EnableScriptThreadBlock). The script hash is read at thread + <paramref name="hashOffset"/>.
        /// </summary>
        public static byte[] ScriptThreadBlock(ulong control, ulong trampoline, int hashOffset, uint[] allowedHashes)
        {
            var c = new CodeBuffer();
            c.MovRaxImm64(control);
            c.Emit(0x80, 0x38, 0x00);                              // cmp byte [rax], 0
            c.Jump8(0x74, "orig");                                 // je orig
            c.Emit(0x8B, 0x41, (byte)hashOffset);                  // mov eax, [rcx+hashOffset]
            foreach (uint hash in allowedHashes)
            {
                c.Emit(0x3D).Imm32(unchecked((int)hash));          // cmp eax, imm32
                c.Jump8(0x74, "orig");                             // je orig
            }

            c.Emit(0x48, 0x31, 0xC0);                              // xor rax, rax
            c.Emit(0xC3);                                          // ret
            c.Label("orig");
            c.JmpAbs(trampoline);
            return c.ToArray();
        }

        /// <summary>
        /// rage::audSound::CombineBuffers(this, combineBuffer) -> void. Runs the original, then edits the combined
        /// settings. Control block (int32 slots): +0 pitch on, +4 pitch, +8 lpf on, +12 lpf, +16 hpf on, +20 hpf,
        /// +24 volume on, +28 volume. Buffer offsets (Legacy): pitch int16 +0xE, lpf int16 +0x8, hpf int16 +0xA,
        /// volume int32 +0x0.
        /// </summary>
        public static byte[] AudioCombineBuffers(ulong control, ulong trampoline,
            byte pitchOffset = 0x0E, byte lpfOffset = 0x08, byte hpfOffset = 0x0A)
        {
            var c = new CodeBuffer();
            c.Emit(0x53);                                          // push rbx
            c.Emit(0x48, 0x83, 0xEC, 0x20);                        // sub rsp, 0x20
            c.Emit(0x48, 0x89, 0xD3);                              // mov rbx, rdx
            c.CallAbs(trampoline);                                 // original(rcx, rdx)
            c.MovRaxImm64(control);

            // pitch: clamp(buf.pitch + value, -5000, 5000)
            c.Emit(0x80, 0x38, 0x00);                              // cmp byte [rax], 0
            c.Jump8(0x74, "lpf");
            c.Emit(0x0F, 0xBF, 0x4B, pitchOffset);                 // movsx ecx, word [rbx+pitch]
            c.Emit(0x03, 0x48, 0x04);                              // add ecx, [rax+4]
            c.Emit(0x81, 0xF9).Imm32(5000);                        // cmp ecx, 5000
            c.Jump8(0x7E, "pitch_lo");                             // jle
            c.Emit(0xB9).Imm32(5000);                              // mov ecx, 5000
            c.Label("pitch_lo");
            c.Emit(0x81, 0xF9).Imm32(-5000);                       // cmp ecx, -5000
            c.Jump8(0x7D, "pitch_store");                          // jge
            c.Emit(0xB9).Imm32(-5000);                             // mov ecx, -5000
            c.Label("pitch_store");
            c.Emit(0x66, 0x89, 0x4B, pitchOffset);                 // mov [rbx+pitch], cx

            // low-pass: min(buf.lpf, value)
            c.Label("lpf");
            c.Emit(0x80, 0x78, 0x08, 0x00);                        // cmp byte [rax+8], 0
            c.Jump8(0x74, "hpf");
            c.Emit(0x0F, 0xBF, 0x4B, lpfOffset);                   // movsx ecx, word [rbx+lpf]
            c.Emit(0x8B, 0x50, 0x0C);                              // mov edx, [rax+12]
            c.Emit(0x39, 0xD1);                                    // cmp ecx, edx
            c.Jump8(0x7E, "hpf");                                  // jle: already lower
            c.Emit(0x66, 0x89, 0x53, lpfOffset);                   // mov [rbx+lpf], dx

            // high-pass: max(buf.hpf, value)
            c.Label("hpf");
            c.Emit(0x80, 0x78, 0x10, 0x00);                        // cmp byte [rax+16], 0
            c.Jump8(0x74, "vol");
            c.Emit(0x0F, 0xBF, 0x4B, hpfOffset);                   // movsx ecx, word [rbx+hpf]
            c.Emit(0x8B, 0x50, 0x14);                              // mov edx, [rax+20]
            c.Emit(0x39, 0xD1);                                    // cmp ecx, edx
            c.Jump8(0x7D, "vol");                                  // jge: already higher
            c.Emit(0x66, 0x89, 0x53, hpfOffset);                   // mov [rbx+hpf], dx

            // volume: buf.volume = value
            c.Label("vol");
            c.Emit(0x80, 0x78, 0x18, 0x00);                        // cmp byte [rax+24], 0
            c.Jump8(0x74, "done");
            c.Emit(0x8B, 0x50, 0x1C);                              // mov edx, [rax+28]
            c.Emit(0x89, 0x13);                                    // mov [rbx], edx

            c.Label("done");
            c.Emit(0x48, 0x83, 0xC4, 0x20);                        // add rsp, 0x20
            c.Emit(0x5B);                                          // pop rbx
            c.Emit(0xC3);                                          // ret
            return c.ToArray();
        }

        /// <summary>
        /// rage::CreateShader(const char* name, BYTE* data, DWORD size, DWORD type, DWORD* out) -> void*.
        /// Control block: byte active +0, char* name filter +8, BYTE* bytecode +16, uint32 size +24.
        /// When active and strstr(name, filter) matches, the original is called with the replacement bytecode.
        /// </summary>
        public static byte[] CreateShader(ulong control, ulong trampoline, ulong strstr)
        {
            var c = new CodeBuffer();
            c.MovRaxImm64(control);
            c.Emit(0x80, 0x38, 0x00);                              // cmp byte [rax], 0
            c.Jump8(0x75, "active");                               // jne active
            c.JmpAbs(trampoline);                                  // inactive: straight to the original
            c.Label("active");
            c.Emit(0x53);                                          // push rbx
            c.Emit(0x56);                                          // push rsi
            c.Emit(0x57);                                          // push rdi
            c.Emit(0x41, 0x54);                                    // push r12
            c.Emit(0x41, 0x55);                                    // push r13
            c.Emit(0x48, 0x83, 0xEC, 0x30);                        // sub rsp, 0x30
            c.Emit(0x48, 0x89, 0xCB);                              // mov rbx, rcx (name)
            c.Emit(0x48, 0x89, 0xD6);                              // mov rsi, rdx (data)
            c.Emit(0x44, 0x89, 0xC7);                              // mov edi, r8d (size)
            c.Emit(0x45, 0x89, 0xCC);                              // mov r12d, r9d (type)
            c.Emit(0x4C, 0x8B, 0xAC, 0x24).Imm32(0x80);            // mov r13, [rsp+0x80] (out: 0x28 + 5 pushes + 0x30)
            c.Emit(0x48, 0x8B, 0x50, 0x08);                        // mov rdx, [rax+8] (filter)
            c.Emit(0x48, 0x85, 0xD2);                              // test rdx, rdx
            c.Jump8(0x74, "original");
            c.Emit(0x48, 0x85, 0xDB);                              // test rbx, rbx (name may be null)
            c.Jump8(0x74, "original");
            c.Emit(0x48, 0x89, 0xD9);                              // mov rcx, rbx
            c.CallAbs(strstr);                                     // strstr(name, filter)
            c.Emit(0x48, 0x85, 0xC0);                              // test rax, rax
            c.Jump8(0x74, "original");
            c.MovRaxImm64(control);
            c.Emit(0x48, 0x8B, 0x70, 0x10);                        // mov rsi, [rax+16] (replacement bytecode)
            c.Emit(0x8B, 0x78, 0x18);                              // mov edi, [rax+24] (its size)
            c.Label("original");
            c.Emit(0x48, 0x89, 0xD9);                              // mov rcx, rbx
            c.Emit(0x48, 0x89, 0xF2);                              // mov rdx, rsi
            c.Emit(0x41, 0x89, 0xF8);                              // mov r8d, edi
            c.Emit(0x45, 0x89, 0xE1);                              // mov r9d, r12d
            c.Emit(0x4C, 0x89, 0x6C, 0x24, 0x20);                  // mov [rsp+0x20], r13
            c.CallAbs(trampoline);
            c.Emit(0x48, 0x83, 0xC4, 0x30);                        // add rsp, 0x30
            c.Emit(0x41, 0x5D);                                    // pop r13
            c.Emit(0x41, 0x5C);                                    // pop r12
            c.Emit(0x5F);                                          // pop rdi
            c.Emit(0x5E);                                          // pop rsi
            c.Emit(0x5B);                                          // pop rbx
            c.Emit(0xC3);                                          // ret
            return c.ToArray();
        }

        /// <summary>
        /// Script Hook V present callback (void* swapChain). Control block: byte refresh pending at +0. When set,
        /// clears it, destroys the resolved shaders of <paramref name="shaderHashes"/> and reloads the shaders
        /// (ChaosModV Memory::InvalidateShaderCache), on the render thread.
        /// </summary>
        public static byte[] ShaderRefreshOnPresent(ulong control, ulong resolveShader, ulong destroyShader, ulong reloadShaders,
            uint[] shaderHashes)
        {
            var c = new CodeBuffer();
            c.MovRaxImm64(control);
            c.Emit(0x80, 0x38, 0x00);                              // cmp byte [rax], 0
            c.Jump8(0x75, "work");
            c.Emit(0xC3);                                          // ret
            c.Label("work");
            c.Emit(0xC6, 0x00, 0x00);                              // mov byte [rax], 0
            c.Emit(0x53);                                          // push rbx (aligns the stack)
            c.Emit(0x48, 0x83, 0xEC, 0x20);                        // sub rsp, 0x20
            for (int i = 0; i < shaderHashes.Length; i++)
            {
                string next = "next" + i;
                c.Emit(0xB9).Imm32(unchecked((int)shaderHashes[i])); // mov ecx, hash
                c.CallAbs(resolveShader);
                c.Emit(0x48, 0x85, 0xC0);                          // test rax, rax
                c.Jump8(0x74, next);
                c.Emit(0x48, 0x89, 0xC1);                          // mov rcx, rax
                c.CallAbs(destroyShader);
                c.Label(next);
            }

            c.CallAbs(reloadShaders);
            c.Emit(0x48, 0x83, 0xC4, 0x20);                        // add rsp, 0x20
            c.Emit(0x5B);                                          // pop rbx
            c.Emit(0xC3);                                          // ret
            return c.ToArray();
        }
    }
}
