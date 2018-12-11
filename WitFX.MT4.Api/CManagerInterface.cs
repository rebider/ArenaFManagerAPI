using System;
using System.Collections.Generic;

namespace WitFX.MT4
{
    public interface CManagerInterface : IDisposable
    {
        int TradesGetByLoginCount(int login, string group);
        DateTimeOffset? ServerTime();
        ReturnCode Connect(string server);
        ReturnCode Disconnect();
        bool IsConnected();
        ReturnCode Login(int login, string password);
        string ErrorDescription(ReturnCode code);
        ReturnCode SymbolsRefresh();
        IReadOnlyList<ConSymbol> SymbolsGetAll();
        ReturnCode SymbolAdd(string symbol);
        ReturnCode PumpingSwitchEx(PumpExCallback pfnFunc, int flags);
        ReturnCode PumpingSwitch(PumpCallback pfnFunc, int flags);
        IReadOnlyList<UserRecord> UserRecordsRequest(IReadOnlyList<int> logins);
        ReturnCode UserRecordNew(UserRecord user);
        IReadOnlyList<UserRecord> UsersGet();
        ReturnCode TradeTransaction(TradeTransInfo info);
        ReturnCode TradeRecordGet(int order, out TradeRecord trade);
        ReturnCode SymbolInfoGet(string symbol, out SymbolInfo si);
        IReadOnlyList<SymbolInfo> SymbolInfoUpdated(int max_info);
        ReturnCode Ping();
        ReturnCode SymbolsGroupsGet(int maxCount, out IReadOnlyList<ConSymbolGroup> grp);
        ReturnCode MarginLevelRequest(int login, out MarginLevel level);
        IReadOnlyList<TradeRecord> TradesUserHistory(int login, DateTimeOffset? from, DateTimeOffset? to);
        IReadOnlyList<TradeRecord> TradesGet();
        IReadOnlyList<MarginLevel> MarginsGet();
        TradeRecord TradeRecordRequest(int order);
    }
}
