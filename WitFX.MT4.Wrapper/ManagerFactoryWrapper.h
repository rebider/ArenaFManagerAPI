#pragma once

#include "Stdafx.h"

namespace WitFX
{
	namespace MT4
	{
		public ref class CManagerFactoryWrapper : MANAGED CManagerFactory
		{
		private:
			NATIVE CManagerFactory* _factory;
			void Free();
			ExceptionHandler _exceptionHandler;
			void EnsureNotDisposed();
		public:
			CManagerFactoryWrapper(String^ dllPath, ExceptionHandler exceptionHandler);
			~CManagerFactoryWrapper();
			!CManagerFactoryWrapper();
			virtual int WinsockStartup();
			virtual bool IsValid();
			virtual CManagerInterface^ Create();
		};
	}
}
