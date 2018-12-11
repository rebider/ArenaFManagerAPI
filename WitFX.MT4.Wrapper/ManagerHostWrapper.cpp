#include "stdafx.h"
#include "ManagerHostWrapper.h"
#include "ManagerFactoryWrapper.h"

namespace WitFX
{
	namespace MT4
	{
		CManagerHostWrapper::CManagerHostWrapper(String^ dllPath)
		{
			if (String::IsNullOrEmpty(dllPath))
				throw gcnew ArgumentException();

			_dllPath = dllPath;
		}

		CManagerFactory^ CManagerHostWrapper::CreateFactory(ExceptionHandler exceptionHandler)
		{
			return gcnew CManagerFactoryWrapper(_dllPath, exceptionHandler);
		}

		void CManagerHostWrapper::Initialize(String^ dllPath)
		{
			ManagerHost::Current = gcnew CManagerHostWrapper(dllPath);
		}
	}
}
