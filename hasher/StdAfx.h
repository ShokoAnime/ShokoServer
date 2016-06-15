//
// stdafx.h
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

// For /Wall
#pragma warning(disable:4668)
#pragma warning(disable:4820)
#pragma warning(disable:4548)

#define _WIN32_WINNT 0x0600

#define STRICT
#define WIN32_LEAN_AND_MEAN
#define BOOST_USE_WINDOWS_H

#include <windows.h>
#include <algorithm>

// define HASHLIB_USE_ASM for assembler use (several times faster)