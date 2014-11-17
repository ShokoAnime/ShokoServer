//
// MD4.h
//
// Copyright (c) Shareaza Development Team, 2002-2014.
// This file is part of SHAREAZA (shareaza.sourceforge.net)
//
// Shareaza is free software; you can redistribute it
// and/or modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2 of
// the License, or (at your option) any later version.
//
// Shareaza is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Shareaza; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//

#pragma once
#include "Utility.hpp"

typedef union
{
	BYTE	n[16];
	BYTE	b[16];
	DWORD	w[4];
} MD4, MD5;

class CMD4
{
public:
	CMD4();
	~CMD4() {}

	void Reset();
	void Add(const void* pData, size_t nLength);
	void Finish();

	struct Digest // 128 bit
	{
		uint32& operator[](size_t i) { return data[i]; }
		const uint32& operator[](size_t i) const { return data[i]; }
		uint32 data[4];
	};

	void GetHash(__in_bcount(16) uchar* pHash) const;

	struct MD4State
	{
		static const size_t blockSize = 64;
		uint64	m_nCount;
		uint32	m_nState[4];
		uchar	m_oBuffer[blockSize];
	};

private:
	MD4State m_State;

#ifndef HASHLIB_USE_ASM
	__forceinline void Transform(const uint32* data);
#endif
};