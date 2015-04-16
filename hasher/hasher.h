#pragma once

typedef int(__stdcall *HASHCALLBACK)(LPCWSTR pszFile, int nProgress);

extern "C" __declspec(dllexport) int __cdecl CalculateHashes_SsyncIO(LPCWSTR pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1);
extern "C" __declspec(dllexport) int __cdecl CalculateHashes_AsyncIO(LPCWSTR pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1);
extern "C" __declspec(dllexport) int __cdecl CalculateHashes(LPCWSTR pszFile, unsigned char * pResult, HASHCALLBACK pHashProgress, bool getCRC32, bool getMD5, bool getSHA1);
