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

#pragma once

class Digest{
protected:
void* addr;
public:
virtual void update(char* buf, int len)=0;
virtual int digest(char* sum, int len)=0;
virtual void clean()=0;
};

class DigestCRC : public Digest{
private:
unsigned long crc_value;
public:
DigestCRC();
void update(char* buf, int len);
int digest(char* sum, int len);
void clean();
};


class DigestSHA : public Digest{
public:
DigestSHA();
void update(char* buf, int len);
int digest(char* sum, int len);
void clean();
};

class DigestMD5 : public Digest{
public:
DigestMD5();
void update(char* buf, int len);
int digest(char* sum, int len);
void clean();
};
