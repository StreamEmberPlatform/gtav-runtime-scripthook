// Linux test of the runtime's detour engine and native stubs against Windows-x64 (ms_abi) functions.
using System;
using System.Runtime.InteropServices;
using System.Text;
using SHVDN;

unsafe class LinuxCodeMemory : INativeCodeMemory
{
    [DllImport("libc", SetLastError = true)] static extern IntPtr mmap(IntPtr a, UIntPtr len, int prot, int flags, int fd, IntPtr off);
    [DllImport("libc", SetLastError = true)] static extern int mprotect(IntPtr a, UIntPtr len, int prot);
    byte* _page; int _used;
    public byte* AllocateExecutable(int size)
    {
        size = (size + 15) & ~15;
        if (_page == null || _used + size > 4096) { _page = (byte*)mmap(IntPtr.Zero, (UIntPtr)4096u, 7, 0x22, -1, IntPtr.Zero); _used = 0; }
        byte* r = _page + _used; _used += size; return r;
    }
    public bool WriteCode(byte* address, byte[] bytes)
    {
        long start = (long)address & ~0xFFFL; long end = ((long)address + bytes.Length + 0xFFF) & ~0xFFFL;
        if (mprotect((IntPtr)start, (UIntPtr)(ulong)(end - start), 7) != 0) return false;
        for (int i = 0; i < bytes.Length; i++) address[i] = bytes[i];
        return true;
    }
}

unsafe static class Program
{
    static int failures;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) failures++; }

    static int Main()
    {
        IntPtr lib = NativeLibrary.Load(Environment.GetEnvironmentVariable("HARNESS") ?? "libharness.so");
        IntPtr S(string n) => NativeLibrary.GetExport(lib, n);
        var callScr = (delegate* unmanaged<IntPtr, IntPtr, long>)S("call_scr");
        var callCombine = (delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)S("call_combine");
        var callShader = (delegate* unmanaged<IntPtr, byte*, IntPtr, uint, uint, IntPtr, IntPtr>)S("call_shader");
        var callPresent = (delegate* unmanaged<IntPtr, void>)S("call_present");
        var getCounts = (delegate* unmanaged<int, int>)S("get_counts");
        var mem = new LinuxCodeMemory();

        // --- decoder ---
        byte[] scrPrologue = { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x57 };
        fixed (byte* p = scrPrologue) Check(PrologueDecoder.StealLength(p, 14) == 15, "decoder: scrThread::Run prologue = 15 bytes");
        byte[] rip = { 0x48, 0x8B, 0x05, 1, 2, 3, 4, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };
        fixed (byte* p = rip) Check(PrologueDecoder.StealLength(p, 14) == 0, "decoder: refuses RIP-relative mov");
        byte[] jmp = { 0xE9, 0, 0, 0, 0, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };
        fixed (byte* p = jmp) Check(PrologueDecoder.StealLength(p, 14) == 0, "decoder: refuses an already hooked function (jmp)");
        byte[] misc = { 0x40, 0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x8B, 0xC4, 0x0F, 0x29, 0x70, 0xE8, 0x48, 0x81, 0xEC, 0x00, 0x01, 0x00, 0x00 };
        fixed (byte* p = misc) Check(PrologueDecoder.StealLength(p, 14) == 20, "decoder: push/sub/mov/movaps/sub imm32 = 20 bytes");

        // --- script thread block ---
        byte* scr = (byte*)S("fake_scr_run");
        var thread = new byte[0x20];
        uint hMain = Joaat("main"), hShop = Joaat("shop_controller");
        byte* control = mem.AllocateExecutable(16); control[0] = 0;
        NativeDetour d = NativeDetour.Create(mem, scr, out string err);
        Check(d != null, "detour scr created " + err);
        byte* stub = mem.AllocateExecutable(256);
        byte[] code = NativeStubs.ScriptThreadBlock((ulong)control, (ulong)d.Trampoline, 0x0C, new[] { hMain, Joaat("main_persistent"), Joaat("control_thread") });
        mem.WriteCode(stub, code);
        Check(d.Install(stub), "detour scr installed");
        fixed (byte* t = thread)
        {
            *(uint*)(t + 0xC) = hShop;
            Check(callScr((IntPtr)scr, (IntPtr)t) == 1, "scr: not blocked -> original runs (1)");
            control[0] = 1;
            Check(callScr((IntPtr)scr, (IntPtr)t) == 0, "scr: blocked shop_controller -> 0");
            *(uint*)(t + 0xC) = hMain;
            Check(callScr((IntPtr)scr, (IntPtr)t) == 1, "scr: blocked but main allowed -> 1");
            control[0] = 0;
            Check(d.Uninstall(), "detour scr uninstalled");
            *(uint*)(t + 0xC) = hShop; control[0] = 1;
            Check(callScr((IntPtr)scr, (IntPtr)t) == 1, "scr: after uninstall original runs");
        }

        // --- audio CombineBuffers ---
        byte* combine = (byte*)S("fake_combine");
        int* actl = (int*)mem.AllocateExecutable(32);
        for (int i = 0; i < 8; i++) actl[i] = 0;
        NativeDetour da = NativeDetour.Create(mem, combine, out err);
        Check(da != null, "detour audio created " + err);
        byte* astub = mem.AllocateExecutable(512);
        mem.WriteCode(astub, NativeStubs.AudioCombineBuffers((ulong)actl, (ulong)da.Trampoline));
        Check(da.Install(astub), "detour audio installed");
        var buf = new byte[0x20];
        fixed (byte* b = buf)
        {
            callCombine((IntPtr)combine, IntPtr.Zero, (IntPtr)b);
            Check(*(short*)(b + 0xE) == 100 && *(short*)(b + 8) == 20000 && *(short*)(b + 0xA) == 10 && *(int*)b == 7, "audio: no override keeps original values");
            actl[1] = 1200; actl[0] = 1;
            actl[3] = 500; actl[2] = 1;
            actl[5] = 3000; actl[4] = 1;
            actl[7] = -2000; actl[6] = 1;
            callCombine((IntPtr)combine, IntPtr.Zero, (IntPtr)b);
            Check(*(short*)(b + 0xE) == 1300, "audio: pitch 100 + 1200 = " + *(short*)(b + 0xE));
            Check(*(short*)(b + 8) == 500, "audio: lpf min(20000, 500) = " + *(short*)(b + 8));
            Check(*(short*)(b + 0xA) == 3000, "audio: hpf max(10, 3000) = " + *(short*)(b + 0xA));
            Check(*(int*)b == -2000, "audio: volume = " + *(int*)b);
            actl[1] = 9000;
            callCombine((IntPtr)combine, IntPtr.Zero, (IntPtr)b);
            Check(*(short*)(b + 0xE) == 5000, "audio: pitch clamped to 5000");
            actl[1] = -9000;
            callCombine((IntPtr)combine, IntPtr.Zero, (IntPtr)b);
            Check(*(short*)(b + 0xE) == -5000, "audio: pitch clamped to -5000");
            actl[3] = 30000; // above the original: keep the original
            callCombine((IntPtr)combine, IntPtr.Zero, (IntPtr)b);
            Check(*(short*)(b + 8) == 20000, "audio: lpf keeps lower original");
        }

        // --- CreateShader ---
        byte* shader = (byte*)S("fake_create_shader");
        byte* sctl = mem.AllocateExecutable(32);
        for (int i = 0; i < 32; i++) sctl[i] = 0;
        NativeDetour ds = NativeDetour.Create(mem, shader, out err);
        Check(ds != null, "detour shader created " + err);
        byte* sstub = mem.AllocateExecutable(512);
        mem.WriteCode(sstub, NativeStubs.CreateShader((ulong)sctl, (ulong)ds.Trampoline, (ulong)S("ms_strstr")));
        Check(ds.Install(sstub), "detour shader installed");
        byte[] filter = Encoding.ASCII.GetBytes("PS_LensDistortion\0");
        byte[] replacement = new byte[64];
        var getData = (delegate* unmanaged<IntPtr>)S("get_shader_data");
        var getSize = (delegate* unmanaged<uint>)S("get_shader_size");
        var getType = (delegate* unmanaged<uint>)S("get_shader_type");
        var getOut = (delegate* unmanaged<IntPtr>)S("get_shader_out");
        fixed (byte* f = filter) fixed (byte* rep = replacement)
        {
            byte[] name1 = Encoding.ASCII.GetBytes("postfx:PS_LensDistortion_x\0");
            byte[] name2 = Encoding.ASCII.GetBytes("postfx:PS_Bloom\0");
            IntPtr orig = (IntPtr)0x1234, outp = (IntPtr)0x5678;
            fixed (byte* n1 = name1) fixed (byte* n2 = name2)
            {
                IntPtr r0 = callShader((IntPtr)shader, n1, orig, 111, 3, outp);
                Check(getData() == orig && getSize() == 111 && getType() == 3 && getOut() == outp && r0 == orig, "shader: inactive passes through all 5 args");
                *(ulong*)(sctl + 8) = (ulong)f; *(ulong*)(sctl + 16) = (ulong)rep; *(uint*)(sctl + 24) = 64; sctl[0] = 1;
                IntPtr r1 = callShader((IntPtr)shader, n1, orig, 111, 3, outp);
                Check(getData() == (IntPtr)rep && getSize() == 64 && getType() == 3 && getOut() == outp && r1 == (IntPtr)rep, "shader: matching name gets the replacement bytecode");
                IntPtr r2 = callShader((IntPtr)shader, n2, orig, 111, 4, outp);
                Check(getData() == orig && getSize() == 111 && getType() == 4 && getOut() == outp && r2 == orig, "shader: other name keeps its bytecode");
                IntPtr r3 = callShader((IntPtr)shader, null, orig, 111, 5, outp);
                Check(getData() == orig && getType() == 5, "shader: null name is safe");
            }
        }

        // --- present refresh ---
        byte* pctl = mem.AllocateExecutable(16); pctl[0] = 0;
        byte* pstub = mem.AllocateExecutable(512);
        uint[] hashes = { 1, 2, 3, 5, 8, 13 };
        mem.WriteCode(pstub, NativeStubs.ShaderRefreshOnPresent((ulong)pctl, (ulong)S("fake_resolve"), (ulong)S("fake_destroy"), (ulong)S("fake_reload"), hashes));
        callPresent((IntPtr)pstub);
        Check(getCounts(0) == 0 && getCounts(2) == 0, "present: nothing pending -> no work");
        pctl[0] = 1;
        callPresent((IntPtr)pstub);
        Check(getCounts(0) == 6 && getCounts(1) == 4 && getCounts(2) == 1 && pctl[0] == 0, $"present: resolve {getCounts(0)}/6, destroy {getCounts(1)}/4 (odd hashes), reload {getCounts(2)}/1, flag cleared");

        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures;
    }

    static uint Joaat(string s) { uint h = 0; foreach (char c in s.ToLowerInvariant()) { h += c; h += h << 10; h ^= h >> 6; } h += h << 3; h ^= h >> 11; h += h << 15; return h; }
}
