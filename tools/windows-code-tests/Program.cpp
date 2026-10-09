#include <Windows.h>
#include <atomic>
#include <cstdio>
#include <cstring>
#include <thread>
extern "C" BOOL __cdecl SE_ReplaceCode(void*, const unsigned char*, const unsigned char*, int);
static void Check(bool ok, const char* name) { std::printf("%s %s\n", ok ? "PASS" : "FAIL", name); if (!ok) ExitProcess(1); }
int main()
{
    auto code = static_cast<unsigned char*>(VirtualAlloc(nullptr, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
    Check(code != nullptr, "test allocation");
    unsigned char one[16], two[16]; std::memset(one, 0x90, sizeof one);
    one[0]=0xB8; one[1]=1; one[2]=one[3]=one[4]=0; one[5]=0xC3;
    std::memcpy(two, one, sizeof one); two[1]=2; std::memcpy(code, one, sizeof one);
    DWORD previous; VirtualProtect(code, 4096, PAGE_EXECUTE_READ, &previous);
    Check(SE_ReplaceCode(code, one, two, 16), "replace expected bytes");
    Check(!SE_ReplaceCode(code, one, two, 16), "refuse foreign bytes");
    Check(!SE_ReplaceCode(code, two, one, 0), "reject empty patch");
    Check(SE_ReplaceCode(code, two, one, 16), "restore own bytes");
    MEMORY_BASIC_INFORMATION region{}; VirtualQuery(code,&region,sizeof region);
    Check(region.Protect == PAGE_EXECUTE_READ, "restore memory protection");
    std::atomic<bool> stop{false}, bad{false}; std::atomic<unsigned long long> calls{0};
    std::thread worker([&]{ auto f=reinterpret_cast<int(*)()>(code); while(!stop.load()){int n=f();if(n!=1&&n!=2)bad=true;++calls;} });
    int installed=0; bool currentOne=true;
    for(int i=0;i<500 && installed<100;++i) {
        if(SE_ReplaceCode(code,currentOne?one:two,currentOne?two:one,16)){currentOne=!currentOne;++installed;}
    }
    stop=true; worker.join();
    std::printf("Transactions: %d; calls: %llu; unexpected result: %d\n", installed, calls.load(), int(bad.load()));
    Check(installed>0 && calls>0 && !bad, "concurrent execution never sees half-written instructions");
    VirtualFree(code,0,MEM_RELEASE);
    std::puts("7 checks passed");
}
