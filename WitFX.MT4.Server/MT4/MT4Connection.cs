using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Extensions;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Backend.Infrastructure.Threading;
using WitFX.Contracts;
using WitFX.MT4.Server.Implementation;
using WitFX.MT4.Server.Services;

namespace WitFX.MT4.Server.MT4
{
    public class MT4Connection : IDisposable
    {
        private readonly eMT4ServerType _serverIndex;
        private readonly ILogger _baseLogger;
        private ServerLogger _logger;
        private readonly MT4Manager _mt4Manager;
        private readonly MasterUserSettingService _masterSettingsService;
        private CManagerInterface _api;
        protected TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);
        private string _server;
        private int _login;
        private string _password;
        private bool _isSymbolsAdded;
        private bool _isPumpEnabled;

        public MT4Connection(
            eMT4ServerType serverIndex, ILogger logger, MT4Manager mt4Manager,
            MasterUserSettingService masterSettingsService)
        {
            _serverIndex = serverIndex;
            _baseLogger = logger;
            _mt4Manager = mt4Manager;
            _masterSettingsService = masterSettingsService;
            //_api = _mt4Manager.CreateApi();
            //Debug.Assert(_api != null);

            //if (_api == null)
            //    throw new InvalidOperationException();
        }

        public void Dispose()
        {
            _api?.Dispose();
            _api = null;
        }

        public async Task ConfigureAsync(string owner, CancellationToken cancellationToken)
        {
            _logger = new ServerLogger(_baseLogger, owner);
            var settings = await _masterSettingsService.GetCachedMasterSettingsAsync(cancellationToken);
            Debug.Assert(settings != null);

            switch (_serverIndex)
            {
                case eMT4ServerType.SRV_TYPE_DEMO:
                    _server = settings._demoServer;
                    _login = settings._demoManagerLogin;
                    _password = settings._demoManagerPassword;
                    break;
                case eMT4ServerType.SRV_TYPE_LIVE:
                    _server = settings._liveServer;
                    _login = settings._liveManagerLogin;
                    _password = settings._liveManagerPassword;
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        public CManagerInterface Api
        {
            get
            {
                Debug.Assert(_api != null);

                if (_api == null)
                    throw new InvalidOperationException("Api is not created");

                return _api;
            }
        }

        protected virtual ReturnCode ConnectAndLogin()
        {
            _isPumpEnabled = false;
            var api = Api;

            if (api.IsConnected())
                api.Disconnect();

            Thread.Sleep(100);

            Debug.Assert(!_server.IsNullOrEmpty());
            _logger.LogInfo($"Try to connect to server {_server}");
            var code = api.Connect(_server);

            if (code == ReturnCode.RET_OK)
                _logger.LogInfo($"Successfully connected to server {_server}");
            else
            {
                LogError($"Unable to connect to server {_server}", code);
                return code;
            }

            Debug.Assert(_login > 0 && !_password.IsNullOrEmpty());
            _logger.LogInfo($"Try to login to server {_server} with id {_login}");
            code = api.Login(_login, _password);

            if (code == ReturnCode.RET_OK)
                _logger.LogInfo($"Successfully logged to server {_server} with id {_login}");
            else
            {
                LogError($"Unable to login to server {_server} with id {_login}", code);
                return code;
            }

            return code;
        }

        protected void LogError(string message, ReturnCode code)
        {
            _logger.LogError($"{message}. {ErrorMessage(code)}");
        }

        protected void LogWarning(string message, ReturnCode code)
        {
            _logger.LogWarning($"{message}. {ErrorMessage(code)}");
        }

        private string ErrorMessage(ReturnCode code)
            => $"Error code #{(int)code} {code}. Error description: {_api?.ErrorDescription(code)}";

        protected virtual ReturnCode Ping()
        {
            var api = Api;
            var code = api.Ping();

            if (code != ReturnCode.RET_OK)
                _logger.LogError($"Ping is not OK. {api.ErrorDescription(code)}");

            return code;
        }

        public ReturnCode CheckConnection(WorkerExecutionContext executionContext, bool pump = false)
        {
            // Check connection

            bool shouldConnect;

            if (_api == null)
            {
                _api = _mt4Manager.CreateApi();
                shouldConnect = true;
            }
            else
                shouldConnect = Ping() != ReturnCode.RET_OK;

            // Connect

            if (shouldConnect)
            {
                var code = ConnectAndLogin();

                if (code != ReturnCode.RET_OK)
                {
                    executionContext.ContinuationDelay = ReconnectDelay;
                    return code;
                }
            }

            if (pump && !_isPumpEnabled)
            {
                var code = RefreshSymbols(executionContext.CancellationToken);

                if (code != ReturnCode.RET_OK)
                {
                    LogError("Unable to enable pump because unable refresh symbols", code);
                    return code;
                }

                code = Api.PumpingSwitchEx(OnPumpEx, 0);

                if (code != ReturnCode.RET_OK)
                {
                    LogError("Unable to enable pump because 'PumpingSwitchEx' method fails", code);
                    return code;
                }

                _isPumpEnabled = true;
            }

            return ReturnCode.RET_OK;
        }

        private void OnPumpEx(PumpCode code, TransType type, object data)
        {
        }

        public ReturnCode RefreshSymbols(CancellationToken cancellationToken)
        {
            var api = Api;
            var code = api.SymbolsRefresh();

            if (code != ReturnCode.RET_OK)
                LogWarning("Unable to refresh symbol", code);

            if (_isSymbolsAdded)
                return ReturnCode.RET_OK;

            var symbols = api.SymbolsGetAll();
            _logger.LogInfo($"Got {symbols.Count} symbols");

            if (symbols.Count > 0)
                foreach (var symbolInfo in symbols)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    code = api.SymbolAdd(symbolInfo.symbol);

                    if (code != ReturnCode.RET_OK)
                        _logger.LogWarning($"Unable to add symbol {symbolInfo.symbol}", code);
                }
            else
                _logger.LogWarning("There is no symbols");

            _isSymbolsAdded = true;
            return ReturnCode.RET_OK;
        }
    }
}
