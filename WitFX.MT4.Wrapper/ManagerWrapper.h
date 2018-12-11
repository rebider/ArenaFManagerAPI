#pragma once

#include "Stdafx.h"

namespace WitFX
{
	namespace MT4
	{
		private delegate void NativePumpExDelegate(int code, int type, void* data, void* param);
		private delegate void NativePumpDelegate(int code);

		public ref class CManagerWrapper : MANAGED CManagerInterface
		{
		private:
			NATIVE CManagerInterface* _manager;
			ExceptionHandler _exceptionHandler;
			PumpExCallback^ _pumpExCallback;
			NativePumpExDelegate^ _nativePumpExDelegate;
			MTAPI_NOTIFY_FUNC_EX _nativePumpExCallback;
			PumpCallback^ _pumpCallback;
			NativePumpDelegate^ _nativePumpDelegate;
			MTAPI_NOTIFY_FUNC _nativePumpCallback;
			void Free();
			void EnsureNotDisposed();
		public:
			CManagerWrapper(NATIVE  CManagerInterface* manager, ExceptionHandler exceptionHandler);
			~CManagerWrapper();
			!CManagerWrapper();
			virtual int TradesGetByLoginCount(int login, String^ group);
			virtual Nullable<DateTimeOffset> ServerTime();
			virtual ReturnCode Connect(String^ server);
			virtual ReturnCode Disconnect();
			virtual bool IsConnected();
			virtual ReturnCode Login(int login, String^ password);
			virtual String^ ErrorDescription(ReturnCode code);
			virtual ReturnCode SymbolsRefresh();
			virtual IReadOnlyList<ConSymbol^>^ SymbolsGetAll();
			virtual ReturnCode SymbolAdd(String^ symbol);
			virtual ReturnCode PumpingSwitchEx(PumpExCallback^ pfnFunc, int flags);
			void OnNativePumpEx(int code, int type, void* data, void* param);
			virtual ReturnCode PumpingSwitch(PumpCallback^ pfnFunc, int flags);
			void OnNativePump(int code);
			virtual IReadOnlyList<UserRecord^>^ UserRecordsRequest(IReadOnlyList<int>^ logins);
			virtual ReturnCode UserRecordNew(UserRecord^ user);
			virtual IReadOnlyList<UserRecord^>^ UsersGet();
			virtual ReturnCode TradeTransaction(TradeTransInfo^ info);
			virtual ReturnCode TradeRecordGet(int order, [Out] TradeRecord^ %trade);
			virtual ReturnCode SymbolInfoGet(String^ symbol, [Out] SymbolInfo^ %si);
			virtual IReadOnlyList<SymbolInfo^>^ SymbolInfoUpdated(int max_info);
			virtual ReturnCode Ping();
			virtual ReturnCode SymbolsGroupsGet(int maxCount, [Out] IReadOnlyList<ConSymbolGroup^>^ %grp);
			virtual ReturnCode MarginLevelRequest(int login, [Out] MarginLevel^ %level);
			virtual IReadOnlyList<TradeRecord^>^ TradesUserHistory(int login, Nullable<DateTimeOffset> from, Nullable<DateTimeOffset> to);
			virtual IReadOnlyList<TradeRecord^>^ TradesGet();
			virtual IReadOnlyList<MarginLevel^>^ MarginsGet();
			virtual TradeRecord^ TradeRecordRequest(int order);
		};
	}
}
