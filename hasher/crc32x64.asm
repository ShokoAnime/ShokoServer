.code
crcCalc		PROC	PUBLIC	USES rax rbx rcx rdx rdi rsi	pdwCrc32:PTR DWORD, ptrCrc32Table:PTR DWORD,bufferAsm:PTR BYTE,dwBytesReadAsm:DWORD

			;mov rax, pdwCrc32			; Load the pointer to dwCrc32
			mov rsi, rcx
			mov ecx, [rsi]				; Dereference the pointer to load dwCrc32

			;mov rdi, ptrCrc32Table		; Load the CRC32 table

			;mov rsi, bufferAsm			; Load buffer
			xor rbx, rbx
			;mov ebx, dwBytesReadAsm		; Load dwBytesRead
			lea rdi, [r8 + r9]		; Calculate the end of the buffer

		crc32loop:
			xor rax, rax				; Clear the eax register
			mov bl, byte ptr [r8]		; Load the current source byte
			
			mov al, cl					; Copy crc value into eax
			inc r8						; Advance the source pointer

			xor al, bl					; Create the index into the CRC32 table
			shr ecx, 8

			mov ebx, [rdx + rax * 4]	; Get the value out of the table
			xor ecx, ebx				; xor with the current byte

			cmp rdi, r8				; Have we reached the end of the buffer?
			jne crc32loop

			; Restore the edi and esi registers
			;pop edi
			;pop esi

			;mov rax, pdwCrc32			; Load the pointer to dwCrc32
			mov [rsi], ecx				; Write the result

			ret

crcCalc		ENDP
END