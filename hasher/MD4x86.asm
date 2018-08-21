; #####################################################################################################################
;
; MD4_asm.asm
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
; MD4_asm - Implementation of MD4 for x86 - use together with MD4.cpp and MD4.h
;
; #####################################################################################################################

                        .586p
                        .model      flat, stdcall
                        option      casemap:none                    ; case sensitive
                        option      prologue:none                   ; we generate our own entry/exit code
                        option      epilogue:none

; #####################################################################################################################

m_nCount0               equ         0
m_nCount1               equ         4

m_nState0               equ         8                              ; offsets as found in MD4.h
m_nState1               equ         12
m_nState2               equ         16
m_nState3               equ         20

m_pBuffer               equ         24

; Some magic numbers for Transform...
MD4_S11                 equ         3
MD4_S12                 equ         7
MD4_S13                 equ         11
MD4_S14                 equ         19

MD4_S21                 equ         3
MD4_S22                 equ         5
MD4_S23                 equ         9
MD4_S24                 equ         13

MD4_S31                 equ         3
MD4_S32                 equ         9
MD4_S33                 equ         11
MD4_S34                 equ         15

                        .data

MD4FF                   MACRO       count:REQ,s:REQ
; a = (a+x[count]+((b&c)|(~b&d)))rol s
; a = (a+x[count]+(d^(b&(c^d))))rol s
                        mov         reg_temp1, reg_c
                        xor         reg_c, reg_d
                        add         reg_a, [ebp+count*4]
                        and         reg_c, reg_b
                        xor         reg_c, reg_d
                        add         reg_a, reg_c
                        rol         reg_a, s
reg_t                   textequ     reg_d
reg_d                   textequ     reg_temp1
reg_temp1               textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM

MD4GG                   MACRO       count:REQ,s:REQ
; a = (a+x[count]+((b&c)|(b&d)|(c&d))+5A827999H) rol s
; a = (a+x[count]+((b&c)|(d&(b|c)))+5A827999H)rol s
                        mov         reg_temp2, reg_b
                        mov         reg_temp1, reg_b
                        add         reg_a, [ebp+count*4]
                        or          reg_b, reg_c
                        and         reg_temp2, reg_c
                        and         reg_b, reg_d
                        add         reg_a, 5A827999H
                        or          reg_b, reg_temp2
                        add         reg_a, reg_b
                        rol         reg_a, s
reg_t                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_temp1
reg_temp1               textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM

MD4HH                   MACRO       count:REQ,s:REQ
; a = (a+x[count]+(b^c^d)+6ED9EBA1H)rol s
                        add         reg_a, [ebp+count*4]
                        mov         reg_temp1, reg_b
                        xor         reg_b, reg_c
                        add         reg_a, 6ED9EBA1H
                        xor         reg_b, reg_d
                        add         reg_a, reg_b
                        rol         reg_a, s
reg_t                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_temp1
reg_temp1               textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM

                        .code

MD4_Transform_p5        PROC                                            ; we expect ebp to point to the Data stream
                                                                        ; all other registers (eax,ebx,ecx,edx,esi,edi) will be destroyed
__this                  textequ     <[esp+32+2*4]>                      ; 1*pusha+2*call
; set alias for registers
reg_a                   textequ     <eax>
reg_b                   textequ     <ebx>
reg_c                   textequ     <ecx>
reg_d                   textequ     <edx>
reg_temp1               textequ     <esi>
reg_temp2               textequ     <edi>
                        mov         reg_temp1, __this
                        mov         reg_a, [reg_temp1+m_nState0]
                        mov         reg_b, [reg_temp1+m_nState1]
                        mov         reg_c, [reg_temp1+m_nState2]
                        mov         reg_d, [reg_temp1+m_nState3]
; round 1
                        MD4FF        0, MD4_S11
                        MD4FF        1, MD4_S12
                        MD4FF        2, MD4_S13
                        MD4FF        3, MD4_S14
                        MD4FF        4, MD4_S11
                        MD4FF        5, MD4_S12
                        MD4FF        6, MD4_S13
                        MD4FF        7, MD4_S14
                        MD4FF        8, MD4_S11
                        MD4FF        9, MD4_S12
                        MD4FF       10, MD4_S13
                        MD4FF       11, MD4_S14
                        MD4FF       12, MD4_S11
                        MD4FF       13, MD4_S12
                        MD4FF       14, MD4_S13
                        MD4FF       15, MD4_S14
; round 2
                        MD4GG        0, MD4_S21
                        MD4GG        4, MD4_S22
                        MD4GG        8, MD4_S23
                        MD4GG       12, MD4_S24
                        MD4GG        1, MD4_S21
                        MD4GG        5, MD4_S22
                        MD4GG        9, MD4_S23
                        MD4GG       13, MD4_S24
                        MD4GG        2, MD4_S21
                        MD4GG        6, MD4_S22
                        MD4GG       10, MD4_S23
                        MD4GG       14, MD4_S24
                        MD4GG        3, MD4_S21
                        MD4GG        7, MD4_S22
                        MD4GG       11, MD4_S23
                        MD4GG       15, MD4_S24
; round 3
                        MD4HH        0, MD4_S31
                        MD4HH        8, MD4_S32
                        MD4HH        4, MD4_S33
                        MD4HH       12, MD4_S34
                        MD4HH        2, MD4_S31
                        MD4HH       10, MD4_S32
                        MD4HH        6, MD4_S33
                        MD4HH       14, MD4_S34
                        MD4HH        1, MD4_S31
                        MD4HH        9, MD4_S32
                        MD4HH        5, MD4_S33
                        MD4HH       13, MD4_S34
                        MD4HH        3, MD4_S31
                        MD4HH       11, MD4_S32
                        MD4HH        7, MD4_S33
                        MD4HH       15, MD4_S34
                        mov         reg_temp1, __this
                        add         [reg_temp1+m_nState0], reg_a
                        add         [reg_temp1+m_nState1], reg_b
                        add         [reg_temp1+m_nState2], reg_c
                        add         [reg_temp1+m_nState3], reg_d
                        ret
MD4_Transform_p5        ENDP

MD4_Add_p5              PROC        PUBLIC, _this:DWORD, _Data:DWORD, _nLength:DWORD

                        pusha
__this                  textequ     <[esp+36]>                              ; different offset due to pusha
__Data                  textequ     <[esp+40]>
__nLength               textequ     <[esp+44]>

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
                        call        MD4_Transform_p5
                        add         ebp, 64
                        jmp         full_blocks

end_of_stream:          mov         edi, __this
                        mov         esi, ebp
                        lea         edi, [edi+m_pBuffer]
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
                        mov         byte ptr [edi+m_pBuffer+64+ecx], bl
                        inc         ecx
                        jnz         @B                                      ; offset = 64
                        mov         __Data, ebp
                        lea         ebp, [edi+m_pBuffer]
                        call        MD4_Transform_p5
                        mov         ebp, __Data
                        jmp         full_blocks

short_stream:           sub         ecx, eax                                ;  --> ecx=_nLength
                        mov         esi, ebp
                        lea         edi, [edi+m_pBuffer+eax]
                        rep movsb

get_out:                popa
                        ret 12

MD4_Add_p5              ENDP

                end