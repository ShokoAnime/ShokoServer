/*
 * Copyright (C) 2001-2005 Jacek Sieka, arnetheduck on gmail point com
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.
 */

#if !defined(CONFIG_H)
#define CONFIG_H

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#ifdef HAVE_CONFIG_H
#include "autoconf.h"
#endif

// Changing this number will change the maximum number of simultaneous users
// we can handle (when using select)...
#define FD_SETSIZE 4096

// Remove this line if hashes are not available in your stl
#define HAVE_HASH 1

// This enables stlport's debug mode (and slows it down to a crawl...)
//# define _STLP_DEBUG 1

// --- Shouldn't have to change anything under here...

#ifndef _REENTRANT
# define _REENTRANT 1
#endif

#ifdef HAVE_STLPORT
# define _STLP_DONT_USE_SHORT_STRING_OPTIM 1	// Lots of memory issues with this undefined...wonder what's up with that..
# define _STLP_USE_PTR_SPECIALIZATIONS 1
# define _STLP_USE_TEMPLATE_EXPRESSION 1
# define _STLP_NO_ANACHRONISMS 1
# define _STLP_NO_CUSTOM_IO 1
# define _STLP_NO_IOSTREAMS 1
# ifndef _DEBUG
#  define _STLP_DONT_USE_EXCEPTIONS 1
# endif
#endif

#ifdef _MSC_VER
# pragma warning(disable: 4711) // function 'xxx' selected for automatic inline expansion
# pragma warning(disable: 4786) // identifier was truncated to '255' characters in the debug information
# pragma warning(disable: 4290) // C++ Exception Specification ignored
# pragma warning(disable: 4127) // constant expression
# pragma warning(disable: 4710) // function not inlined
# pragma warning(disable: 4503) // decorated name length exceeded, name was truncated
//# if _MSC_VER == 1200 || _MSC_VER == 1300 || _MSC_VER == 1310

typedef signed char int8_t;
typedef signed short int16_t;
typedef signed long int32_t;
typedef signed __int64 int64_t;

typedef unsigned char u_int8_t;
typedef unsigned short u_int16_t;
typedef unsigned long u_int32_t;
typedef unsigned __int64 u_int64_t;

//# endif

#endif

#if defined(_MSC_VER)
#define _LL(x) x##ll
#define _ULL(x) x##ull
#define I64_FMT "%I64d"
#elif defined(SIZEOF_LONG) && SIZEOF_LONG == 8
#define _LL(x) x##l
#define _ULL(x) x##ul
#define I64_FMT "%ld"
#else
#define _LL(x) x##ll
#define _ULL(x) x##ull
#define I64_FMT "%lld"
#endif

#ifdef _WIN32

# define PATH_SEPARATOR '\\'
# define PATH_SEPARATOR_STR "\\"

#else

# define PATH_SEPARATOR '/'
# define PATH_SEPARATOR_STR "/"

#endif

#ifdef _MSC_VER

# ifndef CDECL
#  define CDECL _cdecl
# endif

#else // _MSC_VER

# ifndef CDECL
#  define CDECL
# endif

#endif // _MSC_VER

#define BZ_NO_STDIO



#endif // !defined(CONFIG_H)

/**
 * @file
 * $Id: config.h,v 1.35 2005/12/03 20:36:50 arnetheduck Exp $
 */
