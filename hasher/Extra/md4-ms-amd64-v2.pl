#!/usr/bin/perl -w
#
# MD4 optimized for AMD64.
#
# Author: Maximo Piva based on Marc Bevand <bevand_m (at) epita.fr>
# MD5 version
# Licence: I hereby disclaim the copyright on this code and place it
# in the public domain.
#
# Gilles Vollant <info (at) winimage.com > made the port to Intel/Amd
# mnemonic for Microsoft ML64 and Microsoft C++ for Windows x64
#
#  http://etud.epita.fr/~bevand_m/papers/md5-amd64.html
#  http://www.winimage.com/md5-amd64-ms.htm
#
#  Charles Liu made optimisation on
#  http://article.gmane.org/gmane.comp.encryption.openssl.devel/9835
#

use strict;

my $code;

# round1_odd_step() does:
#   dst = x + ((dst + F(x,y,z) + X[k] + T_i) <<< s)
#   %r10d = X[k_next]
#   %r11d = z' (copy of z for the next step)
# Each round1_odd_step() takes about 5.71 clocks (9 instructions, 1.58 IPC)
sub round1_even_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $s) = @_;
    $code .= "  mov r10 , QWORD PTR (0*4)[rsi]      ;/* (NEXT STEP) X[0] */\n" if ($pos == -1);
    $code .= "  mov r11d , edx          ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);
    $code .= <<EOF;
    xor r11d,$y     ;/* y ^ ... */
    lea $dst, [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,$x             ;/* x & ... */
    xor r11d,$z             ;/* z ^ ... */
    mov r10,QWORD PTR ($k_next*4)[rsi]      ;/* (NEXT STEP) X[$k_next] */
    add $dst,r11d           ;/* dst += ... */
    rol $dst, $s            ;/* dst <<< s */
    mov r11d , $y           ;/* (NEXT STEP) z' = $y */
EOF
}

sub round1_odd_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $s) = @_;
    $code .= "  mov r10 , QWORD PTR (0*4)[rsi]      ;/* (NEXT STEP) X[0] */\n" if ($pos == -1);
    $code .= "  mov r11d , edx          ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);
    $code .= <<EOF;
    xor r11d,$y     ;/* y ^ ... */
    lea $dst, [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,$x             ;/* x & ... */
    xor r11d,$z             ;/* z ^ ... */
    ;mov    r10d,DWORD PTR ($k_next*4)[rsi]     ;/* (NEXT STEP) X[$k_next] */
    shr r10,32
    add $dst,r11d           ;/* dst += ... */
    rol $dst, $s            ;/* dst <<< s */
    mov r11d , $y           ;/* (NEXT STEP) z' = $y */
EOF
}

# round2_step() does:
#   dst = x + ((dst + G(x,y,z) + X[k] + T_i) <<< s)
#   %r10d = X[k_next]
#   %r11d = y' (copy of y for the next step)
# Each round2_step() takes about 6.22 clocks (9 instructions, 1.45 IPC)
sub round2_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $s) = @_;
    $code .= " mov  r10d , [rsi]      ;/* (NEXT STEP) X[1] */\n" if ($pos == -1);
    $code .= " mov  r11d, ecx       ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);
    $code .= " mov  r12d, ecx       ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);

    $code .= <<EOF;

    lea $dst,DWORD PTR 5A827999h [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d, $x
    or r12d, $x
    mov r10d , ($k_next*4) [rsi]        ;/* (NEXT STEP) X[$k_next] */    
    and r12d, $z
    or r11d,r12d
    mov  r12d, $x       ;/* (NEXT STEP) z' = $x */
    add $dst,r11d
    mov  r11d, $x       ;/* (NEXT STEP) z' = $x */
    rol $dst , $s           ;/* dst <<< s */

EOF
}

# round3_step() does:
#   dst = x + ((dst + H(x,y,z) + X[k] + T_i) <<< s)
#   %r10d = X[k_next]
#   %r11d = y' (copy of y for the next step)
# Each round3_step() takes about 4.26 clocks (8 instructions, 1.88 IPC)
sub round3_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $s) = @_;
    $code .= " mov  r10d , [rsi]       ;/* (NEXT STEP) X[5] */\n" if ($pos == -1);
    $code .= " mov  r11d , ecx      ;/* (NEXT STEP) y' = %ecx */\n" if ($pos == -1);
    $code .= <<EOF;
    lea $dst,DWORD PTR 6ED9EBA1H [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD PTR ($k_next*4)[rsi]     ;/* (NEXT STEP) X[$k_next] */
    xor r11d,$z         ;/* z ^ ... */
    xor r11d,$x             ;/* x ^ ... */
    add $dst , r11d         ;/* dst += ... */
    rol $dst , $s           ;/* dst <<< s */
    mov r11d , $x           ;/* (NEXT STEP) y' = $x */
EOF
}



$code .= <<EOF;

; MD4 optimized for AMD64.
;
; Author: Maximo Piva based on Marc Bevand <bevand_m (at) epita.fr> md5 version
; Licence: I hereby disclaim the copyright on this code and place it
; in the public domain.
;
; Gilles Vollant <info (at) winimage.com > made the port to Intel/Amd
; mnemonic for Microsoft ML64 and Microsoft C++ for Windows x64
;
; to compile this file, I use option
;   ml64.exe /Flm5n64 /c /Zi m5n64.asm
;   with Microsoft Macro Assembler (x64) for AMD64
;
;   ml64.exe is given with Visual Studio 2005, Windows 2003 server DDK
;
;   (you can get Windows 2003 server DDK with ml64 and cl for AMD64 from
;      http://www.microsoft.com/whdc/devtools/ddk/default.mspx for low price)
;
;  http://etud.epita.fr/~bevand_m/papers/md5-amd64.html
;  http://www.winimage.com/md5-amd64-ms.htm
;
;  Charles Liu made optimisation on
;  http://article.gmane.org/gmane.comp.encryption.openssl.devel/9835
;
.code
md4_block_asm_host_order PROC
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

    mov eax,DWORD PTR 0[r12]    ;# eax = ctx->A
    mov ebx,DWORD PTR 4[r12]    ;# ebx = ctx->B
    mov ecx,DWORD PTR 8[r12]    ;# ecx = ctx->C
    mov edx,DWORD PTR 12[r12]   ;# edx = ctx->D
    ;push   rbp     ;# save ctx
    ;# end is 'rdi'
    ;# ptr is 'rsi'
    ;# A is 'eax'
    ;# B is 'ebx'
    ;# C is 'ecx'
    ;# D is 'edx'

; it is better with align 16 here, I don't known why
align 16
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
EOF
round1_odd_step(-1,'eax','ebx','ecx','edx', '1','3');
round1_even_step( 0,'edx','eax','ebx','ecx', '2','7');
round1_odd_step( 0,'ecx','edx','eax','ebx', '3','11');
round1_even_step( 0,'ebx','ecx','edx','eax', '4','19');
round1_odd_step( 0,'eax','ebx','ecx','edx', '5','3');
round1_even_step( 0,'edx','eax','ebx','ecx', '6','7');
round1_odd_step( 0,'ecx','edx','eax','ebx', '7','11');
round1_even_step( 0,'ebx','ecx','edx','eax', '8','19');
round1_odd_step( 0,'eax','ebx','ecx','edx', '9', '3');
round1_even_step( 0,'edx','eax','ebx','ecx','10','7');
round1_odd_step( 0,'ecx','edx','eax','ebx','11','11');
round1_even_step( 0,'ebx','ecx','edx','eax','12','19');
round1_odd_step( 0,'eax','ebx','ecx','edx','13', '3');
round1_even_step( 0,'edx','eax','ebx','ecx','14','7');
round1_odd_step( 0,'ecx','edx','eax','ebx','15','11');
round1_even_step( 1,'ebx','ecx','edx','eax', '0','19');

round2_step(-1,'eax','ebx','ecx','edx', '4','3');
round2_step( 0,'edx','eax','ebx','ecx','8','5');
round2_step( 0,'ecx','edx','eax','ebx', '12','9');
round2_step( 0,'ebx','ecx','edx','eax', '1','13');
round2_step( 0,'eax','ebx','ecx','edx','5','3');
round2_step( 0,'edx','eax','ebx','ecx','9', '5');
round2_step( 0,'ecx','edx','eax','ebx', '13','9');
round2_step( 0,'ebx','ecx','edx','eax', '2','13');
round2_step( 0,'eax','ebx','ecx','edx','6', '3');
round2_step( 0,'edx','eax','ebx','ecx', '10', '5');
round2_step( 0,'ecx','edx','eax','ebx', '14','9');
round2_step( 0,'ebx','ecx','edx','eax','3','13');
round2_step( 0,'eax','ebx','ecx','edx', '7', '3');
round2_step( 0,'edx','eax','ebx','ecx', '11', '5');
round2_step( 0,'ecx','edx','eax','ebx','15','9');
round2_step( 1,'ebx','ecx','edx','eax', '0','13');

round3_step(-1,'eax','ebx','ecx','edx', '8','3');
round3_step( 0,'edx','eax','ebx','ecx','4','9');
round3_step( 0,'ecx','edx','eax','ebx','12','11');
round3_step( 0,'ebx','ecx','edx','eax', '2','15');
round3_step( 0,'eax','ebx','ecx','edx', '10','3');
round3_step( 0,'edx','eax','ebx','ecx', '6','9');
round3_step( 0,'ecx','edx','eax','ebx','14','11');
round3_step( 0,'ebx','ecx','edx','eax','1','15');
round3_step( 0,'eax','ebx','ecx','edx', '9', '3');
round3_step( 0,'edx','eax','ebx','ecx', '5','9');
round3_step( 0,'ecx','edx','eax','ebx', '13','11');
round3_step( 0,'ebx','ecx','edx','eax', '3', '15');
round3_step( 0,'eax','ebx','ecx','edx','11','3');
round3_step( 0,'edx','eax','ebx','ecx','7','9');
round3_step( 0,'ecx','edx','eax','ebx', '15','11');
round3_step( 1,'ebx','ecx','edx','eax', '0','15');


$code .= <<EOF;
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
    mov DWORD PTR 0[r12],eax            ;# ctx->A = A
    mov DWORD PTR 4[r12],ebx            ;# ctx->B = B
    mov DWORD PTR 8[r12],ecx            ;# ctx->C = C
    mov DWORD PTR 12[r12],edx           ;# ctx->D = D

    pop rdi
    pop rsi
    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx
    pop rbp
    ret
md4_block_asm_host_order ENDP
END
EOF

;print $code;
