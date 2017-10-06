section .text

global sha1_block_asm_data_order
align 10h

sha1_block_asm_data_order:

push rdi
push rsi
push rbx
push rbp


push r12
push r13
push r14
push r15
mov r9,rcx

; c = rcx
; p = rdx
; num = r8

    shl r8,     6
    mov r11,rdx

    add r8,     r11
    mov rbp, rcx

    mov edx,        DWORD 12[rbp]
    sub rsp,        120
    mov edi,        DWORD 16[rbp]
    mov ebx,        DWORD 8[rbp]
    mov QWORD 112[rsp],r8
    ; First we need to setup the X array
$L000start:
    ; First, load the words onto the stack in network byte order
    mov r12d,        DWORD [r11]
    mov r13d,        DWORD 4[r11]
    bswap   r12d
    bswap   r13d

    mov r14d,        DWORD 8[r11]
    mov r15d,        DWORD 12[r11]
    bswap   r14d
    bswap   r15d

    mov r10d,        DWORD 16[r11]
    mov r8d,        DWORD 20[r11]
    bswap   r10d
    bswap   r8d

    mov eax,        DWORD 24[r11]
    mov ecx,        DWORD 28[r11]
    bswap   eax
    bswap   ecx
    mov DWORD 24[rsp],eax
    mov DWORD 28[rsp],ecx
    mov eax,        DWORD 32[r11]
    mov ecx,        DWORD 36[r11]
    bswap   eax
    bswap   ecx
    mov DWORD 32[rsp],eax
    mov DWORD 36[rsp],ecx
    mov eax,        DWORD 40[r11]
    mov ecx,        DWORD 44[r11]
    bswap   eax
    bswap   ecx
    mov DWORD 40[rsp],eax
    mov DWORD 44[rsp],ecx
    mov eax,        DWORD 48[r11]
    mov ecx,        DWORD 52[r11]
    bswap   eax
    bswap   ecx
    mov DWORD 48[rsp],eax
    mov DWORD 52[rsp],ecx
    mov eax,        DWORD 56[r11]
    mov ecx,        DWORD 60[r11]
    bswap   eax
    bswap   ecx
    mov DWORD 56[rsp],eax
    mov DWORD 60[rsp],ecx
    ; We now have the X array on the stack
    ; starting at sp-4
    ;;;;;mov    DWORD 132[rsp],esi

$L001shortcut:
    ;
    ; Start processing
    mov eax,        DWORD [r9]
    mov ecx,        DWORD 4[r9]
    ; 00_15 0
    mov esi,        ebx
    mov ebp,        eax
    rol ebp,        5
    xor esi,        edx
    and esi,        ecx
    add ebp,        edi
;    mov edi,        r12d
    xor esi,        edx
    ror ecx,        2
    lea ebp,        DWORD 1518500249[r12d*1+ebp]
    add ebp,        esi
    ; 00_15 1
    mov edi,        ecx
    mov esi,        ebp
    rol ebp,        5
    xor edi,        ebx
    and edi,        eax
    add ebp,        edx
;    mov edx,        r13d
    xor edi,        ebx
    ror eax,        2
    lea ebp,        DWORD 1518500249[r13d*1+ebp]
    add ebp,        edi
    ; 00_15 2
    mov edx,        eax
    mov edi,        ebp
    rol ebp,        5
    xor edx,        ecx
    and edx,        esi
    add ebp,        ebx
;    mov ebx,        r14d
    xor edx,        ecx
    ror esi,        2
    lea ebp,        DWORD 1518500249[r14d*1+ebp]
    add ebp,        edx
    ; 00_15 3
    mov ebx,        esi
    mov edx,        ebp
    rol ebp,        5
    xor ebx,        eax
    and ebx,        edi
    add ebp,        ecx
    mov ecx,        r15d
    xor ebx,        eax
    ror edi,        2
    lea ebp,        DWORD 1518500249[ecx*1+ebp]
    add ebp,        ebx
    ; 00_15 4
    mov ecx,        edi
    mov ebx,        ebp
    rol ebp,        5
    xor ecx,        esi
    and ecx,        edx
    add ebp,        eax
    mov eax,        r10d
    xor ecx,        esi
    ror edx,        2
    lea ebp,        DWORD 1518500249[eax*1+ebp]
    add ebp,        ecx
    ; 00_15 5
    mov eax,        edx
    mov ecx,        ebp
    rol ebp,        5
    xor eax,        edi
    and eax,        ebx
    add ebp,        esi
    mov esi,        r8d
    xor eax,        edi
    ror ebx,        2
    lea ebp,        DWORD 1518500249[esi*1+ebp]
    add ebp,        eax
    ; 00_15 6
    mov esi,        ebx
    mov eax,        ebp
    rol ebp,        5
    xor esi,        edx
    and esi,        ecx
    add ebp,        edi
    mov edi,        DWORD 24[rsp]
    xor esi,        edx
    ror ecx,        2
    lea ebp,        DWORD 1518500249[edi*1+ebp]
    add ebp,        esi
    ; 00_15 7
    mov edi,        ecx
    mov esi,        ebp
    rol ebp,        5
    xor edi,        ebx
    and edi,        eax
    add ebp,        edx
    mov edx,        DWORD 28[rsp]
    xor edi,        ebx
    ror eax,        2
    lea ebp,        DWORD 1518500249[edx*1+ebp]
    add ebp,        edi
    ; 00_15 8
    mov edx,        eax
    mov edi,        ebp
    rol ebp,        5
    xor edx,        ecx
    and edx,        esi
    add ebp,        ebx
    mov ebx,        DWORD 32[rsp]
    xor edx,        ecx
    ror esi,        2
    lea ebp,        DWORD 1518500249[ebx*1+ebp]
    add ebp,        edx
    ; 00_15 9
    mov ebx,        esi
    mov edx,        ebp
    rol ebp,        5
    xor ebx,        eax
    and ebx,        edi
    add ebp,        ecx
    mov ecx,        DWORD 36[rsp]
    xor ebx,        eax
    ror edi,        2
    lea ebp,        DWORD 1518500249[ecx*1+ebp]
    add ebp,        ebx
    ; 00_15 10
    mov ecx,        edi
    mov ebx,        ebp
    rol ebp,        5
    xor ecx,        esi
    and ecx,        edx
    add ebp,        eax
    mov eax,        DWORD 40[rsp]
    xor ecx,        esi
    ror edx,        2
    lea ebp,        DWORD 1518500249[eax*1+ebp]
    add ebp,        ecx
    ; 00_15 11
    mov eax,        edx
    mov ecx,        ebp
    rol ebp,        5
    xor eax,        edi
    and eax,        ebx
    add ebp,        esi
    mov esi,        DWORD 44[rsp]
    xor eax,        edi
    ror ebx,        2
    lea ebp,        DWORD 1518500249[esi*1+ebp]
    add ebp,        eax
    ; 00_15 12
    mov esi,        ebx
    mov eax,        ebp
    rol ebp,        5
    xor esi,        edx
    and esi,        ecx
    add ebp,        edi
    mov edi,        DWORD 48[rsp]
    xor esi,        edx
    ror ecx,        2
    lea ebp,        DWORD 1518500249[edi*1+ebp]
    add ebp,        esi
    ; 00_15 13
    mov edi,        ecx
    mov esi,        ebp
    rol ebp,        5
    xor edi,        ebx
    and edi,        eax
    add ebp,        edx
    mov edx,        DWORD 52[rsp]
    xor edi,        ebx
    ror eax,        2
    lea ebp,        DWORD 1518500249[edx*1+ebp]
    add ebp,        edi
    ; 00_15 14
    mov edx,        eax
    mov edi,        ebp
    rol ebp,        5
    xor edx,        ecx
    and edx,        esi
    add ebp,        ebx
    mov ebx,        DWORD 56[rsp]
    xor edx,        ecx
    ror esi,        2
    lea ebp,        DWORD 1518500249[ebx*1+ebp]
    add ebp,        edx
    ; 00_15 15
    mov ebx,        esi
    mov edx,        ebp
    rol ebp,        5
    xor ebx,        eax
    and ebx,        edi
    add ebp,        ecx
    mov ecx,        DWORD 60[rsp]
    xor ebx,        eax
    ror edi,        2
    lea ebp,        DWORD 1518500249[ecx*1+ebp]
    add ebx,        ebp
    ; 16_19 16
    mov ecx,        r14d
    mov ebp,        edi
    xor ecx,        r12d
    xor ebp,        esi
    xor ecx,        DWORD 32[rsp]
    and ebp,        edx
    ror edx,        2
    xor ecx,        DWORD 52[rsp]
    rol ecx,        1
    xor ebp,        esi
    mov r12d,ecx
    lea ecx,        DWORD 1518500249[eax*1+ecx]
    mov eax,        ebx
    rol eax,        5
    add ecx,        ebp
    add ecx,        eax
    ; 16_19 17
    mov eax,        r15d
    mov ebp,        edx
    xor eax,        r13d
    xor ebp,        edi
    xor eax,        DWORD 36[rsp]
    and ebp,        ebx
    ror ebx,        2
    xor eax,        DWORD 56[rsp]
    rol eax,        1
    xor ebp,        edi
    mov r13d,eax
    lea eax,        DWORD 1518500249[esi*1+eax]
    mov esi,        ecx
    rol esi,        5
    add eax,        ebp
    add eax,        esi
    ; 16_19 18
    mov esi,        r10d
    mov ebp,        ebx
    xor esi,        r14d
    xor ebp,        edx
    xor esi,        DWORD 40[rsp]
    and ebp,        ecx
    ror ecx,        2
    xor esi,        DWORD 60[rsp]
    rol esi,        1
    xor ebp,        edx
    mov r14d,esi
    lea esi,        DWORD 1518500249[edi*1+esi]
    mov edi,        eax
    rol edi,        5
    add esi,        ebp
    add esi,        edi
    ; 16_19 19
    mov edi,        r8d
    mov ebp,        ecx
    xor edi,        r15d
    xor ebp,        ebx
    xor edi,        DWORD 44[rsp]
    and ebp,        eax
    ror eax,        2
    xor edi,        r12d
    rol edi,        1
    xor ebp,        ebx
    mov r15d,edi
    lea edi,        DWORD 1518500249[edx*1+edi]
    mov edx,        esi
    rol edx,        5
    add edi,        ebp
    add edi,        edx
    ; 20_39 20
    mov ebp,        esi
    mov edx,        r10d
    ror esi,        2
    xor edx,        DWORD 24[rsp]
    xor ebp,        eax
    xor edx,        DWORD 48[rsp]
    xor ebp,        ecx
    xor edx,        r13d
    rol edx,        1
    add ebp,        ebx
    mov r10d,edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 1859775393[ebp*1+edx]
    add edx,        ebx
    ; 20_39 21
    mov ebp,        edi
    mov ebx,        r8d
    ror edi,        2
    xor ebx,        DWORD 28[rsp]
    xor ebp,        esi
    xor ebx,        DWORD 52[rsp]
    xor ebp,        eax
    xor ebx,        r14d
    rol ebx,        1
    add ebp,        ecx
    mov r8d,ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 1859775393[ebp*1+ebx]
    add ebx,        ecx
    ; 20_39 22
    mov ebp,        edx
    mov ecx,        DWORD 24[rsp]
    ror edx,        2
    xor ecx,        DWORD 32[rsp]
    xor ebp,        edi
    xor ecx,        DWORD 56[rsp]
    xor ebp,        esi
    xor ecx,        r15d
    rol ecx,        1
    add ebp,        eax
    mov DWORD 24[rsp],ecx
    mov eax,        ebx
    rol eax,        5
    lea ecx,        DWORD 1859775393[ebp*1+ecx]
    add ecx,        eax
    ; 20_39 23
    mov ebp,        ebx
    mov eax,        DWORD 28[rsp]
    ror ebx,        2
    xor eax,        DWORD 36[rsp]
    xor ebp,        edx
    xor eax,        DWORD 60[rsp]
    xor ebp,        edi
    xor eax,        r10d
    rol eax,        1
    add ebp,        esi
    mov DWORD 28[rsp],eax
    mov esi,        ecx
    rol esi,        5
    lea eax,        DWORD 1859775393[ebp*1+eax]
    add eax,        esi
    ; 20_39 24
    mov ebp,        ecx
    mov esi,        DWORD 32[rsp]
    ror ecx,        2
    xor esi,        DWORD 40[rsp]
    xor ebp,        ebx
    xor esi,        r12d
    xor ebp,        edx
    xor esi,        r8d
    rol esi,        1
    add ebp,        edi
    mov DWORD 32[rsp],esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 1859775393[ebp*1+esi]
    add esi,        edi
    ; 20_39 25
    mov ebp,        eax
    mov edi,        DWORD 36[rsp]
    ror eax,        2
    xor edi,        DWORD 44[rsp]
    xor ebp,        ecx
    xor edi,        r13d
    xor ebp,        ebx
    xor edi,        DWORD 24[rsp]
    rol edi,        1
    add ebp,        edx
    mov DWORD 36[rsp],edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 1859775393[ebp*1+edi]
    add edi,        edx
    ; 20_39 26
    mov ebp,        esi
    mov edx,        DWORD 40[rsp]
    ror esi,        2
    xor edx,        DWORD 48[rsp]
    xor ebp,        eax
    xor edx,        r14d
    xor ebp,        ecx
    xor edx,        DWORD 28[rsp]
    rol edx,        1
    add ebp,        ebx
    mov DWORD 40[rsp],edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 1859775393[ebp*1+edx]
    add edx,        ebx
    ; 20_39 27
    mov ebp,        edi
    mov ebx,        DWORD 44[rsp]
    ror edi,        2
    xor ebx,        DWORD 52[rsp]
    xor ebp,        esi
    xor ebx,        r15d
    xor ebp,        eax
    xor ebx,        DWORD 32[rsp]
    rol ebx,        1
    add ebp,        ecx
    mov DWORD 44[rsp],ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 1859775393[ebp*1+ebx]
    add ebx,        ecx
    ; 20_39 28
    mov ebp,        edx
    mov ecx,        DWORD 48[rsp]
    ror edx,        2
    xor ecx,        DWORD 56[rsp]
    xor ebp,        edi
    xor ecx,        r10d
    xor ebp,        esi
    xor ecx,        DWORD 36[rsp]
    rol ecx,        1
    add ebp,        eax
    mov DWORD 48[rsp],ecx
    mov eax,        ebx
    rol eax,        5
    lea ecx,        DWORD 1859775393[ebp*1+ecx]
    add ecx,        eax
    ; 20_39 29
    mov ebp,        ebx
    mov eax,        DWORD 52[rsp]
    ror ebx,        2
    xor eax,        DWORD 60[rsp]
    xor ebp,        edx
    xor eax,        r8d
    xor ebp,        edi
    xor eax,        DWORD 40[rsp]
    rol eax,        1
    add ebp,        esi
    mov DWORD 52[rsp],eax
    mov esi,        ecx
    rol esi,        5
    lea eax,        DWORD 1859775393[ebp*1+eax]
    add eax,        esi
    ; 20_39 30
    mov ebp,        ecx
    mov esi,        DWORD 56[rsp]
    ror ecx,        2
    xor esi,        r12d
    xor ebp,        ebx
    xor esi,        DWORD 24[rsp]
    xor ebp,        edx
    xor esi,        DWORD 44[rsp]
    rol esi,        1
    add ebp,        edi
    mov DWORD 56[rsp],esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 1859775393[ebp*1+esi]
    add esi,        edi
    ; 20_39 31
    mov ebp,        eax
    mov edi,        DWORD 60[rsp]
    ror eax,        2
    xor edi,        r13d
    xor ebp,        ecx
    xor edi,        DWORD 28[rsp]
    xor ebp,        ebx
    xor edi,        DWORD 48[rsp]
    rol edi,        1
    add ebp,        edx
    mov DWORD 60[rsp],edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 1859775393[ebp*1+edi]
    add edi,        edx
    ; 20_39 32
    mov ebp,        esi
;    mov edx,        r12d
    ror esi,        2
    xor r12d,        r14d
    xor ebp,        eax
    xor r12d,        DWORD 32[rsp]
    xor ebp,        ecx
    xor r12d,        DWORD 52[rsp]
    rol r12d,        1
    add ebp,        ebx
;    mov r12d,edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 1859775393[ebp*1+r12d]
    add edx,        ebx
    ; 20_39 33
    mov ebp,        edi
    mov ebx,        r13d
    ror edi,        2
    xor ebx,        r15d
    xor ebp,        esi
    xor ebx,        DWORD 36[rsp]
    xor ebp,        eax
    xor ebx,        DWORD 56[rsp]
    rol ebx,        1
    add ebp,        ecx
    mov r13d,ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 1859775393[ebp*1+ebx]
    add ebx,        ecx
    ; 20_39 34
    mov ebp,        edx
    mov ecx,        r14d
    ror edx,        2
    xor ecx,        r10d
    xor ebp,        edi
    xor ecx,        DWORD 40[rsp]
    xor ebp,        esi
    xor ecx,        DWORD 60[rsp]
    rol ecx,        1
    add ebp,        eax
    mov r14d,ecx
    mov eax,        ebx
    rol eax,        5
    lea ecx,        DWORD 1859775393[ebp*1+ecx]
    add ecx,        eax
    ; 20_39 35
    mov ebp,        ebx
    mov eax,        r15d
    ror ebx,        2
    xor eax,        r8d
    xor ebp,        edx
    xor eax,        DWORD 44[rsp]
    xor ebp,        edi
    xor eax,        r12d
    rol eax,        1
    add ebp,        esi
    mov r15d,eax
    mov esi,        ecx
    rol esi,        5
    lea eax,        DWORD 1859775393[ebp*1+eax]
    add eax,        esi
    ; 20_39 36
    mov ebp,        ecx
    mov esi,        r10d
    ror ecx,        2
    xor esi,        DWORD 24[rsp]
    xor ebp,        ebx
    xor esi,        DWORD 48[rsp]
    xor ebp,        edx
    xor esi,        r13d
    rol esi,        1
    add ebp,        edi
    mov r10d,esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 1859775393[ebp*1+esi]
    add esi,        edi
    ; 20_39 37
    mov ebp,        eax
    mov edi,        r8d
    ror eax,        2
    xor edi,        DWORD 28[rsp]
    xor ebp,        ecx
    xor edi,        DWORD 52[rsp]
    xor ebp,        ebx
    xor edi,        r14d
    rol edi,        1
    add ebp,        edx
    mov r8d,edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 1859775393[ebp*1+edi]
    add edi,        edx
    ; 20_39 38
    mov ebp,        esi
    mov edx,        DWORD 24[rsp]
    ror esi,        2
    xor edx,        DWORD 32[rsp]
    xor ebp,        eax
    xor edx,        DWORD 56[rsp]
    xor ebp,        ecx
    xor edx,        r15d
    rol edx,        1
    add ebp,        ebx
    mov DWORD 24[rsp],edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 1859775393[ebp*1+edx]
    add edx,        ebx
    ; 20_39 39
    mov ebp,        edi
    mov ebx,        DWORD 28[rsp]
    ror edi,        2
    xor ebx,        DWORD 36[rsp]
    xor ebp,        esi
    xor ebx,        DWORD 60[rsp]
    xor ebp,        eax
    xor ebx,        r10d
    rol ebx,        1
    add ebp,        ecx
    mov DWORD 28[rsp],ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 1859775393[ebp*1+ebx]
    add ebx,        ecx
    ; 40_59 40
    mov ecx,        DWORD 32[rsp]
    mov ebp,        DWORD 40[rsp]
    xor ecx,        ebp
;    mov ebp,        r12d
    xor ecx,        r12d
;    mov ebp,        r8d
    xor ecx,        r8d
    mov ebp,        edx
    rol ecx,        1
    or  ebp,        edi
    mov DWORD 32[rsp],ecx
    and ebp,        esi
    lea ecx,        DWORD 2400959708[eax*1+ecx]
    mov eax,        edx
    ror edx,        2
    and eax,        edi
    or  ebp,        eax
    mov eax,        ebx
    rol eax,        5
    add ecx,        ebp
    add ecx,        eax
    ; 40_59 41
    mov eax,        DWORD 36[rsp]
    mov ebp,        DWORD 44[rsp]
    xor eax,        ebp
;    mov ebp,        r13d
    xor eax,        r13d
    mov ebp,        DWORD 24[rsp]
    xor eax,        ebp
    mov ebp,        ebx
    rol eax,        1
    or  ebp,        edx
    mov DWORD 36[rsp],eax
    and ebp,        edi
    lea eax,        DWORD 2400959708[esi*1+eax]
    mov esi,        ebx
    ror ebx,        2
    and esi,        edx
    or  ebp,        esi
    mov esi,        ecx
    rol esi,        5
    add eax,        ebp
    add eax,        esi
    ; 40_59 42
    mov esi,        DWORD 40[rsp]
    mov ebp,        DWORD 48[rsp]
    xor esi,        ebp
;    mov ebp,        r14d
    xor esi,        r14d
    mov ebp,        DWORD 28[rsp]
    xor esi,        ebp
    mov ebp,        ecx
    rol esi,        1
    or  ebp,        ebx
    mov DWORD 40[rsp],esi
    and ebp,        edx
    lea esi,        DWORD 2400959708[edi*1+esi]
    mov edi,        ecx
    ror ecx,        2
    and edi,        ebx
    or  ebp,        edi
    mov edi,        eax
    rol edi,        5
    add esi,        ebp
    add esi,        edi
    ; 40_59 43
    mov edi,        DWORD 44[rsp]
    mov ebp,        DWORD 52[rsp]
    xor edi,        ebp
;    mov ebp,        r15d
    xor edi,        r15d
    mov ebp,        DWORD 32[rsp]
    xor edi,        ebp
    mov ebp,        eax
    rol edi,        1
    or  ebp,        ecx
    mov DWORD 44[rsp],edi
    and ebp,        ebx
    lea edi,        DWORD 2400959708[edx*1+edi]
    mov edx,        eax
    ror eax,        2
    and edx,        ecx
    or  ebp,        edx
    mov edx,        esi
    rol edx,        5
    add edi,        ebp
    add edi,        edx
    ; 40_59 44
    mov edx,        DWORD 48[rsp]
    mov ebp,        DWORD 56[rsp]
    xor edx,        ebp
;    mov ebp,        r10d
    xor edx,        r10d
    mov ebp,        DWORD 36[rsp]
    xor edx,        ebp
    mov ebp,        esi
    rol edx,        1
    or  ebp,        eax
    mov DWORD 48[rsp],edx
    and ebp,        ecx
    lea edx,        DWORD 2400959708[ebx*1+edx]
    mov ebx,        esi
    ror esi,        2
    and ebx,        eax
    or  ebp,        ebx
    mov ebx,        edi
    rol ebx,        5
    add edx,        ebp
    add edx,        ebx
    ; 40_59 45
    mov ebx,        DWORD 52[rsp]
    mov ebp,        DWORD 60[rsp]
    xor ebx,        ebp
;    mov ebp,        r8d
    xor ebx,        r8d
    mov ebp,        DWORD 40[rsp]
    xor ebx,        ebp
    mov ebp,        edi
    rol ebx,        1
    or  ebp,        esi
    mov DWORD 52[rsp],ebx
    and ebp,        eax
    lea ebx,        DWORD 2400959708[ecx*1+ebx]
    mov ecx,        edi
    ror edi,        2
    and ecx,        esi
    or  ebp,        ecx
    mov ecx,        edx
    rol ecx,        5
    add ebx,        ebp
    add ebx,        ecx
    ; 40_59 46
    mov ecx,        DWORD 56[rsp]
;    mov ebp,        r12d
    xor ecx,        r12d
    mov ebp,        DWORD 24[rsp]
    xor ecx,        ebp
    mov ebp,        DWORD 44[rsp]
    xor ecx,        ebp
    mov ebp,        edx
    rol ecx,        1
    or  ebp,        edi
    mov DWORD 56[rsp],ecx
    and ebp,        esi
    lea ecx,        DWORD 2400959708[eax*1+ecx]
    mov eax,        edx
    ror edx,        2
    and eax,        edi
    or  ebp,        eax
    mov eax,        ebx
    rol eax,        5
    add ecx,        ebp
    add ecx,        eax
    ; 40_59 47
    mov eax,        DWORD 60[rsp]
;    mov ebp,        r13d
    xor eax,        r13d
    mov ebp,        DWORD 28[rsp]
    xor eax,        ebp
    mov ebp,        DWORD 48[rsp]
    xor eax,        ebp
    mov ebp,        ebx
    rol eax,        1
    or  ebp,        edx
    mov DWORD 60[rsp],eax
    and ebp,        edi
    lea eax,        DWORD 2400959708[esi*1+eax]
    mov esi,        ebx
    ror ebx,        2
    and esi,        edx
    or  ebp,        esi
    mov esi,        ecx
    rol esi,        5
    add eax,        ebp
    add eax,        esi
    ; 40_59 48
    mov esi,        r12d
;    mov ebp,        r14d
    xor esi,        r14d
    mov ebp,        DWORD 32[rsp]
    xor esi,        ebp
    mov ebp,        DWORD 52[rsp]
    xor esi,        ebp
    mov ebp,        ecx
    rol esi,        1
    or  ebp,        ebx
    mov r12d,esi
    and ebp,        edx
    lea esi,        DWORD 2400959708[edi*1+esi]
    mov edi,        ecx
    ror ecx,        2
    and edi,        ebx
    or  ebp,        edi
    mov edi,        eax
    rol edi,        5
    add esi,        ebp
    add esi,        edi
    ; 40_59 49
    mov edi,        r13d
;    mov ebp,        r15d
    xor edi,        r15d
    mov ebp,        DWORD 36[rsp]
    xor edi,        ebp
    mov ebp,        DWORD 56[rsp]
    xor edi,        ebp
    mov ebp,        eax
    rol edi,        1
    or  ebp,        ecx
    mov r13d,edi
    and ebp,        ebx
    lea edi,        DWORD 2400959708[edx*1+edi]
    mov edx,        eax
    ror eax,        2
    and edx,        ecx
    or  ebp,        edx
    mov edx,        esi
    rol edx,        5
    add edi,        ebp
    add edi,        edx
    ; 40_59 50
    mov edx,        r14d
;    mov ebp,        r10d
    xor edx,        r10d
    mov ebp,        DWORD 40[rsp]
    xor edx,        ebp
    mov ebp,        DWORD 60[rsp]
    xor edx,        ebp
    mov ebp,        esi
    rol edx,        1
    or  ebp,        eax
    mov r14d,edx
    and ebp,        ecx
    lea edx,        DWORD 2400959708[ebx*1+edx]
    mov ebx,        esi
    ror esi,        2
    and ebx,        eax
    or  ebp,        ebx
    mov ebx,        edi
    rol ebx,        5
    add edx,        ebp
    add edx,        ebx
    ; 40_59 51
    mov ebx,        r15d
;    mov ebp,        r8d
    xor ebx,        r8d
    mov ebp,        DWORD 44[rsp]
    xor ebx,        ebp
;    mov ebp,        r12d
    xor ebx,        r12d
    mov ebp,        edi
    rol ebx,        1
    or  ebp,        esi
    mov r15d,ebx
    and ebp,        eax
    lea ebx,        DWORD 2400959708[ecx*1+ebx]
    mov ecx,        edi
    ror edi,        2
    and ecx,        esi
    or  ebp,        ecx
    mov ecx,        edx
    rol ecx,        5
    add ebx,        ebp
    add ebx,        ecx
    ; 40_59 52
    mov ecx,        r10d
    mov ebp,        DWORD 24[rsp]
    xor ecx,        ebp
    mov ebp,        DWORD 48[rsp]
    xor ecx,        ebp
;    mov ebp,        r13d
    xor ecx,        r13d
    mov ebp,        edx
    rol ecx,        1
    or  ebp,        edi
    mov r10d,ecx
    and ebp,        esi
    lea ecx,        DWORD 2400959708[eax*1+ecx]
    mov eax,        edx
    ror edx,        2
    and eax,        edi
    or  ebp,        eax
    mov eax,        ebx
    rol eax,        5
    add ecx,        ebp
    add ecx,        eax
    ; 40_59 53
    mov eax,        r8d
    mov ebp,        DWORD 28[rsp]
    xor eax,        ebp
    mov ebp,        DWORD 52[rsp]
    xor eax,        ebp
;    mov ebp,        r14d
    xor eax,        r14d
    mov ebp,        ebx
    rol eax,        1
    or  ebp,        edx
    mov r8d,eax
    and ebp,        edi
    lea eax,        DWORD 2400959708[esi*1+eax]
    mov esi,        ebx
    ror ebx,        2
    and esi,        edx
    or  ebp,        esi
    mov esi,        ecx
    rol esi,        5
    add eax,        ebp
    add eax,        esi
    ; 40_59 54
    mov esi,        DWORD 24[rsp]
    mov ebp,        DWORD 32[rsp]
    xor esi,        ebp
    mov ebp,        DWORD 56[rsp]
    xor esi,        ebp
;    mov ebp,        r15d
    xor esi,        r15d
    mov ebp,        ecx
    rol esi,        1
    or  ebp,        ebx
    mov DWORD 24[rsp],esi
    and ebp,        edx
    lea esi,        DWORD 2400959708[edi*1+esi]
    mov edi,        ecx
    ror ecx,        2
    and edi,        ebx
    or  ebp,        edi
    mov edi,        eax
    rol edi,        5
    add esi,        ebp
    add esi,        edi
    ; 40_59 55
    mov edi,        DWORD 28[rsp]
    mov ebp,        DWORD 36[rsp]
    xor edi,        ebp
    mov ebp,        DWORD 60[rsp]
    xor edi,        ebp
;    mov ebp,        r10d
    xor edi,        r10d
    mov ebp,        eax
    rol edi,        1
    or  ebp,        ecx
    mov DWORD 28[rsp],edi
    and ebp,        ebx
    lea edi,        DWORD 2400959708[edx*1+edi]
    mov edx,        eax
    ror eax,        2
    and edx,        ecx
    or  ebp,        edx
    mov edx,        esi
    rol edx,        5
    add edi,        ebp
    add edi,        edx
    ; 40_59 56
    mov edx,        DWORD 32[rsp]
    mov ebp,        DWORD 40[rsp]
    xor edx,        ebp
;    mov ebp,        r12d
    xor edx,        r12d
;    mov ebp,        r8d
    xor edx,        r8d
    mov ebp,        esi
    rol edx,        1
    or  ebp,        eax
    mov DWORD 32[rsp],edx
    and ebp,        ecx
    lea edx,        DWORD 2400959708[ebx*1+edx]
    mov ebx,        esi
    ror esi,        2
    and ebx,        eax
    or  ebp,        ebx
    mov ebx,        edi
    rol ebx,        5
    add edx,        ebp
    add edx,        ebx
    ; 40_59 57
    mov ebx,        DWORD 36[rsp]
    mov ebp,        DWORD 44[rsp]
    xor ebx,        ebp
;    mov ebp,        r13d
    xor ebx,        r13d
    mov ebp,        DWORD 24[rsp]
    xor ebx,        ebp
    mov ebp,        edi
    rol ebx,        1
    or  ebp,        esi
    mov DWORD 36[rsp],ebx
    and ebp,        eax
    lea ebx,        DWORD 2400959708[ecx*1+ebx]
    mov ecx,        edi
    ror edi,        2
    and ecx,        esi
    or  ebp,        ecx
    mov ecx,        edx
    rol ecx,        5
    add ebx,        ebp
    add ebx,        ecx
    ; 40_59 58
    mov ecx,        DWORD 40[rsp]
    mov ebp,        DWORD 48[rsp]
    xor ecx,        ebp
;    mov ebp,        r14d
    xor ecx,        r14d
    mov ebp,        DWORD 28[rsp]
    xor ecx,        ebp
    mov ebp,        edx
    rol ecx,        1
    or  ebp,        edi
    mov DWORD 40[rsp],ecx
    and ebp,        esi
    lea ecx,        DWORD 2400959708[eax*1+ecx]
    mov eax,        edx
    ror edx,        2
    and eax,        edi
    or  ebp,        eax
    mov eax,        ebx
    rol eax,        5
    add ecx,        ebp
    add ecx,        eax
    ; 40_59 59
    mov eax,        DWORD 44[rsp]
    mov ebp,        DWORD 52[rsp]
    xor eax,        ebp
;    mov ebp,        r15d
    xor eax,        r15d
    mov ebp,        DWORD 32[rsp]
    xor eax,        ebp
    mov ebp,        ebx
    rol eax,        1
    or  ebp,        edx
    mov DWORD 44[rsp],eax
    and ebp,        edi
    lea eax,        DWORD 2400959708[esi*1+eax]
    mov esi,        ebx
    ror ebx,        2
    and esi,        edx
    or  ebp,        esi
    mov esi,        ecx
    rol esi,        5
    add eax,        ebp
    add eax,        esi
    ; 20_39 60
    mov ebp,        ecx
    mov esi,        DWORD 48[rsp]
    ror ecx,        2
    xor esi,        DWORD 56[rsp]
    xor ebp,        ebx
    xor esi,        r10d
    xor ebp,        edx
    xor esi,        DWORD 36[rsp]
    rol esi,        1
    add ebp,        edi
    mov DWORD 48[rsp],esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 3395469782[ebp*1+esi]
    add esi,        edi
    ; 20_39 61
    mov ebp,        eax
    mov edi,        DWORD 52[rsp]
    ror eax,        2
    xor edi,        DWORD 60[rsp]
    xor ebp,        ecx
    xor edi,        r8d
    xor ebp,        ebx
    xor edi,        DWORD 40[rsp]
    rol edi,        1
    add ebp,        edx
    mov DWORD 52[rsp],edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 3395469782[ebp*1+edi]
    add edi,        edx
    ; 20_39 62
    mov ebp,        esi
    mov edx,        DWORD 56[rsp]
    ror esi,        2
    xor edx,        r12d
    xor ebp,        eax
    xor edx,        DWORD 24[rsp]
    xor ebp,        ecx
    xor edx,        DWORD 44[rsp]
    rol edx,        1
    add ebp,        ebx
    mov DWORD 56[rsp],edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 3395469782[ebp*1+edx]
    add edx,        ebx
    ; 20_39 63
    mov ebp,        edi
    mov ebx,        DWORD 60[rsp]
    ror edi,        2
    xor ebx,        r13d
    xor ebp,        esi
    xor ebx,        DWORD 28[rsp]
    xor ebp,        eax
    xor ebx,        DWORD 48[rsp]
    rol ebx,        1
    add ebp,        ecx
    mov DWORD 60[rsp],ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 3395469782[ebp*1+ebx]
    add ebx,        ecx
    ; 20_39 64
    mov ebp,        edx
    mov ecx,        r12d
    ror edx,        2
    xor ecx,        r14d
    xor ebp,        edi
    xor ecx,        DWORD 32[rsp]
    xor ebp,        esi
    xor ecx,        DWORD 52[rsp]
    rol ecx,        1
    add ebp,        eax
    mov r12d,ecx
    mov eax,        ebx
    rol eax,        5
    lea ecx,        DWORD 3395469782[ebp*1+ecx]
    add ecx,        eax
    ; 20_39 65
    mov ebp,        ebx
    mov eax,        r13d
    ror ebx,        2
    xor eax,        r15d
    xor ebp,        edx
    xor eax,        DWORD 36[rsp]
    xor ebp,        edi
    xor eax,        DWORD 56[rsp]
    rol eax,        1
    add ebp,        esi
    mov r13d,eax
    mov esi,        ecx
    rol esi,        5
    lea eax,        DWORD 3395469782[ebp*1+eax]
    add eax,        esi
    ; 20_39 66
    mov ebp,        ecx
    mov esi,        r14d
    ror ecx,        2
    xor esi,        r10d
    xor ebp,        ebx
    xor esi,        DWORD 40[rsp]
    xor ebp,        edx
    xor esi,        DWORD 60[rsp]
    rol esi,        1
    add ebp,        edi
    mov r14d,esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 3395469782[ebp*1+esi]
    add esi,        edi
    ; 20_39 67
    mov ebp,        eax
    mov edi,        r15d
    ror eax,        2
    xor edi,        r8d
    xor ebp,        ecx
    xor edi,        DWORD 44[rsp]
    xor ebp,        ebx
    xor edi,        r12d
    rol edi,        1
    add ebp,        edx
    mov r15d,edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 3395469782[ebp*1+edi]
    add edi,        edx
    ; 20_39 68
    mov ebp,        esi
    mov edx,        r10d
    ror esi,        2
    xor edx,        DWORD 24[rsp]
    xor ebp,        eax
    xor edx,        DWORD 48[rsp]
    xor ebp,        ecx
    xor edx,        r13d
    rol edx,        1
    add ebp,        ebx
    mov r10d,edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 3395469782[ebp*1+edx]
    add edx,        ebx
    ; 20_39 69
    mov ebp,        edi
    mov ebx,        r8d
    ror edi,        2
    xor ebx,        DWORD 28[rsp]
    xor ebp,        esi
    xor ebx,        DWORD 52[rsp]
    xor ebp,        eax
    xor ebx,        r14d
    rol ebx,        1
    add ebp,        ecx
    mov r8d,ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 3395469782[ebp*1+ebx]
    add ebx,        ecx
    ; 20_39 70
    mov ebp,        edx
    mov ecx,        DWORD 24[rsp]
    ror edx,        2
    xor ecx,        DWORD 32[rsp]
    xor ebp,        edi
    xor ecx,        DWORD 56[rsp]
    xor ebp,        esi
    xor ecx,        r15d
    rol ecx,        1
    add ebp,        eax
    mov DWORD 24[rsp],ecx
    mov eax,        ebx
    rol eax,        5
    lea ecx,        DWORD 3395469782[ebp*1+ecx]
    add ecx,        eax
    ; 20_39 71
    mov ebp,        ebx
    mov eax,        DWORD 28[rsp]
    ror ebx,        2
    xor eax,        DWORD 36[rsp]
    xor ebp,        edx
    xor eax,        DWORD 60[rsp]
    xor ebp,        edi
    xor eax,        r10d
    rol eax,        1
    add ebp,        esi
    mov DWORD 28[rsp],eax
    mov esi,        ecx
    rol esi,        5
    lea eax,        DWORD 3395469782[ebp*1+eax]
    add eax,        esi
    ; 20_39 72
    mov ebp,        ecx
    mov esi,        DWORD 32[rsp]
    ror ecx,        2
    xor esi,        DWORD 40[rsp]
    xor ebp,        ebx
    xor esi,        r12d
    xor ebp,        edx
    xor esi,        r8d
    rol esi,        1
    add ebp,        edi
    mov DWORD 32[rsp],esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 3395469782[ebp*1+esi]
    add esi,        edi
    ; 20_39 73
    mov ebp,        eax
    mov edi,        DWORD 36[rsp]
    ror eax,        2
    xor edi,        DWORD 44[rsp]
    xor ebp,        ecx
    xor edi,        r13d
    xor ebp,        ebx
    xor edi,        DWORD 24[rsp]
    rol edi,        1
    add ebp,        edx
    mov DWORD 36[rsp],edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 3395469782[ebp*1+edi]
    add edi,        edx
    ; 20_39 74
    mov ebp,        esi
    mov edx,        DWORD 40[rsp]
    ror esi,        2
    xor edx,        DWORD 48[rsp]
    xor ebp,        eax
    xor edx,        r14d
    xor ebp,        ecx
    xor edx,        DWORD 28[rsp]
    rol edx,        1
    add ebp,        ebx
    mov DWORD 40[rsp],edx
    mov ebx,        edi
    rol ebx,        5
    lea edx,        DWORD 3395469782[ebp*1+edx]
    add edx,        ebx
    ; 20_39 75
    mov ebp,        edi
    mov ebx,        DWORD 44[rsp]
    ror edi,        2
    xor ebx,        DWORD 52[rsp]
    xor ebp,        esi
    xor ebx,        r15d
    xor ebp,        eax
    xor ebx,        DWORD 32[rsp]
    rol ebx,        1
    add ebp,        ecx
    mov DWORD 44[rsp],ebx
    mov ecx,        edx
    rol ecx,        5
    lea ebx,        DWORD 3395469782[ebp*1+ebx]
    add ebx,        ecx
    ; 20_39 76
    mov ebp,        edx
    mov ecx,        DWORD 48[rsp]
    ror edx,        2
    xor ecx,        DWORD 56[rsp]
    xor ebp,        edi
    xor ecx,        r10d
    xor ebp,        esi
    xor ecx,        DWORD 36[rsp]
    rol ecx,        1
    add ebp,        eax
    mov DWORD 48[rsp],ecx
    mov eax,        ebx
    rol eax,        5
    lea ecx,        DWORD 3395469782[ebp*1+ecx]
    add ecx,        eax
    ; 20_39 77
    mov ebp,        ebx
    mov eax,        DWORD 52[rsp]
    ror ebx,        2
    xor eax,        DWORD 60[rsp]
    xor ebp,        edx
    xor eax,        r8d
    xor ebp,        edi
    xor eax,        DWORD 40[rsp]
    rol eax,        1
    add ebp,        esi
    mov DWORD 52[rsp],eax
    mov esi,        ecx
    rol esi,        5
    lea eax,        DWORD 3395469782[ebp*1+eax]
    add eax,        esi
    ; 20_39 78
    mov ebp,        ecx
    mov esi,        DWORD 56[rsp]
    ror ecx,        2
    xor esi,        r12d
    xor ebp,        ebx
    xor esi,        DWORD 24[rsp]
    xor ebp,        edx
    xor esi,        DWORD 44[rsp]
    rol esi,        1
    add ebp,        edi
    mov DWORD 56[rsp],esi
    mov edi,        eax
    rol edi,        5
    lea esi,        DWORD 3395469782[ebp*1+esi]
    add esi,        edi
    ; 20_39 79
prefetcht1 [r9]
    mov ebp,        eax
    mov edi,        DWORD 60[rsp]
    ror eax,        2
    xor edi,        r13d
    xor ebp,        ecx
    xor edi,        DWORD 28[rsp]
    xor ebp,        ebx
    xor edi,        DWORD 48[rsp]
    rol edi,        1
    add ebp,        edx
    mov DWORD 60[rsp],edi
    mov edx,        esi
    rol edx,        5
    lea edi,        DWORD 3395469782[ebp*1+edi]
    add edi,        edx
    ; End processing
    ;
prefetcht1 [r11+64]
;   mov ebp,        DWORD 128[rsp]
;mov rbp,r9


    mov edx,        DWORD 12[r9]
    add edx,        ecx
    mov ecx,        DWORD 4[r9]
    add ecx,        esi
    mov esi,        eax
    mov eax,        DWORD [r9]
    mov DWORD 12[r9],edx
    add eax,        edi
    mov edi,        DWORD 16[r9]
    add edi,        ebx
    mov ebx,        DWORD 8[r9]
    add ebx,        esi
    mov DWORD [r9],eax
    add r11,        64

    mov DWORD 8[r9],ebx


    mov DWORD 16[r9],edi
    cmp r11,QWORD 112[rsp]
    mov DWORD 4[r9],ecx
    jb  $L000start

mov DWORD [rsp],r12d
mov DWORD 4[rsp],r13d
mov DWORD 8[rsp],r14d
mov DWORD 12[rsp],r15d
mov DWORD 16[rsp],r10d
mov DWORD 20[rsp],r8d
    add rsp,        120

pop r15
pop r14
pop r13
pop r12




pop rbp
pop rbx
pop rsi
pop rdi

    ret
