// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently,
// but are changed infrequently

#pragma once

#define WINVER         0x0501
#define _WIN32_IE      0x0600

#include <Windows.h>
#pragma comment(lib,"ws2_32.lib")
#include "MT4ManagerAPI.h"
#include <msclr\marshal.h>
#include <msclr\marshal_windows.h>
#include <msclr\marshal_cppstd.h>

using namespace msclr::interop;
using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;
using namespace System::Threading;

#define MANAGED WitFX::MT4::
#define NATIVE ::
#define ExceptionHandler Action<Exception^>^
