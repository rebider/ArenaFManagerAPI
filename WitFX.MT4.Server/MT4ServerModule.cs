using WitFX.Backend.Infrastructure;
using WitFX.Backend.Infrastructure.Maintenance;
using WitFX.Backend.Infrastructure.Threading;
using WitFX.MT4.Server.Managers;
using WitFX.MT4.Server.MT4;
using WitFX.MT4.Server.Services;
using WitFX.MT4.Server.Workers;

namespace WitFX.MT4.Server
{
    public sealed class MT4ServerModule : ModuleBase
    {
        public override void Configure()
        {
            RegisterModule<DatabaseModule>();
            RegisterModule<WorkerModule>();
            RegisterModule<MaintenanceModule>();
            RegisterSingleton<FollowerService>();
            RegisterSingleton<LogService>();
            RegisterSingleton<MasterUserService>();
            RegisterSingleton<MasterUserSettingService>();
            RegisterSingleton<MT4AccountService>();
            RegisterSingleton<MT4SymbolInfoService>();
            RegisterSingleton<ProductService>();
            RegisterSingleton<RebateService>();
            RegisterSingleton<SignalService>();
            RegisterSingleton<SignalSymbolSettingService>();
            RegisterSingleton<TradeDisableService>();
            RegisterSingleton<TradeRecordService>();
            RegisterSingleton<TransactionService>();
            RegisterSingleton<OrderService>();
            RegisterSingleton<AccountManager>();
            RegisterSingleton<FollowingManager>();
            RegisterSingleton<MT4Manager>();
            RegisterSingleton<OrderManager>();
            RegisterSingleton<MarketDataService>();
            RegisterSingleton<MarketDataWorker>();
            RegisterSingleton<MT4ServerApplication>();
            RegisterDependency<MT4Connection>();
            RegisterDependency<MT4UpdateWorker>();
            RegisterSingleton<RuntimeTradeRecordService>();
            RegisterSingleton<MT4AccountStatusService>();
        }
    }
}
