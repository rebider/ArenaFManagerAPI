using System;
using System.Collections.Generic;
using WitFX.Backend.Infrastructure;
using WitFX.Contracts;

namespace WitFX.MT4.Server.Implementation
{
    [Injectable]
    public interface IMT4ServerConnectorHost
    {
        void OnResponse(
            ReturnCode errorcode, string errormessage, eMT4ServerType serverIndex, MT4REQ reqType,
            eOrderStatus trans_status, int masterLogin, /*int orderOrLogin,*/ /*uint socketID,*/
            Guid server_trans_id, eAccountType accType, MT4REQMODE reqMode, MT4Request request);

        void OnMarketData(eMT4ServerType serverIndex, IReadOnlyList<SymbolInfo> ptrArr);

        void OnSymbolInfo(
            eMT4ServerType serverIndex, IReadOnlyList<ConSymbolGroup> ptrSecurityArr,
            IReadOnlyList<ConSymbol> ptrSymbolArr);

        void OnTrade(eMT4ServerType serverIndex, TradeRecord ptrTrade, TransType transType);
        void OnMargin(eMT4ServerType serverIndex, MarginLevel ptrLevel);
        IReadOnlyList<int> GetAllMT4Logins(eMT4ServerType serverIndex);
    }
}
