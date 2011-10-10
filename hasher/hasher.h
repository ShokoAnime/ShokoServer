#pragma once

typedef int (__stdcall *HASHCALLBACK)(const TCHAR * pszFile, int nProgress);

extern "C" __declspec(dllexport) int __cdecl CalculateHashes_SsyncIO(const TCHAR * pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1);
extern "C" __declspec(dllexport) int __cdecl CalculateHashes_AsyncIO(const TCHAR * pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1);
extern "C" __declspec(dllexport) int __cdecl CalculateHashes(const TCHAR * pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1);
