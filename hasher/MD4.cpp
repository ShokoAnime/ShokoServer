//
// Free implementation of the MD4 hash algorithm
// MD4C.C - RSA Data Security, Inc., MD4 message-digest algorithm
//

/*
	Copyright (C) 1990-2, RSA Data Security, Inc. All rights reserved.

	License to copy and use this software is granted provided that it
	is identified as the "RSA Data Security, Inc. MD4 Message-Digest
	Algorithm" in all material mentioning or referencing this software
	or this function.

	License is also granted to make and use derivative works provided
	that such works are identified as "derived from the RSA Data
	Security, Inc. MD4 Message-Digest Algorithm" in all material
	mentioning or referencing the derived work.  

	RSA Data Security, Inc. makes no representations concerning either
	the merchantability of this software or the suitability of this
	software for any particular purpose. It is provided "as is"
	without express or implied warranty of any kind.  

	These notices must be retained in any copies of any part of this
	documentation and/or software.  
*/
#include "StdAfx.h"
#include "MD4.h"

#include <stddef.h>
#include <crtdbg.h>

#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif


///////////////////////////////////////////////////////////////////////////////
// Sanity checks for external assembler implemention
//
extern "C" DWORD MD4_asm_m_nCount0;
extern "C" DWORD MD4_asm_m_nCount1;
extern "C" DWORD MD4_asm_m_nState0;
extern "C" DWORD MD4_asm_m_nState1;
extern "C" DWORD MD4_asm_m_nState2;
extern "C" DWORD MD4_asm_m_nState3;
extern "C" DWORD MD4_asm_m_nBuffer;

bool CMD4::VerifyImplementation()
{
	if (MD4_asm_m_nCount0 != offsetof(CMD4, m_nCount[0]) ||
	    MD4_asm_m_nCount1 != offsetof(CMD4, m_nCount[1]) ){
		_ASSERT(0);
		return false;
	}

	if (MD4_asm_m_nState0 != offsetof(CMD4, m_nState[0]) ||
	    MD4_asm_m_nState1 != offsetof(CMD4, m_nState[1]) ||
	    MD4_asm_m_nState2 != offsetof(CMD4, m_nState[2]) ||
	    MD4_asm_m_nState3 != offsetof(CMD4, m_nState[3]) ){
		_ASSERT(0);
		return false;
	}

	if (MD4_asm_m_nBuffer != offsetof(CMD4, m_nBuffer)){
		_ASSERT(0);
		return false;
	}

	return true;
}


///////////////////////////////////////////////////////////////////////////////
// CMD4
//

CMD4::CMD4()
{
	Reset();
}

CMD4::~CMD4()
{
}

static unsigned char MD4_PADDING[64] = {
	0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
};

extern "C" void MD4_Add_p5(CMD4*, LPCVOID pData, DWORD nLength);

// MD4 initialization. Begins an MD4 operation, writing a new context

void CMD4::Reset()
{
	// Clear counts
	m_nCount[0] = m_nCount[1] = 0;
	// Load magic initialization constants
	m_nState[0] = 0x67452301;
	m_nState[1] = 0xefcdab89;
	m_nState[2] = 0x98badcfe;
	m_nState[3] = 0x10325476;
}

// Fetch hash
void CMD4::GetHash(MD4* pHash)
{
	memcpy(pHash->b, m_nState, 16);
}

// MD4 block update operation. Continues an MD4 message-digest
//     operation, processing another message block, and updating the
//     context
void CMD4::Add(LPCVOID pData, DWORD nLength)
{
	MD4_Add_p5(this, pData, nLength);
}

// MD4 finalization. Ends an MD4 message-digest operation, writing the
//     the message digest and zeroizing the context.

void CMD4::Finish()
{
	unsigned int bits[2], index = 0;
	// Save number of bits
	bits[1] = ( m_nCount[1] << 3 ) + ( m_nCount[0] >> 29);
	bits[0] = m_nCount[0] << 3;
	// Pad out to 56 mod 64.
	index = (unsigned int)(m_nCount[0] & 0x3f);
	MD4_Add_p5(this, MD4_PADDING, (index < 56) ? (56 - index) : (120 - index) );
	// Append length (before padding)
	MD4_Add_p5(this, bits, 8 );
}
