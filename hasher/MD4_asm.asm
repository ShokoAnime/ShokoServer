; #####################################################################################################################
;
; MD4_asm.asm
;
; Copyright (c) Shareaza Development Team, 2002-2004.
; This file is part of SHAREAZA (www.shareaza.com)
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
; created              7.7.2004         by Camper
;
; last modified        20.7.2004        by Camper
;
; The integration into other projects than Shareaza is expressivly encouraged. Feel free to contact me about it.
;
; #####################################################################################################################

                        .586p
                        .model      flat, C 
                        option      casemap:none                    ; case sensitive
                        option      prologue:none                   ; we generate our own entry/exit code
                        option      epilogue:none

; #####################################################################################################################

CMD4_MemberStart		EQU			4								; skip vtbl

m_nState0               EQU         CMD4_MemberStart+0              ; offsets as found in MD4.h
m_nState1               EQU         CMD4_MemberStart+4
m_nState2               EQU         CMD4_MemberStart+8
m_nState3               EQU         CMD4_MemberStart+12

m_nCount0               EQU         CMD4_MemberStart+16
m_nCount1               EQU         CMD4_MemberStart+20

m_nBuffer               EQU         CMD4_MemberStart+24


						.data
MD4_asm_m_nState0       DD		m_nState0
MD4_asm_m_nState1       DD		m_nState1
MD4_asm_m_nState2       DD		m_nState2
MD4_asm_m_nState3       DD		m_nState3
MD4_asm_m_nCount0		DD		m_nCount0
MD4_asm_m_nCount1       DD		m_nCount1
MD4_asm_m_nBuffer       DD		m_nBuffer

						PUBLIC MD4_asm_m_nState0
						PUBLIC MD4_asm_m_nState1
						PUBLIC MD4_asm_m_nState2
						PUBLIC MD4_asm_m_nState3
						PUBLIC MD4_asm_m_nCount0
						PUBLIC MD4_asm_m_nCount1
						PUBLIC MD4_asm_m_nBuffer


; Some magic numbers for Transform...
MD4_S11                 EQU         3
MD4_S12                 EQU         7
MD4_S13                 EQU         11
MD4_S14                 EQU         19
MD4_S21                 EQU         3
MD4_S22                 EQU         5
MD4_S23                 EQU         9
MD4_S24                 EQU         13
MD4_S31                 EQU         3
MD4_S32                 EQU         9
MD4_S33                 EQU         11
MD4_S34                 EQU         15

MD4FF                   MACRO       a:REQ,b:REQ,c:REQ,d:REQ,count:REQ,s:REQ
; a = (a+x[count]+((b&c)|(~b&d))) rol s
                        mov         reg_temp1, b
                        mov         reg_temp2, b
                        add         a, [reg_base+count*4]
reg_t                   textequ     reg_temp1
reg_temp1               textequ     b                                   ; an attempt to improve instruction pairing
b                       textequ     reg_t
                        not         reg_temp1
                        and         reg_temp2, c
                        and         reg_temp1, d
                        or          reg_temp1, reg_temp2
                        add         a, reg_temp1
                        rol         a, s
                        ENDM

MD4GG                   MACRO       a:REQ,b:REQ,c:REQ,d:REQ,count:REQ,s:REQ
; a = (a+x[count]+((b&c)|(b&d)|(c&d))+5A827999H) rol s
                        mov         reg_temp1, b
                        mov         reg_temp2, b
                        add         a, [reg_base+count*4]
reg_t                   textequ     reg_temp1
reg_temp1               textequ     b                                   ; an attempt to improve instruction pairing
b                       textequ     reg_t
                        and         reg_temp1, c
                        and         reg_temp2, d
                        add         a, 5A827999H
                        or          reg_temp1, reg_temp2
                        mov         reg_temp2, c
                        and         reg_temp2, d
                        or          reg_temp1, reg_temp2
                        add         a, reg_temp1
                        rol         a, s
                        ENDM

MD4HH                   MACRO       a:REQ,b:REQ,c:REQ,d:REQ,count:REQ,s:REQ
; a = (a+x[count]+(b^c^d)+6ED9EBA1H) rol s
                        mov         reg_temp1, b
                        add         a, [reg_base+count*4]
reg_t                   textequ     reg_temp1
reg_temp1               textequ     b                                   ; an attempt to improve instruction pairing
b                       textequ     reg_t
                        xor         reg_temp1, c
                        add         a, 6ED9EBA1H
                        xor         reg_temp1, d
                        add         a, reg_temp1
                        rol         a, s
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
reg_base                textequ     <ebp>

                        mov         reg_temp1, __this
                        mov         reg_a, [reg_temp1+m_nState0]
                        mov         reg_b, [reg_temp1+m_nState1]
                        mov         reg_c, [reg_temp1+m_nState2]
                        mov         reg_d, [reg_temp1+m_nState3]

; round 1
                        MD4FF       reg_a, reg_b, reg_c, reg_d,  0, MD4_S11
                        MD4FF       reg_d, reg_a, reg_b, reg_c,  1, MD4_S12
                        MD4FF       reg_c, reg_d, reg_a, reg_b,  2, MD4_S13
                        MD4FF       reg_b, reg_c, reg_d, reg_a,  3, MD4_S14

                        MD4FF       reg_a, reg_b, reg_c, reg_d,  4, MD4_S11
                        MD4FF       reg_d, reg_a, reg_b, reg_c,  5, MD4_S12
                        MD4FF       reg_c, reg_d, reg_a, reg_b,  6, MD4_S13
                        MD4FF       reg_b, reg_c, reg_d, reg_a,  7, MD4_S14

                        MD4FF       reg_a, reg_b, reg_c, reg_d,  8, MD4_S11
                        MD4FF       reg_d, reg_a, reg_b, reg_c,  9, MD4_S12
                        MD4FF       reg_c, reg_d, reg_a, reg_b, 10, MD4_S13
                        MD4FF       reg_b, reg_c, reg_d, reg_a, 11, MD4_S14

                        MD4FF       reg_a, reg_b, reg_c, reg_d, 12, MD4_S11
                        MD4FF       reg_d, reg_a, reg_b, reg_c, 13, MD4_S12
                        MD4FF       reg_c, reg_d, reg_a, reg_b, 14, MD4_S13
                        MD4FF       reg_b, reg_c, reg_d, reg_a, 15, MD4_S14

; round 2

                        MD4GG       reg_a, reg_b, reg_c, reg_d,  0, MD4_S21
                        MD4GG       reg_d, reg_a, reg_b, reg_c,  4, MD4_S22
                        MD4GG       reg_c, reg_d, reg_a, reg_b,  8, MD4_S23
                        MD4GG       reg_b, reg_c, reg_d, reg_a, 12, MD4_S24

                        MD4GG       reg_a, reg_b, reg_c, reg_d,  1, MD4_S21
                        MD4GG       reg_d, reg_a, reg_b, reg_c,  5, MD4_S22
                        MD4GG       reg_c, reg_d, reg_a, reg_b,  9, MD4_S23
                        MD4GG       reg_b, reg_c, reg_d, reg_a, 13, MD4_S24

                        MD4GG       reg_a, reg_b, reg_c, reg_d,  2, MD4_S21
                        MD4GG       reg_d, reg_a, reg_b, reg_c,  6, MD4_S22
                        MD4GG       reg_c, reg_d, reg_a, reg_b, 10, MD4_S23
                        MD4GG       reg_b, reg_c, reg_d, reg_a, 14, MD4_S24

                        MD4GG       reg_a, reg_b, reg_c, reg_d,  3, MD4_S21
                        MD4GG       reg_d, reg_a, reg_b, reg_c,  7, MD4_S22
                        MD4GG       reg_c, reg_d, reg_a, reg_b, 11, MD4_S23
                        MD4GG       reg_b, reg_c, reg_d, reg_a, 15, MD4_S24

; round 3

                        MD4HH       reg_a, reg_b, reg_c, reg_d,  0, MD4_S31
                        MD4HH       reg_d, reg_a, reg_b, reg_c,  8, MD4_S32
                        MD4HH       reg_c, reg_d, reg_a, reg_b,  4, MD4_S33
                        MD4HH       reg_b, reg_c, reg_d, reg_a, 12, MD4_S34

                        MD4HH       reg_a, reg_b, reg_c, reg_d,  2, MD4_S31
                        MD4HH       reg_d, reg_a, reg_b, reg_c, 10, MD4_S32
                        MD4HH       reg_c, reg_d, reg_a, reg_b,  6, MD4_S33
                        MD4HH       reg_b, reg_c, reg_d, reg_a, 14, MD4_S34

                        MD4HH       reg_a, reg_b, reg_c, reg_d,  1, MD4_S31
                        MD4HH       reg_d, reg_a, reg_b, reg_c,  9, MD4_S32
                        MD4HH       reg_c, reg_d, reg_a, reg_b,  5, MD4_S33
                        MD4HH       reg_b, reg_c, reg_d, reg_a, 13, MD4_S34

                        MD4HH       reg_a, reg_b, reg_c, reg_d,  3, MD4_S31
                        MD4HH       reg_d, reg_a, reg_b, reg_c, 11, MD4_S32
                        MD4HH       reg_c, reg_d, reg_a, reg_b,  7, MD4_S33
                        MD4HH       reg_b, reg_c, reg_d, reg_a, 15, MD4_S34

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
                        call        MD4_Transform_p5
                        mov         ebp, __Data
                        jmp         full_blocks

short_stream:           sub         ecx, eax                                ;  --> ecx=_nLength
                        mov         esi, ebp
                        lea         edi, [edi+m_nBuffer+eax]
                        rep movsb

get_out:                popa
                        ret

MD4_Add_p5              ENDP

                end
