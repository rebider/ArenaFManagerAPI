using System;
using System.Collections.Generic;
using System.Threading;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Backend.Infrastructure.Threading;
using WitFX.MT4.Server.Events;
using WitFX.MT4.Server.Services;

namespace WitFX.MT4.Server.Workers
{
    public sealed class MarketDataWorker : WorkerBase
    {
        private readonly List<SymbolInfo> _queue = new List<SymbolInfo>();
        private readonly IMarketDataEvents _marketDataEvents;
        private readonly MarketDataService _marketDataService;

        public MarketDataWorker(
            ILogger logger, IMarketDataEvents marketDataEvents,
            MarketDataService marketDataService)
            : base(logger)
        {
            _marketDataEvents = marketDataEvents;
            _marketDataService = marketDataService;
        }

        internal void Enqueue(IReadOnlyCollection<SymbolInfo> symbols)
        {
            lock (_queue)
            {
                _queue.AddRange(symbols);
                Monitor.Pulse(_queue);
            }
        }

        protected override void Execute(WorkerExecutionContext executionContext)
        {
            executionContext.ContinuationDelay = TimeSpan.Zero;
            SymbolInfo[] queue;

            lock (_queue)
            {
                if (_queue.Count == 0)
                    Monitor.Wait(_queue, Settings.ContinuationDelay);

                if (_queue.Count == 0)
                    return;

                queue = _queue.ToArray();
                _queue.Clear();
            }

            var quotes = _marketDataService.ToMarketData(queue);

            if (quotes.Count > 0)
                _marketDataEvents.OnUpdate(quotes);
        }
    }
}
