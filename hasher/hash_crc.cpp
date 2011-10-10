// Copyright (C) 2006 epoximator
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.


#include "stdafx.h"
#include "hash_wrapper.h"
#include <stdio.h>
#include <ctype.h>

/* Table of CRCs of all 8-bit messages. */
unsigned long crc_table[256];

/* Flag: has the table been computed? Initially false. */
int crc_table_computed = 0;

/* Make the table for a fast CRC. */
void make_crc_table(void){
 unsigned long c;
 int n, k;

 for (n = 0; n < 256; n++) {
   c = (unsigned long) n;
   for (k = 0; k < 8; k++) {
     if (c & 1)
       c = 0xedb88320L ^ (c >> 1);
     else
       c = c >> 1;
   }
   crc_table[n] = c;
 }
 crc_table_computed = 1;
}

/* Update a running CRC with the bytes buf[0..len-1]--the CRC
  should be initialized to all 1's, and the transmitted value
  is the 1's complement of the final running CRC (see the
  crc() routine below)). */

unsigned long update_crc(unsigned long crc, unsigned char *buf, int len)
{
 unsigned long c = crc;
 int n;


 for (n = 0; n < len; n++) {
   c = crc_table[(c ^ buf[n]) & 0xff] ^ (c >> 8);
 }
 return c;
}

/* Return the CRC of the bytes buf[0..len-1]. */
unsigned long crc(unsigned char *buf, int len)
{
 return update_crc(0xffffffffL, buf, len) ^ 0xffffffffL;
}

DigestCRC::DigestCRC(){
	if (!crc_table_computed)
		make_crc_table();
	crc_value = 0xffffffffL;
}
void DigestCRC::clean(){

}
void DigestCRC::update(char* s, int len){
	crc_value = update_crc(crc_value, (unsigned char *)s, len);
}
int DigestCRC::digest(char* sum, int len){
	int val=crc_value^0xffffffffL;
	sum[0]=(val>>24);
	sum[1]=(val>>16)&0xFF;
	sum[2]=(val>>8)&0xFF;
	sum[3]=val&0xFF;
	return 4;
}
