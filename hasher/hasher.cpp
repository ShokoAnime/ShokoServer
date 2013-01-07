// ed2k.cpp : Defines the entry point for the DLL application.
//

#include "stdafx.h"
#include "hasher.h"
#include "md4.h"
#include "hash_wrapper.h"
#include <sys/types.h>
#include <sys/stat.h>
#include <tchar.h>
#include <stdio.h>

/////////////////////////////////////////////////////////////////////////////////
#define ED2K_CHUNK_SIZE  9728000
#define SIZE_HASH_BUFFER  16384

extern "C" __declspec(dllexport) int __cdecl CalculateHashes_SyncIO(const TCHAR * pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1)
{
	struct _stat64 statFile;
	if (_tstat64(pszFile, &statFile) != 0)
		return 1;
	if (statFile.st_size <= 0)
		return 6;

	//hash file in chunks of 9728000 bytes
	UINT uChunkSize = 9728000;
	UINT nChunks = (UINT)(statFile.st_size / uChunkSize);
	UINT64 uChunkSizeLast = statFile.st_size % uChunkSize;
	if (uChunkSizeLast > 0)
		nChunks++;
	else
		uChunkSizeLast = uChunkSize;

	HANDLE hFile = CreateFile(pszFile, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, NULL);
	if (hFile == INVALID_HANDLE_VALUE)
		return 2;

	CMD4 MD4Engine;

	unsigned char pBuf[SIZE_HASH_BUFFER];
	DWORD dwRead = 0;
	unsigned char * pTemp = new unsigned char[nChunks*16];
	UINT64 uReadTotal = 0;

	int nStatus = 0;
	UINT nChunk = 0;
	MD4 md4;
	DigestSHA sha1;
	DigestCRC crc32;
	DigestMD5 md5;

	while (nChunk < nChunks)
	{
		UINT64 uCurrentChunkSize = (nChunk == nChunks - 1)?uChunkSizeLast:uChunkSize;

		//calculate MD4 of chunk
		MD4Engine.Reset();

		unsigned long dwReadChunk = 0;
		unsigned long dwReadToRead = 0;
		while (dwReadChunk < uCurrentChunkSize)
		{
			if (uCurrentChunkSize - dwReadChunk > SIZE_HASH_BUFFER)
				dwReadToRead = SIZE_HASH_BUFFER;
			else
				dwReadToRead = (DWORD)(uCurrentChunkSize - dwReadChunk);
			if (!ReadFile(hFile, pBuf, dwReadToRead, &dwRead, NULL))
			{
				nStatus = 3;
				break;
			}
			dwReadChunk += dwRead;
			uReadTotal += dwRead;

			MD4Engine.Add(pBuf, dwRead);
			if (getSHA1) sha1.update((char *)pBuf,dwRead);
			if (getCRC32) crc32.update((char *)pBuf,dwRead);
			if (getMD5) md5.update((char *)pBuf,dwRead);
		}

		MD4Engine.Finish();
		MD4Engine.GetHash(&md4);
		BYTE * pData = (BYTE*)&md4;
		for (int n=0; n<16; n++)
			pTemp[nChunk*16+n] = pData[n];

		nChunk++;

		//report progress
		if (pHashProgress)
		{
			int nResult = (*pHashProgress)(pszFile, (int)((float)uReadTotal / statFile.st_size * 100));
			if (nResult == 0)
			{
				nStatus = 4;
				break;
			}
		}
	}

	CloseHandle(hFile);
	hFile = NULL;

	//create final hash
	if (nStatus == 0 && nChunk != nChunks)
		nStatus = 5;

	if (nStatus == 0)
	{
		//First ED2K
		if (nChunks > 1)
		{
			MD4Engine.Reset();
			MD4Engine.Add(pTemp, nChunks*16);
			MD4Engine.Finish();
			MD4Engine.GetHash(&md4);
			BYTE * pData = (BYTE*)&md4;
			for (int n=0; n<16; n++)
				pResult[n] = pData[n];
		}
		else
		{
			memcpy(pResult, pTemp, 16);
		}
		if (getCRC32) crc32.digest((char *)(pResult+16),4);
		if (getMD5) md5.digest((char *)(pResult+20),16);
		if (getSHA1) sha1.digest((char *)(pResult+36),20);
	}


	delete pTemp;
	pTemp = NULL;

	return nStatus;
}


static const unsigned int NumBlocksPow = 3;
static const unsigned int NumBlocks = 1 << NumBlocksPow;
static const unsigned int NumBlocksMask = NumBlocks - 1;
static const unsigned int BlockSize = 1024 * 1024;
static const unsigned int uChunkSize = 9728000;

#define LODWORD(l) ((DWORD)((DWORDLONG)(l)))
#define HIDWORD(l) ((DWORD)(((DWORDLONG)(l)>>32)&0xFFFFFFFF))
#define MAKEDWORDLONG(a,b) ((DWORDLONG)(((DWORD)(a))|(((DWORDLONG)((DWORD)(b)))<<32)))

extern "C" __declspec(dllexport) int __cdecl CalculateHashes_AsyncIO(const TCHAR * pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1)
{
	//get file size
	struct _stat64 statFile;
	if (_tstat64(pszFile, &statFile) != 0)
		return 1;
	if (statFile.st_size <= 0)
		return 6;
	unsigned __int64 FileSize = statFile.st_size;

	//hash file in chunks of 9728000 bytes
	UINT nChunks = (UINT)(statFile.st_size / uChunkSize);
	if (statFile.st_size % uChunkSize > 0)
		nChunks++;

	//open file
	HANDLE hFile = CreateFile(pszFile, GENERIC_READ, FILE_SHARE_READ, 0, OPEN_EXISTING,
		FILE_FLAG_NO_BUFFERING | FILE_FLAG_OVERLAPPED | FILE_FLAG_SEQUENTIAL_SCAN, 0);       
	if (hFile == INVALID_HANDLE_VALUE)
		return 2;

	// If the current OS is Vista or later, set the file IO priority to background. This will allow the
	// hashing to operate at full speed unless other, higher IO (low and above) is scheduled. This allows
	// the hashing to be relatively unobtrusive. Remember, this is just a hint, so other factors can come
	// in to play that could cause the IO to be issued at the Normal level.
	OSVERSIONINFOW osVersion;
	SecureZeroMemory(&osVersion, sizeof(OSVERSIONINFOW));
	osVersion.dwOSVersionInfoSize = sizeof(OSVERSIONINFOW);
	if (GetVersionExW(&osVersion) && osVersion.dwMajorVersion > 5)
	{
		FILE_IO_PRIORITY_HINT_INFO priorityHint;
		priorityHint.PriorityHint = IoPriorityHintVeryLow;
		SetFileInformationByHandle(hFile, FileIoPriorityHintInfo, &priorityHint, sizeof(priorityHint));
	}

	//allocate data
	char * blocks[NumBlocks] = {0};
	OVERLAPPED overlapped[NumBlocks] = {0};

	for (int i = 0; i < NumBlocks; i++)
	{			
		// VirtualAlloc() creates storage that is page aligned and so is disk sector aligned
		blocks[i] = static_cast<char *>(VirtualAlloc(0, BlockSize, MEM_COMMIT, PAGE_READWRITE));

		ZeroMemory(&overlapped[i], sizeof(OVERLAPPED));
		overlapped[i].hEvent = CreateEvent(0, FALSE, FALSE, 0);
	}

	CMD4 MD4Engine;

	unsigned int iWriterPos = 0;
	unsigned int iReaderPos = 0;
	unsigned __int64 iIOPos = 0;
	unsigned __int64 iPos = 0;

	unsigned int nLastProgress = -1;
	unsigned char * pTemp = new unsigned char[nChunks*16];
	UINT nChunk = 0;
	unsigned __int64 uChunkEnd = min(uChunkSize, FileSize);
	MD4 md4;
	DigestSHA sha1;
	DigestCRC crc32;
	DigestMD5 md5;

	
	TCHAR szMsg[255] = {0};
	int nStatus = 0;
	do		
	{   
		//read blocks, keep 8 I/O requests active
		while (iWriterPos - iReaderPos != NumBlocks  && iIOPos < FileSize)
		{
			overlapped[iWriterPos & NumBlocksMask].Offset = LODWORD(iIOPos);
			overlapped[iWriterPos & NumBlocksMask].OffsetHigh = HIDWORD(iIOPos);

			const int iMaskedWriterPos = iWriterPos & NumBlocksMask;
			//note: we set 'number of bytes to read' to BlockSize, which may be too much (end of file)
			//      this doesn't matter though, because ReadFile automatically stops at the end of the file
			if (!ReadFile(hFile, blocks[iMaskedWriterPos], BlockSize, NULL, &overlapped[iMaskedWriterPos]) && GetLastError() != ERROR_IO_PENDING)
			{
				nStatus = 3;
				break;
			}

			iWriterPos++;
			iIOPos += BlockSize;
		}

		if (nStatus != 0)
		{
			CancelIo(hFile);
			break;
		}

		//wait until next block is ready
		DWORD dwBytesRead = 0;
		const int iMaskedReaderPos = iReaderPos & NumBlocksMask;
		if (iPos < FileSize && !GetOverlappedResult(hFile, &overlapped[iMaskedReaderPos], &dwBytesRead, TRUE))
		{
			nStatus = 4;
			CancelIo(hFile);
			break;
		}

		//calculate MD4 of chunk
		if (iPos + dwBytesRead < uChunkEnd)
		{
			//update MD4 of current chunk
			MD4Engine.Add(blocks[iMaskedReaderPos], dwBytesRead);
			if (getSHA1) sha1.update(blocks[iMaskedReaderPos], dwBytesRead);
			if (getCRC32) crc32.update(blocks[iMaskedReaderPos], dwBytesRead);
			if (getMD5) md5.update(blocks[iMaskedReaderPos], dwBytesRead);

		}
		else
		{
			//update MD4 of current chunk
			DWORD dwBytesChunkLeft = (DWORD)(uChunkEnd - iPos);
			MD4Engine.Add(blocks[iMaskedReaderPos], dwBytesChunkLeft);
			if (getSHA1) sha1.update(blocks[iMaskedReaderPos], dwBytesChunkLeft);
			if (getCRC32) crc32.update(blocks[iMaskedReaderPos], dwBytesChunkLeft);
			if (getMD5) md5.update(blocks[iMaskedReaderPos], dwBytesChunkLeft);
			//calculate MD4 of chunk
			MD4Engine.Finish();
			MD4Engine.GetHash(&md4);
			BYTE * pData = (BYTE*)&md4;
			for (int n=0; n<16; n++)
				pTemp[nChunk*16+n] = pData[n];
			MD4Engine.Reset();

			//prepare for next chunk
			nChunk++;
			uChunkEnd += uChunkSize;
			if (FileSize < uChunkEnd)
				uChunkEnd = FileSize;

			//update MD4 of next chunk if data was left on the block
			DWORD dwBytesChunkNext = dwBytesRead - dwBytesChunkLeft;
			if (dwBytesChunkNext > 0)
				MD4Engine.Add(blocks[iMaskedReaderPos] + dwBytesChunkLeft, dwBytesChunkNext);
		}

		//prepare for next block
		iReaderPos++;
		iPos += dwBytesRead;

		//report progress
		int nProgress = (int)((float)iPos / statFile.st_size * 100);
		if (pHashProgress && nLastProgress != nProgress)
		{
			int nResult = (*pHashProgress)(pszFile, nProgress);
			if (nResult == 0)
			{
				nStatus = 5;
				break;
			}
			nLastProgress = nProgress;
		}
	}
	while (nChunk < nChunks);
	
	//clean up
	CloseHandle(hFile);	

	for (int i = 0; i < NumBlocks; i++)
	{									
		VirtualFree(blocks[i], 0, MEM_RELEASE);
		CloseHandle(overlapped[i].hEvent);
	}

	//create final hash
	if (nStatus == 0 && nChunk != nChunks)
		nStatus = 6;

	if (nStatus == 0)
	{
		if (nChunks > 1)
		{
			MD4Engine.Reset();
			MD4Engine.Add(pTemp, nChunks*16);
			MD4Engine.Finish();
			MD4Engine.GetHash(&md4);
			BYTE * pData = (BYTE*)&md4;
			for (int n=0; n<16; n++)
				pResult[n] = pData[n];
		}
		else
		{
			memcpy(pResult, pTemp, 16);
		}
		if (getCRC32) crc32.digest((char *)(pResult+16),4);
		if (getSHA1) md5.digest((char *)(pResult+20),16);
		if (getMD5) sha1.digest((char *)(pResult+36),20);
	}

	delete pTemp;
	pTemp = NULL;

	//report final progress
	if (pHashProgress && nLastProgress < 100)
		(*pHashProgress)(pszFile, 100);

	return nStatus;
}

extern "C" __declspec(dllexport) int __cdecl CalculateHashes(const TCHAR * pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1)
{
	return (0 == CalculateHashes_AsyncIO(pszFile, pResult, pHashProgress, getCRC32, getMD5, getSHA1)) ? TRUE : FALSE;
}
