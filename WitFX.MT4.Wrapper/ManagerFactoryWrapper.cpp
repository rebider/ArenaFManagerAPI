#include "Stdafx.h"
#include "ManagerFactoryWrapper.h"
#include "ManagerWrapper.h"

namespace WitFX
{
	namespace MT4
	{
		CManagerFactoryWrapper::CManagerFactoryWrapper(String^ dllPath, ExceptionHandler exceptionHandler)
		{
			if (String::IsNullOrEmpty(dllPath))
				throw gcnew ArgumentNullException("dllPath");

			if (exceptionHandler == nullptr)
				throw gcnew ArgumentNullException("exceptionHandler");

			_exceptionHandler = exceptionHandler;
			auto dllPathStr = marshal_as<std::string, System::String^>(dllPath);
			_factory = new NATIVE CManagerFactory(dllPathStr.c_str());
		}

		CManagerFactoryWrapper::~CManagerFactoryWrapper()
		{
			Free();
		}

		CManagerFactoryWrapper::!CManagerFactoryWrapper()
		{
			Free();
		}

		void CManagerFactoryWrapper::Free()
		{
			if (_factory)
			{
				_factory->WinsockCleanup();
				delete _factory;
				_factory = NULL;
			}
		}

		void CManagerFactoryWrapper::EnsureNotDisposed()
		{
			if (!_factory)
				throw gcnew ObjectDisposedException("CManagerFactoryWrapper");
		}

		int CManagerFactoryWrapper::WinsockStartup()
		{
			EnsureNotDisposed();
			return _factory->WinsockStartup();
		}

		bool CManagerFactoryWrapper::IsValid()
		{
			EnsureNotDisposed();
			return _factory->IsValid();
		}

		CManagerInterface^ CManagerFactoryWrapper::Create()
		{
			EnsureNotDisposed();
			auto nativeManager = _factory->Create(ManAPIVersion);
			return nativeManager ? gcnew CManagerWrapper(nativeManager, _exceptionHandler) : nullptr;
		}
	}
}
