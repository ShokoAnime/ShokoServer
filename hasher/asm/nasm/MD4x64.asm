SECTION .text
global MD4_x64
align 10h
MD4_x64:
    push    rbp
    push    rbx
    push    r12
    push    r13
    push    r14
    push    r15
    push    rsi
    push    rdi
; parameter 1 in rcx, param 2 in rdx , param 3 in r8

; see http://weblogs.asp.net/oldnewthing/archive/2004/01/14/58579.aspx and
; http://msdn.microsoft.com/library/en-us/kmarch/hh/kmarch/64bitAMD_8e951dd2-ee77-4728-8702-55ce4b5dd24a.xml.asp
;
; All registers must be preserved across the call, except for
;   rax, rcx, rdx, r8, r-9, r10, and r11, which are scratch.

    ;# rdi = arg #1 (ctx, MD5_CTX pointer)
    ;# rsi = arg #2 (ptr, data pointer)
    ;# rdx = arg #3 (nbr, number of 16-word blocks to process)

    mov rsi,rdx
    mov edx,r8d

    mov r12,rcx ;# rbp = ctx
    shl rdx,6   ;# rdx = nbr in bytes
    push r12
    lea rdi,[rsi+rdx];  # rdi = end

    mov eax,DWORD 0[r12]    ;# eax = ctx->A
    mov ebx,DWORD 4[r12]    ;# ebx = ctx->B
    mov ecx,DWORD 8[r12]    ;# ecx = ctx->C
    mov edx,DWORD 12[r12]   ;# edx = ctx->D
    ;push   rbp     ;# save ctx
    ;# end is 'rdi'
    ;# ptr is 'rsi'
    ;# A is 'eax'
    ;# B is 'ebx'
    ;# C is 'ecx'
    ;# D is 'edx'


    cmp rsi,rdi     ;# cmp end with ptr
    mov r13d,0ffffffffh
    je  lab1        ;# jmp if ptr == end

    ;# BEGIN of loop over 16-word blocks
lab2:   ;# save old values of A, B, C, D
    mov r8d,eax
    mov r9d,ebx
    mov r14d,ecx
    mov r15d,edx
; BEGIN of the round serie
  mov r10 , QWORD (0*4)[rsi]      ;/* (NEXT STEP) X[0] */
  mov r11d , edx          ;/* (NEXT STEP) z' = %edx */
    xor r11d,ecx     ;/* y ^ ... */
    lea eax, [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ebx             ;/* x & ... */
    xor r11d,edx             ;/* z ^ ... */
    ;mov    r10d,DWORD (1*4)[rsi]     ;/* (NEXT STEP) X[1] */
    shr r10,32
    add eax,r11d           ;/* dst += ... */
    rol eax, 3            ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) z' = ecx */
    xor r11d,ebx     ;/* y ^ ... */
    lea edx, [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,eax             ;/* x & ... */
    xor r11d,ecx             ;/* z ^ ... */
    mov r10,QWORD (2*4)[rsi]      ;/* (NEXT STEP) X[2] */
    add edx,r11d           ;/* dst += ... */
    rol edx, 7            ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) z' = ebx */
    xor r11d,eax     ;/* y ^ ... */
    lea ecx, [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,edx             ;/* x & ... */
    xor r11d,ebx             ;/* z ^ ... */
    ;mov    r10d,DWORD (3*4)[rsi]     ;/* (NEXT STEP) X[3] */
    shr r10,32
    add ecx,r11d           ;/* dst += ... */
    rol ecx, 11            ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) z' = eax */
    xor r11d,edx     ;/* y ^ ... */
    lea ebx, [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ecx             ;/* x & ... */
    xor r11d,eax             ;/* z ^ ... */
    mov r10,QWORD (4*4)[rsi]      ;/* (NEXT STEP) X[4] */
    add ebx,r11d           ;/* dst += ... */
    rol ebx, 19            ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) z' = edx */
    xor r11d,ecx     ;/* y ^ ... */
    lea eax, [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ebx             ;/* x & ... */
    xor r11d,edx             ;/* z ^ ... */
    ;mov    r10d,DWORD (5*4)[rsi]     ;/* (NEXT STEP) X[5] */
    shr r10,32
    add eax,r11d           ;/* dst += ... */
    rol eax, 3            ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) z' = ecx */
    xor r11d,ebx     ;/* y ^ ... */
    lea edx, [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,eax             ;/* x & ... */
    xor r11d,ecx             ;/* z ^ ... */
    mov r10,QWORD (6*4)[rsi]      ;/* (NEXT STEP) X[6] */
    add edx,r11d           ;/* dst += ... */
    rol edx, 7            ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) z' = ebx */
    xor r11d,eax     ;/* y ^ ... */
    lea ecx, [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,edx             ;/* x & ... */
    xor r11d,ebx             ;/* z ^ ... */
    ;mov    r10d,DWORD (7*4)[rsi]     ;/* (NEXT STEP) X[7] */
    shr r10,32
    add ecx,r11d           ;/* dst += ... */
    rol ecx, 11            ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) z' = eax */
    xor r11d,edx     ;/* y ^ ... */
    lea ebx, [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ecx             ;/* x & ... */
    xor r11d,eax             ;/* z ^ ... */
    mov r10,QWORD (8*4)[rsi]      ;/* (NEXT STEP) X[8] */
    add ebx,r11d           ;/* dst += ... */
    rol ebx, 19            ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) z' = edx */
    xor r11d,ecx     ;/* y ^ ... */
    lea eax, [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ebx             ;/* x & ... */
    xor r11d,edx             ;/* z ^ ... */
    ;mov    r10d,DWORD (9*4)[rsi]     ;/* (NEXT STEP) X[9] */
    shr r10,32
    add eax,r11d           ;/* dst += ... */
    rol eax, 3            ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) z' = ecx */
    xor r11d,ebx     ;/* y ^ ... */
    lea edx, [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,eax             ;/* x & ... */
    xor r11d,ecx             ;/* z ^ ... */
    mov r10,QWORD (10*4)[rsi]      ;/* (NEXT STEP) X[10] */
    add edx,r11d           ;/* dst += ... */
    rol edx, 7            ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) z' = ebx */
    xor r11d,eax     ;/* y ^ ... */
    lea ecx, [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,edx             ;/* x & ... */
    xor r11d,ebx             ;/* z ^ ... */
    ;mov    r10d,DWORD (11*4)[rsi]     ;/* (NEXT STEP) X[11] */
    shr r10,32
    add ecx,r11d           ;/* dst += ... */
    rol ecx, 11            ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) z' = eax */
    xor r11d,edx     ;/* y ^ ... */
    lea ebx, [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ecx             ;/* x & ... */
    xor r11d,eax             ;/* z ^ ... */
    mov r10,QWORD (12*4)[rsi]      ;/* (NEXT STEP) X[12] */
    add ebx,r11d           ;/* dst += ... */
    rol ebx, 19            ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) z' = edx */
    xor r11d,ecx     ;/* y ^ ... */
    lea eax, [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ebx             ;/* x & ... */
    xor r11d,edx             ;/* z ^ ... */
    ;mov    r10d,DWORD (13*4)[rsi]     ;/* (NEXT STEP) X[13] */
    shr r10,32
    add eax,r11d           ;/* dst += ... */
    rol eax, 3            ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) z' = ecx */
    xor r11d,ebx     ;/* y ^ ... */
    lea edx, [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,eax             ;/* x & ... */
    xor r11d,ecx             ;/* z ^ ... */
    mov r10,QWORD (14*4)[rsi]      ;/* (NEXT STEP) X[14] */
    add edx,r11d           ;/* dst += ... */
    rol edx, 7            ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) z' = ebx */
    xor r11d,eax     ;/* y ^ ... */
    lea ecx, [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,edx             ;/* x & ... */
    xor r11d,ebx             ;/* z ^ ... */
    ;mov    r10d,DWORD (15*4)[rsi]     ;/* (NEXT STEP) X[15] */
    shr r10,32
    add ecx,r11d           ;/* dst += ... */
    rol ecx, 11            ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) z' = eax */
    xor r11d,edx     ;/* y ^ ... */
    lea ebx, [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,ecx             ;/* x & ... */
    xor r11d,eax             ;/* z ^ ... */
    mov r10,QWORD (0*4)[rsi]      ;/* (NEXT STEP) X[0] */
    add ebx,r11d           ;/* dst += ... */
    rol ebx, 19            ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) z' = edx */
 mov  r10d , [rsi]      ;/* (NEXT STEP) X[1] */
 mov  r11d, ecx       ;/* (NEXT STEP) z' = %edx */
 mov  r12d, ecx       ;/* (NEXT STEP) z' = %edx */

    lea eax,DWORD 5A827999h [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ebx
    or r12d, ebx
    mov r10d , (4*4) [rsi]        ;/* (NEXT STEP) X[4] */    
    and r12d, edx
    or r11d,r12d
    mov  r12d, ebx       ;/* (NEXT STEP) z' = ebx */
    add eax,r11d
    mov  r11d, ebx       ;/* (NEXT STEP) z' = ebx */
    rol eax , 3           ;/* dst <<< s */


    lea edx,DWORD 5A827999h [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, eax
    or r12d, eax
    mov r10d , (8*4) [rsi]        ;/* (NEXT STEP) X[8] */    
    and r12d, ecx
    or r11d,r12d
    mov  r12d, eax       ;/* (NEXT STEP) z' = eax */
    add edx,r11d
    mov  r11d, eax       ;/* (NEXT STEP) z' = eax */
    rol edx , 5           ;/* dst <<< s */


    lea ecx,DWORD 5A827999h [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, edx
    or r12d, edx
    mov r10d , (12*4) [rsi]        ;/* (NEXT STEP) X[12] */    
    and r12d, ebx
    or r11d,r12d
    mov  r12d, edx       ;/* (NEXT STEP) z' = edx */
    add ecx,r11d
    mov  r11d, edx       ;/* (NEXT STEP) z' = edx */
    rol ecx , 9           ;/* dst <<< s */


    lea ebx,DWORD 5A827999h [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ecx
    or r12d, ecx
    mov r10d , (1*4) [rsi]        ;/* (NEXT STEP) X[1] */    
    and r12d, eax
    or r11d,r12d
    mov  r12d, ecx       ;/* (NEXT STEP) z' = ecx */
    add ebx,r11d
    mov  r11d, ecx       ;/* (NEXT STEP) z' = ecx */
    rol ebx , 13           ;/* dst <<< s */


    lea eax,DWORD 5A827999h [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ebx
    or r12d, ebx
    mov r10d , (5*4) [rsi]        ;/* (NEXT STEP) X[5] */    
    and r12d, edx
    or r11d,r12d
    mov  r12d, ebx       ;/* (NEXT STEP) z' = ebx */
    add eax,r11d
    mov  r11d, ebx       ;/* (NEXT STEP) z' = ebx */
    rol eax , 3           ;/* dst <<< s */


    lea edx,DWORD 5A827999h [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, eax
    or r12d, eax
    mov r10d , (9*4) [rsi]        ;/* (NEXT STEP) X[9] */    
    and r12d, ecx
    or r11d,r12d
    mov  r12d, eax       ;/* (NEXT STEP) z' = eax */
    add edx,r11d
    mov  r11d, eax       ;/* (NEXT STEP) z' = eax */
    rol edx , 5           ;/* dst <<< s */


    lea ecx,DWORD 5A827999h [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, edx
    or r12d, edx
    mov r10d , (13*4) [rsi]        ;/* (NEXT STEP) X[13] */    
    and r12d, ebx
    or r11d,r12d
    mov  r12d, edx       ;/* (NEXT STEP) z' = edx */
    add ecx,r11d
    mov  r11d, edx       ;/* (NEXT STEP) z' = edx */
    rol ecx , 9           ;/* dst <<< s */


    lea ebx,DWORD 5A827999h [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ecx
    or r12d, ecx
    mov r10d , (2*4) [rsi]        ;/* (NEXT STEP) X[2] */    
    and r12d, eax
    or r11d,r12d
    mov  r12d, ecx       ;/* (NEXT STEP) z' = ecx */
    add ebx,r11d
    mov  r11d, ecx       ;/* (NEXT STEP) z' = ecx */
    rol ebx , 13           ;/* dst <<< s */


    lea eax,DWORD 5A827999h [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ebx
    or r12d, ebx
    mov r10d , (6*4) [rsi]        ;/* (NEXT STEP) X[6] */    
    and r12d, edx
    or r11d,r12d
    mov  r12d, ebx       ;/* (NEXT STEP) z' = ebx */
    add eax,r11d
    mov  r11d, ebx       ;/* (NEXT STEP) z' = ebx */
    rol eax , 3           ;/* dst <<< s */


    lea edx,DWORD 5A827999h [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, eax
    or r12d, eax
    mov r10d , (10*4) [rsi]        ;/* (NEXT STEP) X[10] */    
    and r12d, ecx
    or r11d,r12d
    mov  r12d, eax       ;/* (NEXT STEP) z' = eax */
    add edx,r11d
    mov  r11d, eax       ;/* (NEXT STEP) z' = eax */
    rol edx , 5           ;/* dst <<< s */


    lea ecx,DWORD 5A827999h [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, edx
    or r12d, edx
    mov r10d , (14*4) [rsi]        ;/* (NEXT STEP) X[14] */    
    and r12d, ebx
    or r11d,r12d
    mov  r12d, edx       ;/* (NEXT STEP) z' = edx */
    add ecx,r11d
    mov  r11d, edx       ;/* (NEXT STEP) z' = edx */
    rol ecx , 9           ;/* dst <<< s */


    lea ebx,DWORD 5A827999h [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ecx
    or r12d, ecx
    mov r10d , (3*4) [rsi]        ;/* (NEXT STEP) X[3] */    
    and r12d, eax
    or r11d,r12d
    mov  r12d, ecx       ;/* (NEXT STEP) z' = ecx */
    add ebx,r11d
    mov  r11d, ecx       ;/* (NEXT STEP) z' = ecx */
    rol ebx , 13           ;/* dst <<< s */


    lea eax,DWORD 5A827999h [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ebx
    or r12d, ebx
    mov r10d , (7*4) [rsi]        ;/* (NEXT STEP) X[7] */    
    and r12d, edx
    or r11d,r12d
    mov  r12d, ebx       ;/* (NEXT STEP) z' = ebx */
    add eax,r11d
    mov  r11d, ebx       ;/* (NEXT STEP) z' = ebx */
    rol eax , 3           ;/* dst <<< s */


    lea edx,DWORD 5A827999h [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, eax
    or r12d, eax
    mov r10d , (11*4) [rsi]        ;/* (NEXT STEP) X[11] */    
    and r12d, ecx
    or r11d,r12d
    mov  r12d, eax       ;/* (NEXT STEP) z' = eax */
    add edx,r11d
    mov  r11d, eax       ;/* (NEXT STEP) z' = eax */
    rol edx , 5           ;/* dst <<< s */


    lea ecx,DWORD 5A827999h [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, edx
    or r12d, edx
    mov r10d , (15*4) [rsi]        ;/* (NEXT STEP) X[15] */    
    and r12d, ebx
    or r11d,r12d
    mov  r12d, edx       ;/* (NEXT STEP) z' = edx */
    add ecx,r11d
    mov  r11d, edx       ;/* (NEXT STEP) z' = edx */
    rol ecx , 9           ;/* dst <<< s */


    lea ebx,DWORD 5A827999h [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, ecx
    or r12d, ecx
    mov r10d , (0*4) [rsi]        ;/* (NEXT STEP) X[0] */    
    and r12d, eax
    or r11d,r12d
    mov  r12d, ecx       ;/* (NEXT STEP) z' = ecx */
    add ebx,r11d
    mov  r11d, ecx       ;/* (NEXT STEP) z' = ecx */
    rol ebx , 13           ;/* dst <<< s */

 mov  r10d , [rsi]       ;/* (NEXT STEP) X[5] */
 mov  r11d , ecx      ;/* (NEXT STEP) y' = %ecx */
    lea eax,DWORD 6ED9EBA1H [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (8*4)[rsi]     ;/* (NEXT STEP) X[8] */
    xor r11d,edx         ;/* z ^ ... */
    xor r11d,ebx             ;/* x ^ ... */
    add eax , r11d         ;/* dst += ... */
    rol eax , 3           ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) y' = ebx */
    lea edx,DWORD 6ED9EBA1H [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (4*4)[rsi]     ;/* (NEXT STEP) X[4] */
    xor r11d,ecx         ;/* z ^ ... */
    xor r11d,eax             ;/* x ^ ... */
    add edx , r11d         ;/* dst += ... */
    rol edx , 9           ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) y' = eax */
    lea ecx,DWORD 6ED9EBA1H [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (12*4)[rsi]     ;/* (NEXT STEP) X[12] */
    xor r11d,ebx         ;/* z ^ ... */
    xor r11d,edx             ;/* x ^ ... */
    add ecx , r11d         ;/* dst += ... */
    rol ecx , 11           ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) y' = edx */
    lea ebx,DWORD 6ED9EBA1H [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (2*4)[rsi]     ;/* (NEXT STEP) X[2] */
    xor r11d,eax         ;/* z ^ ... */
    xor r11d,ecx             ;/* x ^ ... */
    add ebx , r11d         ;/* dst += ... */
    rol ebx , 15           ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) y' = ecx */
    lea eax,DWORD 6ED9EBA1H [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (10*4)[rsi]     ;/* (NEXT STEP) X[10] */
    xor r11d,edx         ;/* z ^ ... */
    xor r11d,ebx             ;/* x ^ ... */
    add eax , r11d         ;/* dst += ... */
    rol eax , 3           ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) y' = ebx */
    lea edx,DWORD 6ED9EBA1H [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (6*4)[rsi]     ;/* (NEXT STEP) X[6] */
    xor r11d,ecx         ;/* z ^ ... */
    xor r11d,eax             ;/* x ^ ... */
    add edx , r11d         ;/* dst += ... */
    rol edx , 9           ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) y' = eax */
    lea ecx,DWORD 6ED9EBA1H [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (14*4)[rsi]     ;/* (NEXT STEP) X[14] */
    xor r11d,ebx         ;/* z ^ ... */
    xor r11d,edx             ;/* x ^ ... */
    add ecx , r11d         ;/* dst += ... */
    rol ecx , 11           ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) y' = edx */
    lea ebx,DWORD 6ED9EBA1H [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (1*4)[rsi]     ;/* (NEXT STEP) X[1] */
    xor r11d,eax         ;/* z ^ ... */
    xor r11d,ecx             ;/* x ^ ... */
    add ebx , r11d         ;/* dst += ... */
    rol ebx , 15           ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) y' = ecx */
    lea eax,DWORD 6ED9EBA1H [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (9*4)[rsi]     ;/* (NEXT STEP) X[9] */
    xor r11d,edx         ;/* z ^ ... */
    xor r11d,ebx             ;/* x ^ ... */
    add eax , r11d         ;/* dst += ... */
    rol eax , 3           ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) y' = ebx */
    lea edx,DWORD 6ED9EBA1H [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (5*4)[rsi]     ;/* (NEXT STEP) X[5] */
    xor r11d,ecx         ;/* z ^ ... */
    xor r11d,eax             ;/* x ^ ... */
    add edx , r11d         ;/* dst += ... */
    rol edx , 9           ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) y' = eax */
    lea ecx,DWORD 6ED9EBA1H [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (13*4)[rsi]     ;/* (NEXT STEP) X[13] */
    xor r11d,ebx         ;/* z ^ ... */
    xor r11d,edx             ;/* x ^ ... */
    add ecx , r11d         ;/* dst += ... */
    rol ecx , 11           ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) y' = edx */
    lea ebx,DWORD 6ED9EBA1H [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (3*4)[rsi]     ;/* (NEXT STEP) X[3] */
    xor r11d,eax         ;/* z ^ ... */
    xor r11d,ecx             ;/* x ^ ... */
    add ebx , r11d         ;/* dst += ... */
    rol ebx , 15           ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) y' = ecx */
    lea eax,DWORD 6ED9EBA1H [ eax * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (11*4)[rsi]     ;/* (NEXT STEP) X[11] */
    xor r11d,edx         ;/* z ^ ... */
    xor r11d,ebx             ;/* x ^ ... */
    add eax , r11d         ;/* dst += ... */
    rol eax , 3           ;/* dst <<< s */
    mov r11d , ebx           ;/* (NEXT STEP) y' = ebx */
    lea edx,DWORD 6ED9EBA1H [ edx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (7*4)[rsi]     ;/* (NEXT STEP) X[7] */
    xor r11d,ecx         ;/* z ^ ... */
    xor r11d,eax             ;/* x ^ ... */
    add edx , r11d         ;/* dst += ... */
    rol edx , 9           ;/* dst <<< s */
    mov r11d , eax           ;/* (NEXT STEP) y' = eax */
    lea ecx,DWORD 6ED9EBA1H [ ecx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (15*4)[rsi]     ;/* (NEXT STEP) X[15] */
    xor r11d,ebx         ;/* z ^ ... */
    xor r11d,edx             ;/* x ^ ... */
    add ecx , r11d         ;/* dst += ... */
    rol ecx , 11           ;/* dst <<< s */
    mov r11d , edx           ;/* (NEXT STEP) y' = edx */
    lea ebx,DWORD 6ED9EBA1H [ ebx * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD (0*4)[rsi]     ;/* (NEXT STEP) X[0] */
    xor r11d,eax         ;/* z ^ ... */
    xor r11d,ecx             ;/* x ^ ... */
    add ebx , r11d         ;/* dst += ... */
    rol ebx , 15           ;/* dst <<< s */
    mov r11d , ecx           ;/* (NEXT STEP) y' = ecx */
;   # add old values of A, B, C, D
    add eax,r8d
    add ebx,r9d
    add ecx,r14d
    add edx,r15d

;   # loop control
    add rsi,64      ;# ptr += 64
    cmp rsi,rdi     ;# cmp end with ptr
    jb  lab2                ;# jmp if ptr < end
;   # END of loop over 16-word blocks

lab1:   ;pop    rbp             ;# restore ctx
pop r12
    mov DWORD 0[r12],eax            ;# ctx->A = A
    mov DWORD 4[r12],ebx            ;# ctx->B = B
    mov DWORD 8[r12],ecx            ;# ctx->C = C
    mov DWORD 12[r12],edx           ;# ctx->D = D

    pop rdi
    pop rsi
    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx
    pop rbp
    ret


