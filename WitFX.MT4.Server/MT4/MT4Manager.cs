using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Extensions;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Backend.Infrastructure.Threading;
using WitFX.Contracts;
using WitFX.MT4.Server.cls;
using WitFX.MT4.Server.Events;
using WitFX.MT4.Server.Implementation;
using WitFX.MT4.Server.Implementation.Helpers;
using WitFX.MT4.Server.Managers;
using WitFX.MT4.Server.Services;
using WitFX.MT4.Server.Workers;

namespace WitFX.MT4.Server.MT4
{
    public sealed class MT4Manager : IMT4ServerConnectorHost, IDisposable
    {
        private WitFXServerConnector _demo;
        private WitFXServerConnector _live;
        private readonly ILogger _baseLogger;
        private readonly ServerLogger m_ptrLogger;
        private readonly Lazy<OrderManager> _orderManager;
        private readonly MT4AccountService _mt4AccountService;
        private readonly SignalService _signalService;
        private readonly MasterUserSettingService _masterSettingsService;
        private readonly IMessageEvents _connectionMgr;
        private readonly MT4SymbolInfoService _mt4SymbolInfoService;
        private readonly MarketDataService _marketDataService;
        private readonly MarketDataWorker _marketDataWorker;
        private readonly OrderService _orderService;
        private CManagerFactory _factory;
        private readonly Func<eMT4ServerType, MT4UpdateWorker> _updateWorkerFactory;
        private readonly WorkerManager _workerManager;

        public MT4Manager(
            ILogger logger, Lazy<OrderManager> orderManager, MT4AccountService mt4AccountService,
            SignalService signalService, MasterUserSettingService masterSettingsService,
            IMessageEvents connectionMgr, MT4SymbolInfoService mt4SymbolInfoService,
            MarketDataService marketDataService, MarketDataWorker marketDataWorker,
            OrderService orderService, Func<eMT4ServerType, MT4UpdateWorker> updateWorkerFactory,
            WorkerManager workerManager)
        {
            m_ptrLogger = new ServerLogger(_baseLogger = logger, nameof(MT4Manager));
            _orderManager = orderManager;
            _mt4AccountService = mt4AccountService;
            _signalService = signalService;
            _masterSettingsService = masterSettingsService;
            _connectionMgr = connectionMgr;
            _mt4SymbolInfoService = mt4SymbolInfoService;
            _marketDataService = marketDataService;
            _marketDataWorker = marketDataWorker;
            _orderService = orderService;
            _updateWorkerFactory = updateWorkerFactory;
            _workerManager = workerManager;
        }

        public WitFXServerConnector Demo
        {
            get
            {
                Debug.Assert(_demo != null);

                if (_demo == null)
                    throw new InvalidOperationException();

                return _demo;
            }
        }

        public WitFXServerConnector Live
        {
            get
            {
                Debug.Assert(_live != null);

                if (_live == null)
                    throw new InvalidOperationException();

                return _live;
            }
        }

        public WitFXServerConnector GetConnector(eMT4ServerType serverIndex)
        {
            switch (serverIndex)
            {
                case eMT4ServerType.SRV_TYPE_DEMO:
                    return Demo;
                case eMT4ServerType.SRV_TYPE_LIVE:
                    return Live;
                default:
                    throw new ArgumentOutOfRangeException(nameof(serverIndex), serverIndex, null);
            }
        }

        private void HandleException(Exception exception)
            => _baseLogger.LogException(exception);

        private volatile bool _isStarting;

        public async Task StartAsync(CancellationToken cancellationToken, bool startThreads = true)
        {
            Debug.Assert(!_isStarting);

            if (_isStarting)
                throw new InvalidOperationException();

            _isStarting = true;
            _factory = ManagerHost.CreateFactory(HandleException);
            CheckFactory();
            _factory.WinsockStartup();
            var m_masterUserSetting = await _masterSettingsService.GetCachedMasterSettingsAsync(cancellationToken);

            var mt4Accounts = await _mt4AccountService.GetAllMT4Accounts(cancellationToken);

            lock (_mt4LoginsByServerIndex)
                foreach (var mt4Account in mt4Accounts)
                    AddMt4LoginNoLock(mt4Account._mt4ServerIndex, mt4Account._mt4Login);

            m_ptrLogger.LogInfo("Initializng DEMO MT4 Manager");
            Debug.Assert(_demo == null);
            _demo = new WitFXServerConnector(
                eMT4ServerType.SRV_TYPE_DEMO, this, _factory,
                m_masterUserSetting._demoServer, m_masterUserSetting._demoManagerLogin,
                m_masterUserSetting._demoManagerPassword, _baseLogger, true, true,
                startThreads: startThreads);

            CppHelper.memcpy(ref _demo.m_masterUserSetting, m_masterUserSetting);

            //_demo.setMT4SocialUsers(m_mapMT4MasterLogin);
            //_demo.setMT4ResponseFunPtr(onMT4Response);
            //_demo.setMarketDataFunPtr(onMarketData);
            //_demo.setSymbolInfoFunPtr(onSymbolInfo);
            //_demo.setOnTradeFunPtr(onTradeResponse);
            //_demo.setOnMarginFunPtr(onMarginLevelResponse);

            m_ptrLogger.LogInfo("Initializng LIVE MT4 Manager");
            Debug.Assert(_live == null);
            _live = new WitFXServerConnector(
                eMT4ServerType.SRV_TYPE_LIVE, this, _factory,
                m_masterUserSetting._liveServer, m_masterUserSetting._liveManagerLogin,
                m_masterUserSetting._liveManagerPassword, _baseLogger, true, true,
                startThreads: startThreads);

            CppHelper.memcpy(ref _live.m_masterUserSetting, m_masterUserSetting);

            //_live.setMT4SocialUsers(m_mapMT4MasterLogin);
            //_live.setMT4ResponseFunPtr(onMT4Response);
            //_live.setOnTradeFunPtr(onTradeResponse);
            //_live.setOnMarginFunPtr(onMarginLevelResponse);

            if (startThreads)
            {
                if (_demo.IsAPIValid())
                {
                    m_ptrLogger.LogInfo("Starting DEMO MT4 Manager connection...");
                    _demo.startNormalManager();
                }
                else
                {
                    m_ptrLogger.LogError("MT4 MANAGER API IS NOT VALID");
                }
                if (_live.IsAPIValid())
                {
                    m_ptrLogger.LogInfo("Starting LIVE MT4 Manager connection...");
                    _live.startNormalManager();
                }
                else
                {
                    m_ptrLogger.LogError("MT4 MANAGER API IS NOT VALID");
                }

                var demoUpdateWorker = _updateWorkerFactory(eMT4ServerType.SRV_TYPE_DEMO);
                await demoUpdateWorker.ConfigureAsync(cancellationToken);
                _workerManager.StartWorker(demoUpdateWorker);

                var liveUpdateWorker = _updateWorkerFactory(eMT4ServerType.SRV_TYPE_LIVE);
                await liveUpdateWorker.ConfigureAsync(cancellationToken);
                _workerManager.StartWorker(liveUpdateWorker);
            }
        }

        private void CheckFactory()
        {
            Debug.Assert(_factory != null && _factory.IsValid());

            if (_factory == null)
                throw new InvalidOperationException("MT4 API factory is not created");

            if (!_factory.IsValid())
                throw new InvalidOperationException("MT4 API factory is not valid");
        }

        public CManagerInterface CreateApi()
        {
            CheckFactory();
            var api = _factory.Create();
            Debug.Assert(api != null);

            if (api == null)
                throw new InvalidOperationException("Could not create MT4 API instance");

            return api;
        }

        public void Stop()
        {
            _demo?.Stop();
            _live?.Stop();
        }

        public void Dispose()
        {
            _demo?.Dispose();
            _demo = null;
            _live?.Dispose();
            _live = null;
            _factory?.Dispose();
            _factory = null;
        }

        #region Events

        void IMT4ServerConnectorHost.OnMargin(eMT4ServerType serverIndex, MarginLevel ptrLevel)
        {
            try
            {
                OnMarginAsync(serverIndex, ptrLevel, CancellationToken.None).Wait();
            }
            catch (Exception exception)
            {
                m_ptrLogger.LogException(exception);
            }
        }

        /// <summary>
        /// WitFXMT4ServerBL.onMarginLevelResponse
        /// </summary>
        private async Task OnMarginAsync(eMT4ServerType serverIndex, MarginLevel ptrMargin, CancellationToken cancellationToken)
        {
            int masterLogin = await _mt4AccountService.GetCachedMasterLoginAsync(serverIndex, ptrMargin.login, cancellationToken);
            if (masterLogin > 0)
            {
                var ptr = new MT4MarginResponse(); // MT_MT4MarginResponse_ID
                ptr._marginInfo = new MT4AccountStatus();
                ptr._marginInfo._balance = ptrMargin.balance;
                ptr._marginInfo._equity = ptrMargin.equity;
                ptr._marginInfo._freeMargin = ptrMargin.margin_free;
                ptr._marginInfo._margin = ptrMargin.margin;
                ptr._marginInfo._marginLevel = ptrMargin.margin_level;
                ptr._marginInfo._mt4ServerIndex = serverIndex;
                ptr._marginInfo._mt4Login = ptrMargin.login;
                _connectionMgr.SentDataUsingLoginID(ptr, MessageTypeID.MT_MT4MarginResponse_ID, masterLogin);
            }
        }

        /// <summary>
        /// WitFXMT4ServerBL.onMarketData
        /// </summary>
        void IMT4ServerConnectorHost.OnMarketData(eMT4ServerType serverIndex, IReadOnlyList<SymbolInfo> symbols)
        {
            if (serverIndex == eMT4ServerType.SRV_TYPE_DEMO)
                try
                {
                    _marketDataService.Put(symbols);
                    _marketDataWorker.Enqueue(symbols);
                }
                catch (Exception exception)
                {
                    m_ptrLogger.LogException(exception);
                }
        }

        void IMT4ServerConnectorHost.OnResponse(
            ReturnCode errorcode, string errormessage, eMT4ServerType serverIndex, MT4REQ reqType,
            eOrderStatus trans_status, int masterLogin, /*int orderOrLogin,*/ Guid server_trans_id,
            eAccountType accType, MT4REQMODE reqMode, MT4Request request)
        {
            try
            {
                OnResponseAsync(
                    errorcode, errormessage, serverIndex, reqType,
                    trans_status, masterLogin, /*orderOrLogin,*/ server_trans_id,
                    accType, reqMode, request, CancellationToken.None).Wait();
            }
            catch (Exception exception)
            {
                m_ptrLogger.LogException(exception);
            }
        }

        /// <summary>
        /// WitFXMT4ServerBL.onMT4Response
        /// </summary>
        private async Task OnResponseAsync(
            ReturnCode errorcode, string errormessage, eMT4ServerType serverIndex, MT4REQ reqType,
            eOrderStatus trans_status, int masterLogin, /*int orderOrLogin,*/ Guid server_trans_id,
            eAccountType accType, MT4REQMODE reqMode, MT4Request request, CancellationToken cancellationToken)
        {
            Debug.Assert(masterLogin > 0);
            //m_ptrMySqlWrapper.insertLog(Utilities.LOG_INFO,masterLogin,orderOrLogin,"X",)
            if (reqType == MT4REQ.MT4REQ_NEW_ACCOUNT)
            {
                var mt4Login = request.User?.login ?? 0;

                if (trans_status == eOrderStatus.ORD_STAT_EXECUTED)
                {
                    Debug.Assert(mt4Login > 0);
                    m_ptrLogger.LogInfo($"EXECUTED : MT4 Account {mt4Login}, master Login {masterLogin}, AccountType: {accType}");
                    MT4Account acc = new MT4Account();
                    //memset(&acc, 0, sizeof(MT4Account));
                    acc._accountType = accType;
                    acc._masterLogin = masterLogin;
                    acc._mt4Login = mt4Login;
                    acc._mt4ServerIndex = serverIndex;

                    //if (serverIndex == SRV_TYPE_DEMO)
                    //{
                    //    Demo.insertMT4Account(orderOrLogin);
                    //}
                    //else
                    //{
                    //    Live.insertMT4Account(orderOrLogin);
                    //}

                    lock (_mt4LoginsByServerIndex)
                        AddMt4LoginNoLock(serverIndex, mt4Login);

                    await _mt4AccountService.InsertMT4Account(acc, cancellationToken);
                    //if (!)
                    //{
                    //    m_ptrLogger.LogError("Unable to insert MT4 account for masterlogin: %d MT4 Login: %d in database", masterLogin, orderOrLogin);
                    //}
                    //else
                    //{

                    #region Rebate
                    //TODO: Claudia: rebate
                    //if (accType == ACC_TYPE_REBATE)
                    //{
                    //    Dictionary<int, int>.iterator it2;

                    //    //lock (m_SyncMapMt4Master)
                    //    //{

                    //    it2 = m_mapMasterRebateAcc.find(masterLogin);
                    //    if (it2 == m_mapMasterRebateAcc.end())
                    //    {
                    //        m_mapMasterRebateAcc.insert(new ValueTuple<int, int>(masterLogin, orderOrLogin));
                    //    }

                    //    //}
                    //}
                    #endregion

                    ////else //if (accType != ACC_TYPE_REBATE)
                    ////{
                    //Dictionary<bool, Dictionary<int, int>>.iterator it1;
                    ////Dictionary<int, int>.iterator it2;

                    ////lock (m_SyncMapMt4Master)
                    ////{

                    //bool isDemoServer = serverIndex == SRV_TYPE_DEMO ? true : false;
                    //it1 = m_mapMT4MasterLogin.find(isDemoServer);
                    //if (it1 == m_mapMT4MasterLogin.end())
                    //{
                    //    Dictionary<int, int> mp2 = new Dictionary<int, int>();
                    //    m_mapMT4MasterLogin.insert(new ValueTuple<bool, Dictionary<int, int>>(isDemoServer, mp2));
                    //    it1 = m_mapMT4MasterLogin.find(isDemoServer);
                    //}
                    //it1.second.insert(new ValueTuple<int, int>(orderOrLogin, masterLogin));



                    ////}
                    ////}
                    ////}


                    if (accType == eAccountType.ACC_TYPE_SSP)
                    {
                        await _signalService.UpdateSignalMT4Login(server_trans_id, mt4Login, cancellationToken);
                        await _signalService.UpdateSSPSignalMT4Login(server_trans_id, mt4Login, cancellationToken);
                        //fetchAllSignal();
                        //fetchAllSSPSignal();
                        //fetchAllSMSignal();
                        //insertDBTransmitData(masterLogin, FDMT_Signal_ID);
                        //insertDBTransmitData(masterLogin, FDMT_SSPSignal_ID);
                    }
                    else if (accType == eAccountType.ACC_TYPE_SM)
                    {
                        await _signalService.UpdateSignalMT4Login(server_trans_id, mt4Login, cancellationToken);
                        await _signalService.UpdateSMSignalMT4Login(server_trans_id, mt4Login, cancellationToken);
                        //fetchAllSignal();
                        //fetchAllSSPSignal();
                        //fetchAllSMSignal();

                        //insertDBTransmitData(masterLogin, FDMT_Signal_ID);
                        //insertDBTransmitData(masterLogin, FDMT_SMSignal_ID);
                    }

                    var m_masterUserSetting = await _masterSettingsService.GetCachedMasterSettingsAsync(cancellationToken);

                    if (accType == eAccountType.ACC_TYPE_FOLLOWER_DEMO || accType == eAccountType.ACC_TYPE_SSP || accType == eAccountType.ACC_TYPE_SM)
                    {
                        MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                        //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                        //ptrMT4Req4.newLoginOrOrderID = orderOrLogin; // Alexey
                        ptrMT4Req4.masterLogin = masterLogin;
                        ptrMT4Req4.reqType = MT4REQ.MT4REQ_BALANCE;
                        //ptrMT4Req4.socketID = socketID;
                        ptrMT4Req4.status = eOrderStatus.ORD_STAT_RECVD;
                        ptrMT4Req4.serverTransID = TransactionService.NewTransactionId();
                        ptrMT4Req4.ptrData = new MT4OrderInfo();
                        //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));
                        MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                        ptrOrd._accountType = accType;
                        ptrOrd._masterLogin = masterLogin;
                        ptrOrd._mt4Login = mt4Login;
                        ptrOrd._mt4ServerIndex = serverIndex;
                        ptrOrd._orderTransMode = eMT4OrderTransMode.ORD_TRANS_CLOSE;
                        ptrOrd._orderType = eMT4OrderType.ORD_TYPE_BALANCE;
                        ptrOrd._price = request.deposit;
                        if (ptrOrd._price == 0) {
                            if (accType == eAccountType.ACC_TYPE_FOLLOWER_DEMO)
                            {
                                ptrOrd._price = m_masterUserSetting._deposit_followerDemo;
                            }
                            else if (accType == eAccountType.ACC_TYPE_SSP)
                            {
                                ptrOrd._price = m_masterUserSetting._deposit_SSP;
                            }
                            else if (accType == eAccountType.ACC_TYPE_SM)
                            {
                                ptrOrd._price = m_masterUserSetting._deposit_SM;
                            }
                        }

                        Demo.insertMT4Request(ptrMT4Req4);
                    }
                    else if (accType == eAccountType.ACC_TYPE_FOLLOWER_LIVE)
                    {
                        MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                        //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                        //ptrMT4Req4.newLoginOrOrderID = orderOrLogin; // Alexey
                        ptrMT4Req4.masterLogin = masterLogin;
                        ptrMT4Req4.reqType = MT4REQ.MT4REQ_BALANCE;
                        //ptrMT4Req4.socketID = socketID;
                        ptrMT4Req4.status = eOrderStatus.ORD_STAT_RECVD;
                        ptrMT4Req4.serverTransID = TransactionService.NewTransactionId();
                        ptrMT4Req4.ptrData = new MT4OrderInfo();
                        //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));
                        MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                        ptrOrd._accountType = accType;
                        ptrOrd._masterLogin = masterLogin;
                        ptrOrd._mt4Login = mt4Login;
                        ptrOrd._mt4ServerIndex = serverIndex;
                        ptrOrd._orderTransMode = eMT4OrderTransMode.ORD_TRANS_CLOSE;
                        ptrOrd._orderType = eMT4OrderType.ORD_TYPE_BALANCE;
                        ptrOrd._price = request.deposit;
                        if (ptrOrd._price == 0)
                        {
                            ptrOrd._price = m_masterUserSetting._deposit_followerLive;
                        }
                        Live.insertMT4Request(ptrMT4Req4);
                    }

                    var ptrResp = new MT4AccountResponse(); //MT_MT4AccountResponse_ID
                    CppHelper.memcpy(ref ptrResp._account, acc);
                    _connectionMgr.SentDataUsingLoginID(ptrResp, MessageTypeID.MT_MT4AccountResponse_ID, masterLogin);

                }
                else if (trans_status == eOrderStatus.ORD_STAT_PROCESSING)
                {
                    // Claudia: it's normal that orderOrLogin parameter is 0, because MT4 is still processing the request                    
                    Debug.Assert(masterLogin > 0);
                    m_ptrLogger.LogInfo("PROCESSING : MT4 Account %d master Login %d AccountType: %d", mt4Login, masterLogin, accType);
                    var ptrResp = new SocialOrderResponse(); //MT_SocialOrderResponse_ID
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    _connectionMgr.SentDataUsingLoginID(ptrResp, MessageTypeID.MT_SocialOrderResponse_ID, masterLogin);

                }
                else if (trans_status == eOrderStatus.ORD_STAT_REJECTED)
                {
                    // Claudia: it's normal that orderOrLogin parameter is 0, because MT4 is still processing the request                    
                    Debug.Assert(masterLogin > 0);
                    m_ptrLogger.LogInfo("REJECTED : MT4 Account %d master Login %d AccountType: %d", mt4Login, masterLogin, accType);
                    if (accType == eAccountType.ACC_TYPE_SSP)
                    {
                        await _signalService.UpdateSignalMT4Login(server_trans_id, mt4Login, cancellationToken, isRemove: true);
                        await _signalService.UpdateSSPSignalMT4Login(server_trans_id, mt4Login, cancellationToken, isRemove: true);
                    }
                    if (accType == eAccountType.ACC_TYPE_SM)
                    {
                        await _signalService.UpdateSignalMT4Login(server_trans_id, mt4Login, cancellationToken, isRemove: true);
                        await _signalService.UpdateSMSignalMT4Login(server_trans_id, mt4Login, cancellationToken, isRemove: true);
                    }
                    var ptrResp = new SocialOrderResponse(); //MT_SocialOrderResponse_ID
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    ptrResp._mt4errorcode = errorcode;
                    CppHelper.strcpy(out ptrResp._mt4errormessage, errormessage);
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    _connectionMgr.SentDataUsingLoginID(ptrResp, MessageTypeID.MT_SocialOrderResponse_ID, masterLogin);
                }
                //insertDBTransmitData(masterLogin, FDMT_MasterUser_ID);
                //insertDBTransmitData(masterLogin, FDMT_MT4Account_ID);
                //insertDBTransmitData(masterLogin, FDMT_SSPSignal_ID);
                //insertDBTransmitData(masterLogin, FDMT_SSPSignal_ID);
            }
            else
            {
                var orderId = request.OrderInfo?._orderID ?? 0;

                if (trans_status == eOrderStatus.ORD_STAT_PROCESSING)
                {
                    // Alexey: it's normal that orderOrLogin parameter is 0, because MT4 is still processing the request                    
                    Debug.Assert(masterLogin > 0 && server_trans_id != Guid.Empty);
                    m_ptrLogger.LogInfo($"PROCESSING : Order {orderId}, master Login {masterLogin}, UID: {server_trans_id}");
                    var ptrResp = new SocialOrderResponse(); //MT_SocialOrderResponse_ID
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    _connectionMgr.SentDataUsingLoginID(ptrResp, MessageTypeID.MT_SocialOrderResponse_ID, masterLogin);

                }
                else if (trans_status == eOrderStatus.ORD_STAT_REJECTED)
                {
                    // Alexey: it's normal that orderOrLogin parameter is 0, because MT4 is still processing the request                    
                    Debug.Assert(masterLogin > 0 && server_trans_id != Guid.Empty);
                    m_ptrLogger.LogInfo($"REJECTED : Order {orderId}, master Login {masterLogin}, UID: {server_trans_id}");
                    var ptrResp = new SocialOrderResponse(); //MT_SocialOrderResponse_ID
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    ptrResp._mt4errorcode = errorcode;
                    CppHelper.strcpy(out ptrResp._mt4errormessage, errormessage);
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    _connectionMgr.SentDataUsingLoginID(ptrResp, MessageTypeID.MT_SocialOrderResponse_ID, masterLogin);
                }
                else if (trans_status == eOrderStatus.ORD_STAT_EXECUTED)
                {
                    if (reqMode == MT4REQMODE.OPEN_TRADE)
                    {
                        Debug.Assert(server_trans_id != Guid.Empty && orderId > 0 && request.Order != null && request.Order.OrderId == 0);
                        request.Order.OrderId = orderId;
                        await _orderService.InsertAsync(request.Order, cancellationToken);
                        //    updateTransLinking(server_trans_id, orderOrLogin);
                    }
                    //if (reqMode == MT4REQMODE.CLOSE_TRADE || reqMode == MT4REQMODE.DELETE_TRADE)
                    //{
                    //    removeTransLinking(server_trans_id);
                    //}
                    Debug.Assert(masterLogin > 0 && server_trans_id != Guid.Empty);
                    m_ptrLogger.LogInfo($"EXECUTED : Order {orderId}, master Login {masterLogin}, UID: {server_trans_id}");
                    var ptrResp = new SocialOrderResponse(); //MT_SocialOrderResponse_ID
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    _connectionMgr.SentDataUsingLoginID(ptrResp, MessageTypeID.MT_SocialOrderResponse_ID, masterLogin);
                }
            }
        }

        void IMT4ServerConnectorHost.OnSymbolInfo(
            eMT4ServerType serverIndex, IReadOnlyList<ConSymbolGroup> ptrSecurityArr,
            IReadOnlyList<ConSymbol> ptrSymbolArr)
        {
            if (serverIndex == eMT4ServerType.SRV_TYPE_DEMO)
                try
                {
                    OnSymbolInfoAsync(ptrSecurityArr, ptrSymbolArr, CancellationToken.None).Wait();
                }
                catch (Exception exception)
                {
                    m_ptrLogger.LogException(exception);
                }
        }

        private async Task OnSymbolInfoAsync(
            IReadOnlyList<ConSymbolGroup> ptrSecurityArr, IReadOnlyList<ConSymbol> ptrSymbolArr,
            CancellationToken cancellationToken)
        {
            m_ptrLogger.LogOk("Adding symbols in database ");
            for (int iLoop = 0; iLoop < ptrSymbolArr.Count; iLoop++)
            {
                var ptrSym = new MT4SymbolInfo();
                //memset(ptrSym, 0, sizeof(MT4SymbolInfo));
                ptrSym._digits = ptrSymbolArr[iLoop].digits;
                ptrSym._spread = ptrSymbolArr[iLoop].spread;
                ptrSym._spread_balance = ptrSymbolArr[iLoop].spread_balance;
                ptrSym._stops_level = ptrSymbolArr[iLoop].stops_level;
                ptrSym._contract_size = ptrSymbolArr[iLoop].contract_size;
                ptrSym._tick_value = ptrSymbolArr[iLoop].tick_value;
                ptrSym._tick_size = ptrSymbolArr[iLoop].tick_size;
                ptrSym._point = ptrSymbolArr[iLoop].point;
                ptrSym._multiply = ptrSymbolArr[iLoop].multiply;
                ptrSym._ask_tickvalue = ptrSymbolArr[iLoop].ask_tickvalue;
                ptrSym._bid_tickvalue = ptrSymbolArr[iLoop].bid_tickvalue;
                CppHelper.COPY_STR_S(out ptrSym._symbol, ptrSymbolArr[iLoop].symbol);
                CppHelper.COPY_STR_S(out ptrSym._description, ptrSymbolArr[iLoop].description);
                CppHelper.COPY_STR_S(out ptrSym._security, ptrSecurityArr[ptrSymbolArr[iLoop].type].name);

                //lock (m_mapSymbolSpec)
                //    m_mapSymbolSpec[ptrSymbolArr[iLoop].symbol] = ptrSym;

                await _mt4SymbolInfoService.InsertUpdateMT4Symbol(ptrSym, cancellationToken);
            }

            //lock (m_csSymbolIndexName) {
            //m_ptrMySqlWrapper.setSymbolMap(m_mapSymbolIndexName, m_mapSymbolNameIndex);
            //}
        }

        public bool GetSymbolProperty(string symbol, ref int digit, ref int stopLevel, ref double pointValue)
        {
            var info = _mt4SymbolInfoService.GetCachedSymbolInfo(symbol);

            if (info != null)
            {
                digit = info._digits;
                stopLevel = info._stops_level;
                pointValue = info._point;
                return true;
            }

            return false;
        }

        void IMT4ServerConnectorHost.OnTrade(
            eMT4ServerType serverIndex, TradeRecord ptrTrade, TransType transType)
        {
            try
            {
                _orderManager.Value.OnTradeAsync(serverIndex, ptrTrade, transType, CancellationToken.None).Wait();
            }
            catch (Exception exception)
            {
                m_ptrLogger.LogException(exception);
            }
        }

        private readonly Dictionary<eMT4ServerType, HashSet<int>> _mt4LoginsByServerIndex =
            new Dictionary<eMT4ServerType, HashSet<int>>();

        private void AddMt4LoginNoLock(eMT4ServerType serverIndex, int mt4Login)
        {
            Debug.Assert(mt4Login > 0);

            if (mt4Login <= 0)
                return;

            if (!_mt4LoginsByServerIndex.TryGetValue(serverIndex, out var mt4Logins))
                _mt4LoginsByServerIndex.Add(serverIndex, mt4Logins = new HashSet<int>());

            mt4Logins.Add(mt4Login);
        }

        IReadOnlyList<int> IMT4ServerConnectorHost.GetAllMT4Logins(eMT4ServerType serverIndex)
        {
            lock (_mt4LoginsByServerIndex)
                return _mt4LoginsByServerIndex.TryGetValue(serverIndex, out var mt4Logins)
                    ? mt4Logins.ToArray()
                    : Array.Empty<int>();
        }

        #endregion
    }
}
