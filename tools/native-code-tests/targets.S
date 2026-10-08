# Windows x64 (ms_abi) functions with MSVC-like prologues, used as detour targets.
    .intel_syntax noprefix
    .text
    .globl fake_scr_run
# __int64 fake_scr_run(scrThread* t): returns 1 ("ran"). Prologue = rage::scrThread::Run (Legacy pattern).
fake_scr_run:
    .byte 0x48,0x89,0x5C,0x24,0x08          # mov [rsp+8], rbx
    .byte 0x48,0x89,0x6C,0x24,0x10          # mov [rsp+16], rbp
    .byte 0x48,0x89,0x74,0x24,0x18          # mov [rsp+24], rsi
    push rdi
    push r14
    push r15
    sub rsp, 0x20
    mov eax, 1
    add rsp, 0x20
    pop r15
    pop r14
    pop rdi
    mov rbx, [rsp+8]
    mov rbp, [rsp+16]
    mov rsi, [rsp+24]
    ret

    .globl fake_combine
# void fake_combine(void* self, char* buf): pitch(+0xE)=100, lpf(+8)=20000, hpf(+0xA)=10, volume(+0)=7
fake_combine:
    .byte 0x48,0x89,0x5C,0x24,0x08          # mov [rsp+8], rbx
    push rdi
    sub rsp, 0x20
    mov rdi, rdx
    mov rbx, rcx
    mov word ptr [rdi+0xE], 100
    mov word ptr [rdi+8], 20000
    mov word ptr [rdi+0xA], 10
    mov dword ptr [rdi], 7
    add rsp, 0x20
    pop rdi
    mov rbx, [rsp+8]
    ret

    .globl fake_create_shader
# void* fake_create_shader(name, data, size, type, out*): stores data/size/type/out in globals, returns data
fake_create_shader:
    .byte 0x48,0x89,0x5C,0x24,0x08          # mov [rsp+8], rbx
    .byte 0x48,0x89,0x74,0x24,0x10          # mov [rsp+16], rsi
    push rdi
    sub rsp, 0x20
    mov [rip+g_shader_data], rdx
    mov [rip+g_shader_size], r8d
    mov [rip+g_shader_type], r9d
    mov rax, [rsp+0x28+0x28]                # 5th argument (after push rdi + sub 0x20)
    mov [rip+g_shader_out], rax
    mov rax, rdx
    add rsp, 0x20
    pop rdi
    mov rbx, [rsp+8]
    mov rsi, [rsp+16]
    ret
    .section .note.GNU-stack,"",@progbits
