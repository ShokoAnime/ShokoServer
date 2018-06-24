SECTION .text
global _crcCalc
align 10h
_crcCalc:	
			pushad
			mov eax, [esp+32+4] 	; Load the pointer to dwCrc32
			mov ecx, [eax]				; Dereference the pointer to load dwCrc32
			mov edi, [esp+32+8]		; Load the CRC32 table
			mov esi, [esp+32+12]			; Load buffer
			mov ebx, [esp+32+16]		; Load dwBytesRead
			lea edx, [esi + ebx]		; Calculate the end of the buffer
crc32loop:
			xor eax, eax				; Clear the eax register
			mov bl, byte [esi]		; Load the current source byte			
			mov al, cl					; Copy crc value into eax
			inc esi						; Advance the source pointer
			xor al, bl					; Create the index into the CRC32 table
			shr ecx, 8
			mov ebx, [edi + eax * 4]	; Get the value out of the table
			xor ecx, ebx				; xor with the current byte
			cmp edx, esi				; Have we reached the end of the buffer?
			jne crc32loop
			mov eax, [esp+32+4]			; Load the pointer to dwCrc32
			mov [eax], ecx				; Write the result
			popad
			ret

