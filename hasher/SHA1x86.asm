; #####################################################################################################################
;
; SHA_asm.asm
;
; Copyright (c) Shareaza Development Team, 2002-2007.
; This file is part of SHAREAZA (shareaza.sourceforge.net)
;
; Shareaza is free software; you can redistribute it
; and/or modify it under the terms of the GNU General Public License
; as published by the Free Software Foundation; either version 2 of
; the License, or (at your option) any later version.
;
; Shareaza is distributed in the hope that it will be useful,
; but WITHOUT ANY WARRANTY; without even the implied warranty of
; MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
; GNU General Public License for more details.
;
; You should have received a copy of the GNU General Public License
; along with Shareaza; if not, write to the Free Software
; Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
;
; #####################################################################################################################
;
; SHA_asm - Implementation of SHA-1 for x86 - use together with SHA.cpp and SHA.h
;
; #####################################################################################################################

                        .586p
                        .model      flat, stdcall
                        option      casemap:none                    ; case sensitive
                        option      prologue:none                   ; we generate our own entry/exit code
                        option      epilogue:none

; #####################################################################################################################

m_nCount0                equ         0                              ; offsets as found in SHA.h
m_nCount1                equ         4

m_nHash0                 equ         8
m_nHash1                 equ         12
m_nHash2                 equ         16
m_nHash3                 equ         20
m_nHash4                 equ         24

m_nBuffer                equ         28

RND_CH                  MACRO       const:REQ
; t=a; a=rotl32(a,5)+e+k+w[i]+((b&c)^(~b&d)); e=d; d=c; c=rotl32(b,30); b=t
;      a=rotl32(a,5)+e+k+w[i]+(d^(b&(c^d)));
                        mov         reg_temp1, reg_a                        ; t=a
                        mov         reg_temp2, reg_c
                        rol         reg_a, 5
                        xor         reg_temp2, reg_d
                        add         reg_a, reg_e
                        and         reg_temp2, reg_b
                        add         reg_a, const
                        xor         reg_temp2, reg_d
                        add         reg_a, [_w+count*4]
                        ror         reg_b, 2
                        add         reg_a, reg_temp2
reg_t                   textequ     reg_e
reg_e                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_temp1
reg_temp1               textequ     reg_t
count                   =           count + 1
                        ENDM                                                ; RND_CH

RND_PARITY              MACRO       const:REQ
; t=a; a=rotl32(a,5)+e+k+w[i]+(b^c^d); e=d; d=c; c=rotl32(b,30); b=t
                        mov         reg_temp1, reg_a                        ; t=a
                        rol         reg_a, 5
                        mov         reg_temp2, reg_d
                        add         reg_a, reg_e
                        xor         reg_temp2, reg_c
                        add         reg_a, const
                        xor         reg_temp2, reg_b
                        add         reg_a, [_w+count*4]
                        ror         reg_b, 2
                        add         reg_a, reg_temp2
reg_t                   textequ     reg_e
reg_e                   textequ     reg_d                                   ; e=d
reg_d                   textequ     reg_c                                   ; d=c
reg_c                   textequ     reg_b                                   ; c=rotl(b,30)
reg_b                   textequ     reg_temp1                               ; b=t
reg_temp1               textequ     reg_t
count                   =           count + 1
                        ENDM                                                ; RND_PARITY

RND_MAJ                 MACRO       const:REQ
; t=a; a=rotl32(a,5)+e+k+w[i]+((b&c)^(b&d)^(c&d)); e=d; d=c; c=rotl32(b,30); b=t
;      a=rotl32(a,5)+e+k+w[i]+((c&d)^(b&(c^d)))
                        mov         reg_temp2, reg_d
                        mov         reg_temp1, reg_a
                        rol         reg_a, 5
                        xor         reg_temp2, reg_c
                        add         reg_a, reg_e
                        and         reg_temp2, reg_b
                        add         reg_a, const
                        mov         reg_e, reg_c
                        add         reg_a, [_w+count*4]
                        and         reg_e, reg_d
                        xor         reg_temp2, reg_e
                        ror         reg_b, 2
                        add         reg_a, reg_temp2
reg_t                   textequ     reg_e
reg_e                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_temp1
reg_temp1               textequ     reg_t
count                   =           count + 1
                        ENDM                                                ; RND_MAJ

INIT_REG_ALIAS          MACRO
reg_accu                textequ     <eax>
reg_base                textequ     <ebp>
reg_i_1                 textequ     <ebx>
reg_i_2                 textequ     <ecx>
reg_i_3                 textequ     <edx>
reg_i_15                textequ     <esi>
reg_i_16                textequ     <edi>
                        ENDM

                        .code

                        ALIGN       16

SHA_Compile_p5          PROC

__this                  textequ     <[esp+40+320]>                       ; pusha + 2 * ret addr in between
_w                      textequ     <esp+4>

                        INIT_REG_ALIAS

count                   =           0
                        REPEAT      16
                        IF          count eq 0
                        mov         reg_i_16, [ebp+count*4]
                        bswap       reg_i_16
                        mov         [_w+count*4], reg_i_16
                        ELSEIF      count eq 1
                        mov         reg_i_15, [ebp+count*4]
                        bswap       reg_i_15
                        mov         [_w+count*4], reg_i_15
                        ELSEIF      count eq 13
                        mov         reg_i_3, [ebp+count*4]
                        bswap       reg_i_3
                        mov         [_w+count*4], reg_i_3
                        ELSEIF      count eq 14
                        mov         reg_i_2, [ebp+count*4]
                        bswap       reg_i_2
                        mov         [_w+count*4], reg_i_2
                        ELSE
                        mov         reg_i_1, [ebp+count*4]
                        bswap       reg_i_1
                        mov         [_w+count*4], reg_i_1
                        ENDIF
count                   =           count + 1
                        ENDM
count                   =           16
                        REPEAT      64
                        xor         reg_i_3, reg_i_16                       ; w[i-16]^w[i-3]
reg_i_14                textequ     reg_i_16                                ; we forget w[i-16]
                        IF          count le 77
                        mov         reg_i_14, [_w+(count-14)*4]
                        xor         reg_i_3, reg_i_14
                        ELSE
                        xor         reg_i_3, [_w+(count-14)*4]
                        ENDIF
                        xor         reg_i_3, [_w+(count-8)*4]
                        rol         reg_i_3, 1
                        mov         [_w+count*4], reg_i_3
;now we prepare for the next iteration                        
reg_i_0                 textequ     reg_i_3
reg_i_3                 textequ     reg_i_2
reg_i_2                 textequ     reg_i_1
reg_i_1                 textequ     reg_i_0
reg_i_16                textequ     reg_i_15
reg_i_15                textequ     reg_i_14
count                   =           count + 1
                        ENDM

reg_a                   textequ     <eax>
reg_b                   textequ     <ebx>
reg_c                   textequ     <ecx>
reg_d                   textequ     <edx>
reg_e                   textequ     <esi>
reg_temp1               textequ     <edi>
reg_temp2               textequ     <ebp>

                        mov         reg_temp2, __this
                        mov         reg_a, [reg_temp2+m_nHash0]
                        mov         reg_b, [reg_temp2+m_nHash1]
                        mov         reg_c, [reg_temp2+m_nHash2]
                        mov         reg_d, [reg_temp2+m_nHash3]
                        mov         reg_e, [reg_temp2+m_nHash4]

count                   =           0

                        REPEAT      20
                        RND_CH      05a827999H
                        ENDM
                        REPEAT      20
                        RND_PARITY  06ed9eba1H
                        ENDM
                        REPEAT      20
                        RND_MAJ     08f1bbcdcH
                        ENDM
                        REPEAT      20
                        RND_PARITY  0ca62c1d6H
                        ENDM

                        mov         reg_temp2, __this
                        add         [reg_temp2+m_nHash0], reg_a
                        add         [reg_temp2+m_nHash1], reg_b
                        add         [reg_temp2+m_nHash2], reg_c
                        add         [reg_temp2+m_nHash3], reg_d
                        add         [reg_temp2+m_nHash4], reg_e
 
                        ret

SHA_Compile_p5          ENDP

                        ALIGN       16

SHA1_Add_p5              PROC        PUBLIC, _this:DWORD, _Data:DWORD, _nLength:DWORD

                        pusha
__this                  textequ     <[esp+36+320]>                              ; different offset due to pusha
__Data                  textequ     <[esp+40+320]>
__nLength               textequ     <[esp+44+320]>

                        sub         esp, 320

                        mov         ecx, __nLength
                        and         ecx, ecx
                        jz          get_out
                        xor         edx, edx
                        mov         ebp, __Data
                        mov         edi, __this
                        mov         ebx, [edi+m_nCount0]
                        mov         eax, ebx
                        add         ebx, ecx
                        mov         [edi+m_nCount0], ebx
                        adc         [edi+m_nCount1], edx

                        and         eax, 63
                        jnz         partial_buffer
full_blocks:            mov         ecx, __nLength
                        and         ecx, ecx
                        jz          get_out
                        sub         ecx, 64
                        jb          end_of_stream
                        mov         __nLength, ecx
                        call        SHA_Compile_p5
                        mov         ebp, __Data
                        add         ebp, 64
                        mov         __Data, ebp
                        jmp         full_blocks

end_of_stream:          mov         edi, __this
                        mov         esi, ebp
                        lea         edi, [edi+m_nBuffer]
                        add         ecx, 64
                        rep movsb
                        jmp         get_out

partial_buffer:         add         ecx, eax                                ; eax = offset in buffer, ecx = _nLength
                        cmp         ecx, 64
                        jb          short_stream                            ; we can't fill the buffer
                        mov         ecx, -64
                        add         ecx, eax
                        add         __nLength, ecx                          ; _nlength += (offset-64)
@@:                     mov         bl, [ebp]
                        inc         ebp
                        mov         byte ptr [edi+m_nBuffer+64+ecx], bl
                        inc         ecx
                        jnz         @B                                      ; offset = 64
                        mov         __Data, ebp
                        lea         ebp, [edi+m_nBuffer]
                        call        SHA_Compile_p5
                        mov         ebp, __Data
                        jmp         full_blocks

short_stream:           sub         ecx, eax                                ;  --> ecx=_nLength
                        mov         esi, ebp
                        lea         edi, [edi+m_nBuffer+eax]
                        rep movsb

get_out:                add         esp, 320
                        popa
                        ret 12

SHA1_Add_p5              ENDP

        end
