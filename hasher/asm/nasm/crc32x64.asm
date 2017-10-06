SECTION .text
global crcCalc
align 10h
crcCalc:	
			push rbx
			push rdi
			push rsi
			mov rsi, rcx          ; Load the pointer to dwCrc32
			mov ecx, [rsi]				; Dereference the pointer to load dwCrc32
			xor rbx, rbx
			lea rdi, [r8 + r9]		; Calculate the end of the buffer
crc32loop:
			xor rax, rax				; Clear the eax register
			mov bl, byte [r8]		; Load the current source byte
			
			mov al, cl					; Copy crc value into eax
			inc r8						; Advance the source pointer

			xor al, bl					; Create the index into the CRC32 table
			shr ecx, 8

			mov ebx, [rdx + rax * 4]	; Get the value out of the table
			xor ecx, ebx				; xor with the current byte

			cmp rdi, r8				; Have we reached the end of the buffer?
			jne crc32loop

			mov [rsi], ecx				; Write the result
			pop rsi
			pop rdi
			pop rbx
			ret

