using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Contracts;
using WitFX.MT4.Server.Implementation.Collections;
using WitFX.MT4.Server.Implementation.Extensions;
using WitFX.MT4.Server.Implementation.Helpers;
using WitFX.MT4.Server.Models;

namespace WitFX.MT4.Server.Implementation
{
    #region Shared Types

    public sealed class MT4Request
    {
        public int masterLogin;
        public MT4REQ reqType;
        public eOrderStatus status;
        public eAccountType accType;
        //public uint socketID;
        public Guid serverTransID;
        //public Guid signalServerTransID;
        public DateTimeOffset reqInsertTime;
        public DateTimeOffset queryTime;
        public MT4REQMODE requestMode;
        public object ptrData;
        public ReturnCode mt4errorcode;
        public string mt4error;
        public int leverage;
        public string group;
        public double deposit;

        private readonly TaskCompletionSource<eOrderStatus> _taskSource =
            new TaskCompletionSource<eOrderStatus>();

        public Task<eOrderStatus> Task => _taskSource.Task;

        public void NotifyCompleted() => _taskSource.TrySetResult(status);

        /// <summary>
        /// Created new user as a result of 'UserRecordNew' operation
        /// </summary>
        public UserRecord User { get; set; }

        /// <summary>
        /// Created new trade as a result of 'TradeTransaction' operation
        /// </summary>
        public TradeTransInfo Trade { get; set; }

        public Order Order { get; set; }

        public MT4OrderInfo OrderInfo => ptrData as MT4OrderInfo;
    }

    public enum MT4REQ
    {
        MT4REQ_NEW_ACCOUNT = 0,
        MT4REQ_BALANCE = 1,
        MT4REQ_TRADE = 2
    }

    public enum MT4REQMODE
    {
        NEW_ACCOUNT = 'N',
        BALANCE = 'B',
        OPEN_TRADE = 'O',
        MODIFY_TRADE = 'M',
        CLOSE_TRADE = 'C',
        DELETE_TRADE = 'D'
    }

    #endregion

    public class WitFXServerConnector : IDisposable
    {
        #region Fields

        private static readonly TimeSpan MAX_WAIT_TIME_IN_SEC = TimeSpan.FromSeconds(22);
        private static readonly TimeSpan CHECK_INTERVAL_LIMIT_SEC = TimeSpan.FromSeconds(2);

        public MasterUserSetting m_masterUserSetting;

        private readonly ServerLogger m_ptrLogger;
        //MT4SERVERCONN_NOTIFY_FUNC_EX						m_fptrPumpSwitchEx;
        //MT4SERVERCONN_NOTIFY_FUNC							m_fptrPumpSwitch;

        private readonly eMT4ServerType m_serverIndex;
        private readonly IMT4ServerConnectorHost _events;
        private volatile bool m_isClosing;
        private bool m_IsNormalMgrConnected;
        private bool m_IsPumpMgrConnected;
        private bool m_IsPumpExMgrConnected;
        //private bool m_isError;

        private bool m_IsPumpMgrInUsed;
        private bool m_IsPumpMgrExInUsed;


        private int m_tradeLogin;
        private int m_PingIntervalSec;

        private bool m_isAllSymbolAddedInMgr;
        private bool m_isAllSymbolAddedInPumpExMgr;
        private bool m_isAllSymbolAddedInPumpMgr;

        private string m_tradePassword;
        private string m_tradeServerURL;

        private volatile bool m_iPumpExMgr;
        private Thread m_hPumpExMgrThrd;
        private EventWaitHandle m_hPumpExMgrEvnt;

        private volatile bool m_iPumpMgr;
        private Thread m_hPumpMgrThrd;
        private EventWaitHandle m_hPumpMgrEvnt;

        private volatile bool m_iNormalMgrThread;
        private Thread m_hNormalMgrThrd;
        private EventWaitHandle m_hNormalMgrThrdEvnt;

        private volatile bool m_iDBMgrThread;
        private Thread m_hDBMgrThrd;
        private EventWaitHandle m_hDBMgrThrdEvnt;

        private volatile bool m_iOpenTradeAnalyThrd;
        private Thread m_hOpenTradeAnalyThrd;
        private EventWaitHandle m_hOpenTradeAnalyThrdEvnt;
        private int m_iOpenTradeAnalyThrdPeriod;

        private volatile bool m_iCloseTradeAnalyThrd;
        private Thread m_hCloseTradeAnalyThrd;
        private EventWaitHandle m_hCloseTradeAnalyThrdEvnt;
        private int m_iCloseTradeAnalyThrdPeriod;

        private volatile bool m_iOpenAccAnalyThrd;
        private Thread m_hOpenAccAnalyThrd;
        private EventWaitHandle m_hOpenAccAnalyThrdEvnt;
        private int m_iOpenAccAnalyThrdPeriod;

        private volatile bool m_iUpdateDataThrd;
        private Thread m_hUpdateDataThrd;
        private EventWaitHandle m_hUpdateDataThrdEvnt;
        private int m_iUpdateDataThrdPeriod;

        private readonly object m_syncManagerNormal = new object();
        private readonly object m_syncManagerPumpEx = new object();

        private readonly Queue<MT4Request> m_queueMT4Request =
            new Queue<MT4Request>();
        private readonly object m_syncMT4RequestQueue = new object();

        //MAP[ LOGIN ][ UID ] = MT4Request
        private readonly Dictionary<int, Dictionary<Guid, MT4Request>>
            m_mapOpenTradeMT4Request =
                new Dictionary<int, Dictionary<Guid, MT4Request>>();
        private readonly object m_syncOpenTradeMT4RequestMap = new object();
        //MAP[ LOGIN ][ UID ] = MT4Request
        private readonly Dictionary<int, Dictionary<Guid, MT4Request>>
            m_mapCloseTradeMT4Request =
                new Dictionary<int, Dictionary<Guid, MT4Request>>();
        private readonly object m_syncCloseTradeMT4RequestMap = new object();

        private readonly object m_syncVecOpenTradeResp = new object();
        private readonly object m_syncVecNewAccResp = new object();
        //VECTOR< UID < LOGIN , ORDERID > >
        private readonly List<ValueTuple<Guid, ValueTuple<int, int>>> m_vecOpenTradeResp =
            new List<(Guid, (int, int))>();
        //VECTOR< UID < LOGIN , MASTERLOGIN > >
        private readonly List<ValueTuple<Guid, ValueTuple<int, int>>> m_vecNewAccResp =
            new List<(Guid, (int, int))>();

        //MAP[ UID ] = MT4 Request
        private readonly Dictionary<Guid, MT4Request> m_mapAccountMT4Request =
            new Dictionary<Guid, MT4Request>();
        private readonly object m_syncAccountMT4RequestMap = new object();

        private volatile bool m_iMT4ReqThrd;
        private Thread m_hMT4ReqThrd;
        private EventWaitHandle m_hMT4ReqThrdEvnt;

        private ManagerAPI m_ptrManagerAPI;
        private CManagerInterface m_ptrMT4Normal;
        private CManagerInterface m_ptrMT4PumpEx;
        private CManagerInterface m_ptrMT4Pump;

        private readonly Queue<int> m_queueMarginReq = new Queue<int>();
        private readonly object m_csQueueMarginReq = new object();

        //private readonly HashSet<int> m_setMt4Accounts = new HashSet<int>();
        //private readonly object m_csMt4Accounts = new object();

        #endregion

        public WitFXServerConnector(
            eMT4ServerType serverIndex, IMT4ServerConnectorHost events, CManagerFactory factory, string traderServer,
            int traderLogin, string traderPassword, ILogger ptrLogger, bool isPump,
            bool isPumpEx, bool startThreads)
        {
            m_serverIndex = serverIndex;
            _events = events ?? throw new ArgumentNullException(nameof(events));
            m_ptrLogger = new ServerLogger(ptrLogger, (serverIndex == eMT4ServerType.SRV_TYPE_DEMO ? "Demo" : "Live") + nameof(WitFXServerConnector));
            m_IsPumpMgrInUsed = isPump;
            m_IsPumpMgrExInUsed = isPumpEx;
            m_tradeLogin = traderLogin;
            m_PingIntervalSec = 9;
            m_tradeServerURL = traderServer;
            m_iOpenTradeAnalyThrdPeriod = 4;
            m_iCloseTradeAnalyThrdPeriod = 4;
            m_iOpenAccAnalyThrdPeriod = 4;
            m_iUpdateDataThrdPeriod = 1;

            m_ptrLogger.LogInfo($"START Login: {m_tradeLogin} Server: {m_tradeServerURL}");

            m_tradePassword = traderPassword;

            m_ptrManagerAPI = new ManagerAPI(factory);

            m_ptrMT4Normal = m_ptrManagerAPI.m_Mgr;
            if (m_IsPumpMgrExInUsed)
            {
                m_ptrMT4PumpEx = m_ptrManagerAPI.m_PumpMgrEx;
            }
            if (m_IsPumpMgrInUsed)
            {
                m_ptrMT4Pump = m_ptrManagerAPI.m_PumpMgr;
            }

            m_ptrManagerAPI.createDBMT4Manager();

            if (startThreads)
            {
                //m_ptrLogger.LogInfo("starting Open Trade Analyzer thread");
                //startOpenTradeAnalyserThread();
                m_ptrLogger.LogInfo("starting Close Trade Analyzer thread");
                startCloseTradeAnalyserThread();
                m_ptrLogger.LogInfo("starting Open account Analyzer thread");
                startOpenAccAnalyserThread();
                m_ptrLogger.LogInfo("starting MT4 Request thread");
                startMT4RequestThread();
                //m_ptrLogger.LogInfo("starting Data Update thread");
                //startUpdateDataThread();
                m_ptrLogger.LogInfo("END");
            }
        }

        public void Dispose() => Stop();

        public void Stop()
        {
            if (m_isClosing)
                return;

            m_isClosing = true;
            m_ptrLogger.LogOk("START");

            m_ptrLogger.LogOk("stopping Open Trade Analyzer thread");
            stopOpenTradeAnalyserThread();
            m_ptrLogger.LogOk("stopping Close Trade Analyzer thread");
            stopCloseTradeAnalyserThread();
            m_ptrLogger.LogOk("stopping Open account Analyzer thread");
            stopOpenAccAnalyserThread();
            m_ptrLogger.LogOk("stopping MT4 Request thread");
            stopMT4RequestThread();

            m_ptrLogger.LogOk("Clearing Request Queue");

            lock (m_syncMT4RequestQueue)
                m_queueMT4Request.Clear();

            m_ptrLogger.LogOk("Clearing OpenTradeResponse Vector");

            lock (m_syncVecOpenTradeResp)
                m_vecOpenTradeResp.Clear();

            m_ptrLogger.LogOk("Clearing NewAccount Vector");

            lock (m_syncVecNewAccResp)
                m_vecNewAccResp.Clear();

            m_ptrLogger.LogOk("Clearing Open Trade Map");

            lock (m_syncOpenTradeMT4RequestMap)
                m_mapOpenTradeMT4Request.Clear();

            m_ptrLogger.LogOk("Clearing Close Trade Map");

            lock (m_syncCloseTradeMT4RequestMap)
                m_mapCloseTradeMT4Request.Clear();

            m_ptrLogger.LogOk("Clearing New Account Req Map");

            lock (m_syncAccountMT4RequestMap)
                m_mapAccountMT4Request.Clear();

            //m_ptrLogger.LogOk("stoppping Data Update thread");
            //stopUpdateDataThread();

            m_ptrLogger.LogOk("stopping normal manager thread");
            stopNormalManager();

            m_ptrLogger.LogOk("stopping DB manager thread");
            stopDBManager();

            m_ptrLogger.LogOk("stopping pumpex manager thread");
            stopPumpingExManager();

            m_ptrLogger.LogOk("stopping pump manager thread");
            stopPumpingManager();

            m_ptrLogger.LogOk("deleting manager api");
            m_ptrManagerAPI.Dispose();

            m_ptrManagerAPI = null;

            m_ptrLogger.LogOk("END");
        }

        #region Events

        private void m_fptrMT4ReqResponse(
            ReturnCode errorcode, string errormessage, eMT4ServerType serverIndex, MT4REQ reqType,
            eOrderStatus trans_status, int masterLogin, /*int orderOrLogin,*/ /*uint socketID,*/
            Guid server_trans_id, eAccountType accType, MT4REQMODE reqMode, MT4Request request)
        {
            if (!m_isClosing)
                _events.OnResponse(
                      errorcode, errormessage, serverIndex, reqType,
                      trans_status, masterLogin, /*orderOrLogin,*/ /*socketID,*/
                      server_trans_id, accType, reqMode, request);
        }

        private void m_fptrMarketData(
            System.Collections.Generic.IReadOnlyList<SymbolInfo> ptrArr)
        {
            if (!m_isClosing)
                _events.OnMarketData(m_serverIndex, ptrArr);
        }

        private void m_fptrSymbolInfo(
            System.Collections.Generic.IReadOnlyList<ConSymbolGroup> ptrSecurityArr,
            System.Collections.Generic.IReadOnlyList<ConSymbol> ptrSymbolArr)
        {
            if (!m_isClosing)
                _events.OnSymbolInfo(m_serverIndex, ptrSecurityArr, ptrSymbolArr);
        }

        private void m_fptrOnTrade(
            eMT4ServerType serverIndex, TradeRecord ptrTrade, TransType transType)
        {
            if (!m_isClosing)
                _events.OnTrade(serverIndex, ptrTrade, transType);
        }

        private void m_fptrOnMargin(
            eMT4ServerType serverIndex, MarginLevel ptrLevel)
        {
            if (!m_isClosing)
                _events.OnMargin(serverIndex, ptrLevel);
        }

        #endregion

        #region Helper Methods

        private Thread _beginthreadex(Func<uint> method)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    method();
                }
                catch (Exception exception)
                {
#if DEBUG
                    if (Debugger.IsAttached)
                        Debugger.Break();
#endif
                    m_ptrLogger.LogException(exception);
                }
                finally
                {
                    m_ptrLogger.LogOk("Thread stopped");
                }
            });
            thread.Start();
            return thread;
        }

        #endregion

        #region Public Methods

        public bool IsAPIValid()
        {
            m_ptrLogger.LogOk("START");
            bool ret = false;
            if (m_ptrManagerAPI.isAPIValid())
            {
                if (m_ptrManagerAPI.isNormalManagerValid())
                {
                    if (m_IsPumpMgrExInUsed)
                    {
                        ret = m_ptrManagerAPI.isPumpManagerExValid();
                        if (!ret)
                        {
                            return ret;
                        }
                    }
                    ret = false;
                    if (m_IsPumpMgrInUsed)
                    {
                        ret = m_ptrManagerAPI.isPumpManagerValid();
                        if (!ret)
                        {
                            return ret;
                        }
                    }
                    ret = false;
                    ret = m_ptrManagerAPI.isNormalManagerValid();
                    if (!ret)
                    {
                        return ret;
                    }
                }//if( m_ptrManagerAPI.isNormalManagerValid() )
            }//if( m_ptrManagerAPI.isAPIValid() )
            return ret;
        }

        public void setServerCredentials(string traderServer, int traderLogin, string traderPassword)
        {
            m_tradeServerURL = traderServer;
            m_tradeLogin = traderLogin;
            m_tradePassword = traderPassword;
        }

        public void startNormalManager()
        {
            m_ptrLogger.LogOk("START");
            stopNormalManager();
            m_hNormalMgrThrdEvnt = CppHelper.CreateEvent(false, false);

            m_iNormalMgrThread = true;
            m_hNormalMgrThrd = _beginthreadex(normalManagerThread);
            CppHelper.Sleep(100);
            CppHelper.SetEvent(m_hNormalMgrThrdEvnt);

            m_ptrLogger.LogOk("Starting DB Manager");
            startDBManager();

            m_ptrLogger.LogOk("END");
        }

        public void startPumpingExManager()
        {
            if (m_IsPumpMgrExInUsed)
            {
                m_ptrLogger.LogOk("START");
                stopPumpingExManager();
                m_hPumpExMgrEvnt = CppHelper.CreateEvent(false, false);

                m_iPumpExMgr = true;
                m_hPumpExMgrThrd = _beginthreadex(pumpManagerExThread);
                m_ptrLogger.LogOk("END");
                CppHelper.SetEvent(m_hPumpExMgrEvnt);
            }
        }

        public void startPumpingManager()
        {
            if (m_IsPumpMgrInUsed)
            {
                m_ptrLogger.LogOk("START");
                stopPumpingManager();
                m_hPumpMgrEvnt = CppHelper.CreateEvent(false, false);

                m_iPumpMgr = true;
                m_hPumpMgrThrd = _beginthreadex(pumpManagerThread);
                m_ptrLogger.LogOk("END");
                CppHelper.SetEvent(m_hPumpMgrEvnt);
            }
        }

        //public void insertMT4Account(int mt4login)
        //{
        //    lock (m_csMt4Accounts)
        //        m_setMt4Accounts.Add(mt4login);
        //}

        //public void setMT4SocialUsers(Dictionary<bool, Dictionary<int, int>> mp)
        //{
        //    var isDemoServer = m_serverIndex == SRV_TYPE_DEMO;

        //    foreach (var it1 in mp)
        //    {
        //        if (it1.Key == isDemoServer)
        //        {
        //            foreach (var it2 in it1.Value)
        //                lock (m_csMt4Accounts)
        //                    m_setMt4Accounts.Add(it2.Key);
        //        }
        //    }
        //}

        public void insertMT4Request(MT4Request ptr)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("CLOSING.....");
                return;
            }
            ptr.queryTime = DateTimeOffset.UtcNow;
            ptr.reqInsertTime = ptr.queryTime;
            ptr.status = eOrderStatus.ORD_STAT_PROCESSING;

            lock (m_syncMT4RequestQueue)
            {
                m_queueMT4Request.Enqueue(ptr);
            }

            try
            {
                m_fptrMT4ReqResponse(ptr.mt4errorcode, ptr.mt4error, m_serverIndex, ptr.reqType, ptr.status, ptr.masterLogin, /*ptr.newLoginOrOrderID,*/ /*ptr.socketID,*/ ptr.serverTransID, ptr.accType, ptr.requestMode, ptr);
            }
            catch (Exception exception)
            {
                m_ptrLogger.LogError("Unknown exception", exception);
            }
            CppHelper.SetEvent(m_hMT4ReqThrdEvnt);
        }

        public int getTotalOpenOrdersCount(int login, string grp)
        {
            int ret = 0;

            lock (m_syncManagerPumpEx)
            {
                ret = m_ptrMT4PumpEx.TradesGetByLoginCount(login, grp);
            }

            return ret;
        }

        public DateTimeOffset getServerTime()
        {
            DateTimeOffset? srvTime;
            if (isMT4Connected())
            {
                lock (m_syncManagerNormal)
                {
                    srvTime = m_ptrMT4Normal.ServerTime();
                }
                Debug.Assert(srvTime != null);
                return srvTime.Value;
            }
            throw new InvalidOperationException();
        }

        public void insertMarginRequest(int login)
        {
            lock (m_csQueueMarginReq)
            {
                m_queueMarginReq.Enqueue(login);
            }
            CppHelper.SetEvent(m_hDBMgrThrdEvnt);
        }

        public bool isMT4Connected()
        {
            //if (m_IsNormalMgrConnected && m_IsPumpMgrConnected && m_IsPumpExMgrConnected)
            if (m_IsNormalMgrConnected) // Alexey HACK
            {
                return true;
            }
            else
            {
                //#if DEBUG
                //                if (Debugger.IsAttached)
                //                    Debugger.Break();
                //#endif
                return false;
            }
        }

        #endregion

        #region Private Methods

        private void initPumpExManager()
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("CLOSING.....");
                return;
            }
            m_ptrLogger.LogOk("START");
            if (m_ptrMT4PumpEx.IsConnected())
            {
                m_ptrMT4PumpEx.Disconnect();
            }
            CppHelper.Sleep(100);
            ReturnCode res = ReturnCode.RET_ERROR;

            m_ptrLogger.LogInfo($"Server: {m_tradeServerURL} Login: {m_tradeLogin} ");

            res = m_ptrMT4PumpEx.Connect(m_tradeServerURL);

            if (res == ReturnCode.RET_OK)
            {
                m_ptrLogger.LogInfo("Successfully PumpManagerEx Connected");
                res = ReturnCode.RET_ERROR;
                res = m_ptrMT4PumpEx.Login(m_tradeLogin, m_tradePassword);
                if (res != ReturnCode.RET_OK)
                {
                    m_ptrLogger.LogError(" Unable to login PumpManagerEx" +
                        " Error: %d - %s", res, m_ptrMT4PumpEx.ErrorDescription(res));
                }
                else
                {
                    m_ptrLogger.LogInfo("Successfully login PumpManagerEx and refreshing symbol");
                    m_ptrMT4PumpEx.SymbolsRefresh();
                    res = ReturnCode.RET_ERROR;
                    if (!m_isAllSymbolAddedInPumpExMgr)
                    {
                        m_ptrLogger.LogOk("Refreshing symbol");
                        m_isAllSymbolAddedInPumpExMgr = true;
                        var ptrSymbol = m_ptrMT4PumpEx.SymbolsGetAll();
                        var iTotal = ptrSymbol.Count;
                        if (iTotal > 0)
                        {
                            for (int iLoop = 0; iLoop < iTotal && !m_isClosing; iLoop++)
                            {
                                var res2 = m_ptrMT4PumpEx.SymbolAdd(ptrSymbol[iLoop].symbol);
                                if (res2 != ReturnCode.RET_OK)
                                {
                                    m_ptrLogger.LogWarning("Unable to add symbol " +
                                        "Error %d - %s \n", res2, m_ptrMT4Normal.ErrorDescription(res2));
                                }
                            }
                        }
                        else
                        {
                            m_ptrLogger.LogWarning("There is no symbol ");
                        }
                    }

                    res = ReturnCode.RET_ERROR;
                    res = m_ptrMT4PumpEx.PumpingSwitchEx(pumpSwitchEx, 0);
                    if (res != ReturnCode.RET_OK)
                    {
                        m_ptrLogger.LogError("Unable to starting pumping in PumpingSwitchEx " +
                            "Error: %d - %s", res, m_ptrMT4PumpEx.ErrorDescription(res));
                    }
                    else
                    {
                        m_ptrLogger.LogInfo("Successfully switched to pump mode PumpingSwitchEx");
                    }
                }
            }//if( res == RET_OK )
            else
            {
                m_ptrLogger.LogError("Unable to connect PumpingSwitchEx " +
                    "Error: %d - %s", res, m_ptrMT4PumpEx.ErrorDescription(res));
            }

            m_ptrLogger.LogOk("END");
        }

        private void initPumpManager()
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("CLOSING.....");
                return;
            }
            m_ptrLogger.LogOk("START");
            if (m_ptrMT4Pump.IsConnected())
            {
                m_ptrMT4Pump.Disconnect();
            }
            CppHelper.Sleep(100);
            var res = ReturnCode.RET_ERROR;

            m_ptrLogger.LogOk("Server: %s Login: %d ", m_tradeServerURL, m_tradeLogin);

            res = m_ptrMT4Pump.Connect(m_tradeServerURL);

            if (res == ReturnCode.RET_OK)
            {
                m_ptrLogger.LogInfo("Successfully PumpManage Connected");
                res = ReturnCode.RET_ERROR;
                res = m_ptrMT4Pump.Login(m_tradeLogin, m_tradePassword);
                if (res != ReturnCode.RET_OK)
                {
                    m_ptrLogger.LogError(" Unable to login PumpManager" +
                        " Error: %d - %s", res, m_ptrMT4Pump.ErrorDescription(res));
                }
                else
                {
                    m_ptrLogger.LogInfo("Successfully login PumpManager");
                    m_ptrMT4Pump.SymbolsRefresh();
                    res = ReturnCode.RET_ERROR;
                    if (!m_isAllSymbolAddedInPumpMgr)
                    {
                        m_ptrLogger.LogOk("Refreshing symbol");
                        m_isAllSymbolAddedInPumpMgr = true;
                        var ptrSymbol = m_ptrMT4Pump.SymbolsGetAll();
                        var iTotal = ptrSymbol.Count;
                        if (iTotal > 0)
                        {
                            for (int iLoop = 0; iLoop < iTotal && !m_isClosing; iLoop++)
                            {

                                var res2 = m_ptrMT4Pump.SymbolAdd(ptrSymbol[iLoop].symbol);
                                if (res2 != ReturnCode.RET_OK)
                                {
                                    m_ptrLogger.LogWarning("Unable to add symbol " +
                                        "Error %d - %s \n", res2, m_ptrMT4Normal.ErrorDescription(res2));
                                }
                            }
                        }
                    }

                    res = ReturnCode.RET_ERROR;
                    res = m_ptrMT4Pump.PumpingSwitch(pumpSwitch, 0);
                    if (res != ReturnCode.RET_OK)
                    {
                        m_ptrLogger.LogError("Unable to starting pumping in PumpingSwitch " +
                            "Error: %d - %s", res, m_ptrMT4Pump.ErrorDescription(res));
                    }
                    else
                    {
                        m_ptrLogger.LogInfo("Successfully switched to pump mode PumpingSwitch");
                    }
                }
            }//if( res == RET_OK )
            else
            {
                m_ptrLogger.LogError("Unable to connect PumpingSwitch " +
                    "Error: %d - %s", res, m_ptrMT4Pump.ErrorDescription(res));
            }

            m_ptrLogger.LogOk("END");
        }

        private void startDBManager()
        {
            m_ptrLogger.LogOk("START");
            stopDBManager();
            m_hDBMgrThrdEvnt = CppHelper.CreateEvent(false, false);

            m_iDBMgrThread = true;
            m_hDBMgrThrd = _beginthreadex(dbManagerThread);
            CppHelper.Sleep(100);
            CppHelper.SetEvent(m_hDBMgrThrdEvnt);

            m_ptrLogger.LogOk("END");
        }

        private void stopNormalManager()
        {
            ////////////////////////
            m_ptrLogger.LogOk("START");
            ////////////////////////
            m_iNormalMgrThread = false;
            if (m_hNormalMgrThrdEvnt != null)
            {
                CppHelper.SetEvent(m_hNormalMgrThrdEvnt);
            }

            if (m_hNormalMgrThrd != null)
            {
                CppHelper.WaitForSingleObject(m_hNormalMgrThrd, CppHelper.INFINITE);
            }
            ////////////////////////
            if (m_ptrMT4Normal != null)
            {
                if (m_ptrMT4Normal.IsConnected())
                {
                    m_ptrMT4Normal.Disconnect();
                }
            }
            if (m_hNormalMgrThrd != null)
            {
                CppHelper.CloseHandle(m_hNormalMgrThrd);
            }

            if (m_hNormalMgrThrdEvnt != null)
            {
                CppHelper.CloseHandle(m_hNormalMgrThrdEvnt);
            }
            ////////////////////////
            m_hNormalMgrThrdEvnt = null;
            m_hNormalMgrThrd = null;
            ////////////////////////

            m_ptrLogger.LogOk("END");
        }

        private void stopDBManager()
        {
            ////////////////////////
            m_ptrLogger.LogOk("START");
            ////////////////////////
            m_iDBMgrThread = false;
            if (m_hDBMgrThrdEvnt != null)
            {
                CppHelper.SetEvent(m_hDBMgrThrdEvnt);
            }

            if (m_hDBMgrThrd != null)
            {
                CppHelper.WaitForSingleObject(m_hDBMgrThrd, CppHelper.INFINITE);
            }
            ////////////////////////
            if (m_ptrManagerAPI.m_DBMgr != null)
            {
                if (m_ptrManagerAPI.m_DBMgr.IsConnected())
                {
                    m_ptrManagerAPI.m_DBMgr.Disconnect();
                }
            }

            if (m_hDBMgrThrd != null)
            {
                CppHelper.CloseHandle(m_hDBMgrThrd);
            }

            if (m_hDBMgrThrdEvnt != null)
            {
                CppHelper.CloseHandle(m_hDBMgrThrdEvnt);
            }
            ////////////////////////
            m_hDBMgrThrdEvnt = null;
            m_hDBMgrThrd = null;
            ////////////////////////

            m_ptrLogger.LogOk("END");
        }

        private void stopPumpingExManager()
        {
            m_iPumpExMgr = false;
            if (m_IsPumpMgrExInUsed)
            {
                m_ptrLogger.LogOk("START");
                m_iPumpExMgr = false;

                if (m_hPumpExMgrEvnt != null)
                {
                    CppHelper.SetEvent(m_hPumpExMgrEvnt);
                }

                if (m_hPumpExMgrThrd != null)
                {
                    CppHelper.WaitForSingleObject(m_hPumpExMgrThrd, CppHelper.INFINITE);
                }

                if (m_ptrMT4PumpEx != null)
                {
                    if (m_ptrMT4PumpEx.IsConnected())
                    {
                        m_ptrMT4PumpEx.Disconnect();
                    }
                }

                if (m_hPumpExMgrThrd != null)
                {
                    CppHelper.CloseHandle(m_hPumpExMgrThrd);
                }

                if (m_hPumpExMgrEvnt != null)
                {
                    CppHelper.CloseHandle(m_hPumpExMgrEvnt);
                }
                m_hPumpExMgrEvnt = null;
                m_hPumpExMgrThrd = null;
            }
            m_ptrLogger.LogOk("END");
        }

        private void stopPumpingManager()
        {
            m_iPumpMgr = false;
            if (m_IsPumpMgrInUsed)
            {
                m_ptrLogger.LogOk("START");
                m_iPumpMgr = false;

                if (m_hPumpMgrEvnt != null)
                {
                    CppHelper.SetEvent(m_hPumpMgrEvnt);
                }

                if (m_hPumpMgrThrd != null)
                {
                    CppHelper.WaitForSingleObject(m_hPumpMgrThrd, CppHelper.INFINITE);
                }

                if (m_ptrMT4Pump != null)
                {
                    if (m_ptrMT4Pump.IsConnected())
                    {
                        m_ptrMT4Pump.Disconnect();
                    }
                }

                if (m_hPumpMgrThrd != null)
                {
                    CppHelper.CloseHandle(m_hPumpMgrThrd);
                }

                if (m_hPumpMgrEvnt != null)
                {
                    CppHelper.CloseHandle(m_hPumpMgrEvnt);
                }
                m_hPumpMgrEvnt = null;
                m_hPumpMgrThrd = null;
            }
            m_ptrLogger.LogOk("END");
        }

        //private void startUpdateDataThread()
        //{
        //    m_ptrLogger.LogOk("START");
        //    stopUpdateDataThread();
        //    m_hUpdateDataThrdEvnt = CreateEvent(true, false);
        //    m_iUpdateDataThrd = true;
        //    m_hUpdateDataThrd = _beginthreadex(updateDataThread);

        //    m_ptrLogger.LogOk("END");
        //}

        //private void stopUpdateDataThread()
        //{
        //    m_ptrLogger.LogOk("START");

        //    m_iUpdateDataThrd = false;

        //    if (m_hUpdateDataThrdEvnt != null)
        //    {
        //        SetEvent(m_hUpdateDataThrdEvnt);
        //    }

        //    if (m_hUpdateDataThrd != null)
        //    {
        //        WaitForSingleObject(m_hUpdateDataThrd, INFINITE);
        //    }

        //    if (m_hUpdateDataThrd != null)
        //    {
        //        CloseHandle(m_hUpdateDataThrd);
        //    }

        //    if (m_hUpdateDataThrdEvnt != null)
        //    {
        //        CloseHandle(m_hUpdateDataThrdEvnt);
        //    }
        //    m_hUpdateDataThrdEvnt = null;
        //    m_hUpdateDataThrd = null;
        //    m_ptrLogger.LogOk("END");
        //}

        private void startMT4RequestThread()
        {
            m_ptrLogger.LogOk("START");
            stopMT4RequestThread();
            m_hMT4ReqThrdEvnt = CppHelper.CreateEvent(false, false);

            m_iMT4ReqThrd = true;
            m_hMT4ReqThrd = _beginthreadex(mt4RequestThread);
            m_ptrLogger.LogOk("END");
        }

        private void stopMT4RequestThread()
        {
            m_ptrLogger.LogOk("START");
            m_iMT4ReqThrd = false;

            if (m_hMT4ReqThrdEvnt != null)
            {
                CppHelper.SetEvent(m_hMT4ReqThrdEvnt);
            }

            if (m_hMT4ReqThrd != null)
            {
                CppHelper.WaitForSingleObject(m_hMT4ReqThrd, CppHelper.INFINITE);
            }

            if (m_hMT4ReqThrd != null)
            {
                CppHelper.CloseHandle(m_hMT4ReqThrd);
            }

            if (m_hMT4ReqThrdEvnt != null)
            {
                CppHelper.CloseHandle(m_hMT4ReqThrdEvnt);
            }
            m_hMT4ReqThrdEvnt = null;
            m_hMT4ReqThrd = null;

            m_ptrLogger.LogOk("END");
        }

        private void startOpenTradeAnalyserThread()
        {
            m_ptrLogger.LogOk("START");
            stopOpenTradeAnalyserThread();
            m_hOpenTradeAnalyThrdEvnt = CppHelper.CreateEvent(true, false);
            m_iOpenTradeAnalyThrd = true;
            m_hOpenTradeAnalyThrd = _beginthreadex(openTradeAnalyserThread);

            m_ptrLogger.LogOk("END");
        }

        private void stopOpenTradeAnalyserThread()
        {
            m_ptrLogger.LogOk("START");

            m_iOpenTradeAnalyThrd = false;

            if (m_hOpenTradeAnalyThrdEvnt != null)
            {
                CppHelper.SetEvent(m_hOpenTradeAnalyThrdEvnt);
            }

            if (m_hOpenTradeAnalyThrd != null)
            {
                CppHelper.WaitForSingleObject(m_hOpenTradeAnalyThrd, CppHelper.INFINITE);
            }

            if (m_hOpenTradeAnalyThrd != null)
            {
                CppHelper.CloseHandle(m_hOpenTradeAnalyThrd);
            }

            if (m_hOpenTradeAnalyThrdEvnt != null)
            {
                CppHelper.CloseHandle(m_hOpenTradeAnalyThrdEvnt);
            }
            m_hOpenTradeAnalyThrdEvnt = null;
            m_hOpenTradeAnalyThrd = null;
            m_ptrLogger.LogOk("END");
        }

        private void startCloseTradeAnalyserThread()
        {
            m_ptrLogger.LogOk("START");
            stopCloseTradeAnalyserThread();
            m_hCloseTradeAnalyThrdEvnt = CppHelper.CreateEvent(true, false);
            m_iCloseTradeAnalyThrd = true;
            m_hCloseTradeAnalyThrd = _beginthreadex(closeTradeAnalyserThread);

            m_ptrLogger.LogOk("END");
        }

        private void stopCloseTradeAnalyserThread()
        {
            m_ptrLogger.LogOk("START");

            m_iCloseTradeAnalyThrd = false;

            if (m_hCloseTradeAnalyThrdEvnt != null)
            {
                CppHelper.SetEvent(m_hCloseTradeAnalyThrdEvnt);
            }

            if (m_hCloseTradeAnalyThrd != null)
            {
                CppHelper.WaitForSingleObject(m_hCloseTradeAnalyThrd, CppHelper.INFINITE);
            }

            if (m_hCloseTradeAnalyThrd != null)
            {
                CppHelper.CloseHandle(m_hCloseTradeAnalyThrd);
            }

            if (m_hCloseTradeAnalyThrdEvnt != null)
            {
                CppHelper.CloseHandle(m_hCloseTradeAnalyThrdEvnt);
            }
            m_hCloseTradeAnalyThrdEvnt = null;
            m_hCloseTradeAnalyThrd = null;
            m_ptrLogger.LogOk("END");
        }

        private void startOpenAccAnalyserThread()
        {
            m_ptrLogger.LogOk("START");
            stopOpenAccAnalyserThread();
            m_hOpenAccAnalyThrdEvnt = CppHelper.CreateEvent(true, false);
            m_iOpenAccAnalyThrd = true;
            m_hOpenAccAnalyThrd = _beginthreadex(openAccountAnalyserThread);

            m_ptrLogger.LogOk("END");
        }

        private void stopOpenAccAnalyserThread()
        {
            m_ptrLogger.LogOk("START");

            m_iOpenAccAnalyThrd = false;

            if (m_hOpenAccAnalyThrdEvnt != null)
            {
                CppHelper.SetEvent(m_hOpenAccAnalyThrdEvnt);
            }

            if (m_hOpenAccAnalyThrd != null)
            {
                CppHelper.WaitForSingleObject(m_hOpenAccAnalyThrd, CppHelper.INFINITE);
            }

            if (m_hOpenAccAnalyThrd != null)
            {
                CppHelper.CloseHandle(m_hOpenAccAnalyThrd);
            }

            if (m_hOpenAccAnalyThrdEvnt != null)
            {
                CppHelper.CloseHandle(m_hOpenAccAnalyThrdEvnt);
            }
            m_hOpenAccAnalyThrdEvnt = null;
            m_hOpenAccAnalyThrd = null;
            m_ptrLogger.LogOk("END");
        }

        private void insertCloseTradeRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("CLOSING.....");
                return;
            }
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;

            Dictionary<int, Dictionary<Guid, MT4Request>>.iterator it1;
            Dictionary<Guid, MT4Request>.iterator it2;

            lock (m_syncCloseTradeMT4RequestMap)
            {

                it1 = m_mapCloseTradeMT4Request.find(ptrOrdInfo._mt4Login);
                if (it1 == m_mapCloseTradeMT4Request.end())
                {
                    Dictionary<Guid, MT4Request> mp = new Dictionary<Guid, MT4Request>();
                    m_mapCloseTradeMT4Request.insert(new ValueTuple<int, Dictionary<Guid, MT4Request>>(ptrOrdInfo._mt4Login, mp));
                    it1 = m_mapCloseTradeMT4Request.find(ptrOrdInfo._mt4Login);
                }
                it2 = it1.second.find(ptrReq.serverTransID);
                if (it2 == it1.second.end())
                {
                    it1.second.insert(new ValueTuple<Guid, MT4Request>(ptrReq.serverTransID, ptrReq));
                }
                else
                {
                    m_ptrLogger.LogError("!!!! Duplicate Server Unique ID: %u !!!!", ptrReq.serverTransID);
                }

            }
        }

        private void insertOpenTradeRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("CLOSING.....");
                return;
            }
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;

            Dictionary<int, Dictionary<Guid, MT4Request>>.iterator it1;
            Dictionary<Guid, MT4Request>.iterator it2;

            lock (m_syncOpenTradeMT4RequestMap)
            {

                it1 = m_mapOpenTradeMT4Request.find(ptrOrdInfo._mt4Login);
                if (it1 == m_mapOpenTradeMT4Request.end())
                {
                    Dictionary<Guid, MT4Request> mp = new Dictionary<Guid, MT4Request>();
                    m_mapOpenTradeMT4Request.insert(new ValueTuple<int, Dictionary<Guid, MT4Request>>(ptrOrdInfo._mt4Login, mp));
                    it1 = m_mapOpenTradeMT4Request.find(ptrOrdInfo._mt4Login);
                }
                it2 = it1.second.find(ptrReq.serverTransID);
                if (it2 == it1.second.end() && ptrOrdInfo._orderTransMode == eMT4OrderTransMode.ORD_TRANS_OPEN)
                {
                    it1.second.insert(new ValueTuple<Guid, MT4Request>(ptrReq.serverTransID, ptrReq));
                }
                else if (it2 != it1.second.end() && ptrOrdInfo._orderTransMode == eMT4OrderTransMode.ORD_TRANS_MODIFY)
                {
                    CppHelper.free(it2.second);
                    it2.second = null;
                    it2.second = ptrReq;
                }
                else if (it2 == it1.second.end() && ptrOrdInfo._orderTransMode == eMT4OrderTransMode.ORD_TRANS_MODIFY)
                {
                    it1.second.insert(new ValueTuple<Guid, MT4Request>(ptrReq.serverTransID, ptrReq));
                }
                else
                {
                    m_ptrLogger.LogError("!!!! Duplicate Server Unique ID: %u !!!!", ptrReq.serverTransID);
                }

            }
        }

        private void insertNewAccountRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("CLOSING.....");
                return;
            }
            Dictionary<Guid, MT4Request>.iterator it2;
            lock (m_syncAccountMT4RequestMap)
            {

                it2 = m_mapAccountMT4Request.find(ptrReq.serverTransID);
                if (it2 == m_mapAccountMT4Request.end())
                {
                    m_mapAccountMT4Request.insert(new ValueTuple<Guid, MT4Request>(ptrReq.serverTransID, ptrReq));
                }
                else
                {
                    m_ptrLogger.LogError("!!!! Duplicate Server Unique ID: %u !!!!", ptrReq.serverTransID);
                }

            }
        }

        private void handleNewAccountRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            var res = ReturnCode.RET_ERROR;
            MasterUser ptrUsrInfo = (MasterUser)ptrReq.ptrData;
            UserRecord user = new UserRecord();
            //memset(&user, 0, sizeof(UserRecord));
            CppHelper.COPY_STR_S(out user.address, ptrUsrInfo._address);
            CppHelper.COPY_STR_S(out user.city, ptrUsrInfo._city);
            CppHelper.COPY_STR_S(out user.comment, ptrUsrInfo._comment);
            CppHelper.COPY_STR_S(out user.country, ptrUsrInfo._country);
            CppHelper.COPY_STR_S(out user.email, ptrUsrInfo._email);
            CppHelper.COPY_STR_S(out user.name, ptrUsrInfo._name);
            CppHelper.COPY_STR_S(out user.first_name, ptrUsrInfo._firstName);
            CppHelper.COPY_STR_S(out user.last_name, ptrUsrInfo._lastName);
            CppHelper.COPY_STR_S(out user.referee_login_id, ptrUsrInfo._refereeLoginID);
            CppHelper.COPY_STR_S(out user.password, ptrUsrInfo._password);
            CppHelper.COPY_STR_S(out user.phone, ptrUsrInfo._phone);
            CppHelper.COPY_STR_S(out user.zipcode, ptrUsrInfo._zipcode);
            user.enable = true;
            user.send_reports = true;
            user.user_color = MT4Helper.USER_COLOR_NONE;

            ////@REQ-UID@MasterLogin@
            //_snprintf(out user.comment, "@%u@%d@", ptrReq.serverTransID, ptrReq.masterLogin);

            if (ptrReq.leverage == 0)
            {
                if (m_serverIndex == eMT4ServerType.SRV_TYPE_DEMO)
                {
                    CppHelper.COPY_STR_S(out user.group, m_masterUserSetting._demoGroup);
                    user.leverage = m_masterUserSetting._demoLeverage;
                }
                else
                {
                    CppHelper.COPY_STR_S(out user.group, m_masterUserSetting._liveGroup);
                    user.leverage = m_masterUserSetting._liveLeverage;
                }
            }
            else
            {
                user.leverage = ptrReq.leverage;
                CppHelper.COPY_STR_S(out user.group, ptrReq.group);
            }


            lock (m_syncManagerNormal)
            {
                res = m_ptrMT4Normal.UserRecordNew(user);
            }


            if (res == ReturnCode.RET_OK)
            {
                Debug.Assert(user.login > 0);
                //ptrReq.newLoginOrOrderID = user.login;
                ptrReq.User = user;
                ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;
            }
            else if (res == ReturnCode.RET_OK_NONE || res == ReturnCode.RET_TRADE_TOO_MANY_REQ)
            {
                ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
            }
            else
            {
                ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
            }

            if (res != ReturnCode.RET_OK)
            {
                m_ptrLogger.LogError("TransID: %u Error in MT4 execution RetCode: %d [ %s ] ", ptrReq.serverTransID, res, m_ptrMT4Normal.ErrorDescription(res));
            }
        }

        private void handleBalanceRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;
            TradeTransInfo info = new TradeTransInfo();
            //memset(&info, 0, sizeof(TradeTransInfo));
            var res = ReturnCode.RET_ERROR;
            info.type = TradeTransType.TT_BR_BALANCE;
            info.cmd = (short)TradeCommand.OP_BALANCE;
            info.orderby = ptrOrdInfo._mt4Login;
            info.price = ptrOrdInfo._price;
            ////@REQ-UID@SIG-UID@
            //_snprintf(out info.comment, "@%u@%u@", ptrReq.serverTransID, ptrReq.signalServerTransID);

            lock (m_syncManagerNormal)
            {
                res = m_ptrMT4Normal.TradeTransaction(info);
                Debug.Assert(res != (ReturnCode)255);
            }

            if (res == ReturnCode.RET_OK)
            {
                ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;
                /*m_csQueueMarginReq.Lock();
                m_queueMarginReq.push(ptrOrdInfo->_mt4Login);
                m_csQueueMarginReq.Unlock();*/
                insertMarginRequest(ptrOrdInfo._mt4Login);
            }
            else if (res == ReturnCode.RET_TRADE_ACCEPTED || res == ReturnCode.RET_OK_NONE ||
                res == ReturnCode.RET_TRADE_PROCESS || res == ReturnCode.RET_TRADE_CONTEXT_BUSY ||
                res == ReturnCode.RET_TRADE_TOO_MANY_ORDERS || res == ReturnCode.RET_TRADE_TOO_MANY_REQ ||
                res == ReturnCode.RET_TRADE_ORDER_LOCKED)
            {
                ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
            }
            else
            {
                ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
            }
            if (res != ReturnCode.RET_OK)
            {
                m_ptrLogger.LogError("TransID: %u Error in MT4 execution RetCode: %d [ %s ] ", ptrReq.serverTransID, res, m_ptrMT4Normal.ErrorDescription(res));
            }
        }

        private void handleCloseTransRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;

            var res = ReturnCode.RET_ERROR;

            TradeRecord usrTrd;
            //memset(&usrTrd, 0, sizeof(TradeRecord));

            lock (m_syncManagerPumpEx)
            {
                res = m_ptrMT4PumpEx.TradeRecordGet(ptrOrdInfo._orderID, out usrTrd);
            }

            if (res == ReturnCode.RET_OK)
            {
                SymbolInfo si;
                //memset(&si, 0, sizeof(SymbolInfo));


                lock (m_syncManagerPumpEx)
                {
                    res = m_ptrMT4PumpEx.SymbolInfoGet(usrTrd.symbol, out si);
                }

                if (res == ReturnCode.RET_OK && si.bid != 0.0 && si.ask != 0.0)
                {
                    TradeTransInfo info = new TradeTransInfo();
                    //memset(&info, 0, sizeof(TradeTransInfo));
                    res = ReturnCode.RET_ERROR;
                    info.type = TradeTransType.TT_BR_ORDER_CLOSE;
                    info.cmd = (short)usrTrd.cmd;
                    info.orderby = usrTrd.login;
                    info.order = usrTrd.order;
                    CppHelper.memcpy(out info.symbol, usrTrd.symbol);
                    info.volume = usrTrd.volume;
                    info.price = info.cmd == (short)TradeCommand.OP_BUY ? si.bid : si.ask;
                    //if flip by server = false then
                    //info.cmd = info.cmd == OP_BUY ? OP_SELL : OP_BUY;
                    ////@REQ-UID@SIG-UID@
                    //_snprintf(out info.comment, sizeof(info.comment) - 1,  "@%u@%u@", ptrReq.serverTransID, ptrReq.signalServerTransID);

                    lock (m_syncManagerNormal)
                    {
                        res = m_ptrMT4Normal.TradeTransaction(info);
                        Debug.Assert(res != (ReturnCode)255);
                    }

                    if (res == ReturnCode.RET_OK)
                    {
                        ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;

                        if (m_serverIndex == eMT4ServerType.SRV_TYPE_DEMO)
                            insertMarginRequest(ptrOrdInfo._mt4Login);
                        else
                            lock (m_csQueueMarginReq)
                                m_queueMarginReq.Enqueue(ptrOrdInfo._mt4Login);
                    }
                    else if (res == ReturnCode.RET_TRADE_ACCEPTED || res == ReturnCode.RET_OK_NONE ||
                        res == ReturnCode.RET_TRADE_PROCESS || res == ReturnCode.RET_TRADE_CONTEXT_BUSY ||
                        res == ReturnCode.RET_TRADE_TOO_MANY_ORDERS || res == ReturnCode.RET_TRADE_TOO_MANY_REQ ||
                        res == ReturnCode.RET_TRADE_ORDER_LOCKED)
                    {
                        ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
                    }
                    else
                    {
                        ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
                    }
                }//if (res == RET_OK && si.bid != 0.0 && si.ask != 0.0)
            }//if (res == RET_OK)
            else
            {
                m_ptrLogger.LogError("Unable to get TradeRecord for closing %d", ptrOrdInfo._orderID);
                ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
            }
            if (res != ReturnCode.RET_OK)
            {
                m_ptrLogger.LogError("TransID: %u Error in MT4 execution RetCode: %d [ %s ] ", ptrReq.serverTransID, res, m_ptrMT4Normal.ErrorDescription(res));
            }
        }

        private void handleDeleteTransRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;

            var res = ReturnCode.RET_ERROR;

            TradeRecord usrTrd;
            //memset(&usrTrd, 0, sizeof(TradeRecord));

            lock (m_syncManagerPumpEx)
            {
                res = m_ptrMT4PumpEx.TradeRecordGet(ptrOrdInfo._orderID, out usrTrd);
            }

            if (res == ReturnCode.RET_OK)
            {
                TradeTransInfo info = new TradeTransInfo();
                //memset(&info, 0, sizeof(TradeTransInfo));

                info.type = TradeTransType.TT_BR_ORDER_DELETE;
                info.cmd = (short)usrTrd.cmd;
                info.orderby = usrTrd.login;
                info.order = usrTrd.order;
                CppHelper.memcpy(out info.symbol, usrTrd.symbol);
                ////@REQ-UID@SIG-UID@
                //_snprintf(out info.comment, sizeof(info.comment) - 1, "@%u@%u@", ptrReq.serverTransID, ptrReq.signalServerTransID);
                res = m_ptrMT4Normal.TradeTransaction(info);
                Debug.Assert(res != (ReturnCode)255);
                if (res == ReturnCode.RET_OK)
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;
                }
                else if (res == ReturnCode.RET_TRADE_ACCEPTED || res == ReturnCode.RET_OK_NONE ||
                    res == ReturnCode.RET_TRADE_PROCESS || res == ReturnCode.RET_TRADE_CONTEXT_BUSY ||
                    res == ReturnCode.RET_TRADE_TOO_MANY_ORDERS || res == ReturnCode.RET_TRADE_TOO_MANY_REQ ||
                    res == ReturnCode.RET_TRADE_ORDER_LOCKED)
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
                }
                else
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
                }
            }
            else
            {
                m_ptrLogger.LogError("Unable to get TradeRecord for delete %d", ptrOrdInfo._orderID);
                ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
            }
            if (res != ReturnCode.RET_OK)
            {
                m_ptrLogger.LogError("TransID: %u Error in MT4 execution RetCode: %d [ %s ] ", ptrReq.serverTransID, res, m_ptrMT4Normal.ErrorDescription(res));
            }
        }

        private void handleModifyTransRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;

            TradeRecord usrTrd;
            //memset(&usrTrd, 0, sizeof(TradeRecord));

            var res = ReturnCode.RET_ERROR;

            lock (m_syncManagerPumpEx)
            {
                res = m_ptrMT4PumpEx.TradeRecordGet(ptrOrdInfo._orderID, out usrTrd);
            }

            if (res == ReturnCode.RET_OK)
            {

                TradeTransInfo info = new TradeTransInfo();
                //memset(&info, 0, sizeof(TradeTransInfo));

                info.type = TradeTransType.TT_BR_ORDER_MODIFY;
                info.cmd = (short)usrTrd.cmd;
                info.orderby = usrTrd.login;
                info.order = ptrOrdInfo._orderID;
                CppHelper.memcpy(out info.symbol, usrTrd.symbol);
                info.volume = usrTrd.volume;
                if (info.cmd == (short)TradeCommand.OP_BUY || info.cmd == (short)TradeCommand.OP_SELL)
                {
                    info.price = usrTrd.open_price;
                }
                else
                {
                    info.price = ptrOrdInfo._price;
                }
                info.sl = ptrOrdInfo._sl;
                info.tp = ptrOrdInfo._tp;

                lock (m_syncManagerNormal)
                {
                    res = m_ptrMT4Normal.TradeTransaction(info);
                    Debug.Assert(res != (ReturnCode)255);
                }

                if (res == ReturnCode.RET_OK)
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;
                }
                else if (res == ReturnCode.RET_TRADE_ACCEPTED || res == ReturnCode.RET_OK_NONE ||
                    res == ReturnCode.RET_TRADE_PROCESS || res == ReturnCode.RET_TRADE_CONTEXT_BUSY ||
                    res == ReturnCode.RET_TRADE_TOO_MANY_ORDERS || res == ReturnCode.RET_TRADE_TOO_MANY_REQ ||
                    res == ReturnCode.RET_TRADE_ORDER_LOCKED)
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
                }
                else
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
                }
            }
            else
            {
                m_ptrLogger.LogError("Unable to get TradeRecord for modify %d", ptrOrdInfo._orderID);
                ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
            }
            if (res != ReturnCode.RET_OK)
            {
                var message =
                    $"TransID: {ptrReq.serverTransID} Error in MT4 execution RetCode: {res} [ {m_ptrMT4Normal.ErrorDescription(res)} ] ";

                if (res == ReturnCode.RET_OK_NONE)
                    m_ptrLogger.LogWarning(message);
                else
                    m_ptrLogger.LogError(message);
            }
        }

        private void handleOpenTransRequest(MT4Request ptrReq)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            var res = ReturnCode.RET_ERROR;
            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;
            if ((int)ptrOrdInfo._orderType == (int)TradeCommand.OP_BUY || (int)ptrOrdInfo._orderType == (int)TradeCommand.OP_SELL)
            {
                SymbolInfo si;
                //memset(&si, 0, sizeof(SymbolInfo));

                lock (m_syncManagerPumpEx)
                {
                    res = m_ptrMT4PumpEx.SymbolInfoGet(ptrOrdInfo._symbol, out si);
                }

                if (res == ReturnCode.RET_OK)
                {
                    TradeTransInfo info = new TradeTransInfo();
                    //memset(&info, 0, sizeof(TradeTransInfo));
                    res = ReturnCode.RET_ERROR;
                    info.type = TradeTransType.TT_BR_ORDER_OPEN;
                    info.cmd = (short)ptrOrdInfo._orderType;
                    info.orderby = ptrOrdInfo._mt4Login;
                    CppHelper.memcpy(out info.symbol, ptrOrdInfo._symbol);
                    info.volume = ptrOrdInfo._volume;
                    info.price = info.cmd == (short)TradeCommand.OP_BUY ? si.ask : si.bid;
                    info.sl = ptrOrdInfo._sl;
                    info.tp = ptrOrdInfo._tp;
                    info.comment = ptrReq.Order?.Comment;

                    ////@REQ-UID@SIG-UID@
                    //_snprintf(out info.comment, "@%u@%u@", ptrReq.serverTransID, ptrReq.signalServerTransID);

                    lock (m_syncManagerNormal)
                    {
                        res = m_ptrMT4Normal.TradeTransaction(info);
                        Debug.Assert(res != (ReturnCode)255);
                    }

                    if (res == ReturnCode.RET_OK)
                    {
                        ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;
                        ptrReq.Trade = info;
                        //ptrReq.newLoginOrOrderID = 
                        ptrOrdInfo._orderID = info.order;
                    }
                    else if (res == ReturnCode.RET_TRADE_ACCEPTED || res == ReturnCode.RET_OK_NONE ||
                        res == ReturnCode.RET_TRADE_PROCESS || res == ReturnCode.RET_TRADE_CONTEXT_BUSY ||
                        res == ReturnCode.RET_TRADE_TOO_MANY_ORDERS || res == ReturnCode.RET_TRADE_TOO_MANY_REQ ||
                        res == ReturnCode.RET_TRADE_ORDER_LOCKED)
                    {
                        ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
                    }
                    else
                    {
                        ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
                    }
                }//if (res == RET_OK)
            }//if (ptrOrdInfo._orderType == OP_BUY || ptrOrdInfo._orderType == OP_SELL)
            else
            {
                TradeTransInfo info = new TradeTransInfo();
                //memset(&info, 0, sizeof(TradeTransInfo));
                res = ReturnCode.RET_ERROR;
                info.type = TradeTransType.TT_BR_ORDER_OPEN;
                info.cmd = (short)ptrOrdInfo._orderType;
                info.orderby = ptrOrdInfo._mt4Login;
                CppHelper.memcpy(out info.symbol, ptrOrdInfo._symbol);
                info.volume = ptrOrdInfo._volume;
                info.price = ptrOrdInfo._price;
                info.sl = ptrOrdInfo._sl;
                info.tp = ptrOrdInfo._tp;
                ////@REQ-UID@SIG-UID@
                //_snprintf(out info.comment, /*"%u",*/ "@%u@%u@", ptrReq.serverTransID, ptrReq.signalServerTransID);

                lock (m_syncManagerNormal)
                {
                    res = m_ptrMT4Normal.TradeTransaction(info);
                    Debug.Assert(res != (ReturnCode)255);
                }

                if (res == ReturnCode.RET_OK)
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_EXECUTED;
                    ptrReq.Trade = info;
                    //ptrReq.newLoginOrOrderID =
                    ptrOrdInfo._orderID = info.order;
                }
                else if (res == ReturnCode.RET_TRADE_ACCEPTED || res == ReturnCode.RET_OK_NONE ||
                    res == ReturnCode.RET_TRADE_PROCESS || res == ReturnCode.RET_TRADE_CONTEXT_BUSY ||
                    res == ReturnCode.RET_TRADE_TOO_MANY_ORDERS || res == ReturnCode.RET_TRADE_TOO_MANY_REQ ||
                    res == ReturnCode.RET_TRADE_ORDER_LOCKED)
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_PROCESSING;
                }
                else
                {
                    ptrReq.status = eOrderStatus.ORD_STAT_REJECTED;
                }
            }//else of if (ptrOrdInfo._orderType == OP_BUY || ptrOrdInfo._orderType == OP_SELL)

            if (res != ReturnCode.RET_OK)
            {
                m_ptrLogger.LogError("TransID: %u Error in MT4 execution RetCode: %d [ %s ] ", ptrReq.serverTransID, res, m_ptrMT4Normal.ErrorDescription(res));
                ptrReq.mt4errorcode = res;
                CppHelper.strcpy(out ptrReq.mt4error, m_ptrMT4Normal.ErrorDescription(res));
            }
        }

        private uint normalManagerThread()
        {
            var isTriedToConnect = false;
            m_ptrLogger.LogOk("START");
            var waitMiliSec = m_PingIntervalSec * 1000;
            while (m_iNormalMgrThread && !m_isClosing)
            {
                if (!m_iNormalMgrThread)
                {
                    break;
                }//if( !m_iNormalMgrThread )
                var retStatus = CppHelper.WaitForSingleObject(m_hNormalMgrThrdEvnt, waitMiliSec);
                if (retStatus == WaitResult.WAIT_TIMEOUT || retStatus == WaitResult.WAIT_OBJECT_0)
                {
                    if (m_isClosing)
                    {
                        break;
                    }//if( !m_iNormalMgrThread )
                    if (!m_iNormalMgrThread)
                    {
                        break;
                    }//if( !m_iNormalMgrThread )

                    if (m_ptrMT4Normal != null)
                    {
                        ReturnCode result;

                        if (isTriedToConnect)
                            lock (m_syncManagerNormal)
                            {
                                result = m_ptrMT4Normal.Ping();
                                Debug.Assert(result == ReturnCode.RET_OK);
                            }
                        else
                            result = ReturnCode.RET_NO_CONNECT;

                        if (result != ReturnCode.RET_OK)
                        {
                            m_IsNormalMgrConnected = false;
                            //m_isError = true;

                            if (isTriedToConnect)
                                m_ptrLogger.LogError("Pinging is not OK. Disconnecting: %s", m_ptrMT4Normal.ErrorDescription(result));

                            if (m_ptrMT4Normal.IsConnected())
                            {
                                m_ptrMT4Normal.Disconnect();
                            }
                            CppHelper.Sleep(100);

                            var res = ReturnCode.RET_ERROR;

                            m_ptrLogger.LogInfo("Server: %s Login: %d ", m_tradeServerURL, m_tradeLogin);
                            res = m_ptrMT4Normal.Connect(m_tradeServerURL);
                            isTriedToConnect = true;
                            if (res == ReturnCode.RET_OK)
                            {
                                m_ptrLogger.LogInfo("Normal manager is connected");
                                res = ReturnCode.RET_ERROR;
                                res = m_ptrMT4Normal.Login(m_tradeLogin, m_tradePassword);
                                if (res != ReturnCode.RET_OK)
                                {
                                    m_ptrLogger.LogError("Unable to login due to " +
                                        "Error: %d - %s", res, m_ptrMT4Normal.ErrorDescription(res));
                                }
                                else
                                {
                                    m_ptrLogger.LogInfo("Manager successfully login");
                                    m_IsNormalMgrConnected = true;
                                    ////////////////////////////////////////////////////////
                                    if (!m_isAllSymbolAddedInMgr)
                                    {
                                        m_ptrLogger.LogOk("Adding Symbols");
                                        m_ptrMT4Normal.SymbolsRefresh();
                                        m_isAllSymbolAddedInMgr = true;

                                        var ptrSymbol = m_ptrMT4Normal.SymbolsGetAll();
                                        var iTotal = ptrSymbol.Count;
                                        if (iTotal > 0)
                                        {
                                            for (int iLoop = 0; iLoop < iTotal && !m_isClosing; iLoop++)
                                            {
                                                var res2 = m_ptrMT4Normal.SymbolAdd(ptrSymbol[iLoop].symbol);
                                                if (res2 != ReturnCode.RET_OK)
                                                {
                                                    m_ptrLogger.LogWarning("Unable to add symbol " +

                                                        "Error %d - %s \n", res2, m_ptrMT4Normal.ErrorDescription(res2));
                                                }
                                            }
                                            if (m_serverIndex == eMT4ServerType.SRV_TYPE_DEMO)
                                            {
                                                if (m_ptrMT4Normal.SymbolsGroupsGet(MT4Helper.MAX_SEC_GROUPS, out var securities) == ReturnCode.RET_OK)
                                                {
                                                    m_fptrSymbolInfo(securities, ptrSymbol);
                                                }
                                            }
                                            //m_ptrMT4Normal.MemFree(ptrSymbol);
                                        }

                                        m_ptrLogger.LogOk("Total Symbol %d", iTotal);
                                        startPumpingManager();
                                        startPumpingExManager();
                                    }
                                    //////////////////////////////////////////////////////////
                                }
                            }//if( res==RET_OK )
                            else
                            {
                                m_ptrLogger.LogError("Unable to connect due to " +
                                    "Error: %d - %s", res, m_ptrMT4Normal.ErrorDescription(res));
                            }
                        }//if( result != RET_OK  )
                    }//if( m_ptrManager != null )
                }//if( WaitForSingleObject( m_hNormalManagerThreadEvent , waitMiliSec ) == WAIT_TIMEOUT )
                if (m_PingIntervalSec != 9)
                {
                    m_PingIntervalSec = 9;
                    waitMiliSec = m_PingIntervalSec * 1000;
                }//if( m_PingIntervalSec !=  9 )
                if (!m_IsNormalMgrConnected)
                {
                    m_PingIntervalSec = 2;
                    waitMiliSec = m_PingIntervalSec * 1000;
                }//if( !m_IsNormalMgrConnected )
            }//while( m_iNormalMgrThread )

            m_ptrLogger.LogOk("END");
            return 0;
        }

        private uint dbManagerThread()
        {
            var isTriedToConnect = false;
            Queue<int> tempQueue = new Queue<int>();
            List<int> tempVecMT4Login = new List<int>();
            //HashSet<int> setTotalMT4Login = new HashSet<int>();
            //HashSet<int>.iterator itSetInt;
            //List<int>.iterator itVecInt;
            //MarginLevel mrLvl;
            m_ptrLogger.LogOk("START");
            var waitMiliSec = 9 * 1000;
            DateTimeOffset? lastTime = null;
            while (m_iDBMgrThread && !m_isClosing)
            {
                if (!m_iDBMgrThread)
                {
                    break;
                }//if( !m_iDBMgrThread )
                var retStatus = CppHelper.WaitForSingleObject(m_hDBMgrThrdEvnt, waitMiliSec);
                if (retStatus == WaitResult.WAIT_TIMEOUT || retStatus == WaitResult.WAIT_OBJECT_0)
                {
                    if (m_isClosing)
                    {
                        break;
                    }//if( !m_iDBMgrThread )
                    if (!m_iDBMgrThread)
                    {
                        break;
                    }//if( !m_iDBMgrThread )

                    if (m_ptrManagerAPI.m_DBMgr != null)
                    {
                        ReturnCode result;

                        if (isTriedToConnect)
                        {
                            result = m_ptrManagerAPI.m_DBMgr.Ping();
                            Debug.Assert(result == ReturnCode.RET_OK);
                        }
                        else
                            result = ReturnCode.RET_NO_CONNECT;

                        if (result != ReturnCode.RET_OK)
                        {
                            if (isTriedToConnect)
                                m_ptrLogger.LogError("DB Pinging is not OK. Disconnecting: %s", m_ptrManagerAPI.m_DBMgr.ErrorDescription(result));

                            if (m_ptrManagerAPI.m_DBMgr.IsConnected())
                            {
                                m_ptrManagerAPI.m_DBMgr.Disconnect();
                            }
                            CppHelper.Sleep(100);

                            var res = ReturnCode.RET_ERROR;

                            m_ptrLogger.LogInfo("Server: %s Login: %d ", m_tradeServerURL, m_tradeLogin);
                            res = m_ptrManagerAPI.m_DBMgr.Connect(m_tradeServerURL);
                            isTriedToConnect = true;
                            if (res == ReturnCode.RET_OK)
                            {
                                m_ptrLogger.LogInfo("DB manager is connected");
                                res = ReturnCode.RET_ERROR;
                                res = m_ptrManagerAPI.m_DBMgr.Login(m_tradeLogin, m_tradePassword);
                                if (res != ReturnCode.RET_OK)
                                {
                                    m_ptrLogger.LogError("Unable to login due to " +
                                        "Error: %d - %s", res, m_ptrManagerAPI.m_DBMgr.ErrorDescription(res));
                                }
                                else
                                {
                                    m_ptrLogger.LogInfo("DB Manager successfully login");
                                }
                            }//if( res==RET_OK )
                            else
                            {
                                m_ptrLogger.LogError("DB Unable to connect due to " +
                                    "Error: %d - %s", res, m_ptrManagerAPI.m_DBMgr.ErrorDescription(res));
                            }
                        }//if( result != RET_OK  )
                        else
                        {

                            //////////////////////////////////////////////////////////////////////
                            lock (m_csQueueMarginReq)
                            {
                                while (!m_queueMarginReq.empty() && !m_isClosing)
                                {
                                    tempQueue.Enqueue(m_queueMarginReq.front());
                                    m_queueMarginReq.pop();
                                }
                            }
                            //////////////////////////////////////////////////////////////////////
                            //lock (m_csMt4Accounts)
                            //{
                            //    for (itSetInt = m_setMt4Accounts.begin(); itSetInt != m_setMt4Accounts.end(); itSetInt++)
                            //    {
                            //        if (setTotalMT4Login.find(itSetInt) == setTotalMT4Login.end())
                            //        {
                            //            setTotalMT4Login.insert(itSetInt);
                            //        }
                            //    }
                            //}
                            var setTotalMT4Login = _events.GetAllMT4Logins(m_serverIndex);
                            //////////////////////////////////////////////////////////////////////
                            while (!tempQueue.empty() && !m_isClosing)
                            {
                                int mt4Login = tempQueue.front();
                                //memset(&mrLvl, 0, sizeof(MarginLevel));
                                //mrLvl.login = mt4Login;
                                var res = m_ptrManagerAPI.m_DBMgr.MarginLevelRequest(mt4Login, out var mrLvl);
                                CppHelper.Sleep(0);
                                if (res == ReturnCode.RET_OK)
                                {
                                    Debug.Assert(mrLvl != null && mrLvl.login == mt4Login);
                                    m_fptrOnMargin(m_serverIndex, mrLvl);
                                }
                                else
                                {
                                    tempVecMT4Login.push_back(mt4Login);
                                }
                                tempQueue.pop();
                            }
                            //////////////////////////////////////////////////////////////////////
                            int totalUsers = 0;
                            int totalRcrds = 0;
                            var fromTime = m_ptrManagerAPI.m_DBMgr.ServerTime();
                            Debug.Assert(fromTime != null);
                            //for (itSetInt = setTotalMT4Login.begin(); itSetInt != setTotalMT4Login.end(); itSetInt++)
                            foreach (var itSetInt in setTotalMT4Login)
                            {
                                var arrTrades = m_ptrManagerAPI.m_DBMgr.TradesUserHistory(itSetInt, lastTime, fromTime);
                                totalRcrds = arrTrades.Count;
                                if (totalRcrds > 0 && arrTrades != null)
                                {
                                    for (int iTradeLoop = 0; iTradeLoop < totalRcrds && !m_isClosing; iTradeLoop++)
                                    {
                                        m_fptrOnTrade(m_serverIndex, arrTrades[iTradeLoop], TransType.TRANS_DELETE);
                                    }
                                    //m_ptrManagerAPI.m_DBMgr.MemFree(arrTrades);
                                    arrTrades = null;
                                }

                                var res = m_ptrManagerAPI.m_DBMgr.MarginLevelRequest(itSetInt, out var mrLvl);

                                if (res == ReturnCode.RET_OK)
                                {
                                    m_fptrOnMargin(m_serverIndex, mrLvl);
                                }
                                else
                                {
                                    tempVecMT4Login.push_back(itSetInt);
                                }

                                CppHelper.Sleep(1);
                            }
                            totalUsers = tempVecMT4Login.size();
                            if (totalUsers != 0)
                            {
                                var arrUsrs = m_ptrManagerAPI.m_DBMgr.UserRecordsRequest(tempVecMT4Login);
                                totalUsers = arrUsrs.Count;
                                if (totalUsers > 0 && arrUsrs != null)
                                {
                                    for (int iUser = 0; iUser < totalUsers && !m_isClosing; iUser++)
                                    {
                                        var mrLvl = new MarginLevel();
                                        //memset(&mrLvl, 0, sizeof(MarginLevel));
                                        mrLvl.login = arrUsrs[iUser].login;
                                        mrLvl.balance = arrUsrs[iUser].balance;
                                        mrLvl.equity = arrUsrs[iUser].prevequity;
                                        m_fptrOnMargin(m_serverIndex, mrLvl);
                                    }
                                    //m_ptrManagerAPI.m_DBMgr.MemFree(arrUsrs);
                                    arrUsrs = null;
                                }
                            }
                            tempVecMT4Login.Clear();
                            lastTime = fromTime;
                        }
                    }//if( m_ptrManager != null )
                }//if( WaitForSingleObject( m_hDBManagerThreadEvent , waitMiliSec ) == WAIT_TIMEOUT )
            }//while( m_iDBMgrThread )

            m_ptrLogger.LogOk("END");
            return 0;
        }

        private uint pumpManagerExThread()
        {
            m_ptrLogger.LogOk("START");
            while (m_iPumpExMgr && !m_isClosing)
            {
                CppHelper.WaitForSingleObject(m_hPumpExMgrEvnt, CppHelper.INFINITE);
                m_ptrLogger.LogInfo("Try to connecting pumpEx manager");
                if (!m_iPumpExMgr || m_isClosing)
                {
                    break;
                }
                while (m_ptrMT4PumpEx.IsConnected() != true && !m_isClosing)
                {
                    //m_isError = true;
                    if (!m_iPumpExMgr)
                    {
                        break;
                    }
                    initPumpExManager();
                    CppHelper.Sleep(3000);
                    if (!m_iPumpExMgr)
                    {
                        break;
                    }
                }
            }
            m_ptrLogger.LogOk("END");
            return 0;
        }

        private uint pumpManagerThread()
        {
            m_ptrLogger.LogOk("START");
            while (m_iPumpMgr && !m_isClosing)
            {
                CppHelper.WaitForSingleObject(m_hPumpMgrEvnt, CppHelper.INFINITE);
                m_ptrLogger.LogInfo("Try to connecting pump manager");
                if (!m_iPumpMgr || m_isClosing)
                {
                    break;
                }
                while (m_ptrMT4Pump.IsConnected() != true && !m_isClosing)
                {
                    //m_isError = true;
                    if (!m_iPumpMgr)
                    {
                        break;
                    }
                    initPumpManager();
                    CppHelper.Sleep(3000);
                    if (!m_iPumpMgr)
                    {
                        break;
                    }
                }
            }
            m_ptrLogger.LogOk("END");
            return 0;
        }

        private uint mt4RequestThread()
        {
            Queue<MT4Request> tempQueue = new Queue<MT4Request>();
            //Dictionary<int, Dictionary<uint, MT4Request>>.iterator it1;
            //Dictionary<uint, MT4Request>.iterator it2;

            m_ptrLogger.LogOk("START");

            while (m_iMT4ReqThrd && !m_isClosing)
            {
                CppHelper.WaitForSingleObject(m_hMT4ReqThrdEvnt, CppHelper.INFINITE);
                if (!m_iMT4ReqThrd || m_isClosing)
                {
                    break;
                }
                lock (m_syncMT4RequestQueue)
                {
                    while (!m_queueMT4Request.empty())
                    {
                        tempQueue.Enqueue(m_queueMT4Request.front());
                        m_queueMT4Request.pop();
                    }
                }

                while (!tempQueue.empty() && !m_isClosing)
                {

                    MT4Request ptrReq = tempQueue.front();
                    switch (ptrReq.reqType)
                    {
                        case MT4REQ.MT4REQ_NEW_ACCOUNT:
                            {
                                m_ptrLogger.LogTrade("New Account Request TransID: %u MasterLogin: %d", ptrReq.serverTransID, ptrReq.masterLogin);
                                handleNewAccountRequest(ptrReq);
                                if (ptrReq.status != eOrderStatus.ORD_STAT_EXECUTED)
                                {
                                    insertNewAccountRequest(ptrReq);
                                }
                            }
                            break;
                        case MT4REQ.MT4REQ_BALANCE:
                            {
                                MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;
                                m_ptrLogger.LogTrade("Balance Request TransID: %u MasterLogin: %d MT4 Login: %d", ptrReq.serverTransID, ptrReq.masterLogin, ptrOrdInfo._mt4Login);
                                handleBalanceRequest(ptrReq);
                                if (ptrReq.status != eOrderStatus.ORD_STAT_EXECUTED)
                                {
                                    insertCloseTradeRequest(ptrReq);
                                }
                            }
                            break;
                        case MT4REQ.MT4REQ_TRADE:
                            {
                                MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrReq.ptrData;
                                switch (ptrOrdInfo._orderTransMode)
                                {
                                    case eMT4OrderTransMode.ORD_TRANS_CLOSE:
                                        {
                                            m_ptrLogger.LogTrade("Trade Close Request TransID: %u MasterLogin: %d MT4 Login: %d OrderID: %d", ptrReq.serverTransID, ptrReq.masterLogin, ptrOrdInfo._mt4Login, ptrOrdInfo._orderID);
                                            handleCloseTransRequest(ptrReq);
                                            //ptrReq.newLoginOrOrderID = ptrOrdInfo._orderID;
                                            Debug.Assert(ptrOrdInfo._orderID > 0);
                                            if (ptrReq.status != eOrderStatus.ORD_STAT_EXECUTED)
                                            {
                                                insertCloseTradeRequest(ptrReq);
                                            }
                                        }
                                        break;
                                    case eMT4OrderTransMode.ORD_TRANS_DELETE:
                                        {
                                            m_ptrLogger.LogTrade("Trade Delete Request TransID: %u MasterLogin: %d MT4 Login: %d OrderID: %d", ptrReq.serverTransID, ptrReq.masterLogin, ptrOrdInfo._mt4Login, ptrOrdInfo._orderID);
                                            handleDeleteTransRequest(ptrReq);
                                            //ptrReq.newLoginOrOrderID = ptrOrdInfo._orderID;
                                            Debug.Assert(ptrOrdInfo._orderID > 0);
                                            if (ptrReq.status != eOrderStatus.ORD_STAT_EXECUTED)
                                            {
                                                insertCloseTradeRequest(ptrReq);
                                            }
                                        }
                                        break;
                                    case eMT4OrderTransMode.ORD_TRANS_MODIFY:
                                        {
                                            m_ptrLogger.LogTrade("Trade Modify Request TransID: %u MasterLogin: %d MT4 Login: %d OrderID: %d", ptrReq.serverTransID, ptrReq.masterLogin, ptrOrdInfo._mt4Login, ptrOrdInfo._orderID);
                                            handleModifyTransRequest(ptrReq);
                                            //ptrReq.newLoginOrOrderID = ptrOrdInfo._orderID;
                                            Debug.Assert(ptrOrdInfo._orderID > 0);
                                            if (ptrReq.status != eOrderStatus.ORD_STAT_EXECUTED)
                                            {
                                                insertOpenTradeRequest(ptrReq);
                                            }
                                        }
                                        break;
                                    case eMT4OrderTransMode.ORD_TRANS_OPEN:
                                        {
                                            m_ptrLogger.LogTrade("Trade Open Request TransID: %u MasterLogin: %d MT4 Login: %d", ptrReq.serverTransID, ptrReq.masterLogin, ptrOrdInfo._mt4Login);
                                            handleOpenTransRequest(ptrReq);
                                            //ptrReq.newLoginOrOrderID = ptrOrdInfo._orderID;
                                            Debug.Assert(ptrOrdInfo._orderID > 0);
                                            if (ptrReq.status != eOrderStatus.ORD_STAT_EXECUTED)
                                            {
                                                insertOpenTradeRequest(ptrReq);
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    if (ptrReq.status == eOrderStatus.ORD_STAT_EXECUTED)
                    {
                        try
                        {
                            m_fptrMT4ReqResponse(ptrReq.mt4errorcode, ptrReq.mt4error, m_serverIndex, ptrReq.reqType, ptrReq.status, ptrReq.masterLogin, /*ptrReq.newLoginOrOrderID,*/ /*ptrReq.socketID,*/ ptrReq.serverTransID, ptrReq.accType, ptrReq.requestMode, ptrReq);
                        }
                        catch (Exception exception)
                        {
                            m_ptrLogger.LogError("Unknown exception", exception);
                        }
                        CppHelper.free(ptrReq.ptrData);
                        ptrReq.ptrData = null;
                        CppHelper.free(ptrReq);

                        //ptrReq = null;
                    }
                    ptrReq.NotifyCompleted();
                    tempQueue.pop();
                }
            }
            //while (!tempQueue.empty())
            //{
            //    //Before free insert in DB
            //    free(tempQueue.front().ptrData);
            //    tempQueue.front().ptrData = null;
            //    free(tempQueue.front());
            //    tempQueue.front() = null;
            //    tempQueue.pop();
            //}
            tempQueue.Clear();
            m_ptrLogger.LogOk("END");
            return 0;
        }

        private void pumpSwitchEx(PumpCode code, TransType type, object data)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            switch (code)
            {

                case PumpCode.PUMP_UPDATE_BIDASK:
                    {
                        if (m_IsPumpExMgrConnected == false)
                        {
                            m_IsPumpExMgrConnected = true;
                        }

                        if (m_serverIndex == eMT4ServerType.SRV_TYPE_DEMO)
                        {
                            //memset(m_symbolMarketData, 0, sizeof(SymbolInfo) * 32);
                            //---
                            System.Collections.Generic.IReadOnlyList<SymbolInfo> m_symbolMarketData;
                            while ((m_symbolMarketData = m_ptrMT4PumpEx.SymbolInfoUpdated(32)).Count > 0 && !m_isClosing)
                            {
                                m_fptrMarketData(m_symbolMarketData);
                            }
                        }
                    }
                    break;
                case PumpCode.PUMP_STOP_PUMPING:
                    {
                        m_IsPumpExMgrConnected = false;
                        CppHelper.SetEvent(m_hPumpExMgrEvnt);
                        m_ptrLogger.LogWarning("Manager PumpEx stopped");
                    }
                    break;
                case PumpCode.PUMP_START_PUMPING:
                    {
                        m_ptrLogger.LogInfo("Manager PumpEx started");
                    }
                    break;
                case PumpCode.PUMP_UPDATE_TRADES:
                    {
                        TradeRecord ptrTrade = (TradeRecord)data;
                        if (data == null)
                        {
                            break;
                        }
                        m_fptrOnTrade(m_serverIndex, ptrTrade, type);
                        if (type == TransType.TRANS_ADD)
                        {
                            //char comment[32];
                            //uint reqUid, sigUid;
                            //reqUid = sigUid = 0;
                            //memset(comment, 0, 32);
                            //memcpy(out var comment, ptrTrade.comment);
                            if (getValueFromComment(ptrTrade.comment, out var reqUid, out var sigUid))
                            {
                                if (reqUid != Guid.Empty)
                                {
                                    var pr1 = (ptrTrade.login, ptrTrade.order);
                                    var pr2 = (reqUid, pr1);

                                    lock (m_syncVecOpenTradeResp)
                                    {
                                        m_vecOpenTradeResp.push_back(pr2);
                                    }
                                }
                            }
                        }
                        else if (type == TransType.TRANS_DELETE)
                        {

                        }
                        /*else if (type == TRANS_UPDATE)
                        {

                        }*/

                    }
                    break;
                default:
                    break;
            }
        }

        private void pumpSwitch(PumpCode code)
        {
            if (m_isClosing)
            {
                m_ptrLogger.LogOk("Closing....");
                return;
            }
            switch (code)
            {
                case PumpCode.PUMP_UPDATE_BIDASK:
                    {
                        if (m_IsPumpMgrConnected == false)
                        {
                            m_IsPumpMgrConnected = true;
                        }
                    }
                    break;
                case PumpCode.PUMP_STOP_PUMPING:
                    {
                        m_IsPumpMgrConnected = false;
                        CppHelper.SetEvent(m_hPumpMgrEvnt);
                        m_ptrLogger.LogError("Manager Pump stopped");
                    }
                    break;
                case PumpCode.PUMP_START_PUMPING:
                    {
                        m_ptrLogger.LogInfo("Manager Pump started");
                    }
                    break;
                case PumpCode.PUMP_UPDATE_TRADES:
                    {
                        //TradeRecord openTrades = null;
                        //int total = -1;
                        DateTimeOffset currentTime;

                        lock (m_syncManagerNormal)
                        {
                            Debug.Assert(m_ptrMT4Normal.IsConnected());
                            var serverTime = m_ptrMT4Normal.ServerTime();
                            Debug.Assert(m_ptrMT4Normal.IsConnected());
                            Debug.Assert(serverTime != null);
                            currentTime = serverTime.Value;
                        }

                        currentTime = currentTime - TimeSpan.FromSeconds(10);//(1 * 10); // 60 seconds back
                        var openTrades = m_ptrMT4Pump.TradesGet();
                        var total = openTrades.Count;
                        if (openTrades != null)
                        {
                            //char comment[32];
                            //uint reqUid, sigUid;
                            //reqUid = sigUid = 0;
                            //memset(comment, 0, 32);

                            lock (m_syncVecOpenTradeResp)
                            {

                                for (int iTradeLoop = total - 1; iTradeLoop >= 0; iTradeLoop--)
                                {
                                    if (openTrades[iTradeLoop].open_time >= currentTime /*|| openTrades[iTradeLoop].timestamp > currentTime*/)
                                    {
                                        //memcpy(out var comment, openTrades[iTradeLoop].comment);
                                        if (getValueFromComment(openTrades[iTradeLoop].comment, out var reqUid, out var sigUid))
                                        {
                                            if (reqUid != Guid.Empty)
                                            {
                                                var pr1 = (openTrades[iTradeLoop].login, openTrades[iTradeLoop].order);
                                                var pr2 = (reqUid, pr1);

                                                m_vecOpenTradeResp.push_back(pr2);

                                            }
                                        }
                                        //memset(comment, 0, 32);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                    //m_fptrOnTrade(m_serverIndex, &(openTrades[iTradeLoop]), -1);
                                    if (iTradeLoop % 10 == 0)
                                    {
                                        CppHelper.Sleep(1);
                                    }
                                }

                            }

                            //m_ptrMT4Pump.MemFree(openTrades);
                            openTrades = null;
                            total = 0;
                        }
                    }
                    break;
                case PumpCode.PUMP_UPDATE_USERS:
                    {
                        //UserRecord* usrRcrd = null;
                        //int total = -1;
                        DateTimeOffset currentTime;

                        lock (m_syncManagerNormal)
                        {
                            Debug.Assert(m_ptrMT4Normal.IsConnected());
                            var serverTime = m_ptrMT4Normal.ServerTime();
                            Debug.Assert(m_ptrMT4Normal.IsConnected());
                            Debug.Assert(serverTime != null);
                            currentTime = serverTime.Value;
                        }

                        currentTime = currentTime - TimeSpan.FromSeconds(2 * 60); // 1 min back
                        var usrRcrd = m_ptrMT4Pump.UsersGet();
                        var total = usrRcrd.Count;
                        if (usrRcrd != null)
                        {
                            //char comment[64];
                            //uint reqUid, sigUid;
                            //reqUid = sigUid = 0;
                            //memset(comment, 0, 64);
                            for (int iUserLoop = total - 1; iUserLoop >= 0; iUserLoop--)
                            {
                                if (usrRcrd[iUserLoop].enable)
                                {
                                    if (usrRcrd[iUserLoop].regdate > currentTime || usrRcrd[iUserLoop].timestamp > currentTime)
                                    {
                                        //@REQ-UID@MasterLogin@
                                        var comment = usrRcrd[iUserLoop].comment; // added by Alexey and Claudia after comparison with PUMP_UPDATE_TRADES case (because comment was always empty)
                                        if (getValueFromComment(comment, out var reqUid, out var sigUid))
                                        {
                                            if (reqUid != Guid.Empty && sigUid != 0)
                                            {
                                                var pr1 = (usrRcrd[iUserLoop].login, (int)sigUid);
                                                var pr2 = (reqUid, pr1);
                                                lock (m_syncVecNewAccResp)
                                                {
                                                    m_vecNewAccResp.push_back(pr2);
                                                }
                                            }
                                        }
                                        //memset(comment, 0, 64);
                                    }
                                }
                            }
                            //m_ptrMT4Pump.MemFree(usrRcrd);
                            usrRcrd = null;
                            total = 0;
                        }

                    }
                    break;
                default:
                    break;

            }
        }

        private uint openTradeAnalyserThread()
        {
            var waitMiliSec = m_iOpenTradeAnalyThrdPeriod * 1000;
            List<ValueTuple<Guid, ValueTuple<int, int>>> vec = new List<(Guid, (int, int))>();
            Dictionary<int, Dictionary<Guid, MT4Request>>.iterator it1;
            Dictionary<Guid, MT4Request>.iterator it2;
            List<ValueTuple<Guid, ValueTuple<int, int>>>.iterator it3;
            List<Guid> vecRequestRemove = new List<Guid>();
            List<Guid>.iterator it4;

            while (m_iOpenTradeAnalyThrd && !m_isClosing)
            {
                if (CppHelper.WaitForSingleObject(m_hOpenTradeAnalyThrdEvnt, waitMiliSec) == WaitResult.WAIT_TIMEOUT)
                {
                    if (!m_iOpenTradeAnalyThrd || m_isClosing)
                    {
                        break;
                    }

                    vec.Clear();
                    //Filling open order id and request id [UID-LOGIN-ORDERID]
                    lock (m_syncVecOpenTradeResp)
                    {
                        for (it3 = m_vecOpenTradeResp.begin(); it3 != m_vecOpenTradeResp.end(); it3++)
                        {
                            vec.push_back(it3);
                        }
                        m_vecOpenTradeResp.Clear();
                    }

                    //Checking all open order status
                    lock (m_syncOpenTradeMT4RequestMap)
                    {
                        for (it3 = vec.begin(); it3 != vec.end(); it3++)
                        {
                            it1 = m_mapOpenTradeMT4Request.find(it3.second().first());
                            if (it1 != m_mapOpenTradeMT4Request.end())
                            {
                                it2 = it1.second.find(it3.first());
                                if (it2 != it1.second.end())
                                {
                                    it2.second.status = eOrderStatus.ORD_STAT_EXECUTED;
                                    try
                                    {
                                        m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*it3.second().second(),*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                    }
                                    catch (Exception exception)
                                    {
                                        m_ptrLogger.LogError("Unknown exception", exception);
                                    }
                                    CppHelper.free(it2.second.ptrData);
                                    it2.second.ptrData = null;
                                    CppHelper.free(it2.second);
                                    it2.second = null;
                                    it1.second.erase(it2);
                                    if (it1.second.size() == 0)
                                    {
                                        m_mapOpenTradeMT4Request.erase(it1);
                                    }
                                }//if (it2 != it1.second.end())
                            }//if (it1 != m_mapOpenTradeMT4Request.end())
                        }//for (it3 = vec.begin(); it3 != vec.end(); it3++)
                    }


                    lock (m_syncOpenTradeMT4RequestMap)
                    {
                        for (it1 = m_mapOpenTradeMT4Request.begin(); it1 != m_mapOpenTradeMT4Request.end(); it1++)
                        {
                            vecRequestRemove.Clear();
                            for (it2 = it1.second.begin(); it2 != it1.second.end(); it2++)
                            {
                                DateTimeOffset currTime = DateTimeOffset.UtcNow;
                                var timespan = currTime - it2.second.reqInsertTime;
                                if (timespan > MAX_WAIT_TIME_IN_SEC)
                                {
                                    vecRequestRemove.push_back(it2.first);
                                    it2.second.status = eOrderStatus.ORD_STAT_REJECTED;
                                    try
                                    {
                                        m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*-1,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                    }
                                    catch (Exception exception)
                                    {
                                        m_ptrLogger.LogError("Unknown exception", exception);
                                    }
                                    continue;
                                }
                                timespan = currTime - it2.second.queryTime;
                                if (timespan < CHECK_INTERVAL_LIMIT_SEC)
                                {
                                    //it already handled wait for next time bye dear
                                    continue;
                                }
                                it2.second.queryTime = currTime;
                                handleOpenTransRequest(it2.second);
                                if (it2.second.status == eOrderStatus.ORD_STAT_EXECUTED)
                                {
                                    vecRequestRemove.push_back(it2.first);
                                    try
                                    {
                                        m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*it2.second.newLoginOrOrderID,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                    }
                                    catch (Exception exception)
                                    {
                                        m_ptrLogger.LogError("Unknown exception", exception);
                                    }
                                }
                            }//for (it2 = it1.second.begin(); it2 != it1.second.end(); it2++)
                            for (it4 = vecRequestRemove.begin(); it4 != vecRequestRemove.end(); it4++)
                            {
                                it2 = it1.second.find(it4);
                                if (it2 != it1.second.end())
                                {
                                    CppHelper.free(it2.second.ptrData);
                                    it2.second.ptrData = null;
                                    CppHelper.free(it2.second);
                                    it2.second = null;
                                    it1.second.erase(it2);
                                }
                            }//for (it4 = vecRequestRemove.begin(); it4 != vecRequestRemove.end(); it4++)
                        }//for (it1 = m_mapOpenTradeMT4Request.begin(); it1 != m_mapOpenTradeMT4Request.end(); it1++)
                    }
                }//if (WaitForSingleObject(m_hOpenTradeAnalyThrdEvnt, waitMiliSec) == WAIT_TIMEOUT)
            }//while (m_iOpenTradeAnalyThrd && !m_isClosing)
            return 0;
        }

        private bool _traceUpdateDataThread = true;

        //private uint updateDataThread()
        //{
        //    var waitMiliSec = m_iUpdateDataThrdPeriod * 1000;
        //    //TradeRecord openTrades = null;
        //    //MarginLevel ptrMarginLvl = null;
        //    //MarginLevel mrLvl;
        //    //int total = -1;
        //    int iLoop = 0;
        //    //Queue<int> tempQueue;
        //    while (m_iUpdateDataThrd && !m_isClosing)
        //    {
        //        if (WaitForSingleObject(m_hUpdateDataThrdEvnt, waitMiliSec) == WAIT_TIMEOUT)
        //        {
        //            if (!m_iUpdateDataThrd || m_isClosing)
        //            {
        //                break;
        //            }
        //            if (m_IsPumpMgrConnected)
        //            {
        //                //total = -1;
        //                if (_traceUpdateDataThread) m_ptrLogger.LogOk("Begin TradesGet");
        //                var openTrades = m_ptrMT4Pump.TradesGet();
        //                if (_traceUpdateDataThread) m_ptrLogger.LogOk($"End TradesGet: count = {openTrades.Count}");
        //                var total = openTrades.Count;

        //                if (openTrades != null && total > 0)
        //                {
        //                    for (iLoop = 0; iLoop < total && !m_isClosing; iLoop++)
        //                    {
        //                        m_fptrOnTrade(m_serverIndex, (openTrades[iLoop]), (TransType)(-1));

        //                        if (iLoop % 10 == 0)
        //                        {
        //                            Sleep(1);
        //                        }
        //                    }

        //                    //m_ptrMT4Pump.MemFree(openTrades);
        //                    openTrades = null;
        //                    total = 0;
        //                }

        //                //total = -1;

        //                if (_traceUpdateDataThread) m_ptrLogger.LogOk("Begin MarginsGet");
        //                var ptrMarginLvl = m_ptrMT4Pump.MarginsGet();
        //                if (_traceUpdateDataThread) m_ptrLogger.LogOk($"End MarginsGet: count = {ptrMarginLvl.Count}");
        //                total = ptrMarginLvl.Count;
        //                if (ptrMarginLvl != null)
        //                {
        //                    for (iLoop = 0; iLoop < total && !m_isClosing; iLoop++)
        //                    {
        //                        m_fptrOnMargin(m_serverIndex, (ptrMarginLvl[iLoop]));
        //                        if (iLoop % 10 == 0)
        //                        {
        //                            Sleep(1);
        //                        }
        //                    }

        //                    //m_ptrMT4Pump.MemFree(ptrMarginLvl);
        //                    ptrMarginLvl = null;
        //                    total = -1;
        //                }
        //                ////////////////////////////////////////////////////////////////////////
        //                //lock (m_csQueueMarginReq) {
        //                //while (!m_queueMarginReq.empty() && !m_isClosing)
        //                //{
        //                //	tempQueue.Enqueue(m_queueMarginReq.front());
        //                //	m_queueMarginReq.pop();
        //                //}
        //                //}
        //                ////////////////////////////////////////////////////////////////////////
        //                //while (!tempQueue.empty() && !m_isClosing)
        //                //{
        //                //	int mt4Login = tempQueue.front();
        //                //	//memset(&mrLvl, 0, sizeof(MarginLevel));
        //                //	mrLvl.login = mt4Login;
        //                //	/*int res = m_ptrMT4Pump.MarginLevelGet(mt4Login, null, &mrLvl);
        //                //	if (res == RET_OK)
        //                //	{
        //                //		m_fptrOnMargin(m_serverIndex, &mrLvl );
        //                //	}* /

        //                //	int res = m_ptrManagerAPI.m_DBMgr.MarginLevelRequest(mt4Login, &mrLvl);
        //                //	Sleep(0);
        //                //	if (res == RET_OK)
        //                //	{
        //                //		m_fptrOnMargin(m_serverIndex, &mrLvl);
        //                //	}
        //                //	
        //                //	tempQueue.pop();
        //                //}
        //                ////////////////////////////////////////////////////////////////////////
        //            }
        //        }//if (WaitForSingleObject(m_hOpenTradeAnalyThrdEvnt, waitMiliSec) == WAIT_TIMEOUT)
        //    }//while (m_iOpenTradeAnalyThrd && !m_isClosing)
        //    return 0;
        //}

        private uint closeTradeAnalyserThread()
        {
            var waitMiliSec = m_iCloseTradeAnalyThrdPeriod * 1000;
            Dictionary<int, Dictionary<Guid, MT4Request>>.iterator it1;
            Dictionary<Guid, MT4Request>.iterator it2;
            List<Guid>.iterator it4;
            List<Guid> vecRequestRemove = new List<Guid>();
            //int tot = 1;
            while (m_iCloseTradeAnalyThrd && !m_isClosing)
            {
                if (CppHelper.WaitForSingleObject(m_hCloseTradeAnalyThrdEvnt, waitMiliSec) == WaitResult.WAIT_TIMEOUT)
                {
                    if (!m_iCloseTradeAnalyThrd || m_isClosing)
                    {
                        break;
                    }
                    //get all closed orders
                    lock (m_syncCloseTradeMT4RequestMap)
                    {
                        for (it1 = m_mapCloseTradeMT4Request.begin(); it1 != m_mapCloseTradeMT4Request.end(); it1++)
                        {
                            vecRequestRemove.Clear();
                            for (it2 = it1.second.begin(); it2 != it1.second.end(); it2++)
                            {
                                if (it2.second.reqType == MT4REQ.MT4REQ_BALANCE)
                                {
                                    DateTimeOffset currTime = DateTimeOffset.UtcNow;
                                    var timespan = DateTimeOffset.UtcNow - it2.second.reqInsertTime;
                                    if (timespan > MAX_WAIT_TIME_IN_SEC)
                                    {
                                        vecRequestRemove.push_back(it2.first);
                                        it2.second.status = eOrderStatus.ORD_STAT_REJECTED;
                                        try
                                        {
                                            m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*-1,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                        }
                                        catch (Exception exception)
                                        {
                                            m_ptrLogger.LogError("Unknown exception", exception);
                                        }
                                        continue;
                                    }
                                    timespan = DateTimeOffset.UtcNow - it2.second.queryTime;
                                    if (timespan < CHECK_INTERVAL_LIMIT_SEC)
                                    {
                                        //it already handled wait for next time bye dear
                                        continue;
                                    }
                                    it2.second.queryTime = currTime;
                                    handleBalanceRequest(it2.second);
                                    if (it2.second.status == eOrderStatus.ORD_STAT_EXECUTED)
                                    {
                                        vecRequestRemove.push_back(it2.first);
                                        try
                                        {
                                            m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*it2.second.newLoginOrOrderID,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                        }
                                        catch (Exception exception)
                                        {
                                            m_ptrLogger.LogError("Unknown exception", exception);
                                        }
                                    }

                                }
                                else
                                {
                                    MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)it2.second.ptrData;
                                    TradeRecord ptrTradeRec;
                                    lock (m_syncManagerNormal)
                                    {
                                        ptrTradeRec = m_ptrMT4Normal.TradeRecordRequest(ptrOrdInfo._orderID);
                                    }

                                    if (ptrTradeRec != null && ptrTradeRec.close_time != null)
                                    {
                                        vecRequestRemove.push_back(it2.first);
                                        it2.second.status = eOrderStatus.ORD_STAT_EXECUTED;
                                        try
                                        {
                                            m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*-1,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                        }
                                        catch (Exception exception)
                                        {
                                            m_ptrLogger.LogError("Unknown exception", exception);
                                        }
                                        //m_ptrMT4Normal.MemFree(ptrTradeRec);
                                        ptrTradeRec = null;
                                    }
                                    else
                                    {
                                        DateTimeOffset currTime = DateTimeOffset.UtcNow;
                                        var timespan = currTime - it2.second.reqInsertTime;
                                        if (timespan > MAX_WAIT_TIME_IN_SEC)
                                        {
                                            vecRequestRemove.push_back(it2.first);
                                            it2.second.status = eOrderStatus.ORD_STAT_REJECTED;
                                            try
                                            {
                                                m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*-1,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                            }
                                            catch (Exception exception)
                                            {
                                                m_ptrLogger.LogError("Unknown exception", exception);
                                            }
                                            continue;
                                        }
                                        timespan = currTime - it2.second.queryTime;
                                        if (timespan < CHECK_INTERVAL_LIMIT_SEC)
                                        {
                                            //it already handled wait for next time bye dear
                                            continue;
                                        }
                                        it2.second.queryTime = currTime;
                                        handleCloseTransRequest(it2.second);
                                        if (it2.second.status == eOrderStatus.ORD_STAT_EXECUTED)
                                        {
                                            vecRequestRemove.push_back(it2.first);
                                            try
                                            {
                                                m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*it2.second.newLoginOrOrderID,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                            }
                                            catch (Exception exception)
                                            {
                                                m_ptrLogger.LogError("Unknown exception", exception);
                                            }
                                        }
                                    }
                                }

                            }
                            for (it4 = vecRequestRemove.begin(); it4 != vecRequestRemove.end(); it4++)
                            {
                                it2 = it1.second.find(it4);
                                if (it2 != it1.second.end())
                                {
                                    CppHelper.free(it2.second.ptrData);
                                    it2.second.ptrData = null;
                                    CppHelper.free(it2.second);
                                    it2.second = null;
                                    it1.second.erase(it2);
                                }
                            }//for (it4 = vecRequestRemove.begin(); it4 != vecRequestRemove.end(); it4++)
                        }
                    }
                }
            }
            return 0;
        }

        private uint openAccountAnalyserThread()
        {
            var waitMiliSec = m_iOpenAccAnalyThrdPeriod * 1000;
            List<ValueTuple<Guid, ValueTuple<int, int>>> vec = new List<(Guid, (int, int))>();
            Dictionary<Guid, MT4Request>.iterator it2;
            List<ValueTuple<Guid, ValueTuple<int, int>>>.iterator it3;
            List<Guid> vecRequestRemove = new List<Guid>();
            List<Guid>.iterator it4;

            while (m_iOpenAccAnalyThrd && !m_isClosing)
            {
                if (CppHelper.WaitForSingleObject(m_hOpenAccAnalyThrdEvnt, waitMiliSec) == WaitResult.WAIT_TIMEOUT)
                {
                    if (!m_iOpenAccAnalyThrd || m_isClosing)
                    {
                        break;
                    }
                    vec.Clear();

                    lock (m_syncVecNewAccResp)
                    {
                        for (it3 = m_vecNewAccResp.begin(); it3 != m_vecNewAccResp.end(); it3++)
                        {
                            vec.push_back(it3);
                        }
                        m_vecNewAccResp.Clear();
                    }

                    //Checking all open accounts
                    lock (m_syncAccountMT4RequestMap)
                    {
                        for (it3 = vec.begin(); it3 != vec.end(); it3++)
                        {
                            it2 = m_mapAccountMT4Request.find(it3.first());
                            if (it2 != m_mapAccountMT4Request.end())
                            {
                                it2.second.status = eOrderStatus.ORD_STAT_EXECUTED;
                                try
                                {
                                    m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*it3.second().first(),*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                }
                                catch (Exception exception)
                                {
                                    m_ptrLogger.LogError("Unknown exception", exception);
                                }
                                CppHelper.free(it2.second.ptrData);
                                it2.second.ptrData = null;
                                CppHelper.free(it2.second);
                                it2.second = null;
                                m_mapAccountMT4Request.erase(it2);
                            }//if (it1 != m_mapOpenTradeMT4Request.end())
                        }//for (it3 = vec.begin(); it3 != vec.end(); it3++)
                    }

                    vecRequestRemove.Clear();
                    lock (m_syncAccountMT4RequestMap)
                    {
                        for (it2 = m_mapAccountMT4Request.begin(); it2 != m_mapAccountMT4Request.end(); it2++)
                        {
                            DateTimeOffset currTime = DateTimeOffset.UtcNow;
                            var timespan = DateTimeOffset.UtcNow - it2.second.reqInsertTime;
                            if (timespan > MAX_WAIT_TIME_IN_SEC)
                            {
                                vecRequestRemove.push_back(it2.first);
                                it2.second.status = eOrderStatus.ORD_STAT_REJECTED;
                                try
                                {
                                    m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*-1,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                }
                                catch (Exception exception)
                                {
                                    m_ptrLogger.LogError("Unknown exception", exception);
                                }
                                continue;
                            }
                            timespan = DateTimeOffset.UtcNow - it2.second.queryTime;
                            if (timespan < CHECK_INTERVAL_LIMIT_SEC)
                            {
                                //it already handled wait for next time bye dear
                                continue;
                            }
                            it2.second.queryTime = currTime;
                            handleNewAccountRequest(it2.second);
                            if (it2.second.status == eOrderStatus.ORD_STAT_EXECUTED)
                            {
                                vecRequestRemove.push_back(it2.first);
                                try
                                {
                                    m_fptrMT4ReqResponse(it2.second.mt4errorcode, it2.second.mt4error, m_serverIndex, it2.second.reqType, it2.second.status, it2.second.masterLogin, /*it2.second.newLoginOrOrderID,*/ /*it2.second.socketID,*/ it2.second.serverTransID, it2.second.accType, it2.second.requestMode, it2.second);
                                }
                                catch (Exception exception)
                                {
                                    m_ptrLogger.LogError("Unknown exception", exception);
                                }
                            }
                        }//for (it2 = it1.second.begin(); it2 != it1.second.end(); it2++)
                        for (it4 = vecRequestRemove.begin(); it4 != vecRequestRemove.end(); it4++)
                        {
                            it2 = m_mapAccountMT4Request.find(it4);
                            if (it2 != m_mapAccountMT4Request.end())
                            {
                                CppHelper.free(it2.second.ptrData);
                                it2.second.ptrData = null;
                                CppHelper.free(it2.second);
                                it2.second = null;
                                m_mapAccountMT4Request.erase(it2);
                            }

                        }//for (it4 = vecRequestRemove.begin(); it4 != vecRequestRemove.end(); it4++)
                    }

                }
            }
            return 0;
        }

        private bool getValueFromComment(string comm, out Guid req_uid, out uint signal_uid)
        {
            if (!string.IsNullOrWhiteSpace(comm))
            {
                var parts = comm.Trim().Split('@');

                if (parts.Length == 4 &&
                    Guid.TryParseExact(parts[1].Trim(), "N", out req_uid) &&
                    uint.TryParse(parts[2].Trim(), out signal_uid))
                    return true;
            }

            req_uid = Guid.Empty;
            signal_uid = 0;
            return false;

            /*
            bool ret = false;
            string  cp;
            string  start;
            string  tmp = comm;
            req_uid = 0;
            signal_uid = 0;
            //---- skip spaces
            //@4294967295@4294967295@
            start = tmp;
            if ((cp = strchr(start, '@')) != null)
            {
                *cp = 0;
                cp++;
                start = cp;
                if ((cp = strchr(start, '@')) != null)
                {
                    *cp = 0;
                    req_uid = Utilities.stringUtils.naiveToUnsignedInt(start);
                    cp++;
                    start = cp;
                    if ((cp = strchr(start, '@')) != null)
                    {
                        *cp = 0;
                        signal_uid = Utilities.stringUtils.naiveToUnsignedInt(start);
                        ret = true;
                    }
                }

            }
            return ret;
            */
        }

        #endregion
    }
}
