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

#include "MD5.h"


DigestMD5::DigestMD5(){
	addr = (void*)new CMD5();
}
void DigestMD5::clean(){
	CMD5* md5 = (CMD5*)addr;
	delete md5;
}
void DigestMD5::update(char* buf, int len){
	CMD5* md5 = (CMD5*)addr;
	md5->Add(buf, len);
}
int DigestMD5::digest(char* sum, int len){
	CMD5* md5= (CMD5*)addr;
	md5->Finish();
	md5->GetHash((uchar *)sum);
	return 16;
}
