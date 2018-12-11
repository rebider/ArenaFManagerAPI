#pragma once

#include "Stdafx.h"

namespace WitFX
{
	namespace MT4
	{
		public ref class CManagerHostWrapper : IManagerHost
		{
		private:
			String^ _dllPath;
			CManagerHostWrapper(String^ _dllPath);
		public:
			virtual CManagerFactory^ CreateFactory(ExceptionHandler exceptionHandler);
			static void Initialize(String^ dllPath);
		};
	}
}
