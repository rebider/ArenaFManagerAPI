using System.Collections.Generic;
using WitFX.Backend.Infrastructure;
using WitFX.Contracts;

namespace WitFX.MT4.Server.Events
{
    [Injectable]
    public interface IMarketDataEvents
    {
        void OnUpdate(IReadOnlyList<MarketData> quotes);
    }
}
