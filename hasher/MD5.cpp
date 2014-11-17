//
// MD5.cpp
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
#include "MD5.h"


#ifdef HASHLIB_USE_ASM
extern "C" void __stdcall MD5_Add_p5(CMD5::MD5State*, const void* pData, std::size_t nLength);
#endif

const unsigned char hashPadding[64] = {
	0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
};


CMD5::CMD5()
{
	Reset();
}

void CMD5::GetHash(__in_bcount(16) uchar* pHash) const
{
	std::transform(m_State.m_nState,
		m_State.m_nState + sizeof(m_State.m_nState) / sizeof(m_State.m_nState[0]),
		(uint32*)pHash, transformToLE< uint32 >);
}

void CMD5::Reset()
{
	m_State.m_nCount = 0;
	// Load magic initialization constants
	m_State.m_nState[0] = 0x67452301;
	m_State.m_nState[1] = 0xefcdab89;
	m_State.m_nState[2] = 0x98badcfe;
	m_State.m_nState[3] = 0x10325476;
}

void CMD5::Finish()
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

void CMD5::Add(const void* pData, std::size_t nLength)
{
	MD5_Add_p5(&m_State, pData, nLength);
}

#else // HASHLIB_USE_ASM

namespace
{
	// Constants for Transform routine.
	template< uint32 tier, uint32 stage > struct S;
	template<> struct S< 0, 0 > { static const uint32 value = 7; };
	template<> struct S< 0, 1 > { static const uint32 value = 12; };
	template<> struct S< 0, 2 > { static const uint32 value = 17; };
	template<> struct S< 0, 3 > { static const uint32 value = 22; };
	template<> struct S< 1, 0 > { static const uint32 value = 5; };
	template<> struct S< 1, 1 > { static const uint32 value = 9; };
	template<> struct S< 1, 2 > { static const uint32 value = 14; };
	template<> struct S< 1, 3 > { static const uint32 value = 20; };
	template<> struct S< 2, 0 > { static const uint32 value = 4; };
	template<> struct S< 2, 1 > { static const uint32 value = 11; };
	template<> struct S< 2, 2 > { static const uint32 value = 16; };
	template<> struct S< 2, 3 > { static const uint32 value = 23; };
	template<> struct S< 3, 0 > { static const uint32 value = 6; };
	template<> struct S< 3, 1 > { static const uint32 value = 10; };
	template<> struct S< 3, 2 > { static const uint32 value = 15; };
	template<> struct S< 3, 3 > { static const uint32 value = 21; };

	// F transformation
	template< uint32 round, uint32 magic >
	__forceinline void F(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = round;
		static const uint32 s = S< 0, round % 4 >::value;
		a += (d ^ (b & (c ^ d))) + transformFromLE(data[x]) + magic;
		a = rotateLeft(a, s) + b;
	}
	// G transformation
	template< uint32 round, uint32 magic >
	__forceinline void G(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = (1 + (round & 3) * 5 + (round & 12)) & 15;
		static const uint32 s = S< 1, round % 4 >::value;
		a += (c ^ (d & (b ^ c))) + transformFromLE(data[x]) + magic;
		a = rotateLeft(a, s) + b;
	}
	// H transformation
	template< uint32 round, uint32 magic >
	__forceinline void H(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = (5 + (round & 7) * 3 + (round & 8)) & 15;
		static const uint32 s = S< 2, round % 4 >::value;
		a += (b ^ c ^ d) + transformFromLE(data[x]) + magic;
		a = rotateLeft(a, s) + b;
	}
	// I transformation
	template< uint32 round, uint32 magic >
	__forceinline void I(const uint32* data, uint32& a, uint32 b, uint32 c, uint32 d)
	{
		static const uint32 x = ((round & 3) * 7 + (round & 4) * 3 + (round & 8)) & 15;
		static const uint32 s = S< 3, round % 4 >::value;
		a += (c ^ (b | ~d)) + transformFromLE(data[x]) + magic;
		a = rotateLeft(a, s) + b;
	}
} // namespace

// MD5 basic transformation. Transforms state based on block.
void CMD5::Transform(const uint32* data)
{
	uint32 a = m_State.m_nState[0];
	uint32 b = m_State.m_nState[1];
	uint32 c = m_State.m_nState[2];
	uint32 d = m_State.m_nState[3];

	F<  0, 0xd76aa478 >(data, a, b, c, d);
	F<  1, 0xe8c7b756 >(data, d, a, b, c);
	F<  2, 0x242070db >(data, c, d, a, b);
	F<  3, 0xc1bdceee >(data, b, c, d, a);
	F<  4, 0xf57c0faf >(data, a, b, c, d);
	F<  5, 0x4787c62a >(data, d, a, b, c);
	F<  6, 0xa8304613 >(data, c, d, a, b);
	F<  7, 0xfd469501 >(data, b, c, d, a);
	F<  8, 0x698098d8 >(data, a, b, c, d);
	F<  9, 0x8b44f7af >(data, d, a, b, c);
	F< 10, 0xffff5bb1 >(data, c, d, a, b);
	F< 11, 0x895cd7be >(data, b, c, d, a);
	F< 12, 0x6b901122 >(data, a, b, c, d);
	F< 13, 0xfd987193 >(data, d, a, b, c);
	F< 14, 0xa679438e >(data, c, d, a, b);
	F< 15, 0x49b40821 >(data, b, c, d, a);

	G<  0, 0xf61e2562 >(data, a, b, c, d);
	G<  1, 0xc040b340 >(data, d, a, b, c);
	G<  2, 0x265e5a51 >(data, c, d, a, b);
	G<  3, 0xe9b6c7aa >(data, b, c, d, a);
	G<  4, 0xd62f105d >(data, a, b, c, d);
	G<  5, 0x02441453 >(data, d, a, b, c);
	G<  6, 0xd8a1e681 >(data, c, d, a, b);
	G<  7, 0xe7d3fbc8 >(data, b, c, d, a);
	G<  8, 0x21e1cde6 >(data, a, b, c, d);
	G<  9, 0xc33707d6 >(data, d, a, b, c);
	G< 10, 0xf4d50d87 >(data, c, d, a, b);
	G< 11, 0x455a14ed >(data, b, c, d, a);
	G< 12, 0xa9e3e905 >(data, a, b, c, d);
	G< 13, 0xfcefa3f8 >(data, d, a, b, c);
	G< 14, 0x676f02d9 >(data, c, d, a, b);
	G< 15, 0x8d2a4c8a >(data, b, c, d, a);

	H<  0, 0xfffa3942 >(data, a, b, c, d);
	H<  1, 0x8771f681 >(data, d, a, b, c);
	H<  2, 0x6d9d6122 >(data, c, d, a, b);
	H<  3, 0xfde5380c >(data, b, c, d, a);
	H<  4, 0xa4beea44 >(data, a, b, c, d);
	H<  5, 0x4bdecfa9 >(data, d, a, b, c);
	H<  6, 0xf6bb4b60 >(data, c, d, a, b);
	H<  7, 0xbebfbc70 >(data, b, c, d, a);
	H<  8, 0x289b7ec6 >(data, a, b, c, d);
	H<  9, 0xeaa127fa >(data, d, a, b, c);
	H< 10, 0xd4ef3085 >(data, c, d, a, b);
	H< 11, 0x04881d05 >(data, b, c, d, a);
	H< 12, 0xd9d4d039 >(data, a, b, c, d);
	H< 13, 0xe6db99e5 >(data, d, a, b, c);
	H< 14, 0x1fa27cf8 >(data, c, d, a, b);
	H< 15, 0xc4ac5665 >(data, b, c, d, a);

	I<  0, 0xf4292244 >(data, a, b, c, d);
	I<  1, 0x432aff97 >(data, d, a, b, c);
	I<  2, 0xab9423a7 >(data, c, d, a, b);
	I<  3, 0xfc93a039 >(data, b, c, d, a);
	I<  4, 0x655b59c3 >(data, a, b, c, d);
	I<  5, 0x8f0ccc92 >(data, d, a, b, c);
	I<  6, 0xffeff47d >(data, c, d, a, b);
	I<  7, 0x85845dd1 >(data, b, c, d, a);
	I<  8, 0x6fa87e4f >(data, a, b, c, d);
	I<  9, 0xfe2ce6e0 >(data, d, a, b, c);
	I< 10, 0xa3014314 >(data, c, d, a, b);
	I< 11, 0x4e0811a1 >(data, b, c, d, a);
	I< 12, 0xf7537e82 >(data, a, b, c, d);
	I< 13, 0xbd3af235 >(data, d, a, b, c);
	I< 14, 0x2ad7d2bb >(data, c, d, a, b);
	I< 15, 0xeb86d391 >(data, b, c, d, a);

	m_State.m_nState[0] += a;
	m_State.m_nState[1] += b;
	m_State.m_nState[2] += c;
	m_State.m_nState[3] += d;
}

void CMD5::Add(const void* pData, std::size_t nLength)
{
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
			Transform(reinterpret_cast< const uint32* >(m_State.m_oBuffer));
		}
	}
	// Transform as many times as possible using the original data stream
	const char* const end = input + nLength - nLength % m_State.blockSize;
	nLength %= m_State.blockSize;
	for (; input != end; input += m_State.blockSize)
		Transform(reinterpret_cast< const uint32* >(input));
	// Buffer remaining input
	if (nLength)
		std::memcpy(m_State.m_oBuffer, input, nLength);
}

#endif // HASHLIB_USE_ASM

// MD5.CPP - RSA Data Security, Inc., MD5 message-digest algorithm

// Copyright (C) 1991-2, RSA Data Security, Inc. Created 1991. All
// Copyrights reserved.
//
// License to copy and use this software is granted provided that it
// is identified as the "RSA Data Security, Inc. MD5 Message-Digest
// Algorithm" in all material mentioning or referencing this software
// or this function.
//
// License is also granted to make and use derivative works provided
// that such works are identified as "derived from the RSA Data
// Security, Inc. MD5 Message-Digest Algorithm" in all material
// mentioning or referencing the derived work.
//
// RSA Data Security, Inc. makes no representations concerning either
// the merchantability of this software or the suitability of this
// software for any particular purpose. It is provided "as is"
// without express or implied warranty of any kind.
//
// These notices must be retained in any copies of any part of this
// documentation and/or software.
