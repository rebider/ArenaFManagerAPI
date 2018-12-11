#include "Stdafx.h"
#include "ManagerWrapper.h"
#include "Converters.h"

#define BEGIN() EnsureNotDisposed(); Monitor::Enter(this); try {
#define END() } finally { Monitor::Exit(this); }

namespace WitFX
{
	namespace MT4
	{
		CManagerWrapper::CManagerWrapper(NATIVE CManagerInterface* manager, ExceptionHandler exceptionHandler)
		{
			if (!manager || exceptionHandler == nullptr)
				throw gcnew ArgumentNullException();

			_manager = manager;
			_exceptionHandler = exceptionHandler;

			_nativePumpExDelegate = gcnew NativePumpExDelegate(this, &CManagerWrapper::OnNativePumpEx);

			_nativePumpExCallback = static_cast<MTAPI_NOTIFY_FUNC_EX>(
				Marshal::GetFunctionPointerForDelegate(_nativePumpExDelegate).ToPointer());

			_nativePumpDelegate = gcnew NativePumpDelegate(this, &CManagerWrapper::OnNativePump);

			_nativePumpCallback = static_cast<MTAPI_NOTIFY_FUNC>(
				Marshal::GetFunctionPointerForDelegate(_nativePumpDelegate).ToPointer());
		}

		CManagerWrapper::~CManagerWrapper()
		{
			Free();
		}

		CManagerWrapper::!CManagerWrapper()
		{
			Free();
		}

		void CManagerWrapper::Free()
		{
			if (_manager)
			{
				_manager->Disconnect();
				_manager->Release();
				_manager = NULL;
			}

			_nativePumpExDelegate = nullptr;
			_nativePumpExCallback = NULL;
			_nativePumpDelegate = nullptr;
			_nativePumpCallback = NULL;
		}

		void CManagerWrapper::EnsureNotDisposed()
		{
			if (!_manager)
				throw gcnew ObjectDisposedException("CManagerFactoryWrapper");
		}

		int CManagerWrapper::TradesGetByLoginCount(int login, String^ group)
		{
			BEGIN();
			auto groupStr = marshal_as<std::string, System::String^>(group);
			int total = -1;
			auto result = _manager->TradesGetByLogin(login, groupStr.c_str(), &total);

			if (result)
				_manager->MemFree(result);

			return total;
			END();
		}

		Nullable<DateTimeOffset> CManagerWrapper::ServerTime()
		{
			BEGIN();
			auto mt4Time = _manager->ServerTime();
			return FromMT4Time(mt4Time);
			END();
		}

		ReturnCode CManagerWrapper::Connect(String^ server)
		{
			BEGIN();
			auto serverStr = marshal_as<std::string, System::String^>(server);
			return (ReturnCode)_manager->Connect(serverStr.c_str());
			END();
		}

		ReturnCode CManagerWrapper::Disconnect()
		{
			BEGIN();
			return (ReturnCode)_manager->Disconnect();
			END();
		}

		bool CManagerWrapper::IsConnected()
		{
			BEGIN();
			return _manager->IsConnected();
			END();
		}

		ReturnCode CManagerWrapper::Login(int login, String^ password)
		{
			BEGIN();
			auto passwordStr = marshal_as<std::string, System::String^>(password);
			return (ReturnCode)_manager->Login(login, passwordStr.c_str());
			END();
		}

		String^ CManagerWrapper::ErrorDescription(ReturnCode code)
		{
			BEGIN();
			auto native = _manager->ErrorDescription((int)code);
			return marshal_as<System::String^, LPCSTR>(native);
			END();
		}

		ReturnCode CManagerWrapper::SymbolsRefresh()
		{
			BEGIN();
			return (ReturnCode)_manager->SymbolsRefresh();
			END();
		}

		IReadOnlyList<ConSymbol^>^ CManagerWrapper::SymbolsGetAll()
		{
			BEGIN();
			int total = -1;
			auto native = _manager->SymbolsGetAll(&total);
			auto managed = CreateArrayOrEmpty<ConSymbol^>(total);

			if (native)
			{
				for (int i = 0; i < total; i++)
					managed[i] = ToManagedConSymbol(&native[i]);

				_manager->MemFree(native);
			}

			return (IReadOnlyList<ConSymbol^>^)managed;
			END();
		}

		ReturnCode CManagerWrapper::SymbolAdd(String^ symbol)
		{
			BEGIN();
			auto symbolStr = marshal_as<std::string, System::String^>(symbol);
			return (ReturnCode)_manager->SymbolAdd(symbolStr.c_str());
			END();
		}

		ReturnCode CManagerWrapper::PumpingSwitchEx(PumpExCallback^ pfnFunc, int flags)
		{
			BEGIN();

			if (pfnFunc == nullptr)
				throw gcnew ArgumentNullException();

			_pumpExCallback = pfnFunc;
			return (ReturnCode)_manager->PumpingSwitchEx(_nativePumpExCallback, flags, NULL);
			END();
		}

		void CManagerWrapper::OnNativePumpEx(int code, int type, void* data, void* param)
		{
			try
			{
				Object^ managedData = nullptr;

				if (code == PUMP_UPDATE_TRADES)
				{
					if (data != NULL)
						managedData = ToManagedTradeRecord((NATIVE TradeRecord*)data);
				}

				_pumpExCallback((PumpCode)code, (TransType)type, managedData);
			}
			catch (Exception^ exception)
			{
				_exceptionHandler(exception);
			}

			//return TRUE;
		}

		ReturnCode CManagerWrapper::PumpingSwitch(PumpCallback^ pfnFunc, int flags)
		{
			BEGIN();

			if (pfnFunc == nullptr)
				throw gcnew ArgumentNullException();

			_pumpCallback = pfnFunc;
			return (ReturnCode)_manager->PumpingSwitch(_nativePumpCallback, NULL, 0, flags);
			END();
		}

		void CManagerWrapper::OnNativePump(int code)
		{
			try
			{
				_pumpCallback((PumpCode)code);
			}
			catch (Exception^ exception)
			{
				_exceptionHandler(exception);
			}

			//return TRUE;
		}

		IReadOnlyList<UserRecord^>^ CManagerWrapper::UserRecordsRequest(IReadOnlyList<int>^ logins)
		{
			BEGIN();
			int count = logins->Count;

			if (!count)
				throw gcnew ArgumentException();

			std::vector<int> loginsVector(count);

			for (int i = 0; i < count; i++)
				loginsVector.push_back(logins[i]);

			auto native = _manager->UserRecordsRequest(&loginsVector[0], &count);
			auto managed = CreateArrayOrEmpty<UserRecord^>(count);

			if (native)
			{
				for (int i = 0; i < count; i++)
					managed[i] = ToManagedUserRecord(&native[i]);

				_manager->MemFree(native);
			}

			return (IReadOnlyList<UserRecord^>^)managed;
			END();
		}

		ReturnCode CManagerWrapper::UserRecordNew(UserRecord^ user)
		{
			BEGIN();
			NATIVE UserRecord nativeUser;
			ToNativeUserRecord(user, &nativeUser);
			int code = _manager->UserRecordNew(&nativeUser);

			if (code == RET_OK)
				user->login = nativeUser.login;

			return (ReturnCode)code;
			END();
		}

		IReadOnlyList<UserRecord^>^ CManagerWrapper::UsersGet()
		{
			BEGIN();
			int total = -1;
			auto native = _manager->UsersGet(&total);
			auto managed = CreateArrayOrEmpty<UserRecord^>(total);

			if (native)
			{
				for (int i = 0; i < total; i++)
					managed[i] = ToManagedUserRecord(&native[i]);

				_manager->MemFree(native);
			}

			return (IReadOnlyList<UserRecord^>^)managed;
			END();
		}

		ReturnCode CManagerWrapper::TradeTransaction(TradeTransInfo^ info)
		{
			BEGIN();
			NATIVE TradeTransInfo nativeInfo;
			ToNativeTradeTransInfo(info, &nativeInfo);
			int code = _manager->TradeTransaction(&nativeInfo);

			if (code == RET_OK)
				info->order = nativeInfo.order;

			return (ReturnCode)code;
			END();
		}

		ReturnCode CManagerWrapper::TradeRecordGet(int order, [Out] TradeRecord^ %trade)
		{
			BEGIN();
			NATIVE TradeRecord nativeTrade;
			memset(&nativeTrade, 0, sizeof(NATIVE TradeRecord));
			int code = _manager->TradeRecordGet(order, &nativeTrade);

			if (code == RET_OK)
				trade = ToManagedTradeRecord(&nativeTrade);
			else
				trade = nullptr;

			return (ReturnCode)code;
			END();
		}

		ReturnCode CManagerWrapper::SymbolInfoGet(String^ symbol, [Out] SymbolInfo^ %si)
		{
			BEGIN();
			auto symbolStr = marshal_as<std::string, System::String^>(symbol);
			NATIVE SymbolInfo nativeSi;
			memset(&nativeSi, 0, sizeof(NATIVE SymbolInfo));
			int code = _manager->SymbolInfoGet(symbolStr.c_str(), &nativeSi);

			if (code == RET_OK)
				si = ToManagedSymbolInfo(&nativeSi);
			else
				si = nullptr;

			return (ReturnCode)code;
			END();
		}

		IReadOnlyList<SymbolInfo^>^ CManagerWrapper::SymbolInfoUpdated(int max_info)
		{
			BEGIN();

			if (max_info <= 0)
				throw gcnew ArgumentOutOfRangeException();

			std::vector<NATIVE SymbolInfo> native;
			native.resize(max_info);
			int count = _manager->SymbolInfoUpdated(&native[0], max_info);
			auto managed = CreateArrayOrEmpty<SymbolInfo^>(count);

			for (int i = 0; i < count; i++)
				managed[i] = ToManagedSymbolInfo(&native[i]);

			return (IReadOnlyList<SymbolInfo^>^)managed;
			END();
		}

		ReturnCode CManagerWrapper::Ping()
		{
			BEGIN();
			return (ReturnCode)_manager->Ping();
			END();
		}

		ReturnCode CManagerWrapper::SymbolsGroupsGet(int maxCount, [Out] IReadOnlyList<ConSymbolGroup^>^ %grp)
		{
			BEGIN();

			if (maxCount <= 0)
				throw gcnew ArgumentOutOfRangeException();

			std::vector<NATIVE ConSymbolGroup> native;
			native.resize(maxCount);
			int code = _manager->SymbolsGroupsGet(&native[0]);

			if (code == RET_OK) {
				auto managed = gcnew array<ConSymbolGroup^>(maxCount);

				for (int i = 0; i < maxCount; i++)
					managed[i] = ToManagedConSymbolGroup(&native[0]);

				grp = (IReadOnlyList<ConSymbolGroup^>^)managed;
			}
			else
				grp = nullptr;

			return (ReturnCode)code;
			END();
		}

		ReturnCode CManagerWrapper::MarginLevelRequest(int login, [Out] MarginLevel^ %level)
		{
			BEGIN();
			NATIVE MarginLevel nativeLevel;
			memset(&nativeLevel, 0, sizeof(NATIVE MarginLevel));
			int code = _manager->MarginLevelRequest(login, &nativeLevel);

			if (code == RET_OK)
				level = ToManagedMarginLevel(&nativeLevel);
			else
				level = nullptr;

			return (ReturnCode)code;
			END();
		}

		IReadOnlyList<TradeRecord^>^ CManagerWrapper::TradesUserHistory(int login, Nullable<DateTimeOffset> from, Nullable<DateTimeOffset> to)
		{
			BEGIN();
			auto nativeFrom = ToMT4Time(from);
			auto nativeTo = ToMT4Time(to);
			int total = -1;
			auto native = _manager->TradesUserHistory(login, nativeFrom, nativeTo, &total);
			auto managed = CreateArrayOrEmpty<TradeRecord^>(total);

			if (native)
			{
				for (int i = 0; i < total; i++)
					managed[i] = ToManagedTradeRecord(&native[i]);

				_manager->MemFree(native);
			}

			return (IReadOnlyList<TradeRecord^>^)managed;
			END();
		}

		IReadOnlyList<TradeRecord^>^ CManagerWrapper::TradesGet()
		{
			BEGIN();
			int total = -1;
			auto native = _manager->TradesGet(&total);
			auto managed = CreateArrayOrEmpty<TradeRecord^>(total);

			if (native)
			{
				for (int i = 0; i < total; i++)
					managed[i] = ToManagedTradeRecord(&native[i]);

				_manager->MemFree(native);
			}

			return (IReadOnlyList<TradeRecord^>^)managed;
			END();
		}

		IReadOnlyList<MarginLevel^>^ CManagerWrapper::MarginsGet()
		{
			BEGIN();
			int total = -1;
			auto native = _manager->MarginsGet(&total);
			auto managed = CreateArrayOrEmpty<MarginLevel^>(total);

			if (native)
			{
				for (int i = 0; i < total; i++)
					managed[i] = ToManagedMarginLevel(&native[i]);

				_manager->MemFree(native);
			}

			return (IReadOnlyList<MarginLevel^>^)managed;
			END();
		}

		TradeRecord^ CManagerWrapper::TradeRecordRequest(int order)
		{
			BEGIN();
			int total = 1;
			auto native = _manager->TradeRecordsRequest(&order, &total);
			TradeRecord^ managed = nullptr;

			if (native)
			{
				managed = ToManagedTradeRecord(native);
				_manager->MemFree(native);
			}

			return managed;
			END();
		}
	}
}
