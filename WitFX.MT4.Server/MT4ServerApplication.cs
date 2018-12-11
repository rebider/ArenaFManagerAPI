using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Threading;
using WitFX.MT4.Server.MT4;
using WitFX.MT4.Server.Services;
using WitFX.MT4.Server.Workers;

namespace WitFX.MT4.Server
{
    public class MT4ServerApplication
    {
        private readonly WorkerManager _workerManager;
        private readonly MT4Manager _mt4Manager;
        private readonly MT4SymbolInfoService _symbolService;

        public MT4ServerApplication(
            WorkerManager workerManager, MT4Manager mt4Manager, MT4SymbolInfoService symbolService)
        {
            _workerManager = workerManager;
            _mt4Manager = mt4Manager;
            _symbolService = symbolService;
        }

        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            await _symbolService.LoadCacheAsync(cancellationToken);
            await _mt4Manager.StartAsync(cancellationToken);
            _workerManager.StartWorker<MarketDataWorker>();
        }

        public virtual void Stop()
        {
            _workerManager?.StopWorkers(Timeout.InfiniteTimeSpan);
            _mt4Manager?.Stop();
        }
    }
}
