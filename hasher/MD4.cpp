//
// MD4.cpp
//
// Copyright (c) Shareaza Development Team, 2002-2008.
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

// other copyright notices: see end of file

#include "StdAfx.h"
#include "MD4.h"



const unsigned char hashPadding[64] = {
	0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
};


CMD4::CMD4()
{
	Reset();
}

void CMD4::GetHash(__in_bcount(16) uchar* pHash) const
{
	std::transform(m_State.m_nState,
		m_State.m_nState + sizeof(m_State.m_nState) / sizeof(m_State.m_nState[0]),
		(uint32*)pHash, transformToLE< uint32 >);
}

void CMD4::Reset()
{
	m_State.m_nCount = 0;
	// Load magic initialization constants
	m_State.m_nState[0] = 0x67452301;
	m_State.m_nState[1] = 0xefcdab89;
	m_State.m_nState[2] = 0x98badcfe;
	m_State.m_nState[3] = 0x10325476;
}

void CMD4::Finish()
{
	// Save number of bits
	uint64 bits = transformToLE(m_State.m_nCount * 8);
	// Pad out to 56 mod 64.
	uint32 index = static_cast< uint32 >(m_State.m_nCount % m_State.blockSize);
	Add(hashPadding, m_State.blockSize - sizeof(bits) - index
		+ (index < m_State.blockSize - sizeof(bits) ? 0 : m_State.blockSize));
	Add(&bits, sizeof(bits));
}



#ifdef HASHLIB_USE_ASM

#if defined(_WIN64) || defined(__x86_64__)
extern "C" void __fastcall MD4_x64(const void *, const void* pData, std::size_t nLength);
#else
extern "C" void __stdcall MD4_Add_p5(CMD4::MD4State*, const void* pData, std::size_t nLength);
#endif

void CMD4::Add(const void* pData, std::size_t nLength)
{
#if defined(_WIN64) || defined(__x86_64__)
	// Update number of bytes
	const char* input = static_cast< const char* >(pData);
	{
		uint32 index = static_cast< uint32 >(m_State.m_nCount % m_State.blockSize);
		m_State.m_nCount += nLength;
		if (index)
		{
			// buffer has some data already - lets fill it
			// before doing the rest of the transformation on the original data
			if (index + nLength < m_State.blockSize)
			{
				std::memcpy(m_State.m_oBuffer + index, input, nLength);
				return;
			}
			std::memcpy(m_State.m_oBuffer + index, input, m_State.blockSize - index);
			nLength -= m_State.blockSize - index;
			input += m_State.blockSize - index;
			MD4_x64(&(m_State.m_nState[0]), m_State.m_oBuffer, 1);
		}
	}
	// Transform as many times as possible using the original data stream
	const char* const end = input + nLength - nLength % m_State.blockSize;
	size_t abs = nLength / m_State.blockSize;
	MD4_x64(&(m_State.m_nState[0]), input, abs);
	abs *= m_State.blockSize;
	input += abs;
	nLength %= m_State.blockSize;
	// Buffer remaining input
	if (nLength)
		std::memcpy(m_State.m_oBuffer, input, nLength);
#else
	MD4_Add_p5(&m_State, pData, nLength);
#endif


}
#else // HASHLIB_USE_ASM

namespace
{
	// Constants for MD4 Transform
	template< uint32 tier, uint32 stage > struct S;
	template<> struct S< 0, 0 > { static const uint32 value = 3; };
	template<> struct S< 0, 1 > { static const uint32 value = 7; };
	template<> struct S< 0, 2 > { static const uint32 value = 11; };
	template<> struct S< 0, 3 > { static const uint32 value = 19; };
	template<> struct S< 1, 0 > { static const uint32 value = 3; };
	template<> struct S< 1, 1 > { static const uint32 value = 5; };
	template<> struct S< 1, 2 > { static const uint32 value = 9; };
	template<> struct S< 1, 3 > { static const uint32 value = 13; };
	template<> struct S< 2, 0 > { static const uint32 value = 3; };
	template<> struct S< 2, 1 > { static const uint32 value = 9; };
	template<> struct S< 2, 2 > { static const uint32 value = 11; };
	template<> struct S< 2, 3 > { static const uint32 value = 15; };


	// F transformation
	template< uint32 round >
	__forceinline void F(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = round;
		static const uint8 s = S< 0, round % 4 >::value;
		a += (d ^ (b & (c ^ d))) + transformFromLE(data[x]);
		a = rotateLeft(a, s);
	}
	// G transformation
	template< uint32 round >
	__forceinline void G(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = (round % 4) * 4 + (round / 4);
		static const uint8 s = S< 1, round % 4 >::value;
		a += ((b & c) | (d & (b | c))) + transformFromLE(data[x]) + 0x5a827999u;
		a = rotateLeft(a, s);
	}
	// H transformation
	template< uint32 round >
	__forceinline void H(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = (round % 2) * 8 + (round / 2 % 2) * 4
			+ (round / 4 % 2) * 2 + (round / 8);
		static const uint8 s = S< 2, round % 4 >::value;
		a += (b ^ c ^ d) + transformFromLE(data[x]) + 0x6ed9eba1u;
		a = rotateLeft(a, s);
	}
} // namespace

// MD4 basic transformation. Transforms state based on block.
void CMD4::Transform(const uint32* data)
{
	uint32 a = m_State.m_nState[0];
	uint32 b = m_State.m_nState[1];
	uint32 c = m_State.m_nState[2];
	uint32 d = m_State.m_nState[3];

	F<  0 >(data, a, b, c, d);
	F<  1 >(data, d, a, b, c);
	F<  2 >(data, c, d, a, b);
	F<  3 >(data, b, c, d, a);
	F<  4 >(data, a, b, c, d);
	F<  5 >(data, d, a, b, c);
	F<  6 >(data, c, d, a, b);
	F<  7 >(data, b, c, d, a);
	F<  8 >(data, a, b, c, d);
	F<  9 >(data, d, a, b, c);
	F< 10 >(data, c, d, a, b);
	F< 11 >(data, b, c, d, a);
	F< 12 >(data, a, b, c, d);
	F< 13 >(data, d, a, b, c);
	F< 14 >(data, c, d, a, b);
	F< 15 >(data, b, c, d, a);

	G<  0 >(data, a, b, c, d);
	G<  1 >(data, d, a, b, c);
	G<  2 >(data, c, d, a, b);
	G<  3 >(data, b, c, d, a);
	G<  4 >(data, a, b, c, d);
	G<  5 >(data, d, a, b, c);
	G<  6 >(data, c, d, a, b);
	G<  7 >(data, b, c, d, a);
	G<  8 >(data, a, b, c, d);
	G<  9 >(data, d, a, b, c);
	G< 10 >(data, c, d, a, b);
	G< 11 >(data, b, c, d, a);
	G< 12 >(data, a, b, c, d);
	G< 13 >(data, d, a, b, c);
	G< 14 >(data, c, d, a, b);
	G< 15 >(data, b, c, d, a);

	H<  0 >(data, a, b, c, d);
	H<  1 >(data, d, a, b, c);
	H<  2 >(data, c, d, a, b);
	H<  3 >(data, b, c, d, a);
	H<  4 >(data, a, b, c, d);
	H<  5 >(data, d, a, b, c);
	H<  6 >(data, c, d, a, b);
	H<  7 >(data, b, c, d, a);
	H<  8 >(data, a, b, c, d);
	H<  9 >(data, d, a, b, c);
	H< 10 >(data, c, d, a, b);
	H< 11 >(data, b, c, d, a);
	H< 12 >(data, a, b, c, d);
	H< 13 >(data, d, a, b, c);
	H< 14 >(data, c, d, a, b);
	H< 15 >(data, b, c, d, a);

	m_State.m_nState[0] += a;
	m_State.m_nState[1] += b;
	m_State.m_nState[2] += c;
	m_State.m_nState[3] += d;
}
void CMD4::Add(const void* pData, std::size_t nLength)
{
	// Update number of bytes
	const char* input = static_cast<const char*>(pData);
	{
		uint32 index = static_cast<uint32>(m_State.m_nCount % m_State.blockSize);
		m_State.m_nCount += nLength;
		if (index)
		{
			// buffer has some data already - lets fill it
			// before doing the rest of the transformation on the original data
			if (index + nLength < m_State.blockSize)
			{
				std::memcpy(m_State.m_oBuffer + index, input, nLength);
				return;
			}
			std::memcpy(m_State.m_oBuffer + index, input, m_State.blockSize - index);
			nLength -= m_State.blockSize - index;
			input += m_State.blockSize - index;
			Transform(reinterpret_cast<const uint32*>(m_State.m_oBuffer));
		}
	}
	// Transform as many times as possible using the original data stream
	const char* const end = input + nLength - nLength % m_State.blockSize;
	nLength %= m_State.blockSize;
	for (; input != end; input += m_State.blockSize)
		Transform(reinterpret_cast<const uint32*>(input));
	// Buffer remaining input
	if (nLength)
		std::memcpy(m_State.m_oBuffer, input, nLength);
}


#endif // HASHLIB_USE_ASM

//
// Free implementation of the MD4 hash algorithm
// MD4C.C - RSA Data Security, Inc., MD4 message-digest algorithm
//


// Copyright (C) 1990-2, RSA Data Security, Inc. All rights reserved.

// License to copy and use this software is granted provided that it
// is identified as the "RSA Data Security, Inc. MD4 Message-Digest
// Algorithm" in all material mentioning or referencing this software
// or this function.

// License is also granted to make and use derivative works provided
// that such works are identified as "derived from the RSA Data
// Security, Inc. MD4 Message-Digest Algorithm" in all material
// mentioning or referencing the derived work.  

// RSA Data Security, Inc. makes no representations concerning either
// the merchantability of this software or the suitability of this
// software for any particular purpose. It is provided "as is"
// without express or implied warranty of any kind.  

// These notices must be retained in any copies of any part of this
// documentation and/or software.  