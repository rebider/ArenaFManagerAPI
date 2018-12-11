using System;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Backend.Infrastructure.Threading;
using WitFX.Contracts;
using WitFX.MT4.Server.Implementation;
using WitFX.MT4.Server.MT4;

namespace WitFX.MT4.Server.Workers
{
    public sealed class MT4UpdateWorker : WorkerBase
    {
        private readonly eMT4ServerType _serverIndex;
        private readonly string _name;
        private readonly MT4Connection _connection;
        private readonly ServerLogger m_ptrLogger;
        private readonly IMT4ServerConnectorHost _host;

        public MT4UpdateWorker(
            eMT4ServerType serverIndex, ILogger logger, Func<eMT4ServerType, MT4Connection> connectionFactory,
            IMT4ServerConnectorHost host)
            : base(logger)
        {
            _serverIndex = serverIndex;
            _connection = connectionFactory(serverIndex);
            _name = (serverIndex == eMT4ServerType.SRV_TYPE_DEMO ? "Demo" : "Live") + "." + GetType().Name;
            m_ptrLogger = new ServerLogger(logger, _name);
            _host = host;
        }

        public override string Name => _name;

        public async Task ConfigureAsync(CancellationToken cancellationToken)
        {
            await _connection.ConfigureAsync(Name + ".Connection", cancellationToken);
        }

        //private bool _traceUpdateDataThread = false;

        protected override void Execute(WorkerExecutionContext executionContext)
        {
            if (_connection.CheckConnection(executionContext, pump: true) != ReturnCode.RET_OK)
                return;

            var cancellationToken = executionContext.CancellationToken;
            //if (_traceUpdateDataThread) m_ptrLogger.LogOk("Begin TradesGet");
            var openTrades = _connection.Api.TradesGet();
            //if (_traceUpdateDataThread) m_ptrLogger.LogOk($"End TradesGet: Count = {openTrades.Count}");

            foreach (var openTrade in openTrades)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _host.OnTrade(_serverIndex, openTrade, (TransType)(-1));
            }

            //if (_traceUpdateDataThread) m_ptrLogger.LogOk("Begin MarginsGet");
            var marginLevels = _connection.Api.MarginsGet();
            //if (_traceUpdateDataThread) m_ptrLogger.LogOk($"End MarginsGet: Count = {marginLevels.Count}");

            foreach (var marginLevel in marginLevels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _host.OnMargin(_serverIndex, marginLevel);
            }
        }
    }
}
