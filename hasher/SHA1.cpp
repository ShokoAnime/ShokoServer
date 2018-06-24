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
#include "SHA.h"




const unsigned char hashPadding[64] = {
	0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
};


CSHA::CSHA()
{
	Reset();
}

void CSHA::GetHash(__in_bcount(20) uchar* pHash) const
{
	std::transform(m_State.m_nState,
		m_State.m_nState + sizeof(m_State.m_nState) / sizeof(m_State.m_nState[0]),
		(uint32*)pHash, transformToBE< uint32 >);
}

void CSHA::Reset()
{
	m_State.m_nCount = 0;
	m_State.m_nState[0] = 0x67452301;
	m_State.m_nState[1] = 0xefcdab89;
	m_State.m_nState[2] = 0x98badcfe;
	m_State.m_nState[3] = 0x10325476;
	m_State.m_nState[4] = 0xc3d2e1f0;
}

void CSHA::Finish()
{
	// Save number of bits
	uint64 bits = transformToBE(m_State.m_nCount * 8);
	// Pad out to 56 mod 64.
	uint32 index = static_cast< uint32 >(m_State.m_nCount % m_State.blockSize);
	Add(hashPadding, m_State.blockSize - sizeof(bits) - index
		+ (index < m_State.blockSize - sizeof(bits) ? 0 : m_State.blockSize));
	Add(&bits, sizeof(bits));
}

#ifdef HASHLIB_USE_ASM

#ifdef _WIN64 || __x86_64__
extern "C" void __fastcall sha1_block_asm_data_order(const void *, const void* pData, std::size_t nLength);
#else
extern "C" void __stdcall SHA1_Add_p5(CSHA::SHA1State*, const void* pData, std::size_t nLength);
#endif
void CSHA::Add(const void* pData, std::size_t nLength)
{
#ifdef _WIN64 || __x86_64__
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
			sha1_block_asm_data_order(&(m_State.m_nState[0]), m_State.m_oBuffer, 1);
		}
	}
	// Transform as many times as possible using the original data stream
	const char* const end = input + nLength - nLength % m_State.blockSize;
	size_t abs = nLength / m_State.blockSize;
	sha1_block_asm_data_order(&(m_State.m_nState[0]), input, abs);
	abs *= m_State.blockSize;
	input += abs;
	nLength %= m_State.blockSize;
	// Buffer remaining input
	if (nLength)
		std::memcpy(m_State.m_oBuffer, input, nLength);
#else
	SHA1_Add_p5(&m_State, pData, nLength);
#endif


}
#else // HASHLIB_USE_ASM

CSHA::TransformArray::TransformArray(const uint32* const buffer)
{
	m_buffer[0] = transformFromBE(buffer[0]),
		m_buffer[1] = transformFromBE(buffer[1]),
		m_buffer[2] = transformFromBE(buffer[2]),
		m_buffer[3] = transformFromBE(buffer[3]),
		m_buffer[4] = transformFromBE(buffer[4]),
		m_buffer[5] = transformFromBE(buffer[5]),
		m_buffer[6] = transformFromBE(buffer[6]),
		m_buffer[7] = transformFromBE(buffer[7]),
		m_buffer[8] = transformFromBE(buffer[8]),
		m_buffer[9] = transformFromBE(buffer[9]),
		m_buffer[10] = transformFromBE(buffer[10]),
		m_buffer[11] = transformFromBE(buffer[11]),
		m_buffer[12] = transformFromBE(buffer[12]),
		m_buffer[13] = transformFromBE(buffer[13]),
		m_buffer[14] = transformFromBE(buffer[14]),
		m_buffer[15] = transformFromBE(buffer[15]);

	m_buffer[16] = rotateLeft(m_buffer[0] ^ m_buffer[2] ^ m_buffer[8] ^ m_buffer[13], 1);
	m_buffer[17] = rotateLeft(m_buffer[1] ^ m_buffer[3] ^ m_buffer[9] ^ m_buffer[14], 1);
	m_buffer[18] = rotateLeft(m_buffer[2] ^ m_buffer[4] ^ m_buffer[10] ^ m_buffer[15], 1);
	m_buffer[19] = rotateLeft(m_buffer[3] ^ m_buffer[5] ^ m_buffer[11] ^ m_buffer[16], 1);
	m_buffer[20] = rotateLeft(m_buffer[4] ^ m_buffer[6] ^ m_buffer[12] ^ m_buffer[17], 1);
	m_buffer[21] = rotateLeft(m_buffer[5] ^ m_buffer[7] ^ m_buffer[13] ^ m_buffer[18], 1);
	m_buffer[22] = rotateLeft(m_buffer[6] ^ m_buffer[8] ^ m_buffer[14] ^ m_buffer[19], 1);
	m_buffer[23] = rotateLeft(m_buffer[7] ^ m_buffer[9] ^ m_buffer[15] ^ m_buffer[20], 1);
	m_buffer[24] = rotateLeft(m_buffer[8] ^ m_buffer[10] ^ m_buffer[16] ^ m_buffer[21], 1);
	m_buffer[25] = rotateLeft(m_buffer[9] ^ m_buffer[11] ^ m_buffer[17] ^ m_buffer[22], 1);
	m_buffer[26] = rotateLeft(m_buffer[10] ^ m_buffer[12] ^ m_buffer[18] ^ m_buffer[23], 1);
	m_buffer[27] = rotateLeft(m_buffer[11] ^ m_buffer[13] ^ m_buffer[19] ^ m_buffer[24], 1);
	m_buffer[28] = rotateLeft(m_buffer[12] ^ m_buffer[14] ^ m_buffer[20] ^ m_buffer[25], 1);
	m_buffer[29] = rotateLeft(m_buffer[13] ^ m_buffer[15] ^ m_buffer[21] ^ m_buffer[26], 1);
	m_buffer[30] = rotateLeft(m_buffer[14] ^ m_buffer[16] ^ m_buffer[22] ^ m_buffer[27], 1);
	m_buffer[31] = rotateLeft(m_buffer[15] ^ m_buffer[17] ^ m_buffer[23] ^ m_buffer[28], 1);

	m_buffer[32] = rotateLeft(m_buffer[16] ^ m_buffer[18] ^ m_buffer[24] ^ m_buffer[29], 1);
	m_buffer[33] = rotateLeft(m_buffer[17] ^ m_buffer[19] ^ m_buffer[25] ^ m_buffer[30], 1);
	m_buffer[34] = rotateLeft(m_buffer[18] ^ m_buffer[20] ^ m_buffer[26] ^ m_buffer[31], 1);
	m_buffer[35] = rotateLeft(m_buffer[19] ^ m_buffer[21] ^ m_buffer[27] ^ m_buffer[32], 1);
	m_buffer[36] = rotateLeft(m_buffer[20] ^ m_buffer[22] ^ m_buffer[28] ^ m_buffer[33], 1);
	m_buffer[37] = rotateLeft(m_buffer[21] ^ m_buffer[23] ^ m_buffer[29] ^ m_buffer[34], 1);
	m_buffer[38] = rotateLeft(m_buffer[22] ^ m_buffer[24] ^ m_buffer[30] ^ m_buffer[35], 1);
	m_buffer[39] = rotateLeft(m_buffer[23] ^ m_buffer[25] ^ m_buffer[31] ^ m_buffer[36], 1);
	m_buffer[40] = rotateLeft(m_buffer[24] ^ m_buffer[26] ^ m_buffer[32] ^ m_buffer[37], 1);
	m_buffer[41] = rotateLeft(m_buffer[25] ^ m_buffer[27] ^ m_buffer[33] ^ m_buffer[38], 1);
	m_buffer[42] = rotateLeft(m_buffer[26] ^ m_buffer[28] ^ m_buffer[34] ^ m_buffer[39], 1);
	m_buffer[43] = rotateLeft(m_buffer[27] ^ m_buffer[29] ^ m_buffer[35] ^ m_buffer[40], 1);
	m_buffer[44] = rotateLeft(m_buffer[28] ^ m_buffer[30] ^ m_buffer[36] ^ m_buffer[41], 1);
	m_buffer[45] = rotateLeft(m_buffer[29] ^ m_buffer[31] ^ m_buffer[37] ^ m_buffer[42], 1);
	m_buffer[46] = rotateLeft(m_buffer[30] ^ m_buffer[32] ^ m_buffer[38] ^ m_buffer[43], 1);
	m_buffer[47] = rotateLeft(m_buffer[31] ^ m_buffer[33] ^ m_buffer[39] ^ m_buffer[44], 1);

	m_buffer[48] = rotateLeft(m_buffer[32] ^ m_buffer[34] ^ m_buffer[40] ^ m_buffer[45], 1);
	m_buffer[49] = rotateLeft(m_buffer[33] ^ m_buffer[35] ^ m_buffer[41] ^ m_buffer[46], 1);
	m_buffer[50] = rotateLeft(m_buffer[34] ^ m_buffer[36] ^ m_buffer[42] ^ m_buffer[47], 1);
	m_buffer[51] = rotateLeft(m_buffer[35] ^ m_buffer[37] ^ m_buffer[43] ^ m_buffer[48], 1);
	m_buffer[52] = rotateLeft(m_buffer[36] ^ m_buffer[38] ^ m_buffer[44] ^ m_buffer[49], 1);
	m_buffer[53] = rotateLeft(m_buffer[37] ^ m_buffer[39] ^ m_buffer[45] ^ m_buffer[50], 1);
	m_buffer[54] = rotateLeft(m_buffer[38] ^ m_buffer[40] ^ m_buffer[46] ^ m_buffer[51], 1);
	m_buffer[55] = rotateLeft(m_buffer[39] ^ m_buffer[41] ^ m_buffer[47] ^ m_buffer[52], 1);
	m_buffer[56] = rotateLeft(m_buffer[40] ^ m_buffer[42] ^ m_buffer[48] ^ m_buffer[53], 1);
	m_buffer[57] = rotateLeft(m_buffer[41] ^ m_buffer[43] ^ m_buffer[49] ^ m_buffer[54], 1);
	m_buffer[58] = rotateLeft(m_buffer[42] ^ m_buffer[44] ^ m_buffer[50] ^ m_buffer[55], 1);
	m_buffer[59] = rotateLeft(m_buffer[43] ^ m_buffer[45] ^ m_buffer[51] ^ m_buffer[56], 1);
	m_buffer[60] = rotateLeft(m_buffer[44] ^ m_buffer[46] ^ m_buffer[52] ^ m_buffer[57], 1);
	m_buffer[61] = rotateLeft(m_buffer[45] ^ m_buffer[47] ^ m_buffer[53] ^ m_buffer[58], 1);
	m_buffer[62] = rotateLeft(m_buffer[46] ^ m_buffer[48] ^ m_buffer[54] ^ m_buffer[59], 1);
	m_buffer[63] = rotateLeft(m_buffer[47] ^ m_buffer[49] ^ m_buffer[55] ^ m_buffer[60], 1);

	m_buffer[64] = rotateLeft(m_buffer[48] ^ m_buffer[50] ^ m_buffer[56] ^ m_buffer[61], 1);
	m_buffer[65] = rotateLeft(m_buffer[49] ^ m_buffer[51] ^ m_buffer[57] ^ m_buffer[62], 1);
	m_buffer[66] = rotateLeft(m_buffer[50] ^ m_buffer[52] ^ m_buffer[58] ^ m_buffer[63], 1);
	m_buffer[67] = rotateLeft(m_buffer[51] ^ m_buffer[53] ^ m_buffer[59] ^ m_buffer[64], 1);
	m_buffer[68] = rotateLeft(m_buffer[52] ^ m_buffer[54] ^ m_buffer[60] ^ m_buffer[65], 1);
	m_buffer[69] = rotateLeft(m_buffer[53] ^ m_buffer[55] ^ m_buffer[61] ^ m_buffer[66], 1);
	m_buffer[70] = rotateLeft(m_buffer[54] ^ m_buffer[56] ^ m_buffer[62] ^ m_buffer[67], 1);
	m_buffer[71] = rotateLeft(m_buffer[55] ^ m_buffer[57] ^ m_buffer[63] ^ m_buffer[68], 1);
	m_buffer[72] = rotateLeft(m_buffer[56] ^ m_buffer[58] ^ m_buffer[64] ^ m_buffer[69], 1);
	m_buffer[73] = rotateLeft(m_buffer[57] ^ m_buffer[59] ^ m_buffer[65] ^ m_buffer[70], 1);
	m_buffer[74] = rotateLeft(m_buffer[58] ^ m_buffer[60] ^ m_buffer[66] ^ m_buffer[71], 1);
	m_buffer[75] = rotateLeft(m_buffer[59] ^ m_buffer[61] ^ m_buffer[67] ^ m_buffer[72], 1);
	m_buffer[76] = rotateLeft(m_buffer[60] ^ m_buffer[62] ^ m_buffer[68] ^ m_buffer[73], 1);
	m_buffer[77] = rotateLeft(m_buffer[61] ^ m_buffer[63] ^ m_buffer[69] ^ m_buffer[74], 1);
	m_buffer[78] = rotateLeft(m_buffer[62] ^ m_buffer[64] ^ m_buffer[70] ^ m_buffer[75], 1);
	m_buffer[79] = rotateLeft(m_buffer[63] ^ m_buffer[65] ^ m_buffer[71] ^ m_buffer[76], 1);
}

namespace
{
	// ch transformation
	template< uint32 round >
	__forceinline void F(const CSHA::TransformArray& data, uint32& a, uint32& b, uint32 c, uint32 d, uint32 e, uint32& t)
	{
		t = a;
		a = rotateLeft(a, 5) + e + data[round] + 0x5a827999 + (d ^ (b & (c ^ d)));
		b = rotateLeft(b, 30);
	}
	// parity transformation
	template< uint32 round >
	__forceinline void G(const CSHA::TransformArray& data, uint32& a, uint32& b, uint32 c, uint32 d, uint32 e, uint32& t)
	{
		t = a;
		a = rotateLeft(a, 5) + e + data[round + 20] + 0x6ed9eba1 + (b ^ c ^ d);
		b = rotateLeft(b, 30);
	}
	// maj transformation
	template< uint32 round >
	__forceinline void H(const CSHA::TransformArray& data, uint32& a, uint32& b, uint32 c, uint32 d, uint32 e, uint32& t)
	{
		t = a;
		a = rotateLeft(a, 5) + e + data[round + 40] + 0x8f1bbcdc + ((c & d) ^ (b & (c ^ d)));
		b = rotateLeft(b, 30);
	}
	// parity transformation
	template< uint32 round >
	__forceinline void I(const CSHA::TransformArray& data, uint32& a, uint32& b, uint32 c, uint32 d, uint32 e, uint32& t)
	{
		t = a;
		a = rotateLeft(a, 5) + e + data[round + 60] + 0xca62c1d6 + (b ^ c ^ d);
		b = rotateLeft(b, 30);
	}
} // namespace

void CSHA::Transform(TransformArray w)
{
	uint32 a = m_State.m_nState[0];
	uint32 b = m_State.m_nState[1];
	uint32 c = m_State.m_nState[2];
	uint32 d = m_State.m_nState[3];
	uint32 e = m_State.m_nState[4];
	uint32 t;

	F<  0 >(w, a, b, c, d, e, t);
	F<  1 >(w, a, t, b, c, d, e);
	F<  2 >(w, a, e, t, b, c, d);
	F<  3 >(w, a, d, e, t, b, c);
	F<  4 >(w, a, c, d, e, t, b);
	F<  5 >(w, a, b, c, d, e, t);
	F<  6 >(w, a, t, b, c, d, e);
	F<  7 >(w, a, e, t, b, c, d);
	F<  8 >(w, a, d, e, t, b, c);
	F<  9 >(w, a, c, d, e, t, b);
	F< 10 >(w, a, b, c, d, e, t);
	F< 11 >(w, a, t, b, c, d, e);
	F< 12 >(w, a, e, t, b, c, d);
	F< 13 >(w, a, d, e, t, b, c);
	F< 14 >(w, a, c, d, e, t, b);
	F< 15 >(w, a, b, c, d, e, t);
	F< 16 >(w, a, t, b, c, d, e);
	F< 17 >(w, a, e, t, b, c, d);
	F< 18 >(w, a, d, e, t, b, c);
	F< 19 >(w, a, c, d, e, t, b);

	G<  0 >(w, a, b, c, d, e, t);
	G<  1 >(w, a, t, b, c, d, e);
	G<  2 >(w, a, e, t, b, c, d);
	G<  3 >(w, a, d, e, t, b, c);
	G<  4 >(w, a, c, d, e, t, b);
	G<  5 >(w, a, b, c, d, e, t);
	G<  6 >(w, a, t, b, c, d, e);
	G<  7 >(w, a, e, t, b, c, d);
	G<  8 >(w, a, d, e, t, b, c);
	G<  9 >(w, a, c, d, e, t, b);
	G< 10 >(w, a, b, c, d, e, t);
	G< 11 >(w, a, t, b, c, d, e);
	G< 12 >(w, a, e, t, b, c, d);
	G< 13 >(w, a, d, e, t, b, c);
	G< 14 >(w, a, c, d, e, t, b);
	G< 15 >(w, a, b, c, d, e, t);
	G< 16 >(w, a, t, b, c, d, e);
	G< 17 >(w, a, e, t, b, c, d);
	G< 18 >(w, a, d, e, t, b, c);
	G< 19 >(w, a, c, d, e, t, b);

	H<  0 >(w, a, b, c, d, e, t);
	H<  1 >(w, a, t, b, c, d, e);
	H<  2 >(w, a, e, t, b, c, d);
	H<  3 >(w, a, d, e, t, b, c);
	H<  4 >(w, a, c, d, e, t, b);
	H<  5 >(w, a, b, c, d, e, t);
	H<  6 >(w, a, t, b, c, d, e);
	H<  7 >(w, a, e, t, b, c, d);
	H<  8 >(w, a, d, e, t, b, c);
	H<  9 >(w, a, c, d, e, t, b);
	H< 10 >(w, a, b, c, d, e, t);
	H< 11 >(w, a, t, b, c, d, e);
	H< 12 >(w, a, e, t, b, c, d);
	H< 13 >(w, a, d, e, t, b, c);
	H< 14 >(w, a, c, d, e, t, b);
	H< 15 >(w, a, b, c, d, e, t);
	H< 16 >(w, a, t, b, c, d, e);
	H< 17 >(w, a, e, t, b, c, d);
	H< 18 >(w, a, d, e, t, b, c);
	H< 19 >(w, a, c, d, e, t, b);

	I<  0 >(w, a, b, c, d, e, t);
	I<  1 >(w, a, t, b, c, d, e);
	I<  2 >(w, a, e, t, b, c, d);
	I<  3 >(w, a, d, e, t, b, c);
	I<  4 >(w, a, c, d, e, t, b);
	I<  5 >(w, a, b, c, d, e, t);
	I<  6 >(w, a, t, b, c, d, e);
	I<  7 >(w, a, e, t, b, c, d);
	I<  8 >(w, a, d, e, t, b, c);
	I<  9 >(w, a, c, d, e, t, b);
	I< 10 >(w, a, b, c, d, e, t);
	I< 11 >(w, a, t, b, c, d, e);
	I< 12 >(w, a, e, t, b, c, d);
	I< 13 >(w, a, d, e, t, b, c);
	I< 14 >(w, a, c, d, e, t, b);
	I< 15 >(w, a, b, c, d, e, t);
	I< 16 >(w, a, t, b, c, d, e);
	I< 17 >(w, a, e, t, b, c, d);
	I< 18 >(w, a, d, e, t, b, c);
	I< 19 >(w, a, c, d, e, t, b);

	m_State.m_nState[0] += a;
	m_State.m_nState[1] += b;
	m_State.m_nState[2] += c;
	m_State.m_nState[3] += d;
	m_State.m_nState[4] += e;
}


void CSHA::Add(const void* pData, std::size_t nLength)
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
	{
		Transform(reinterpret_cast< const uint32* >(input));
	}
	// Buffer remaining input
	if (nLength)
		std::memcpy(m_State.m_oBuffer, input, nLength);
}

#endif // HASHLIB_USE_ASM

// ---------------------------------------------------------------------------
// Copyright (c) 2002, Dr Brian Gladman <brg@gladman.me.uk>, Worcester, UK.
// All rights reserved.

// LICENSE TERMS

// The free distribution and use of this software in both source and binary 
// form is allowed (with or without changes) provided that:

//   1. distributions of this source code include the above copyright 
//      notice, this list of conditions and the following disclaimer;

//   2. distributions in binary form include the above copyright
//      notice, this list of conditions and the following disclaimer
//      in the documentation and/or other associated materials;

//   3. the copyright holder's name is not used to endorse products 
//      built using this software without specific written permission. 

// ALTERNATIVELY, provided that this notice is retained in full, this product
// may be distributed under the terms of the GNU General Public License (GPL),
// in which case the provisions of the GPL apply INSTEAD OF those given above.
//
// DISCLAIMER

// This software is provided 'as is' with no explicit or implied warranties
// in respect of its properties, including, but not limited to, correctness 
// and/or fitness for purpose.
// ---------------------------------------------------------------------------
// Issue Date: 30/11/2002

// This is a byte oriented version of SHA1 that operates on arrays of bytes
// stored in memory. It runs at 22 cycles per byte on a Pentium P4 processor