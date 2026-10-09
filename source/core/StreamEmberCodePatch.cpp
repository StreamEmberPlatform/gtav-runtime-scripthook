// StreamEmber: short unmanaged patch transaction. No CLR work, allocation or logging while threads are stopped.
#pragma managed(push, off)
#include <Windows.h>
#include <TlHelp32.h>
#include <cstring>

static SRWLOCK patchLock = SRWLOCK_INIT;

extern "C" __declspec(dllexport) BOOL __cdecl SE_ReplaceCode(void* target, const unsigned char* expected,
                                                           const unsigned char* replacement, int size)
{
    if (!target || !expected || !replacement || size <= 0 || size > 4096) return FALSE;
    AcquireSRWLockExclusive(&patchLock);
    HANDLE handles[2048] = {};
    int count = 0, suspended = 0;
    bool ok = true;
    const DWORD process = GetCurrentProcessId(), self = GetCurrentThreadId();
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    if (snapshot == INVALID_HANDLE_VALUE) { ReleaseSRWLockExclusive(&patchLock); return FALSE; }
    THREADENTRY32 entry = {}; entry.dwSize = sizeof(entry);
    if (!Thread32First(snapshot, &entry)) ok = false;
    else do {
        if (entry.th32OwnerProcessID != process || entry.th32ThreadID == self) continue;
        if (count == 2048) { ok = false; break; }
        HANDLE thread = OpenThread(THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION, FALSE, entry.th32ThreadID);
        if (!thread) { if (GetLastError() != ERROR_INVALID_PARAMETER) ok = false; continue; }
        handles[count++] = thread;
    } while (Thread32Next(snapshot, &entry));
    CloseHandle(snapshot);
    // Resolve protection before suspension; never split a write across protection regions.
    MEMORY_BASIC_INFORMATION region = {};
    if (!VirtualQuery(target, &region, sizeof(region)) || region.State != MEM_COMMIT ||
        (region.Protect & (PAGE_NOACCESS | PAGE_GUARD)) ||
        static_cast<unsigned char*>(target) + size > static_cast<unsigned char*>(region.BaseAddress) + region.RegionSize) ok = false;
    if (ok) {
        for (; suspended < count; ++suspended) {
            if (SuspendThread(handles[suspended]) == DWORD(-1)) { ok = false; break; }
            CONTEXT context = {}; context.ContextFlags = CONTEXT_CONTROL;
            if (!GetThreadContext(handles[suspended], &context) ||
                (context.Rip >= reinterpret_cast<DWORD64>(target) && context.Rip < reinterpret_cast<DWORD64>(target) + size)) {
                ++suspended; ok = false; break; // refuse a busy prologue; caller can leave this feature unsupported
            }
        }
    }
    if (ok) {
        __try {
            if (std::memcmp(target, expected, size) != 0) ok = false; // another mod owns this location now
            DWORD previous = 0;
            if (ok && !VirtualProtect(target, size, PAGE_EXECUTE_READWRITE, &previous)) ok = false;
            if (ok) {
                std::memcpy(target, replacement, size);
                FlushInstructionCache(GetCurrentProcess(), target, size);
                DWORD ignored;
                VirtualProtect(target, size, previous, &ignored);
            }
        } __except(EXCEPTION_EXECUTE_HANDLER) { ok = false; }
    }
    for (int i = suspended - 1; i >= 0; --i) ResumeThread(handles[i]);
    for (int i = 0; i < count; ++i) CloseHandle(handles[i]);
    ReleaseSRWLockExclusive(&patchLock);
    return ok ? TRUE : FALSE;
}
#pragma managed(pop)
