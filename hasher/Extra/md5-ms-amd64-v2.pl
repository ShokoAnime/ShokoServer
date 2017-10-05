#!/usr/bin/perl -w
#
# MD5 optimized for AMD64.
#
# Author: Marc Bevand <bevand_m (at) epita.fr>
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
    my ($pos, $dst, $x, $y, $z, $k_next, $T_i, $s) = @_;
    $code .= "  mov r10 , QWORD PTR (0*4)[rsi]      ;/* (NEXT STEP) X[0] */\n" if ($pos == -1);
    $code .= "  mov r11d , edx          ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);
    $code .= <<EOF;
    xor r11d,$y     ;/* y ^ ... */
    lea $dst,DWORD PTR $T_i [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,$x             ;/* x & ... */
    xor r11d,$z             ;/* z ^ ... */
    mov r10,QWORD PTR ($k_next*4)[rsi]      ;/* (NEXT STEP) X[$k_next] */
    add $dst,r11d           ;/* dst += ... */
    rol $dst, $s            ;/* dst <<< s */
    mov r11d , $y           ;/* (NEXT STEP) z' = $y */
    add $dst , $x           ;/* dst += x */
EOF
}

sub round1_odd_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $T_i, $s) = @_;
    $code .= "  mov r10 , QWORD PTR (0*4)[rsi]      ;/* (NEXT STEP) X[0] */\n" if ($pos == -1);
    $code .= "  mov r11d , edx          ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);
    $code .= <<EOF;
    xor r11d,$y     ;/* y ^ ... */
    lea $dst,DWORD PTR $T_i [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    and r11d,$x             ;/* x & ... */
    xor r11d,$z             ;/* z ^ ... */
    ;mov    r10d,DWORD PTR ($k_next*4)[rsi]     ;/* (NEXT STEP) X[$k_next] */
    shr r10,32
    add $dst,r11d           ;/* dst += ... */
    rol $dst, $s            ;/* dst <<< s */
    mov r11d , $y           ;/* (NEXT STEP) z' = $y */
    add $dst , $x           ;/* dst += x */
EOF
}

# round2_step() does:
#   dst = x + ((dst + G(x,y,z) + X[k] + T_i) <<< s)
#   %r10d = X[k_next]
#   %r11d = y' (copy of y for the next step)
# Each round2_step() takes about 6.22 clocks (9 instructions, 1.45 IPC)
sub round2_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $T_i, $s) = @_;
    $code .= " mov  r10d , 4 [rsi]      ;/* (NEXT STEP) X[1] */\n" if ($pos == -1);
    $code .= " mov  r11d, edx       ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);
    $code .= " mov  r12d, edx       ;/* (NEXT STEP) z' = %edx */\n" if ($pos == -1);

    $code .= <<EOF;
    not r11d
    lea $dst,DWORD PTR $T_i [ $dst * 1 +r10d ]      ;/* Const + dst + ... */

    and r12d,$x     ;/* x & z */
    and r11d,$y     ;/* y & (not z) */

    mov r10d , ($k_next*4) [rsi]        ;/* (NEXT STEP) X[$k_next] */



    or  r12d,r11d   ;/* (y & (not z)) | (x & z) */
    mov r11d,$y     ;/* (NEXT STEP) z' = $y */
    add $dst, r12d  ;   /* dst += ... */
    mov r12d,$y     ;/* (NEXT STEP) z' = $y */


    rol $dst , $s           ;/* dst <<< s */
    add $dst , $x           ;/* dst += x */
EOF
}

# round3_step() does:
#   dst = x + ((dst + H(x,y,z) + X[k] + T_i) <<< s)
#   %r10d = X[k_next]
#   %r11d = y' (copy of y for the next step)
# Each round3_step() takes about 4.26 clocks (8 instructions, 1.88 IPC)
sub round3_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $T_i, $s) = @_;
    $code .= " mov  r10d , (5*4)[rsi]       ;/* (NEXT STEP) X[5] */\n" if ($pos == -1);
    $code .= " mov  r11d , ecx      ;/* (NEXT STEP) y' = %ecx */\n" if ($pos == -1);
    $code .= <<EOF;
    lea $dst,DWORD PTR $T_i [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    mov r10d,DWORD PTR ($k_next*4)[rsi]     ;/* (NEXT STEP) X[$k_next] */
    xor r11d,$z         ;/* z ^ ... */
    xor r11d,$x             ;/* x ^ ... */
    add $dst , r11d         ;/* dst += ... */
    rol $dst , $s           ;/* dst <<< s */
    mov r11d , $x           ;/* (NEXT STEP) y' = $x */
    add $dst , $x           ;/* dst += x */
EOF
}

# round4_step() does:
#   dst = x + ((dst + I(x,y,z) + X[k] + T_i) <<< s)
#   %r10d = X[k_next]
#   %r11d = not z' (copy of not z for the next step)
# Each round4_step() takes about 5.27 clocks (9 instructions, 1.71 IPC)
sub round4_step
{
    my ($pos, $dst, $x, $y, $z, $k_next, $T_i, $s) = @_;
    $code .= " mov  r10d , (0*4)[rsi]       ;/* (NEXT STEP) X[0] */\n" if ($pos == -1);
    $code .= " mov  r11d , r13d ;0ffffffffh ;%r11d\n" if ($pos == -1);
    $code .= " xor  r11d , edx      ;/* (NEXT STEP) not z' = not %edx*/\n"
    if ($pos == -1);
    $code .= <<EOF;
    lea $dst,DWORD PTR $T_i [ $dst * 1 +r10d ]      ;/* Const + dst + ... */
    or  r11d , $x           ;/* x | ... */
    xor r11d , $y           ;/* y ^ ... */
    add $dst , r11d         ;/* dst += ... */
    mov r10d , DWORD PTR ($k_next*4)[rsi]       ;/* (NEXT STEP) X[$k_next] */
    mov r11d , r13d ; 0ffffffffh
    rol $dst , $s           ;/* dst <<< s */
    xor r11d , $y           ;/* (NEXT STEP) not z' = not $y */
    add $dst , $x           ;/* dst += x */
EOF
}




$code .= <<EOF;

; MD5 optimized for AMD64.
;
; Author: Marc Bevand <bevand_m (at) epita.fr>
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
md5_block_asm_host_order PROC
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
round1_odd_step(-1,'eax','ebx','ecx','edx', '1','0d76aa478h', '7');
round1_even_step( 0,'edx','eax','ebx','ecx', '2','0e8c7b756h','12');
round1_odd_step( 0,'ecx','edx','eax','ebx', '3','0242070dbh','17');
round1_even_step( 0,'ebx','ecx','edx','eax', '4','0c1bdceeeh','22');
round1_odd_step( 0,'eax','ebx','ecx','edx', '5','0f57c0fafh', '7');
round1_even_step( 0,'edx','eax','ebx','ecx', '6','04787c62ah','12');
round1_odd_step( 0,'ecx','edx','eax','ebx', '7','0a8304613h','17');
round1_even_step( 0,'ebx','ecx','edx','eax', '8','0fd469501h','22');
round1_odd_step( 0,'eax','ebx','ecx','edx', '9','0698098d8h', '7');
round1_even_step( 0,'edx','eax','ebx','ecx','10','08b44f7afh','12');
round1_odd_step( 0,'ecx','edx','eax','ebx','11','0ffff5bb1h','17');
round1_even_step( 0,'ebx','ecx','edx','eax','12','0895cd7beh','22');
round1_odd_step( 0,'eax','ebx','ecx','edx','13','06b901122h', '7');
round1_even_step( 0,'edx','eax','ebx','ecx','14','0fd987193h','12');
round1_odd_step( 0,'ecx','edx','eax','ebx','15','0a679438eh','17');
round1_even_step( 1,'ebx','ecx','edx','eax', '0','049b40821h','22');

round2_step(-1,'eax','ebx','ecx','edx', '6','0f61e2562h', '5');
round2_step( 0,'edx','eax','ebx','ecx','11','0c040b340h', '9');
round2_step( 0,'ecx','edx','eax','ebx', '0','0265e5a51h','14');
round2_step( 0,'ebx','ecx','edx','eax', '5','0e9b6c7aah','20');
round2_step( 0,'eax','ebx','ecx','edx','10','0d62f105dh', '5');
round2_step( 0,'edx','eax','ebx','ecx','15', '02441453h', '9');
round2_step( 0,'ecx','edx','eax','ebx', '4','0d8a1e681h','14');
round2_step( 0,'ebx','ecx','edx','eax', '9','0e7d3fbc8h','20');
round2_step( 0,'eax','ebx','ecx','edx','14','021e1cde6h', '5');
round2_step( 0,'edx','eax','ebx','ecx', '3','0c33707d6h', '9');
round2_step( 0,'ecx','edx','eax','ebx', '8','0f4d50d87h','14');
round2_step( 0,'ebx','ecx','edx','eax','13','0455a14edh','20');
round2_step( 0,'eax','ebx','ecx','edx', '2','0a9e3e905h', '5');
round2_step( 0,'edx','eax','ebx','ecx', '7','0fcefa3f8h', '9');
round2_step( 0,'ecx','edx','eax','ebx','12','0676f02d9h','14');
round2_step( 1,'ebx','ecx','edx','eax', '0','08d2a4c8ah','20');

round3_step(-1,'eax','ebx','ecx','edx', '8','0fffa3942h', '4');
round3_step( 0,'edx','eax','ebx','ecx','11','08771f681h','11');
round3_step( 0,'ecx','edx','eax','ebx','14','06d9d6122h','16');
round3_step( 0,'ebx','ecx','edx','eax', '1','0fde5380ch','23');
round3_step( 0,'eax','ebx','ecx','edx', '4','0a4beea44h', '4');
round3_step( 0,'edx','eax','ebx','ecx', '7','04bdecfa9h','11');
round3_step( 0,'ecx','edx','eax','ebx','10','0f6bb4b60h','16');
round3_step( 0,'ebx','ecx','edx','eax','13','0bebfbc70h','23');
round3_step( 0,'eax','ebx','ecx','edx', '0','0289b7ec6h', '4');
round3_step( 0,'edx','eax','ebx','ecx', '3','0eaa127fah','11');
round3_step( 0,'ecx','edx','eax','ebx', '6','0d4ef3085h','16');
round3_step( 0,'ebx','ecx','edx','eax', '9', '04881d05h','23');
round3_step( 0,'eax','ebx','ecx','edx','12','0d9d4d039h', '4');
round3_step( 0,'edx','eax','ebx','ecx','15','0e6db99e5h','11');
round3_step( 0,'ecx','edx','eax','ebx', '2','01fa27cf8h','16');
round3_step( 1,'ebx','ecx','edx','eax', '0','0c4ac5665h','23');

round4_step(-1,'eax','ebx','ecx','edx', '7','0f4292244h', '6');
round4_step( 0,'edx','eax','ebx','ecx','14','0432aff97h','10');
round4_step( 0,'ecx','edx','eax','ebx', '5','0ab9423a7h','15');
round4_step( 0,'ebx','ecx','edx','eax','12','0fc93a039h','21');
round4_step( 0,'eax','ebx','ecx','edx', '3','0655b59c3h', '6');
round4_step( 0,'edx','eax','ebx','ecx','10','08f0ccc92h','10');
round4_step( 0,'ecx','edx','eax','ebx', '1','0ffeff47dh','15');
round4_step( 0,'ebx','ecx','edx','eax', '8','085845dd1h','21');
round4_step( 0,'eax','ebx','ecx','edx','15','06fa87e4fh', '6');
round4_step( 0,'edx','eax','ebx','ecx', '6','0fe2ce6e0h','10');
round4_step( 0,'ecx','edx','eax','ebx','13','0a3014314h','15');
round4_step( 0,'ebx','ecx','edx','eax', '4','04e0811a1h','21');
round4_step( 0,'eax','ebx','ecx','edx','11','0f7537e82h', '6');
round4_step( 0,'edx','eax','ebx','ecx', '2','0bd3af235h','10');
round4_step( 0,'ecx','edx','eax','ebx', '9','02ad7d2bbh','15');
round4_step( 1,'ebx','ecx','edx','eax', '0','0eb86d391h','21');
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
md5_block_asm_host_order ENDP
END
EOF

;print $code;
