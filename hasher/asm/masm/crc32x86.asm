.586p
.model      flat, C

.code
crcCalc		PROC	PUBLIC	USES eax ebx ecx edx edi esi	pdwCrc32:PTR DWORD, ptrCrc32Table:PTR DWORD,bufferAsm:PTR BYTE,dwBytesReadAsm:DWORD

			mov eax, pdwCrc32			; Load the pointer to dwCrc32
			mov ecx, [eax]				; Dereference the pointer to load dwCrc32

			mov edi, ptrCrc32Table		; Load the CRC32 table

			mov esi, bufferAsm			; Load buffer
			mov ebx, dwBytesReadAsm		; Load dwBytesRead
			lea edx, [esi + ebx]		; Calculate the end of the buffer

		crc32loop:
			xor eax, eax				; Clear the eax register
			mov bl, byte ptr [esi]		; Load the current source byte
			
			mov al, cl					; Copy crc value into eax
			inc esi						; Advance the source pointer

			xor al, bl					; Create the index into the CRC32 table
			shr ecx, 8

			mov ebx, [edi + eax * 4]	; Get the value out of the table
			xor ecx, ebx				; xor with the current byte

			cmp edx, esi				; Have we reached the end of the buffer?
			jne crc32loop

			; Restore the edi and esi registers
			;pop edi
			;pop esi

			mov eax, pdwCrc32			; Load the pointer to dwCrc32
			mov [eax], ecx				; Write the result

			ret

crcCalc		ENDP
END