; #####################################################################################################################
;
; MD5_asm.asm
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
; MD5_asm - Implementation of MD5 for x86 - use together with MD5.cpp and MD5.h
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

m_nState0               equ         8                              ; offsets as laid out in MD5.h
m_nState1               equ         12
m_nState2               equ         16
m_nState3               equ         20

m_pBuffer               equ         24

; Some magic numbers for Transform...
MD5_S11                 equ         7
MD5_S12                 equ         12
MD5_S13                 equ         17
MD5_S14                 equ         22
MD5_S21                 equ         5
MD5_S22                 equ         9
MD5_S23                 equ         14
MD5_S24                 equ         20
MD5_S31                 equ         4
MD5_S32                 equ         11
MD5_S33                 equ         16
MD5_S34                 equ         23
MD5_S41                 equ         6
MD5_S42                 equ         10
MD5_S43                 equ         15
MD5_S44                 equ         21

MD5FF                   MACRO       count:REQ,s:REQ,ac:REQ
; a = b+(a+x[count]+ac+((b&c)|(~b&d)))rol s
; a = b+(a+x[count]+ax+(d^(b&(c^d))))rol s
                        mov         reg_temp1, reg_c
reg_t                   textequ     reg_temp1
reg_temp1               textequ     reg_c
reg_c                   textequ     reg_t
                        xor         reg_temp1, reg_d
                        add         reg_a, [reg_base+count*4]
                        and         reg_temp1, reg_b
                        add         reg_a, ac
                        xor         reg_temp1, reg_d
                        add         reg_a, reg_temp1
                        rol         reg_a, s
                        add         reg_a, reg_b
reg_t                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM


MD5GG                   MACRO       count:REQ,s:REQ,ac:REQ
; a = b+(a+x[count]+ac+((d&b)|(~d&c)))rol s
; a = b+(a+x[count]+ac+(c^(d&(b^c))))rols s
                        mov         reg_temp1, reg_b
reg_t                   textequ     reg_temp1
reg_temp1               textequ     reg_b
reg_b                   textequ     reg_t
                        xor         reg_temp1, reg_c
                        add         reg_a, [reg_base+count*4]
                        and         reg_temp1, reg_d
                        add         reg_a, ac
                        xor         reg_temp1, reg_c
                        add         reg_a, reg_temp1
                        rol         reg_a, s
                        add         reg_a, reg_b
reg_t                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM

MD5HH                   MACRO       count:REQ,s:REQ,ac:REQ
; a = b+(a+x[count]+ac+(b^c^d)) rol s
                        mov         reg_temp1, reg_b
reg_t                   textequ     reg_temp1
reg_temp1               textequ     reg_b
reg_b                   textequ     reg_t
                        xor         reg_temp1, reg_c
                        add         reg_a, [reg_base+count*4]
                        xor         reg_temp1, reg_d
                        add         reg_a, ac
                        add         reg_a, reg_temp1
                        rol         reg_a, s
                        add         reg_a, reg_b
reg_t                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM

MD5II                   MACRO       count:REQ,s:REQ,ac:REQ
; a = b+(a+x[count]+ac+(c^(~d|b))) rol s
                        mov         reg_temp1, reg_d
reg_t                   textequ     reg_temp1
reg_temp1               textequ     reg_d
reg_d                   textequ     reg_t
                        not         reg_temp1
                        add         reg_a, [reg_base+count*4]
                        or          reg_temp1, reg_b
                        add         reg_a, ac
                        xor         reg_temp1, reg_c
                        add         reg_a, reg_temp1
                        rol         reg_a, s
                        add         reg_a, reg_b
reg_t                   textequ     reg_d
reg_d                   textequ     reg_c
reg_c                   textequ     reg_b
reg_b                   textequ     reg_a
reg_a                   textequ     reg_t
                        ENDM

                        .code

MD5_Transform_p5        PROC                                            ; we expect ebp to point to the Data stream
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
                        mov         reg_temp2, __this
                        mov         reg_a, [reg_temp2+m_nState0]
                        mov         reg_b, [reg_temp2+m_nState1]
                        mov         reg_c, [reg_temp2+m_nState2]
                        mov         reg_d, [reg_temp2+m_nState3]
; round 1
                        MD5FF        0, MD5_S11,0D76AA478H  ;  1
                        MD5FF        1, MD5_S12,0E8C7B756H  ;  2
                        MD5FF        2, MD5_S13, 242070DBH  ;  3
                        MD5FF        3, MD5_S14,0C1BDCEEEH  ;  4
                        MD5FF        4, MD5_S11,0F57C0FAFH  ;  5
                        MD5FF        5, MD5_S12, 4787C62AH  ;  6
                        MD5FF        6, MD5_S13,0A8304613H  ;  7
                        MD5FF        7, MD5_S14,0FD469501H  ;  8
                        MD5FF        8, MD5_S11, 698098D8H  ;  9
                        MD5FF        9, MD5_S12, 8B44F7AFH  ; 10
                        MD5FF       10, MD5_S13,0FFFF5BB1H  ; 11
                        MD5FF       11, MD5_S14, 895CD7BEH  ; 12
                        MD5FF       12, MD5_S11, 6B901122H  ; 13
                        MD5FF       13, MD5_S12,0FD987193H  ; 14
                        MD5FF       14, MD5_S13,0A679438EH  ; 15
                        MD5FF       15, MD5_S14, 49B40821H  ; 16
; round 2
                        MD5GG        1, MD5_S21,0F61E2562H  ; 17
                        MD5GG        6, MD5_S22,0C040B340H  ; 18
                        MD5GG       11, MD5_S23, 265E5A51H  ; 19
                        MD5GG        0, MD5_S24,0E9B6C7AAH  ; 20
                        MD5GG        5, MD5_S21,0D62F105DH  ; 21
                        MD5GG       10, MD5_S22,  2441453H  ; 22
                        MD5GG       15, MD5_S23,0D8A1E681H  ; 23
                        MD5GG        4, MD5_S24,0E7D3FBC8H  ; 24
                        MD5GG        9, MD5_S21, 21E1CDE6H  ; 25
                        MD5GG       14, MD5_S22,0C33707D6H  ; 26
                        MD5GG        3, MD5_S23,0F4D50D87H  ; 27
                        MD5GG        8, MD5_S24, 455A14EDH  ; 28
                        MD5GG       13, MD5_S21,0A9E3E905H  ; 29
                        MD5GG        2, MD5_S22,0FCEFA3F8H  ; 30
                        MD5GG        7, MD5_S23, 676F02D9H  ; 31
                        MD5GG       12, MD5_S24, 8D2A4C8AH  ; 32
; round 3
                        MD5HH        5, MD5_S31,0FFFA3942H  ; 33
                        MD5HH        8, MD5_S32, 8771F681H  ; 34
                        MD5HH       11, MD5_S33, 6D9D6122H  ; 35
                        MD5HH       14, MD5_S34,0FDE5380CH  ; 36
                        MD5HH        1, MD5_S31,0A4BEEA44H  ; 37
                        MD5HH        4, MD5_S32, 4BDECFA9H  ; 38
                        MD5HH        7, MD5_S33,0F6BB4B60H  ; 39
                        MD5HH       10, MD5_S34,0BEBFBC70H  ; 40
                        MD5HH       13, MD5_S31, 289B7EC6H  ; 41
                        MD5HH        0, MD5_S32,0EAA127FAH  ; 42
                        MD5HH        3, MD5_S33,0D4EF3085H  ; 43
                        MD5HH        6, MD5_S34,  4881D05H  ; 44
                        MD5HH        9, MD5_S31,0D9D4D039H  ; 45
                        MD5HH       12, MD5_S32,0E6DB99E5H  ; 46
                        MD5HH       15, MD5_S33, 1FA27CF8H  ; 47
                        MD5HH        2, MD5_S34,0C4AC5665H  ; 48
; round 4
                        MD5II        0, MD5_S41,0F4292244H  ; 49
                        MD5II        7, MD5_S42, 432AFF97H  ; 50
                        MD5II       14, MD5_S43,0AB9423A7H  ; 51
                        MD5II        5, MD5_S44,0FC93A039H  ; 52
                        MD5II       12, MD5_S41, 655B59C3H  ; 53
                        MD5II        3, MD5_S42, 8F0CCC92H  ; 54
                        MD5II       10, MD5_S43,0FFEFF47DH  ; 55
                        MD5II        1, MD5_S44, 85845DD1H  ; 56
                        MD5II        8, MD5_S41, 6FA87E4FH  ; 57
                        MD5II       15, MD5_S42,0FE2CE6E0H  ; 58
                        MD5II        6, MD5_S43,0A3014314H  ; 59
                        MD5II       13, MD5_S44, 4E0811A1H  ; 60
                        MD5II        4, MD5_S41,0F7537E82H  ; 61
                        MD5II       11, MD5_S42,0BD3AF235H  ; 62
                        MD5II        2, MD5_S43, 2AD7D2BBH  ; 63
                        MD5II        9, MD5_S44,0EB86D391H  ; 64
                        add         [reg_temp2+m_nState0], reg_a
                        add         [reg_temp2+m_nState1], reg_b
                        add         [reg_temp2+m_nState2], reg_c
                        add         [reg_temp2+m_nState3], reg_d
                        ret
MD5_Transform_p5        ENDP

MD5_Add_p5              PROC        PUBLIC, _this:DWORD, _Data:DWORD, _nLength:DWORD
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
                        call        MD5_Transform_p5
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
                        call        MD5_Transform_p5
                        mov         ebp, __Data
                        jmp         full_blocks
short_stream:           sub         ecx, eax                                ;  --> ecx=_nLength
                        mov         esi, ebp
                        lea         edi, [edi+m_pBuffer+eax]
                        rep movsb
get_out:                popa
                        ret 12

MD5_Add_p5              ENDP

                end
