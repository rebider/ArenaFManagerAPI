using WitFX.Backend.Infrastructure.Logging;
using WitFX.MT4;
using WitFX.MT4Server.Implementation.Extensions;
using WitFX.MT4Server.Implementation.Models;
using WitFXTCPStructDotNet.cls;
using System;
using System.Threading;
using static WitFX.MT4Server.Implementation.Helpers.CppHelper;
using static WitFXTCPStructDotNet.cls.eOrderStatus;
using static WitFX.MT4.TradeCommand;
using static WitFXTCPStructDotNet.cls.eMT4ServerType;
using static WitFX.MT4.TransType;
using static WitFXTCPStructDotNet.cls.eMT4OrderTransMode;
using static WitFXTCPStructDotNet.cls.MessageTypeID;
using static WitFX.MT4Server.Implementation.MT4REQ;
using static WitFXTCPStructDotNet.cls.eAccountType;
using static WitFXTCPStructDotNet.cls.eReturnCode;
using static WitFXTCPStructDotNet.cls.eDataTransMode;
using static WitFXTCPStructDotNet.cls.FileDataTypeID;
using static WitFXTCPStructDotNet.cls.eMT4OrderType;
using static WitFX.MT4Server.Implementation.Helpers.WaitResult;
using WitFX.MT4Server.Implementation.Collections;
using Sys = System.Collections.Generic;
using System.Diagnostics;
using static WitFX.MT4.Server.Implementation.Utilities.Common;
using System.Linq;
using static WitFX.MT4.Server.Implementation.Utilities.Misc;
using WitFX.MT4.Server.Implementation.Models;

namespace WitFX.MT4Server.Implementation
{
    public sealed class WitFXMT4ServerBL : IDisposable
    {
        #region Fields

        #region

        //private string m_userName;
        //private string m_password;
        private readonly ILogger _baseLogger;
        private readonly ServerLogger m_ptrLogger;
        private bool m_isResetting;

        //private bool m_isSymbolAdded;
        //private ulong m_SeqNo;
        private MYSQLWrapper m_ptrMySqlWrapper;

        private IConnectionMgr m_connectionMgr;
        private MasterUserSetting m_masterUserSetting;

        #endregion

        #region

        /// <summary>
        /// MAP[ IsDemoServer ][ MT4Login ] = MasterLogin
        /// </summary>
        private readonly Dictionary<bool, Dictionary<int, int>> m_mapMT4MasterLogin =
            new Dictionary<bool, Dictionary<int, int>>();

        /// <summary>
        /// MAP[ MASTER ACC ] = REBATE MT4 ACC
        /// </summary>
        private readonly Dictionary<int, int> m_mapMasterRebateAcc =
            new Dictionary<int, int>();

        private readonly object m_SyncMapMt4Master = new object();

        #endregion

        #region Signal MGT.

        /// <summary>
        /// MAP[ SINGAL_INDEX ] = PAIR< Symbol Index , Strategy Type> 
        /// </summary>
        private readonly Dictionary<int, (int, eStrategyType)> m_mapSignalSSP =
            new Dictionary<int, (int, eStrategyType)>();

        /// <summary>
        /// MAP[ MT4 Login ] = SSP Signal Index
        /// </summary>
        private readonly Dictionary<int, int> m_mapSSPMT4SignalIndex =
            new Dictionary<int, int>();

        private readonly object m_SyncSignalSSP = new object();

        ///// <summary>
        ///// MAP [ _sspMT4Login ] = Value not used
        ///// </summary>
        //private readonly Dictionary<int, HashSet<int>> m_mapSSPMT4OpenOrder =
        //    new Dictionary<int, HashSet<int>>();

        ///// <summary>
        ///// Relates to m_mapSSPMT4OpenOrder.
        ///// </summary>
        //private readonly object m_SyncOpenOrderSSP = new object();

        /// <summary>
        /// MAP[ SIGNAL_INDEX ] = SIGNAL				
        /// </summary>
        private readonly Dictionary<int, Signal> m_mapSignal =
            new Dictionary<int, Signal>();

        private readonly object m_SyncSignal = new object();

        #endregion

        #region Follower MGt. & Order Linking

        /// <summary>
        /// MAP[ SM OR SSP MT4 Login ] = VECTOR< IsDemo , FollowerMT4Login>
        /// </summary>
        private readonly Dictionary<int, List<(bool, int)>> m_mapFollowers =
            new Dictionary<int, List<(bool, int)>>();

        /// <summary>
        /// SET[ SM-MT4Login ]
        /// </summary>
        private readonly HashSet<int> m_setSM_MT4Login =
            new HashSet<int>();

        /// <summary>
        /// MAP[ MT4 Login ] = Signal Index
        /// </summary>
        private readonly Dictionary<int, int> m_mapSMSignalIndex =
            new Dictionary<int, int>();

        private readonly object m_SyncAccLinking = new object();

        private readonly HashSet<int> m_setAdminLogin = new HashSet<int>();

        /// <summary>
        /// _mapTransLinking[TRANSID] = VEC<TRANSID>
        /// </summary>
        private readonly Dictionary<uint, List<uint>> m_mapTransLinking =
            new Dictionary<uint, List<uint>>();

        /// <summary>
        /// _mapFollowerTransDetail[TRANSID] = OrderLinking
        /// </summary>
        private readonly Dictionary<uint, FollowerOrderLinking> m_mapFollowerTransDetail =
            new Dictionary<uint, FollowerOrderLinking>();

        /// <summary>
        /// _maSignalTransDetail[TRANSID] = OrderLinking
        /// </summary>
        private readonly Dictionary<uint, SignalOrderLinking> m_mapSignalTransDetail =
            new Dictionary<uint, SignalOrderLinking>();

        /// <summary>
        /// _mapTransIDOrderID[TransID ] = PAIR< OrderID , IsDemoServer > 
        /// </summary>
        private readonly Dictionary<uint, (int, bool)> m_mapTransIDOrderID =
            new Dictionary<uint, (int, bool)>();

        /// <summary>
        /// _mapTransIDOrderID[IsDemo][OrderID ] = TransID 
        /// </summary>
        private readonly Dictionary<bool, Dictionary<int, uint>> m_mapOrderIDTransID =
            new Dictionary<bool, Dictionary<int, uint>>();

        private readonly object m_SyncOrderLinking = new object();

        #endregion

        #region Unique Token ID

        private uint m_iUniqueID;
        private readonly object m_csUniqueID = new object();
        //private int m_signalTradeVolume;

        #endregion

        #region Symbol Setting

        private readonly object m_SyncSymbolSetting = new object();

        /// <summary>
        /// MAP[SymbolIndex][Strategy] = Setting
        /// </summary>
        private readonly Dictionary<int, Dictionary<eStrategyType, SignalSymbolSetting>>
            m_mapSignalSymSetting =
                new Dictionary<int, Dictionary<eStrategyType, SignalSymbolSetting>>();

        #endregion

        #region Follower Trade Volume

        /// <summary>
        /// MAP[IsDemoServer][SignalIndex][MT4Login] = Trade Volume | // true for demo
        /// </summary>
        private readonly Dictionary<bool, Dictionary<int, Dictionary<int, int>>>
            m_mapfollowerTradeVol =
                new Dictionary<bool, Dictionary<int, Dictionary<int, int>>>();

        private readonly object m_SyncfollowerTradeVol = new object();

        #endregion

        #region Symbol Setting

        private readonly object m_SyncQuoteBook = new object();
        private readonly object m_SyncQuoteSubs = new object();

        /// <summary>
        /// MAP[SymbolIndex] = Symbol
        /// </summary>
        private readonly Dictionary<int, string> m_mapSymbolIndexName =
            new Dictionary<int, string>();

        /// <summary>
        /// MAP[Symbol] = SymbolIndex
        /// </summary>
        private readonly Dictionary<string, int> m_mapSymbolNameIndex =
            new Dictionary<string, int>();

        /// <summary>
        /// MAP[SymbolIndex] = Quote
        /// </summary>
        private readonly Dictionary<int, MarketData> m_mapQuoteBook =
            new Dictionary<int, MarketData>();

        ///// <summary>
        ///// MAP[INT] = SET<SocketID>
        ///// </summary>
        //private readonly Dictionary<int, HashSet<uint>> m_mapQuoteSubs =
        //    new Dictionary<int, HashSet<uint>>();

        /// <summary>
        /// MAP[SymbolName] = SymbolSpec
        /// </summary>
        private readonly Dictionary<string, MT4SymbolInfo> m_mapSymbolSpec =
            new Dictionary<string, MT4SymbolInfo>();

        #endregion

        #region

        private volatile bool m_iMarketDataThrd;
        private Thread m_hMarketDataThrd;
        private EventWaitHandle m_hMarketDataThrdrEvnt;
        private readonly Queue<MarketData> m_queueMarketData = new Queue<MarketData>();
        private readonly object m_csQueueMarketData = new object();

        private volatile bool m_iTempSocialTradeThrd;
        private Thread m_hTempSocialTradeThrd;
        private EventWaitHandle m_hTempSocialTradeThrdrEvnt;
        private readonly Queue<TempSocialRecord> m_queueTempSocialTrade =
            new Queue<TempSocialRecord>();
        private readonly object m_csQueueTempSocialTrade = new object();

        private volatile bool m_iDBTransmitThrd;
        private Thread m_hDBTransmitThrd;
        private EventWaitHandle m_hTDBTransmithrdrEvnt;
        private readonly Queue<(int, FileDataTypeID)> m_queueDBTransmit =
            new Queue<(int, FileDataTypeID)>();
        private readonly object m_csQueueDBTransmit = new object();

        private volatile bool m_iRebateThrd;
        private Thread m_hRebateThrd;
        private EventWaitHandle m_hRebateThrdEvnt;
        private readonly Queue<RebateData> m_queueRebate = new Queue<RebateData>();
        private readonly object m_csQueueRebate = new object();

        private volatile bool m_iRankingThrd;
        private Thread m_hRankingThrd;
        private EventWaitHandle m_hRankingThrdEvnt;
        private int m_iRankingThrdPeriod;

        private volatile bool m_iIsTradingDisabledThrd;
        private Thread m_hIsTradingDisabledThrd;
        private EventWaitHandle m_hIsTradingDisabledThrdEvnt;
        private int m_iIsTradingDisabledThrdPeriod;

        private MT4ServerConnector m_ptrDemoMT4Manager;
        private MT4ServerConnector m_ptrLiveMT4Manager;

        #endregion

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

        public WitFXMT4ServerBL(MYSQLWrapper mySqlWrapper, ILogger logger, IConnectionMgr connectionMgr)
        {
            Debug.Assert(mySqlWrapper != null);
            Debug.Assert(logger != null);
            Debug.Assert(connectionMgr != null);

            m_ptrMySqlWrapper = mySqlWrapper;
            m_connectionMgr = connectionMgr;
            _baseLogger = logger;
            m_ptrLogger = new ServerLogger(logger, nameof(WitFXMT4ServerBL));
        }

        private bool _isStarting;

        public void Start(bool startThreads = true)
        {
            Debug.Assert(!_isStarting);

            if (_isStarting)
                throw new InvalidOperationException();

            _isStarting = true;
            m_ptrLogger.LogInfo("START");
            //_heartBeatInterval = cfg.HeartBeatInSec;

            m_ptrLogger.LogInfo("Getting User setting");

            m_masterUserSetting = m_ptrMySqlWrapper.getMasterSetting();

            m_iRankingThrdPeriod = 20;
            m_iIsTradingDisabledThrdPeriod = 900;

            var signalTransID = new List<uint>();
            var followerTransID = new List<uint>();
            var followerMT4Acc = new List<int>();
            var followerMasterAcc = new List<int>();
            var followerOrderID = new List<int>();
            var followerVolume = new List<int>();
            var isFollowerDemo = new List<bool>();

            var signalIndex = new List<int>();
            var signalMT4Acc = new List<int>();
            var sspAccForSM = new List<int>();
            var signalMasterAcc = new List<int>();
            var signalOrderID = new List<int>();
            var isSSP = new List<bool>();

            m_ptrMySqlWrapper.loadSignalOrderLinking(
                signalTransID, signalIndex, signalMT4Acc, signalMasterAcc, isSSP, sspAccForSM);

            var loopCnt = signalTransID.Count;

            for (var loop = 0; loop < loopCnt; loop++)
                insertSignalOrderLinking(
                    signalIndex[loop], signalMT4Acc[loop], signalMasterAcc[loop],
                    isSSP[loop], signalTransID[loop], sspAccForSM[loop]);

            m_ptrMySqlWrapper.loadFollowerOrderLinking(
                signalTransID, followerTransID, followerMT4Acc, followerMasterAcc,
                isFollowerDemo, followerVolume);

            loopCnt = signalTransID.Count;
            for (var loop = 0; loop < loopCnt; loop++)
                insertFollowerOrderLinking(
                    signalTransID[loop], followerMT4Acc[loop], followerMasterAcc[loop],
                    isFollowerDemo[loop], followerVolume[loop], followerTransID[loop]);

            lock (m_SyncOrderLinking)
            {
                m_ptrMySqlWrapper.loadTransLinking(m_mapTransLinking);
                m_ptrMySqlWrapper.loadTransIDOrderID(m_mapTransIDOrderID, m_mapOrderIDTransID);
            }

            //update order linking //HIGH ALERT

            lock (m_csUniqueID)
                m_iUniqueID = m_masterUserSetting._requestUID;

            m_ptrLogger.LogInfo($"Unique ID {m_iUniqueID}");

            m_ptrLogger.LogInfo("Fetching all signals....");
            fetchAllSignal();

            m_ptrLogger.LogInfo("Fetching all SSP signals....");
            fetchAllSSPSignal();

            m_ptrLogger.LogInfo("Fetching all SM signals....");
            fetchAllSMSignal();

            m_ptrLogger.LogInfo("Fetching signal symbol setting....");
            fetchSymbolSetting();

            m_ptrLogger.LogInfo("Fetching all mt4 accounts and master acc from DB");

            lock (m_SyncMapMt4Master)
                m_ptrMySqlWrapper.setAllMT4AccountsInMap(
                    m_mapMT4MasterLogin, m_mapMasterRebateAcc);

            m_ptrLogger.LogInfo("Init MT4...");
            //initMT4(startThreads: startThreads);

            if (startThreads)
            {
                startMarketDataThread();

                m_ptrLogger.LogInfo("Start Temp Record Thread...");
                startTempSocialTradeThread();

                m_ptrLogger.LogInfo("Start DB Transmit Thread...");
                startDBTransmitThread();

                m_ptrLogger.LogInfo("Start Rebate Thread...");
                startRebateThread();

                m_ptrLogger.LogInfo("Start Ranking Thread...");
                startRankingThread();

                m_ptrLogger.LogInfo("Start Is Trading Disabled Thread...");
                startIsTradingDisabledThread();
            }

            m_ptrLogger.LogInfo("END");
        }

        public void Stop()
        {
            Dispose();
        }

        private bool _isDisposed;

        //=================================================================================================
        public void Dispose()
        {
            if (_isDisposed)
                return;

            m_isResetting = true;
            m_ptrLogger.LogOk("In Destructor.....START");

            m_ptrLogger.LogOk("DeInit MT4...");
            DeInitMT4();

            lock (m_csQueueMarketData)
            {
                //while (!m_queueMarketData.empty())
                //{
                //    free(m_queueMarketData.front());
                //    m_queueMarketData.pop();
                //}
                m_queueMarketData.Clear();
            }

            stopMarketDataThread();

            Dictionary<int, MarketData>.iterator it;
            lock (m_SyncQuoteBook)
            {
                //for (it = m_mapQuoteBook.begin(); it != m_mapQuoteBook.end(); it++)
                //{
                //    free(it.second);
                //    it.second = null;
                //}
                m_mapQuoteBook.Clear();
            }

            Dictionary<int, Signal>.iterator itSignal;
            lock (m_SyncSignal)
            {
                //for (itSignal = m_mapSignal.begin(); itSignal != m_mapSignal.end(); itSignal++)
                //{
                //    free(itSignal.second);
                //    itSignal.second = null;
                //}
                m_mapSignal.Clear();
            }


            lock (m_csUniqueID)
            {
                m_ptrLogger.LogOk("Updating master setting");
                m_ptrMySqlWrapper.updateMasterSetting_UID(m_iUniqueID);
            }

            Dictionary<uint, FollowerOrderLinking>.iterator itFollowLinking;
            Dictionary<uint, SignalOrderLinking>.iterator itSignalLinking;

            m_ptrLogger.LogOk("Updating order linking in database");

            lock (m_SyncOrderLinking)
            {

                m_ptrMySqlWrapper.clearTransIdOrderidLinkingTable();
                m_ptrMySqlWrapper.clearTransIdLinkingTable();
                m_ptrMySqlWrapper.clearFollowerOrderLinkingTable();
                m_ptrMySqlWrapper.clearSignalOrderLinkingTable();

                for (itSignalLinking = m_mapSignalTransDetail.begin(); itSignalLinking != m_mapSignalTransDetail.end(); itSignalLinking++)
                {
                    m_ptrMySqlWrapper.insertSignalOrderLinking(itSignalLinking.second._signal_transID, itSignalLinking.second._signal_index,
                        itSignalLinking.second._signal_mt4Acc, itSignalLinking.second._signal_masterAcc,
                        itSignalLinking.second._signal_IsSSP, itSignalLinking.second._sspMt4AccForSM);

                    //free(itSignalLinking.second);
                    //itSignalLinking.second = null;
                }
                m_mapSignalTransDetail.Clear();


                for (itFollowLinking = m_mapFollowerTransDetail.begin(); itFollowLinking != m_mapFollowerTransDetail.end(); itFollowLinking++)
                {
                    m_ptrMySqlWrapper.insertFollowerOrderLinking(itFollowLinking.second._signal_transID, itFollowLinking.second._follower_transID,
                        itFollowLinking.second._follower_mt4Acc, itFollowLinking.second._follower_masterAcc,
                        itFollowLinking.second._followerVolume, itFollowLinking.second._isFollowerDemo);

                    //free(itFollowLinking.second);
                    //itFollowLinking.second = null;
                }
                m_mapFollowerTransDetail.Clear();

                if (m_mapTransLinking.size() != 0)
                    m_ptrMySqlWrapper.insertTransLinking(m_mapTransLinking);

                m_mapTransLinking.Clear();

                if (m_mapTransIDOrderID.size() != 0)
                    m_ptrMySqlWrapper.insertTransIDOrderID(m_mapTransIDOrderID);

                m_mapTransIDOrderID.Clear();
            }


            //m_ptrLogger.LogOk("Deleting MYSQL CONNECTOR");
            //m_ptrMySqlWrapper.Dispose();
            //m_ptrMySqlWrapper = null;

            m_ptrLogger.LogOk("Clearing symbol spec memory");
            Dictionary<string, MT4SymbolInfo>.iterator itSymSpec;
            //for (itSymSpec = m_mapSymbolSpec.begin(); itSymSpec != m_mapSymbolSpec.end(); itSymSpec++)
            //{
            //    free(itSymSpec.second);
            //    itSymSpec.second = null;
            //}
            m_mapSymbolSpec.Clear();

            m_ptrLogger.LogOk("Clearing symbol setting memory");
            //Dictionary<int, Dictionary<int, SignalSymbolSetting>>.iterator itSymSett1;
            //Dictionary<int, SignalSymbolSetting>.iterator itSymSett12;
            lock (m_SyncSymbolSetting)
            {

                //for (var itSymSett1 = m_mapSignalSymSetting.begin(); itSymSett1 != m_mapSignalSymSetting.end(); itSymSett1++)
                //{
                //    for (var itSymSett12 = itSymSett1.second.begin(); itSymSett12 != itSymSett1.second.end(); itSymSett12++)
                //    {
                //        free(itSymSett12.second);
                //        itSymSett12.second = null;
                //    }
                //}
                m_mapSignalSymSetting.Clear();
            }

            m_ptrLogger.LogOk("Stop Temp Record Thread...");
            stopTempSocialTradeThread();

            m_ptrLogger.LogOk("Stop DB Transmit Thread...");
            stopDBTransmitThread();

            m_ptrLogger.LogOk("Stop Rebate Thread...");
            stopRebateThread();

            m_ptrLogger.LogOk("Stop Ranking Thread...");
            stopRankingThread();

            m_ptrLogger.LogOk("Stop Is Trading Disabled Thread...");
            stopIsTradingDisabledThread();

            lock (m_csQueueRebate)
            {
                //while (!m_queueRebate.empty())
                //{
                //    free(m_queueRebate.front());
                //    m_queueRebate.pop();
                //}

                m_queueRebate.Clear();
            }

            lock (m_csQueueTempSocialTrade)
            {
                //while (!m_queueTempSocialTrade.empty())
                //{
                //    free(m_queueTempSocialTrade.front());
                //    m_queueTempSocialTrade.pop();
                //}

                m_queueTempSocialTrade.Clear();
            }

            m_ptrLogger.LogOk("In Destructor.....END");
            _isDisposed = true;
        }
        //=================================================================================================
        //void initMT4(bool startThreads)
        //{
        //    m_ptrLogger.LogInfo("Initializng DEMO MT4 Manager");
        //    m_ptrDemoMT4Manager = new MT4ServerConnector(SRV_TYPE_DEMO,
        //        m_masterUserSetting._demoServer, m_masterUserSetting._demoManagerLogin,
        //        m_masterUserSetting._demoManagerPassword, _baseLogger, true, true,
        //        startThreads: startThreads);

        //    memcpy(ref m_ptrDemoMT4Manager.m_masterUserSetting, m_masterUserSetting);

        //    m_ptrDemoMT4Manager.setMT4SocialUsers(m_mapMT4MasterLogin);
        //    m_ptrDemoMT4Manager.setMT4ResponseFunPtr(onMT4Response);
        //    m_ptrDemoMT4Manager.setMarketDataFunPtr(onMarketData);
        //    m_ptrDemoMT4Manager.setSymbolInfoFunPtr(onSymbolInfo);
        //    m_ptrDemoMT4Manager.setOnTradeFunPtr(onTradeResponse);
        //    m_ptrDemoMT4Manager.setOnMarginFunPtr(onMarginLevelResponse);

        //    m_ptrLogger.LogInfo("Initializng LIVE MT4 Manager");
        //    m_ptrLiveMT4Manager = new MT4ServerConnector(SRV_TYPE_LIVE,
        //        m_masterUserSetting._liveServer, m_masterUserSetting._liveManagerLogin,
        //        m_masterUserSetting._liveManagerPassword, _baseLogger, true, true,
        //        startThreads: startThreads);

        //    memcpy(ref m_ptrLiveMT4Manager.m_masterUserSetting, m_masterUserSetting);

        //    m_ptrLiveMT4Manager.setMT4SocialUsers(m_mapMT4MasterLogin);
        //    m_ptrLiveMT4Manager.setMT4ResponseFunPtr(onMT4Response);
        //    m_ptrLiveMT4Manager.setOnTradeFunPtr(onTradeResponse);
        //    m_ptrLiveMT4Manager.setOnMarginFunPtr(onMarginLevelResponse);

        //    if (startThreads)
        //    {
        //        if (m_ptrDemoMT4Manager.IsAPIValid())
        //        {
        //            m_ptrLogger.LogInfo("Starting DEMO MT4 Manager connection...");
        //            m_ptrDemoMT4Manager.startNormalManager();
        //        }
        //        else
        //        {
        //            m_ptrLogger.LogError("MT4 MANAGER API IS NOT VALID");
        //        }
        //        if (m_ptrLiveMT4Manager.IsAPIValid())
        //        {
        //            m_ptrLogger.LogInfo("Starting LIVE MT4 Manager connection...");
        //            m_ptrLiveMT4Manager.startNormalManager();
        //        }
        //        else
        //        {
        //            m_ptrLogger.LogError("MT4 MANAGER API IS NOT VALID");
        //        }
        //    }
        //}
        //=================================================================================================
        void DeInitMT4()
        {
            //use critical section here i have to think
            m_ptrDemoMT4Manager.Dispose();
            m_ptrLiveMT4Manager.Dispose();
            m_ptrDemoMT4Manager = null;
            m_ptrLiveMT4Manager = null;
        }

        public GenericFileDataResponse handleRankingRequest(RankingRequest ptr)
        {
            GenericFileDataResponse ptrFileResp = null;
            m_ptrLogger.LogOk("START");

            var res = m_ptrMySqlWrapper.readSignalD1data(ptr._days);
            var rankingCount = res.Count;

            if (rankingCount > 0 && res != null)
            {
                ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                ptrFileResp._fileDataMessageType = FDMT_Ranking_ID;
                ptrFileResp._header._fileDataMessageType = FDMT_Ranking_ID;
                //ptrFileResp._header._fileSize = sizeof(Ranking) * rankingCount;
                ptrFileResp._header._loginID = ptr._header._loginID;
                //ptrFileResp._header._socketID = ptr._header._socketID;
                //m_connectionMgr.SendAsFile(ptr._header._socketID, ptrFileResp, res);
            }

            m_ptrLogger.LogOk("END");
            return ptrFileResp;
        }

        public GenericFileDataResponse handleProductRequest(ProductRequest ptr)
        {
            GenericFileDataResponse ptrFileResp = null;
            m_ptrLogger.LogOk("START");

            var res = m_ptrMySqlWrapper.getAllProducts();
            var productCount = res.Count;

            if (productCount > 0 && res != null)
            {
                ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                ptrFileResp._fileDataMessageType = FDMT_Product_ID;
                ptrFileResp._header._fileDataMessageType = FDMT_Product_ID;
                //ptrFileResp._header._fileSize = sizeof(Product) * productCount;
                ptrFileResp._header._loginID = ptr._header._loginID;
                //ptrFileResp._header._socketID = ptr._header._socketID;
                //m_connectionMgr.SendAsFile(ptr._header._socketID, ptrFileResp, res);
            }

            m_ptrLogger.LogOk("END");
            return ptrFileResp;
        }

        public GenericFileDataResponse handleGraphRequest(GraphRequest ptr)
        {
            GenericFileDataResponse ptrFileResp = null;
            m_ptrLogger.LogOk("START");

            var res = m_ptrMySqlWrapper.getDataForGraph(ptr._signalindex);
            var count = res.Count;

            if (count > 0 && res != null)
            {
                ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                ptrFileResp._fileDataMessageType = FDMT_Graph_ID;
                ptrFileResp._header._fileDataMessageType = FDMT_Graph_ID;
                //ptrFileResp._header._fileSize = sizeof(Ranking) * count;
                ptrFileResp._header._loginID = ptr._header._loginID;
                //ptrFileResp._header._socketID = ptr._header._socketID;
                // m_connectionMgr.SendAsFile(ptr._header._socketID, ptrFileResp, res);
            }

            m_ptrLogger.LogOk("END");
            return ptrFileResp;
        }

        //=================================================================================================
        void onMT4Response(ReturnCode errorcode, string errormessage, eMT4ServerType serverIndex, MT4REQ reqType, eOrderStatus trans_status, int masterLogin, int orderOrLogin, /*uint socketID,*/ uint server_trans_id, eAccountType accType, MT4REQMODE reqMode)
        {
            //m_ptrMySqlWrapper.insertLog(Utilities.LOG_INFO,masterLogin,orderOrLogin,"X",)
            if (reqType == MT4REQ_NEW_ACCOUNT)
            {
                if (trans_status == ORD_STAT_EXECUTED)
                {
                    m_ptrLogger.LogInfo("EXECUTED : MT4 Account %d master Login %d AccountType: %d", orderOrLogin, masterLogin, accType);
                    MT4Account acc = new MT4Account();
                    //memset(&acc, 0, sizeof(MT4Account));
                    acc._accountType = accType;
                    acc._masterLogin = masterLogin;
                    acc._mt4Login = orderOrLogin;
                    acc._mt4ServerIndex = serverIndex;

                    if (serverIndex == SRV_TYPE_DEMO)
                    {
                        m_ptrDemoMT4Manager.insertMT4Account(orderOrLogin);
                    }
                    else
                    {
                        m_ptrLiveMT4Manager.insertMT4Account(orderOrLogin);
                    }


                    if (!m_ptrMySqlWrapper.insertMT4Account(acc))
                    {
                        m_ptrLogger.LogError("Unable to insert MT4 account for masterlogin: %d MT4 Login: %d in database", masterLogin, orderOrLogin);
                    }
                    else
                    {
                        if (accType == ACC_TYPE_REBATE)
                        {
                            Dictionary<int, int>.iterator it2;

                            lock (m_SyncMapMt4Master)
                            {

                                it2 = m_mapMasterRebateAcc.find(masterLogin);
                                if (it2 == m_mapMasterRebateAcc.end())
                                {
                                    m_mapMasterRebateAcc.insert(new ValueTuple<int, int>(masterLogin, orderOrLogin));
                                }

                            }
                        }
                        //else //if (accType != ACC_TYPE_REBATE)
                        {
                            Dictionary<bool, Dictionary<int, int>>.iterator it1;
                            //Dictionary<int, int>.iterator it2;

                            lock (m_SyncMapMt4Master)
                            {

                                bool isDemoServer = serverIndex == SRV_TYPE_DEMO ? true : false;
                                it1 = m_mapMT4MasterLogin.find(isDemoServer);
                                if (it1 == m_mapMT4MasterLogin.end())
                                {
                                    Dictionary<int, int> mp2 = new Dictionary<int, int>();
                                    m_mapMT4MasterLogin.insert(new ValueTuple<bool, Dictionary<int, int>>(isDemoServer, mp2));
                                    it1 = m_mapMT4MasterLogin.find(isDemoServer);
                                }
                                it1.second.insert(new ValueTuple<int, int>(orderOrLogin, masterLogin));



                            }
                        }
                    }


                    if (accType == ACC_TYPE_SSP)
                    {
                        m_ptrMySqlWrapper.updateSignalMT4Login(server_trans_id, orderOrLogin);
                        m_ptrMySqlWrapper.updateSSPSignalMT4Login(server_trans_id, orderOrLogin);
                        fetchAllSignal();
                        fetchAllSSPSignal();
                        fetchAllSMSignal();
                        insertDBTransmitData(masterLogin, FDMT_Signal_ID);
                        insertDBTransmitData(masterLogin, FDMT_SSPSignal_ID);
                    }
                    else if (accType == ACC_TYPE_SM)
                    {
                        m_ptrMySqlWrapper.updateSignalMT4Login(server_trans_id, orderOrLogin);
                        m_ptrMySqlWrapper.updateSMSignalMT4Login(server_trans_id, orderOrLogin);
                        fetchAllSignal();
                        fetchAllSSPSignal();
                        fetchAllSMSignal();

                        insertDBTransmitData(masterLogin, FDMT_Signal_ID);
                        insertDBTransmitData(masterLogin, FDMT_SMSignal_ID);
                    }

                    if (accType == ACC_TYPE_FOLLOWER_DEMO || accType == ACC_TYPE_SSP || accType == ACC_TYPE_SM)
                    {
                        MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                        //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                        ptrMT4Req4.masterLogin = masterLogin;
                        ptrMT4Req4.reqType = MT4REQ_BALANCE;
                        //ptrMT4Req4.socketID = socketID;
                        ptrMT4Req4.status = ORD_STAT_RECVD;
                        ptrMT4Req4.serverTransID = getUniqueRequestID();
                        ptrMT4Req4.ptrData = new MT4OrderInfo();
                        //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));
                        MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                        ptrOrd._accountType = accType;
                        ptrOrd._masterLogin = masterLogin;
                        ptrOrd._mt4Login = orderOrLogin;
                        ptrOrd._mt4ServerIndex = serverIndex;
                        ptrOrd._orderTransMode = ORD_TRANS_CLOSE;
                        ptrOrd._orderType = ORD_TYPE_BALANCE;
                        ptrOrd._price = 5000;
                        if (accType == ACC_TYPE_FOLLOWER_DEMO)
                        {
                            ptrOrd._price = m_masterUserSetting._deposit_followerDemo;
                        }
                        else if (accType == ACC_TYPE_SSP)
                        {
                            ptrOrd._price = m_masterUserSetting._deposit_SSP;
                        }
                        else if (accType == ACC_TYPE_SM)
                        {
                            ptrOrd._price = m_masterUserSetting._deposit_SM;
                        }

                        m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req4);
                    }
                    else if (accType == ACC_TYPE_FOLLOWER_LIVE)
                    {
                        MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                        //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                        ptrMT4Req4.masterLogin = masterLogin;
                        ptrMT4Req4.reqType = MT4REQ_BALANCE;
                        //ptrMT4Req4.socketID = socketID;
                        ptrMT4Req4.status = ORD_STAT_RECVD;
                        ptrMT4Req4.serverTransID = getUniqueRequestID();
                        ptrMT4Req4.ptrData = new MT4OrderInfo();
                        //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));
                        MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                        ptrOrd._accountType = accType;
                        ptrOrd._masterLogin = masterLogin;
                        ptrOrd._mt4Login = orderOrLogin;
                        ptrOrd._mt4ServerIndex = serverIndex;
                        ptrOrd._orderTransMode = ORD_TRANS_CLOSE;
                        ptrOrd._orderType = ORD_TYPE_BALANCE;
                        ptrOrd._price = m_masterUserSetting._deposit_followerLive;
                        m_ptrLiveMT4Manager.insertMT4Request(ptrMT4Req4);
                    }

                    MT4AccountResponse ptrResp = GetMessageObject<MT4AccountResponse>(MT_MT4AccountResponse_ID);
                    memcpy(ref ptrResp._account, acc);
                    SentDataUsingLoginID(ptrResp, MT_MT4AccountResponse_ID, masterLogin);

                }
                else if (trans_status == ORD_STAT_PROCESSING)
                {
                    m_ptrLogger.LogInfo("PROCESSING : MT4 Account %d master Login %d AccountType: %d", orderOrLogin, masterLogin, accType);
                    SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    SentDataUsingLoginID(ptrResp, MT_SocialOrderResponse_ID, masterLogin);

                }
                else if (trans_status == ORD_STAT_REJECTED)
                {
                    m_ptrLogger.LogInfo("REJECTED : MT4 Account %d master Login %d AccountType: %d", orderOrLogin, masterLogin, accType);
                    if (accType == ACC_TYPE_SSP)
                    {
                        m_ptrMySqlWrapper.updateSignalMT4Login(server_trans_id, orderOrLogin, true);
                        m_ptrMySqlWrapper.updateSSPSignalMT4Login(server_trans_id, orderOrLogin, true);
                    }
                    if (accType == ACC_TYPE_SM)
                    {
                        m_ptrMySqlWrapper.updateSignalMT4Login(server_trans_id, orderOrLogin, true);
                        m_ptrMySqlWrapper.updateSMSignalMT4Login(server_trans_id, orderOrLogin, true);
                    }
                    SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    ptrResp._mt4errorcode = errorcode;
                    strcpy(out ptrResp._mt4errormessage, errormessage);
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    SentDataUsingLoginID(ptrResp, MT_SocialOrderResponse_ID, masterLogin);
                }
                insertDBTransmitData(masterLogin, FDMT_MasterUser_ID);
                insertDBTransmitData(masterLogin, FDMT_MT4Account_ID);
                insertDBTransmitData(masterLogin, FDMT_SSPSignal_ID);
                insertDBTransmitData(masterLogin, FDMT_SSPSignal_ID);

            }
            else
            {
                if (trans_status == ORD_STAT_PROCESSING)
                {
                    m_ptrLogger.LogInfo("PROCESSING : Order %d master Login %d UID: %u", orderOrLogin, masterLogin, server_trans_id);
                    SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    SentDataUsingLoginID(ptrResp, MT_SocialOrderResponse_ID, masterLogin);

                }
                else if (trans_status == ORD_STAT_REJECTED)
                {
                    m_ptrLogger.LogInfo("REJECTED : Order %d master Login %d UID: %u", orderOrLogin, masterLogin, server_trans_id);
                    SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    ptrResp._mt4errorcode = errorcode;
                    strcpy(out ptrResp._mt4errormessage, errormessage);
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    SentDataUsingLoginID(ptrResp, MT_SocialOrderResponse_ID, masterLogin);
                }
                else if (trans_status == ORD_STAT_EXECUTED)
                {
                    if (reqMode == MT4REQMODE.OPEN_TRADE)
                    {
                        updateTransLinking(server_trans_id, orderOrLogin);
                    }
                    if (reqMode == MT4REQMODE.CLOSE_TRADE || reqMode == MT4REQMODE.DELETE_TRADE)
                    {
                        removeTransLinking(server_trans_id);
                    }
                    m_ptrLogger.LogInfo("EXECUTED : Ordert %d master Login %d UID: %u", orderOrLogin, masterLogin, server_trans_id);
                    SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                    ptrResp._serverTransID = server_trans_id;
                    ptrResp._requestMode = reqMode;
                    ptrResp._retCode = (eReturnCode)trans_status;
                    //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, socketID);
                    SentDataUsingLoginID(ptrResp, MT_SocialOrderResponse_ID, masterLogin);
                }
            }
        }
        //=================================================================================================
        void onMarketData(Sys.IReadOnlyList<SymbolInfo> ptrArr)
        {
            if (m_isResetting)
            {
                m_ptrLogger.LogOk("Resetting.....");
                return;
            }

            for (int i = 0; i < ptrArr.Count; i++)
            {
                int symIndex = getSymbolIndex(ptrArr[i].symbol);
                if (symIndex < 0)
                {
                    m_ptrLogger.LogError("Unable to find symbol index for %s symbol", ptrArr[i].symbol);
                }
                else
                {
                    MarketData ptr = (MarketData)new MarketData();
                    //memset(ptr, 0, sizeof(MarketData));
                    ptr._symbolIndex = symIndex;
                    ptr._ask = ptrArr[i].ask;
                    ptr._bid = ptrArr[i].bid;
                    ptr._high = ptrArr[i].high;
                    ptr._low = ptrArr[i].low;
                    ptr._lastTickTime = ptrArr[i].lasttime.Value;
                    insertMarketData(ptr);
                }
            }
        }
        //=================================================================================================
        void onSymbolInfo(Sys.IReadOnlyList<ConSymbolGroup> ptrSecurityArr, Sys.IReadOnlyList<ConSymbol> ptrSymbolArr)
        {
            if (m_isResetting)
            {
                m_ptrLogger.LogOk("Resetting.....");
                return;
            }
            m_ptrLogger.LogOk("Adding symbols in database ");
            for (int iLoop = 0; iLoop < ptrSymbolArr.Count; iLoop++)
            {
                MT4SymbolInfo ptrSym = (MT4SymbolInfo)new MT4SymbolInfo();
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
                COPY_STR_S(out ptrSym._symbol, ptrSymbolArr[iLoop].symbol);
                COPY_STR_S(out ptrSym._description, ptrSymbolArr[iLoop].description);
                COPY_STR_S(out ptrSym._security, ptrSecurityArr[ptrSymbolArr[iLoop].type].name);
                m_mapSymbolSpec.insert(new ValueTuple<string, MT4SymbolInfo>(ptrSymbolArr[iLoop].symbol, ptrSym));
                m_ptrMySqlWrapper.insertUpdateMT4Symbol(ptrSym);
            }

            //lock (m_csSymbolIndexName) {
            m_ptrMySqlWrapper.setSymbolMap(m_mapSymbolIndexName, m_mapSymbolNameIndex);
            //}
        }
        //=================================================================================================
        void onTradeResponse(eMT4ServerType serverIndex, TradeRecord ptrTrade, TransType transType)
        {
            MyTradeRecordResponse ptr = GetMessageObject<MyTradeRecordResponse>(MT_MyTradeRecordResponse_ID);
            ptr._trans = transType;
            ptr._isDemoServer = serverIndex == SRV_TYPE_DEMO ? true : false;
            memcpy(ref ptr._trade, ptrTrade);
            int masterlogin = getMasterLogin(serverIndex, ptrTrade.login);
            SentDataUsingLoginID(ptr, MT_MyTradeRecordResponse_ID, masterlogin);

            if (transType == TRANS_DELETE && (ptrTrade.cmd == (int)OP_BUY || ptrTrade.cmd == (int)OP_SELL || ptrTrade.cmd == (int)OP_BALANCE))
            {
                if (serverIndex == SRV_TYPE_DEMO)
                {
                    m_ptrMySqlWrapper.insertDemoCloseTrades(ptrTrade);
                }
                else
                {
                    m_ptrMySqlWrapper.insertLiveCloseTrades(ptrTrade);
                }
                if (ptrTrade.profit < 0)
                {
                    m_ptrMySqlWrapper.getSSPSignal(ptrTrade.login, out var symbolIndex, out var strategyType);
                    if (symbolIndex != 0 && strategyType != 0)
                    {
                        SignalSymbolSetting symbolSettings = m_ptrMySqlWrapper.getSignalSymbolBlockTime(symbolIndex, strategyType);

                        if (symbolSettings != null)
                        {
                            double lossinpips = 0.0;
                            if (strstr(ptrTrade.symbol, "JPY"))
                            {
                                lossinpips = abs(ptrTrade.open_price - ptrTrade.close_price) * 100;
                            }
                            else
                                lossinpips = abs(ptrTrade.open_price - ptrTrade.close_price) * 10000;
                            if (lossinpips > symbolSettings._maxLossInPips)
                            {
                                var startTime = ptrTrade.close_time.Value;
                                //tm* tmTime = localtime(&startTime);
                                //tmTime.tm_hour = tmTime.tm_hour + symbolSettings._blockTimeInHrs;
                                DateTimeOffset endTime = startTime.AddHours(symbolSettings._blockTimeInHrs);
                                m_ptrMySqlWrapper.insertTradeDisableRow(masterlogin, ptrTrade.login, serverIndex, (int)ptrTrade.cmd, startTime, endTime);
                                //send update to service
                                TradeDisableResponse tradingptr = GetMessageObject<TradeDisableResponse>(MT_TradeDisableResponse_ID);
                                tradingptr._serverTime = DateTimeOffset.UtcNow;
                                MT4TradeDisableInfo ptrInfo = null;
                                ptrInfo = (MT4TradeDisableInfo)new MT4TradeDisableInfo();
                                //memset(ptrInfo, 0, sizeof(MT4TradeDisableInfo));
                                ptrInfo._masterLogin = masterlogin;
                                ptrInfo._mt4ServerIndex = serverIndex;
                                ptrInfo._mt4Login = ptrTrade.login;
                                ptrInfo._isDisabled = true;
                                memcpy(ref tradingptr._tradeDisableInfo, ptrInfo);
                                SentDataUsingLoginID(tradingptr, MT_TradeDisableResponse_ID, masterlogin);
                            }
                        }//if (blocktime != 0)
                    }//if (symbolIndex != 0)
                }//if (ptrTrade.profit < 0)
            }
        }
        //=================================================================================================
        void onMarginLevelResponse(eMT4ServerType serverIndex, MarginLevel ptrMargin)
        {
            int masterLogin = getMasterLogin(serverIndex, ptrMargin.login);
            if (masterLogin != 0)
            {
                MT4MarginResponse ptr = GetMessageObject<MT4MarginResponse>(MT_MT4MarginResponse_ID);
                ptr._marginInfo = new MT4AccountStatus();
                ptr._marginInfo._balance = ptrMargin.balance;
                ptr._marginInfo._equity = ptrMargin.equity;
                ptr._marginInfo._freeMargin = ptrMargin.margin_free;
                ptr._marginInfo._margin = ptrMargin.margin;
                ptr._marginInfo._marginLevel = ptrMargin.margin_level;
                ptr._marginInfo._mt4ServerIndex = serverIndex;
                ptr._marginInfo._mt4Login = ptrMargin.login;
                SentDataUsingLoginID(ptr, MT_MT4MarginResponse_ID, masterLogin);
            }
        }
        //=================================================================================================
        void startMarketDataThread()
        {
            m_ptrLogger.LogOk("START");
            stopMarketDataThread();
            m_hMarketDataThrdrEvnt = CreateEvent(false, false);

            m_iMarketDataThrd = true;
            m_hMarketDataThrd = _beginthreadex(marketDataThread);
            m_ptrLogger.LogOk("END");
            SetEvent(m_hMarketDataThrdrEvnt);
        }
        //=================================================================================================
        void stopMarketDataThread()
        {
            m_ptrLogger.LogOk("START");
            m_iMarketDataThrd = false;

            if (m_hMarketDataThrdrEvnt != null)
            {
                SetEvent(m_hMarketDataThrdrEvnt);
            }

            if (m_hMarketDataThrd != null)
            {
                WaitForSingleObject(m_hMarketDataThrd, INFINITE);
            }

            if (m_hMarketDataThrd != null)
            {
                CloseHandle(m_hMarketDataThrd);
            }

            if (m_hMarketDataThrdrEvnt != null)
            {
                CloseHandle(m_hMarketDataThrdrEvnt);
            }
            m_hMarketDataThrdrEvnt = null;
            m_hMarketDataThrd = null;
            m_ptrLogger.LogOk("END");
        }
        //=================================================================================================
        void insertMarketData(MarketData ptrData)
        {
            lock (m_csQueueMarketData)
            {
                m_queueMarketData.Enqueue(ptrData);
            }

            SetEvent(m_hMarketDataThrdrEvnt);
        }
        //=================================================================================================
        uint marketDataThread()
        {
            Queue<MarketData> tempQueue = new Queue<MarketData>();
            Dictionary<int, MarketData>.iterator it;
            m_ptrLogger.LogOk("START");

            while (m_iMarketDataThrd)
            {
                WaitForSingleObject(m_hMarketDataThrdrEvnt, INFINITE);

                lock (m_csQueueMarketData)
                {
                    while (!m_queueMarketData.empty() && m_iMarketDataThrd)
                    {
                        tempQueue.Enqueue(m_queueMarketData.front());
                        m_queueMarketData.pop();
                    }
                }

                while (!tempQueue.empty() && m_iMarketDataThrd)
                {
                    lock (m_SyncQuoteBook)
                    {
                        it = m_mapQuoteBook.find(tempQueue.front()._symbolIndex);
                        if (it == m_mapQuoteBook.end())
                        {
                            MarketData ptr = (MarketData)new MarketData();
                            memcpy(ref ptr, tempQueue.front());
                            m_mapQuoteBook.insert(new ValueTuple<int, MarketData>(tempQueue.front()._symbolIndex, ptr));
                        }
                        else
                        {
                            it.second = tempQueue.front();
                        }
                        broadcastMarketData(tempQueue.front());
                        tempQueue.pop();
                    }
                }
            }
            //while (!tempQueue.empty())
            //{
            //    free(tempQueue.front());
            //    tempQueue.pop();
            //}
            tempQueue.Clear();
            m_ptrLogger.LogOk("END");
            return 0;
        }
        //=================================================================================================
        void broadcastMarketData(MarketData ptrData)
        {
            //Dictionary<int, HashSet<uint>>.iterator it;
            //HashSet<uint>.iterator it2;
            //lock (m_SyncQuoteSubs)
            //{
            //    it = m_mapQuoteSubs.find(ptrData._symbolIndex);
            //    if (it != m_mapQuoteSubs.end())
            //    {
            //        for (it2 = it.second.begin(); it2 != it.second.end() && !m_isResetting; it2++)
            //        {
            MarketDataResponse ptrResp = GetMessageObject<MarketDataResponse>(MT_MarketDataResponse_ID);
            ptrResp._quote = ptrData;
            //            memcpy(ref ptrResp._quote, ptrData);
            //            SentDataUsingSocketID(ptrResp, MT_MarketDataResponse_ID, it2);
            m_connectionMgr.SentDataToAll(ptrResp, MT_MarketDataResponse_ID);
            //        }
            //    }
            //}
            //free(ptrData);
            //ptrData = null;
        }
        //=================================================================================================
        public NewAccountResponse handleNewAccountRequest(NewAccountRequest ptr)
        {
            m_ptrLogger.LogOk("START");
            m_ptrMySqlWrapper.insertMasterAccount(ptr._usrDetails);
            bool ret = true;
            NewAccountResponse response;
            if (ret)
            {
                NewAccountResponse ptrResp = GetMessageObject<NewAccountResponse>(MT_NewAccountResponse_ID);
                memcpy(ref ptrResp._usrDetails, ptr._usrDetails);
                ptrResp._retCode = RC_OK;
                response = ptrResp; //m_connectionMgr.SendResponseToQueue(ptr._header._socketID, ptrResp, MT_NewAccountResponse_ID);

                var ptrproductDetails = m_ptrMySqlWrapper.getProductDetails(ptr._productid);
                var cnt = ptrproductDetails.Count;
                for (int it = 0; it < cnt; it++)
                {
                    MT4Request ptrMT4Req = (MT4Request)new MT4Request();
                    //memset(ptrMT4Req, 0, sizeof(MT4Request));
                    ptrMT4Req.accType = ptrproductDetails[it]._accounttype;
                    COPY_STR_S(out ptrMT4Req.group, ptrproductDetails[it]._group);
                    ptrMT4Req.leverage = ptr._leverage;
                    ptrMT4Req.masterLogin = ptr._usrDetails._login;
                    ptrMT4Req.reqType = MT4REQ_NEW_ACCOUNT;
                    ptrMT4Req.requestMode = MT4REQMODE.NEW_ACCOUNT;
                    //ptrMT4Req.socketID = ptr._header._socketID;
                    ptrMT4Req.status = ORD_STAT_RECVD;
                    ptrMT4Req.serverTransID = getUniqueRequestID();
                    ptrMT4Req.ptrData = new MasterUser();
                    memcpy(ref ptrMT4Req.ptrData, ptr._usrDetails);
                    if (ptrproductDetails[it]._serverid == SRV_TYPE_DEMO)
                        m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req);
                    else
                        m_ptrLiveMT4Manager.insertMT4Request(ptrMT4Req);

                    ptrResp.MT4Requests.Add(ptrMT4Req);
                }

                /*MT4Request ptrMT4Req1 = (MT4Request)new MT4Request();
                //memset(ptrMT4Req1, 0, sizeof(MT4Request));
                ptrMT4Req1.accType = ACC_TYPE_FOLLOWER_DEMO;
                ptrMT4Req1.masterLogin = ptr._usrDetails._login;
                ptrMT4Req1.reqType = MT4REQ_NEW_ACCOUNT;
                ptrMT4Req1.requestMode = MT4REQMODE.NEW_ACCOUNT;
                ptrMT4Req1.socketID = ptr._header._socketID;
                ptrMT4Req1.status = ORD_STAT_RECVD;
                ptrMT4Req1.serverTransID = getUniqueRequestID();
                ptrMT4Req1.ptrData = new MasterUser();
                memcpy(out ptrMT4Req1.ptrData, &ptr._usrDetails, sizeof(MasterUser));
                m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req1);

                MT4Request ptrMT4Req2 = (MT4Request)new MT4Request();
                //memset(ptrMT4Req2, 0, sizeof(MT4Request));
                ptrMT4Req2.accType = ACC_TYPE_FOLLOWER_LIVE;
                ptrMT4Req2.masterLogin = ptr._usrDetails._login;
                ptrMT4Req2.reqType = MT4REQ_NEW_ACCOUNT;
                ptrMT4Req2.requestMode = MT4REQMODE.NEW_ACCOUNT;
                ptrMT4Req2.socketID = ptr._header._socketID;
                ptrMT4Req2.status = ORD_STAT_RECVD;
                ptrMT4Req2.serverTransID = getUniqueRequestID();
                ptrMT4Req2.ptrData = new MasterUser();
                memcpy(out ptrMT4Req2.ptrData, &ptr._usrDetails, sizeof(MasterUser));
                m_ptrLiveMT4Manager.insertMT4Request(ptrMT4Req2);

                MT4Request ptrMT4Req3 = (MT4Request)new MT4Request();
                //memset(ptrMT4Req3, 0, sizeof(MT4Request));
                ptrMT4Req3.accType = ACC_TYPE_REBATE;
                ptrMT4Req3.masterLogin = ptr._usrDetails._login;
                ptrMT4Req3.reqType = MT4REQ_NEW_ACCOUNT;
                ptrMT4Req3.requestMode = MT4REQMODE.NEW_ACCOUNT;
                ptrMT4Req3.socketID = ptr._header._socketID;
                ptrMT4Req3.status = ORD_STAT_RECVD;
                ptrMT4Req3.serverTransID = getUniqueRequestID();
                ptrMT4Req3.ptrData = new MasterUser();
                memcpy(out ptrMT4Req3.ptrData, &ptr._usrDetails, sizeof(MasterUser));
                m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req3); */

            }//if (ret)
            else
            {
                NewAccountResponse ptrResp = GetMessageObject<NewAccountResponse>(MT_NewAccountResponse_ID);
                memcpy(ref ptrResp._usrDetails, ptr._usrDetails);
                ptrResp._retCode = RC_ERROR;
                response = ptrResp; //  m_connectionMgr.SendResponseToQueue(ptr._header._socketID, ptrResp, MT_NewAccountResponse_ID);
            }//else of if (ret)
            m_ptrLogger.LogOk("END");
            Debug.Assert(response._usrDetails._login > 0);
            insertDBTransmitData(response._usrDetails._login, FDMT_MasterUser_ID);
            //send email to user
            //sendRegistrationEmail(ptr->_usrDetails._login, ptr->_usrDetails._password, ptr-            
            return response;
        }
        //=================================================================================================
        public MasterAccTransResponse handleMasterAccountTransRequest(MasterAccTransRequest ptr)
        {
            MasterAccTransResponse ptrResp = null;
            m_ptrLogger.LogOk("START");
            if (ptr._dataTransMode == DT_TRANS_ADD)
            {
                m_ptrMySqlWrapper.insertMasterAccount(ptr._masteruser);
                bool ret = true;
                if (ret)
                {
                    ptrResp = GetMessageObject<MasterAccTransResponse>(MT_MasterAccTransResponse_ID);
                    ptrResp._dataTransMode = ptr._dataTransMode;
                    memcpy(ref ptrResp._masteruser, ptr._masteruser);
                    ptrResp._retCode = RC_OK;
                    //SentDataUsingSocketID(ptrResp, MT_MasterAccTransResponse_ID, ptr._header._socketID);
                    //openDefaultTwoMT4LiveDemoAccount(&ptr._masteruser);
                }
            }
            else if (ptr._dataTransMode == DT_TRANS_DELETE)
            {
                //NOT SUPPORTED
            }
            else if (ptr._dataTransMode == DT_TRANS_MODIFY)
            {
                m_ptrMySqlWrapper.updateMasterAccount(ptr._masteruser);
                bool ret = true;
                if (ret)
                {
                    ptrResp = GetMessageObject<MasterAccTransResponse>(MT_MasterAccTransResponse_ID);
                    ptrResp._dataTransMode = ptr._dataTransMode;
                    memcpy(ref ptrResp._masteruser, ptr._masteruser);
                    ptrResp._retCode = eReturnCode.RC_OK;
                    //SentDataUsingSocketID(ptrResp, MT_MasterAccTransResponse_ID, ptr._header._socketID);
                }
            }
            m_ptrLogger.LogOk("END");
            Debug.Assert(ptr._header._loginID > 0);
            insertDBTransmitData(ptr._header._loginID, FDMT_MasterUser_ID);
            return ptrResp;
        }
        //=================================================================================================
        //void sendMarketSnapShot(int masterLogin, uint socketID)
        //{
        //    Dictionary<int, MarketData>.iterator it;
        //    MarketData[] ptrFileData = null;
        //    int structCnt = 0;
        //    lock (m_SyncQuoteBook)
        //    {
        //        int index = 0;
        //        structCnt = m_mapQuoteBook.size();
        //        if (structCnt > 0)
        //        {
        //            ptrFileData = new MarketData[structCnt];
        //            for (it = m_mapQuoteBook.begin(); it != m_mapQuoteBook.end() && !m_isResetting; it++)
        //            {
        //                memcpy(ref ptrFileData[index], it.second);
        //                ++index;
        //            }
        //        }
        //    }
        //    if (structCnt > 0)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_MarketData_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_MarketData_ID;
        //        //ptrFileResp._header._fileSize = sizeof(MarketData) * structCnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrFileData);
        //    }
        //}
        //=================================================================================================

        public SSPSignalCreateResult handleSSPAccountRequest(SSPSignalCreateRequest ptr)
        {
            m_ptrLogger.LogOk("START");

            if (m_ptrMySqlWrapper.getSSPSignalCount(ptr._masterLogin) >= m_masterUserSetting._maxSSPAccount)
            {
                m_ptrLogger.LogWarning("Max SSP Signal reached. Ignoring SSP request");
                return null;
            }
            if (m_ptrMySqlWrapper.isSignalNameExist(ptr._signalName))
            {
                m_ptrLogger.LogWarning("Signal name already exist.... %s", ptr._signalName);
                return null;
            }

            MasterUser ptrUser = m_ptrMySqlWrapper.getMasterAccount(ptr._masterLogin);
            var result = new SSPSignalCreateResult();

            if (ptrUser != null)
            {
                Signal sig = new Signal();
                sig._isSSP = true;
                sig._masterLogin = ptr._masterLogin;
                COPY_STR_S(out sig._signalName, ptr._signalName);
                sig._signalIndex = -1;
                sig._mt4ServerIndex = SRV_TYPE_DEMO;
                sig._mt4Login = -1;

                var ptrMT4Req2 = (MT4Request)new MT4Request();
                //memset(ptrMT4Req2, 0, sizeof(MT4Request));
                ptrMT4Req2.accType = ACC_TYPE_SSP;
                ptrMT4Req2.masterLogin = ptrUser._login;
                ptrMT4Req2.reqType = MT4REQ_NEW_ACCOUNT;
                //ptrMT4Req2.socketID = ptr._header._socketID;
                ptrMT4Req2.status = ORD_STAT_RECVD;
                ptrMT4Req2.serverTransID = getUniqueRequestID();
                ptrMT4Req2.ptrData = (MasterUser)new MasterUser();
                memcpy(ref ptrMT4Req2.ptrData, ptrUser);

                sig._mt4Login = (int)ptrMT4Req2.serverTransID;
                sig._rebateAmount = m_masterUserSetting._rebate_SSP;
                sig._sspPercentage = 100.0F;
                m_ptrLogger.LogInfo("Inserting SSP signal in DB Signal: %s MasterLogin: %d Dummy MT4 Login: %d", ptr._signalName, ptr._masterLogin, sig._mt4Login);
                m_ptrMySqlWrapper.insertSignal(sig);
                bool res = true;
                if (res)
                {
                    SSPSignal ssp = new SSPSignal();
                    ssp._signalIndex = sig._signalIndex;
                    ssp._sspMasterLogin = sig._masterLogin;
                    ssp._sspMT4Login = sig._mt4Login;
                    ssp._sspMT4ServerIndex = SRV_TYPE_DEMO;
                    ssp._strategyType = ptr._strategyType;
                    ssp._symbolIndex = ptr._symbolIndex;
                    /*if (m_ptrMySqlWrapper.insertSSPSignal(&ssp))
                    {
                        SSPSignalCreateResponse ptrResp = GetMessageObject<SSPSignalCreateResponse>(MT_SSPSignalCreateResponse_ID);
                        ptrResp._header._loginID = ptr._header._loginID;
                        ptrResp._header._socketID = ptr._header._socketID;
                        ptrResp._retCode = eReturnCode.RC_OK;
                        memcpy(out ptrResp._sspSignal, &ssp, sizeof(SSPSignal));
                        SentDataUsingSocketID(ptrResp, MT_SSPSignalCreateResponse_ID, ptrResp._header._socketID);

                        m_ptrLogger.LogOk("Sending request to connector for opening SSP MT4 account");
                        m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req2);
                    }
                    else
                    {
                        m_ptrLogger.LogError("Unable to insert SSPSignal for signal Index: %d master login: %d Dummy MT4 Login: %d", ssp._signalIndex, ptr._masterLogin, sig._mt4Login);
                    } */

                    //When SSP MT4 account created then we will send response to client side
                    m_ptrMySqlWrapper.insertSSPSignal(ssp);
                    result.SSPSignal = ssp;
                    bool res2 = true;
                    if (!res2)
                    {
                        m_ptrLogger.LogError("Unable to insert SSPSignal for signal Index: %d master login: %d Dummy MT4 Login: %d", ssp._signalIndex, ptr._masterLogin, sig._mt4Login);
                    }
                    else
                    {
                        m_ptrLogger.LogOk("Sending request to connector for opening SSP MT4 account");
                        m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req2);
                        result.MT4Request = ptrMT4Req2;
                    }
                }
                else
                {
                    m_ptrLogger.LogError("Unable to insert SSP signal in DB Signal: %s MasterLogin: %d Dummy MT4 Login: %d", ptr._signalName, ptr._masterLogin, sig._mt4Login);
                }

                free(ptrUser);
                ptrUser = null;

            }
            m_ptrLogger.LogOk("END");
            return result;
        }
        //=================================================================================================
        public SMSignalCreateResponse handleSMAccountRequest(SMSignalCreateRequest ptr)
        {
            SMSignalCreateResponse ptrResp = null;
            m_ptrLogger.LogOk("START");

            if (ptr._isSignalCreated)
            {
                if (m_ptrMySqlWrapper.getSSPCountForSMSignal(ptr._smSignal._smMasterLogin, ptr._smSignal._signalIndex) >= m_masterUserSetting._maxSSPInSM)
                {
                    m_ptrLogger.LogInfo("Max SSP Signal reached for SM signal index %d. Ignoring SM request", ptr._smSignal._signalIndex);
                    return null;
                }
                SMSignal smSig = new SMSignal();
                //memset(&smSig, 0, sizeof(SMSignal));

                smSig._signalIndex = ptr._smSignal._signalIndex;
                smSig._smMasterLogin = ptr._smSignal._smMasterLogin;
                smSig._smMT4Login = ptr._smSignal._smMT4Login;
                smSig._smMT4ServerIndex = SRV_TYPE_DEMO;
                smSig._sspMasterLogin = ptr._smSignal._sspMasterLogin;
                smSig._sspMT4Login = ptr._smSignal._sspMT4Login;
                smSig._sspMT4ServerIndex = ptr._smSignal._sspMT4ServerIndex;
                smSig._sspSignalIndex = ptr._smSignal._sspSignalIndex;

                m_ptrMySqlWrapper.insertSMSignal(smSig);
                bool res = true;

                if (res)
                {
                    ptrResp = GetMessageObject<SMSignalCreateResponse>(MT_SMSignalCreateResponse_ID);
                    ptrResp._header._loginID = ptr._header._loginID;
                    //ptrResp._header._socketID = ptr._header._socketID;
                    ptrResp._retCode = RC_OK;
                    //memcpy(ref ptrResp._smSignal, smSig);
                    ptrResp._smSignal = smSig;
                    //SentDataUsingSocketID(ptrResp, MT_SSPSignalCreateResponse_ID, ptrResp._header._socketID);

                    addFollower(smSig._sspMT4Login, smSig._smMT4Login, smSig._smMT4ServerIndex);
                    addFollowerVolume(smSig._sspSignalIndex, smSig._smMT4ServerIndex, smSig._smMT4Login, m_masterUserSetting._signalTradeVolume);
                }
                else
                {
                    m_ptrLogger.LogError("Unable to insert SMSignal for signal Index: %d master login: %d MT4 Login: %d", smSig._signalIndex, ptr._smSignal._smMasterLogin, smSig._smMT4Login);
                }
            }
            else
            {
                if (m_ptrMySqlWrapper.getSMSignalCount(ptr._smSignal._smMasterLogin) >= m_masterUserSetting._maxSMAccount)
                {
                    m_ptrLogger.LogWarning("Max SM Signal reached. Ignoring SM request");
                    return null;
                }

                if (m_ptrMySqlWrapper.isSignalNameExist(ptr._signalName))
                {
                    m_ptrLogger.LogWarning("Signal name already exist.... %s", ptr._signalName);
                    return null;
                }


                MasterUser ptrUser = m_ptrMySqlWrapper.getMasterAccount(ptr._smSignal._smMasterLogin);
                if (ptrUser != null)
                {
                    Signal sig = new Signal();
                    sig._isSSP = false;
                    sig._masterLogin = ptr._smSignal._smMasterLogin;
                    COPY_STR_S(out sig._signalName, ptr._signalName);
                    sig._signalIndex = -1;
                    sig._mt4ServerIndex = SRV_TYPE_DEMO;
                    sig._mt4Login = -1;


                    MT4Request ptrMT4Req2 = (MT4Request)new MT4Request();
                    //memset(ptrMT4Req2, 0, sizeof(MT4Request));
                    ptrMT4Req2.accType = ACC_TYPE_SM;
                    ptrMT4Req2.masterLogin = ptr._smSignal._smMasterLogin;
                    ptrMT4Req2.reqType = MT4REQ_NEW_ACCOUNT;
                    //ptrMT4Req2.socketID = ptr._header._socketID;
                    ptrMT4Req2.status = ORD_STAT_RECVD;
                    ptrMT4Req2.serverTransID = getUniqueRequestID();
                    ptrMT4Req2.ptrData = (MasterUser)new MasterUser();
                    memcpy(ref ptrMT4Req2.ptrData, ptrUser);

                    sig._mt4Login = (int)ptrMT4Req2.serverTransID;
                    sig._rebateAmount = m_masterUserSetting._rebate_SM;
                    sig._sspPercentage = m_masterUserSetting._ssp_per;
                    m_ptrLogger.LogInfo("Inserting SM signal in DB Signal: %s MasterLogin: %d Dummy MT4 Login: %d", ptr._signalName, ptr._smSignal._smMasterLogin, sig._mt4Login);

                    m_ptrMySqlWrapper.insertSignal(sig);
                    bool res2 = true;

                    if (res2)
                    {
                        if (ptr._smSignal._sspMT4Login != 0)
                        {
                            SMSignal smSig = new SMSignal();
                            //memset(&smSig, 0, sizeof(SMSignal));
                            smSig._signalIndex = sig._signalIndex;
                            smSig._smMasterLogin = sig._masterLogin;
                            smSig._smMT4Login = sig._mt4Login;
                            smSig._smMT4ServerIndex = SRV_TYPE_DEMO;
                            smSig._sspMasterLogin = ptr._smSignal._sspMasterLogin;
                            smSig._sspMT4Login = ptr._smSignal._sspMT4Login;
                            smSig._sspMT4ServerIndex = ptr._smSignal._sspMT4ServerIndex;
                            smSig._sspSignalIndex = ptr._smSignal._sspSignalIndex;

                            m_ptrMySqlWrapper.insertSMSignal(smSig);
                            bool res3 = true;

                            if (res3)
                            {
                                ptrResp = GetMessageObject<SMSignalCreateResponse>(MT_SMSignalCreateResponse_ID);
                                ptrResp._header._loginID = ptr._header._loginID;
                                //ptrResp._header._socketID = ptr._header._socketID;
                                ptrResp._retCode = RC_OK;
                                //memcpy(ref ptrResp._smSignal, smSig);
                                ptrResp._smSignal = smSig;
                                ptrResp.Signal = sig;
                                //SentDataUsingSocketID(ptrResp, MT_SSPSignalCreateResponse_ID, ptrResp._header._socketID);

                                addFollower(smSig._sspMT4Login, smSig._smMasterLogin, smSig._smMT4ServerIndex);
                                addFollowerVolume(smSig._sspSignalIndex, smSig._smMT4ServerIndex, smSig._smMasterLogin, m_masterUserSetting._signalTradeVolume);
                            }
                            else
                            {
                                m_ptrLogger.LogError("Unable to insert SMSignal for signal Index: %d master login: %d MT4 Login: %d", smSig._signalIndex, ptr._smSignal._smMasterLogin, sig._mt4Login);
                            }

                        }

                    }
                    else
                    {
                        m_ptrLogger.LogError("Unable to insert SM signal in DB Signal: %s MasterLogin: %d Dummy MT4 Login: %d", ptr._signalName, ptr._smSignal._smMasterLogin, sig._mt4Login);
                    }
                    m_ptrLogger.LogOk("Sending request to connector for opening SM MT4 account");
                    m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req2);

                    if (ptrResp == null)
                    {
                        ptrResp = new SMSignalCreateResponse { _header = new MessageHeader { _loginID = ptr._header._loginID } };
                    }

                    ptrResp.MT4Request = ptrMT4Req2;
                    free(ptrUser);
                    ptrUser = null;
                }
            }
            m_ptrLogger.LogOk("END");
            return ptrResp;
        }//public void handleSMAccountRequest(SMSignalCreateRequest ptr)
         //=================================================================================================
        public AddSSPFollowerResponse handleSSPFollowerRequest(AddSSPFollowerRequest ptr)
        {
            AddSSPFollowerResponse ptrResp = null;
            m_ptrLogger.LogOk("START");

            if (ptr._dataTransMode == DT_TRANS_ADD)
            {
                m_ptrMySqlWrapper.insertSSPFollower(ptr._sspfollower);
                bool ret = true;
                ptrResp = GetMessageObject<AddSSPFollowerResponse>(MT_AddSSPFollowerResponse_ID);
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                memcpy(ref ptrResp._sspfollower, ptr._sspfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSSPFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    addFollower(ptr._sspfollower._sspMT4Login, ptr._sspfollower._followerMT4Login, ptr._sspfollower._followerMT4ServerIndex);
                    addFollowerVolume(ptr._sspfollower._sspSignalIndex, ptr._sspfollower._followerMT4ServerIndex, ptr._sspfollower._followerMT4Login, ptr._sspfollower._followervolume);

                    m_ptrLogger.LogInfo(
                        "SSP follower added. Follower master login: %d SSP master login : %d SignalIndex: %d",
                        ptr._sspfollower._followerMasterLogin, ptr._sspfollower._sspMasterLogin, ptr._sspfollower._sspSignalIndex);
                }
                else
                {
                    m_ptrLogger.LogError(
                        "Unable to add SSP follower. Follower master login: %d SSP master login : %d SignalIndex: %d",
                        ptr._sspfollower._followerMasterLogin, ptr._sspfollower._sspMasterLogin, ptr._sspfollower._sspSignalIndex);
                }
            }
            else if (ptr._dataTransMode == DT_TRANS_MODIFY)
            {
                m_ptrMySqlWrapper.modifySSPFollower(ptr._sspfollower);
                bool ret = true;
                ptrResp = GetMessageObject<AddSSPFollowerResponse>(MT_AddSSPFollowerResponse_ID);
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                memcpy(ref ptrResp._sspfollower, ptr._sspfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSSPFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    updateFollowerVolume(ptr._sspfollower._sspSignalIndex, ptr._sspfollower._followerMT4ServerIndex, ptr._sspfollower._followerMT4Login, ptr._sspfollower._followervolume);

                    m_ptrLogger.LogInfo(
                        "SSP follower modified. Follower master login: %d SSP master login : %d SignalIndex: %d",
                        ptr._sspfollower._followerMasterLogin, ptr._sspfollower._sspMasterLogin, ptr._sspfollower._sspSignalIndex);
                }
                else
                {
                    m_ptrLogger.LogError(
                        "Unable to modify SSP follower. Follower master login: %d SSP master login : %d SignalIndex: %d",
                        ptr._sspfollower._followerMasterLogin, ptr._sspfollower._sspMasterLogin, ptr._sspfollower._sspSignalIndex);
                }
            }
            else if (ptr._dataTransMode == DT_TRANS_DELETE)
            {
                m_ptrMySqlWrapper.deleteSSPFollower(ptr._sspfollower);
                bool ret = true;
                ptrResp = GetMessageObject<AddSSPFollowerResponse>(MT_AddSSPFollowerResponse_ID);
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                memcpy(ref ptrResp._sspfollower, ptr._sspfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSSPFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    removeFollower(ptr._sspfollower._sspMT4Login, ptr._sspfollower._followerMT4Login, ptr._sspfollower._followerMT4ServerIndex);
                    removeFollowerVolume(ptr._sspfollower._sspSignalIndex, ptr._sspfollower._followerMT4ServerIndex, ptr._sspfollower._followerMT4Login, ptr._sspfollower._followervolume);

                    m_ptrLogger.LogInfo(
                        "SSP follower deleted. Follower master login: %d SSP master login : %d SignalIndex: %d",
                        ptr._sspfollower._followerMasterLogin, ptr._sspfollower._sspMasterLogin, ptr._sspfollower._sspSignalIndex);
                }
                else
                {
                    m_ptrLogger.LogError(
                        "Unable to delete SSP follower. Follower master login: %d SSP master login : %d SignalIndex: %d",
                        ptr._sspfollower._followerMasterLogin, ptr._sspfollower._sspMasterLogin, ptr._sspfollower._sspSignalIndex);
                }
            }

            m_ptrLogger.LogOk("END");
            return ptrResp;
        }

        public GetSSPFollowerResponse handleGetSSPFollowerRequest(GetSSPFollowerRequest ptr)
        {
            m_ptrLogger.LogOk("START");
            GetSSPFollowerResponse response = null;
            Sys.IReadOnlyList<SSPFollowerUser> ptrAllFollowers =
                m_ptrMySqlWrapper.getAllSSPFollowerUsers(ptr._sspmt4login, ptr._followermasterlogin);

            if (ptrAllFollowers != null)
            {
                /*GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_SSPFollower_ID;
                ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_SSPFollower_ID;
                //ptrFileResp._header._fileSize = sizeof(SSPFollower) * cnt;
                ptrFileResp._header._loginID = ptr._header._loginID;
                ptrFileResp._header._socketID = ptr._header._socketID;
                m_connectionMgr.SendAsFile(ptr._header._socketID, ptrFileResp, ptrAllFollowers); */

                GetSSPFollowerResponse ptrResp = GetMessageObject<GetSSPFollowerResponse>(MT_GetSSPFollowerResponse_ID);
                ptrResp._sspfollower = ptrAllFollowers;
                ptrResp._retCode = RC_OK;
                response = ptrResp;//  m_connectionMgr.SendResponseToQueue(ptr._header._socketID, ptrResp, MT_GetSSPFollowerResponse_ID);
            }
            m_ptrLogger.LogOk("END");
            return response;
        }
        //=================================================================================================
        public SMSignalRemoveResponse handleRemoveSSPinSMRequest(SMSignalRemoveRequest ptr)
        {
            m_ptrLogger.LogOk("START");

            m_ptrMySqlWrapper.deleteSSPinSM(ptr._smSignal);
            bool ret = true;
            SMSignalRemoveResponse ptrResp = GetMessageObject<SMSignalRemoveResponse>(MT_RemoveSMSignalResponse_ID);
            ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
            ptrResp._dataTransMode = ptr._dataTransMode;
            memcpy(ref ptrResp._smSignal, ptr._smSignal);
            //SentDataUsingSocketID(ptrResp, MT_RemoveSMSignalResponse_ID, ptr._header._socketID);
            if (ret)
            {
                removeFollower(ptr._smSignal._smMT4Login, ptr._smSignal._sspMT4Login, ptr._smSignal._sspMT4ServerIndex);
                //removeFollowerVolume(ptr._smSignal._sspSignalIndex, ptr._smSignal._sspMT4Login, ptr._smSignal._followerMT4Login, ptr._smSignal._followervolume);

                m_ptrLogger.LogInfo(
                    "SSP %d in SM %d deleted", ptr._smSignal._sspMasterLogin, ptr._smSignal._smMasterLogin);
            }
            else
            {
                m_ptrLogger.LogError(
                    "Unable to delete %d SSP from %d SM", ptr._smSignal._sspMasterLogin, ptr._smSignal._smMasterLogin);
            }

            m_ptrLogger.LogOk("END");
            return ptrResp;
        }
        //=================================================================================================
        public AddSMFollowerResponse handleSMFollowerRequest(AddSMFollowerRequest ptr)
        {
            AddSMFollowerResponse ptrResp = null;
            m_ptrLogger.LogOk("START");

            if (ptr._dataTransMode == DT_TRANS_ADD)
            {
                m_ptrMySqlWrapper.insertSMFollower(ptr._smfollower);
                bool ret = true;
                ptrResp = GetMessageObject<AddSMFollowerResponse>(MT_AddSMFollowerResponse_ID);
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                memcpy(ref ptrResp._smfollower, ptr._smfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSMFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    addFollower(ptr._smfollower._smMT4Login, ptr._smfollower._followerMT4Login, ptr._smfollower._followerMT4ServerIndex);
                    addFollowerVolume(ptr._smfollower._smSignalIndex, ptr._smfollower._followerMT4ServerIndex, ptr._smfollower._followerMT4Login, ptr._smfollower._followervolume);

                    m_ptrLogger.LogInfo(
                        "SM follower added Follower master login: %d SM master login : %d SignalIndex: %d",
                        ptr._smfollower._followerMasterLogin, ptr._smfollower._smMasterLogin, ptr._smfollower._smSignalIndex);
                }
                else
                {
                    m_ptrLogger.LogError(
                        "Unable to add SM follower added Follower master login: %d SM master login : %d SignalIndex: %d",
                        ptr._smfollower._followerMasterLogin, ptr._smfollower._smMasterLogin, ptr._smfollower._smSignalIndex);
                }
            }
            else if (ptr._dataTransMode == DT_TRANS_MODIFY)
            {
                m_ptrMySqlWrapper.modifySMFollower(ptr._smfollower);
                bool ret = true;
                ptrResp = GetMessageObject<AddSMFollowerResponse>(MT_AddSMFollowerResponse_ID);
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                memcpy(ref ptrResp._smfollower, ptr._smfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSMFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    updateFollowerVolume(ptr._smfollower._smSignalIndex, ptr._smfollower._followerMT4ServerIndex, ptr._smfollower._followerMT4Login, ptr._smfollower._followervolume);
                    m_ptrLogger.LogInfo(
                        "SM follower modified. Follower master login: %d SM master login : %d SignalIndex: %d",
                        ptr._smfollower._followerMasterLogin, ptr._smfollower._smMasterLogin, ptr._smfollower._smSignalIndex);
                }
                else
                {
                    m_ptrLogger.LogError(
                        "Unable to modify SM follower. Follower master login: %d SM master login : %d SignalIndex: %d",
                        ptr._smfollower._followerMasterLogin, ptr._smfollower._smMasterLogin, ptr._smfollower._smSignalIndex);
                }
            }
            else if (ptr._dataTransMode == DT_TRANS_DELETE)
            {
                m_ptrMySqlWrapper.deleteSMFollower(ptr._smfollower);
                bool ret = true;
                ptrResp = GetMessageObject<AddSMFollowerResponse>(MT_AddSMFollowerResponse_ID);
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                memcpy(ref ptrResp._smfollower, ptr._smfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSMFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    removeFollower(ptr._smfollower._smMT4Login, ptr._smfollower._followerMT4Login, ptr._smfollower._followerMT4ServerIndex);
                    removeFollowerVolume(ptr._smfollower._smSignalIndex, ptr._smfollower._followerMT4ServerIndex, ptr._smfollower._followerMT4Login, ptr._smfollower._followervolume);

                    m_ptrLogger.LogInfo(
                        "SM follower deleted Follower master login: %d SM master login : %d SignalIndex: %d",
                        ptr._smfollower._followerMasterLogin, ptr._smfollower._smMasterLogin, ptr._smfollower._smSignalIndex);
                }
                else
                {
                    m_ptrLogger.LogError(
                        "Unable to delete SM follower added Follower master login: %d SM master login : %d SignalIndex: %d",
                        ptr._smfollower._followerMasterLogin, ptr._smfollower._smMasterLogin, ptr._smfollower._smSignalIndex);
                }
            }

            m_ptrLogger.LogOk("END");
            return ptrResp;
        }

        public GetSMFollowerResponse handleGetSMFollowerRequest(GetSMFollowerRequest ptr)
        {
            m_ptrLogger.LogOk("START");
            GetSMFollowerResponse ptrResp = null;
            Sys.IReadOnlyList<SMFollowerUser> ptrAllFollowers = m_ptrMySqlWrapper.getAllSMFollowerUsers(ptr._smmt4login, ptr._followermasterlogin);

            if (ptrAllFollowers != null)
            {
                /*GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_SSPFollower_ID;
                ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_SSPFollower_ID;
                //ptrFileResp._header._fileSize = sizeof(SMFollower) * cnt;
                ptrFileResp._header._loginID = ptr._header._loginID;
                ptrFileResp._header._socketID = ptr._header._socketID;
                m_connectionMgr.SendAsFile(ptr._header._socketID, ptrFileResp, ptrAllFollowers); */

                ptrResp = GetMessageObject<GetSMFollowerResponse>(MT_GetSMFollowerResponse_ID);
                ptrResp._smfollower = ptrAllFollowers;
                ptrResp._retCode = RC_OK;
                //m_connectionMgr.SendResponseToQueue(ptr._header._socketID, ptrResp, MT_GetSMFollowerResponse_ID);
            }
            m_ptrLogger.LogOk("END");
            return ptrResp;
        }
        //=================================================================================================
        //void sendInitialDataToUser(int masterLogin, uint socketID)
        //{
        //    m_ptrLogger.LogOk("START");
        //    int cnt = 0;

        //    //---------Sending user setting
        //    MasterSettingUpdateResponse ptrResp1 = GetMessageObject<MasterSettingUpdateResponse>(MT_MasterSettingUpdateResponse_ID);
        //    ptrResp1._header._loginID = masterLogin;
        //    ptrResp1._header._socketID = socketID;
        //    memcpy(ref ptrResp1._mastersetting, m_masterUserSetting);
        //    ptrResp1._mastersetting._demoManagerLogin = -1;
        //    //memset(ptrResp1._mastersetting._demoManagerPassword, 0, 16);
        //    ptrResp1._mastersetting._liveManagerLogin = -1;
        //    //memset(ptrResp1._mastersetting._liveManagerPassword, 0, 16);
        //    //SentDataUsingSocketID(ptrResp1, MT_MasterSettingUpdateResponse_ID, socketID);
        //    //-------Sending  all symbol
        //    var ptrSymArr = m_ptrMySqlWrapper.getAllMT4Symbols();
        //    cnt = ptrSymArr.Count;
        //    if (cnt > 0)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_MT4SymbolInfo_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_MT4SymbolInfo_ID;
        //        //ptrFileResp._header._fileSize = sizeof(MT4SymbolInfo) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSymArr);
        //    }
        //    //---------Sending signal symbol setting
        //    var ptrSSSArr = m_ptrMySqlWrapper.getAllSignalSymbolSettings();
        //    cnt = ptrSSSArr.Count;
        //    if (cnt > 0)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_SignalSymbolSetting_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_SignalSymbolSetting_ID;
        //        //ptrFileResp._header._fileSize = sizeof(SignalSymbolSetting) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSSSArr);
        //    }
        //    //---------Sending market snap shot
        //    sendMarketSnapShot(masterLogin, socketID);
        //    //---------Sending all mt4 accs
        //    List<int> demoMT4Login = new List<int>();
        //    List<int> liveMT4Login = new List<int>();
        //    var ptrAccArr = m_ptrMySqlWrapper.getAllMT4AccountsForMasterLogin(masterLogin);
        //    cnt = ptrAccArr.Count;
        //    if (cnt > 0)
        //    {
        //        for (int iAccLoop = 0; iAccLoop < cnt; iAccLoop++)
        //        {
        //            if (ptrAccArr[iAccLoop]._mt4ServerIndex == SRV_TYPE_DEMO)
        //            {
        //                demoMT4Login.push_back(ptrAccArr[iAccLoop]._mt4Login);
        //            }
        //            else
        //            {
        //                liveMT4Login.push_back(ptrAccArr[iAccLoop]._mt4Login);
        //            }
        //        }
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_MT4Account_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_MT4Account_ID;
        //        //ptrFileResp._header._fileSize = sizeof(MT4Account) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAccArr);
        //    }
        //    //--------sending all trade disable info
        //    /*MT4TradeDisableInfo ptrMt4DisableTrades = m_ptrMySqlWrapper.getAllTradeDisableInfos(cnt, masterLogin);
        //    if (cnt > 0)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_MT4TradeDisableInfo_ID;
        //        ptrFileResp._reserved1 = m_ptrDemoMT4Manager.getServerTime();
        //        ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_MT4TradeDisableInfo_ID;
        //        //ptrFileResp._header._fileSize = sizeof(MT4TradeDisableInfo)*cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAccArr, sizeof(MT4TradeDisableInfo)*cnt);
        //    } */

        //    //-----------sending all signal
        //    var ptrAllSignal = m_ptrMySqlWrapper.getAllSignals();
        //    cnt = ptrAllSignal.Count;

        //    if (cnt > 0 && ptrAllSignal != null)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_Signal_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_Signal_ID;
        //        //ptrFileResp._header._fileSize = sizeof(Signal) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSignal);
        //    }


        //    ////-----------sending all SSP signals
        //    var ptrAllSignalSSP = m_ptrMySqlWrapper.getAllSSPSignals();
        //    cnt = ptrAllSignalSSP.Count;
        //    if (cnt > 0 && ptrAllSignalSSP != null)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_SSPSignal_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_SSPSignal_ID;
        //        //ptrFileResp._header._fileSize = sizeof(SSPSignal) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSignalSSP);
        //    }

        //    //-----------sending all SM signals
        //    var ptrAllSignalSM = m_ptrMySqlWrapper.getAllSMSignals();
        //    cnt = ptrAllSignalSM.Count;
        //    if (cnt > 0 && ptrAllSignalSM != null)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_SMSignal_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_SMSignal_ID;
        //        //ptrFileResp._header._fileSize = sizeof(SMSignal) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSignalSM);
        //    }
        //    //---------sending all SSP subscription

        //    var ptrAllSSPFollower = m_ptrMySqlWrapper.getSSPSubscribedSignals(masterLogin);
        //    cnt = ptrAllSSPFollower.Count;
        //    if (cnt > 0 && ptrAllSSPFollower != null)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_SSPFollower_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_SSPFollower_ID;
        //        //ptrFileResp._header._fileSize = sizeof(SSPFollower) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSSPFollower);
        //    }
        //    //---------sending all SM subscription
        //    var ptrAllSMFollower = m_ptrMySqlWrapper.getSMSubscribedSignals(masterLogin);
        //    cnt = ptrAllSMFollower.Count;
        //    if (cnt > 0 && ptrAllSMFollower != null)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_SMFollower_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_SMFollower_ID;
        //        //ptrFileResp._header._fileSize = sizeof(SMFollower) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSMFollower);
        //    }
        //    //---------sending ranking
        //    var ptrAllRanking = m_ptrMySqlWrapper.readSignalD1data(30);
        //    cnt = ptrAllRanking.Count;
        //    if (cnt > 0 && ptrAllRanking != null)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._fileDataMessageType = FDMT_Ranking_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_Ranking_ID;
        //        //ptrFileResp._header._fileSize = sizeof(Ranking) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllRanking);
        //    }
        //    //-------
        //    if (isThisAdmin(masterLogin))
        //    {
        //        var ptrMUsers = m_ptrMySqlWrapper.getAllMasterAccounts();
        //        cnt = ptrMUsers.Count;
        //        if (cnt > 0)
        //        {
        //            GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //            ptrFileResp._fileDataMessageType = FDMT_MasterUser_ID;
        //            ptrFileResp._header._fileDataMessageType = FDMT_MasterUser_ID;
        //            //ptrFileResp._header._fileSize = sizeof(MasterUser) * cnt;
        //            ptrFileResp._header._loginID = masterLogin;
        //            ptrFileResp._header._socketID = socketID;
        //            m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrMUsers);
        //        }

        //        var ptrAllSMFollower2 = m_ptrMySqlWrapper.getSMSubscribedSignals();
        //        cnt = ptrAllSMFollower2.Count;
        //        if (cnt > 0)
        //        {
        //            GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //            ptrFileResp._fileDataMessageType = FDMT_SMFollower_ID;
        //            ptrFileResp._header._fileDataMessageType = FDMT_SMFollower_ID;
        //            //ptrFileResp._header._fileSize = sizeof(SMFollower) * cnt;
        //            ptrFileResp._header._loginID = masterLogin;
        //            ptrFileResp._header._socketID = socketID;
        //            m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSMFollower2);
        //        }

        //        var ptrAllSSPFollower2 = m_ptrMySqlWrapper.getSSPSubscribedSignals();
        //        cnt = ptrAllSSPFollower2.Count;
        //        if (cnt > 0)
        //        {
        //            GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //            ptrFileResp._fileDataMessageType = FDMT_SSPFollower_ID;
        //            ptrFileResp._header._fileDataMessageType = FDMT_SSPFollower_ID;
        //            //ptrFileResp._header._fileSize = sizeof(SSPFollower) * cnt;
        //            ptrFileResp._header._loginID = masterLogin;
        //            ptrFileResp._header._socketID = socketID;
        //            m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAllSSPFollower2);
        //        }

        //        var ptrAccArr2 = m_ptrMySqlWrapper.getAllMT4Accounts();
        //        cnt = ptrAccArr2.Count;
        //        if (cnt > 0)
        //        {
        //            GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //            ptrFileResp._fileDataMessageType = FDMT_MT4Account_ID;
        //            ptrFileResp._header._fileDataMessageType = FDMT_MT4Account_ID;
        //            //ptrFileResp._header._fileSize = sizeof(MT4Account) * cnt;
        //            ptrFileResp._header._loginID = masterLogin;
        //            ptrFileResp._header._socketID = socketID;
        //            m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAccArr2);
        //        }
        //    }

        //    List<int>.iterator itVec;
        //    for (itVec = demoMT4Login.begin(); itVec != demoMT4Login.end(); itVec++)
        //    {
        //        m_ptrDemoMT4Manager.insertMarginRequest(itVec);
        //    }
        //    for (itVec = liveMT4Login.begin(); itVec != liveMT4Login.end(); itVec++)
        //    {
        //        m_ptrLiveMT4Manager.insertMarginRequest(itVec);
        //    }

        //    var currtime = m_ptrDemoMT4Manager.getServerTime();
        //    var ptrTradesDemoArr = m_ptrMySqlWrapper.getDemoCloseTradesPvt(demoMT4Login, currtime.AddDays(-7), currtime);
        //    cnt = ptrTradesDemoArr.Count;
        //    if (cnt > 0)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._reserved1 = masterLogin;
        //        ptrFileResp._reserved2 = SRV_TYPE_DEMO;
        //        ptrFileResp._fileDataMessageType = FDMT_MT4Trades_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_MT4Trades_ID;
        //        //ptrFileResp._header._fileSize = sizeof(MyTradeRecord) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrTradesDemoArr);
        //    }

        //    currtime = m_ptrLiveMT4Manager.getServerTime();
        //    var ptrTradesLiveArr = m_ptrMySqlWrapper.getLiveCloseTrades(liveMT4Login);
        //    cnt = ptrTradesLiveArr.Count;
        //    if (cnt > 0)
        //    {
        //        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //        ptrFileResp._reserved1 = masterLogin;
        //        ptrFileResp._reserved2 = SRV_TYPE_LIVE;
        //        ptrFileResp._fileDataMessageType = FDMT_MT4Trades_ID;
        //        ptrFileResp._header._fileDataMessageType = FDMT_MT4Trades_ID;
        //        //ptrFileResp._header._fileSize = sizeof(MyTradeRecord) * cnt;
        //        ptrFileResp._header._loginID = masterLogin;
        //        ptrFileResp._header._socketID = socketID;
        //        m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrTradesLiveArr);
        //    }
        //    m_ptrLogger.LogOk("END");
        //}
        //=================================================================================================
        //void sendInitialDataToAdmin(int masterLogin, uint socketID)
        //{
        //	m_ptrLogger.LogOk("START");
        //	sendInitialDataToUser(masterLogin, socketID);
        //	//int cnt = 0;
        //	////---------Sending user setting
        //	//MasterSettingUpdateResponse ptrResp1 = GetMessageObject<MasterSettingUpdateResponse>(MT_MasterSettingUpdateResponse_ID);
        //	//ptrResp1._header._loginID = masterLogin;
        //	//ptrResp1._header._socketID = socketID;
        //	//memcpy(out ptrResp1._mastersetting, &m_masterUserSetting, sizeof(MasterUserSetting));
        //	//ptrResp1._mastersetting._demoManagerLogin = -1;
        //	////memset(ptrResp1._mastersetting._demoManagerPassword, 0, 16);
        //	//ptrResp1._mastersetting._liveManagerLogin = -1;
        //	////memset(ptrResp1._mastersetting._liveManagerPassword, 0, 16);
        //	//SentDataUsingSocketID(ptrResp1, MT_MasterSettingUpdateResponse_ID, socketID);
        //	////-------Sending  all master users
        //	//MasterUser ptrUserArr = m_ptrMySqlWrapper.getAllMasterAccounts(cnt);
        //	//if (cnt > 0)
        //	//{
        //	//	GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//	ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_MasterUser_ID;
        //	//	ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_MasterUser_ID;
        //	//	ptrFileResp._header._fileSize = sizeof(MasterUser)*cnt;
        //	//	ptrFileResp._header._loginID = masterLogin;
        //	//	ptrFileResp._header._socketID = socketID;
        //	//	m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrUserArr, sizeof(MasterUser)*cnt);
        //	//}
        //	////-------Sending  all symbol
        //	//MT4SymbolInfo ptrSymArr = m_ptrMySqlWrapper.getAllMT4Symbols(cnt);
        //	//if (cnt > 0)
        //	//{
        //	//	GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//	ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_MT4SymbolInfo_ID;
        //	//	ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_MT4SymbolInfo_ID;
        //	//	ptrFileResp._header._fileSize = sizeof(MT4SymbolInfo)*cnt;
        //	//	ptrFileResp._header._loginID = masterLogin;
        //	//	ptrFileResp._header._socketID = socketID;
        //	//	m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSymArr, sizeof(MT4SymbolInfo)*cnt);
        //	//}
        //	////---------Sending market snap shot
        //	//sendMarketSnapShot(masterLogin, socketID);
        //	////---------Sending signal symbol setting
        //	//cnt = 0;
        //	//SignalSymbolSetting ptrSSSArr = m_ptrMySqlWrapper.getAllSignalSymbolSettings(cnt);
        //	//if (cnt > 0)
        //	//{
        //	//	GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//	ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_SignalSymbolSetting_ID;
        //	//	ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_SignalSymbolSetting_ID;
        //	//	ptrFileResp._header._fileSize = sizeof(SignalSymbolSetting)*cnt;
        //	//	ptrFileResp._header._loginID = masterLogin;
        //	//	ptrFileResp._header._socketID = socketID;
        //	//	m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSSSArr, sizeof(SignalSymbolSetting)*cnt);
        //	//}
        //	////---------Sending all mt4 accs
        //	//cnt = 0;
        //	//pMT4Account ptrAccArr = m_ptrMySqlWrapper.getAllMT4Accounts(cnt);
        //	//if (cnt > 0)
        //	//{
        //	//	GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//	ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_MT4Account_ID;
        //	//	ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_MT4Account_ID;
        //	//	ptrFileResp._header._fileSize = sizeof(MT4Account)*cnt;
        //	//	ptrFileResp._header._loginID = masterLogin;
        //	//	ptrFileResp._header._socketID = socketID;
        //	//	m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAccArr, sizeof(MT4Account)*cnt);
        //	//}
        //	////--------sending all trade disable info
        //	//cnt = 0;
        //	//MT4TradeDisableInfo ptrMt4DisableTrades = m_ptrMySqlWrapper.getAllTradeDisableInfo(cnt);
        //	//if (cnt > 0)
        //	//{
        //	//	GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//	ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_MT4TradeDisableInfo_ID;
        //	//	ptrFileResp._reserved1 = m_ptrDemoMT4Manager.getServerTime();
        //	//	ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_MT4TradeDisableInfo_ID;
        //	//	ptrFileResp._header._fileSize = sizeof(MT4TradeDisableInfo)*cnt;
        //	//	ptrFileResp._header._loginID = masterLogin;
        //	//	ptrFileResp._header._socketID = socketID;
        //	//	m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrAccArr, sizeof(MT4TradeDisableInfo)*cnt);
        //	//}
        //	////-----------sending all signal
        //	////Dictionary<int, List<Signal>>							m_mapSignal;
        //	//Dictionary<int, List<Signal>>.iterator itSignal;
        //	//List<Signal>.iterator itVecSignal;
        //	//cnt = 0;
        //	//lock (m_csSignal) {//@@@@
        //	//itSignal = m_mapSignal.find(masterLogin);
        //	//for (itSignal = m_mapSignal.begin(); itSignal != m_mapSignal.end(); itSignal++)
        //	//{
        //	//	if (itSignal.second.size() != 0)
        //	//	{
        //	//		size_t signalSize = itSignal.second.size() * sizeof(Signal);
        //	//		Signal ptrSigArr = (Signal)malloc(signalSize);
        //	//		//memset(ptrSigArr, 0, signalSize);
        //	//		cnt = 0;
        //	//		for (itVecSignal = itSignal.second.begin(); itVecSignal != itSignal.second.end(); itVecSignal++)
        //	//		{
        //	//			memcpy(out ptrSigArr[cnt], (*itVecSignal), sizeof(Signal));
        //	//			++cnt;
        //	//		}
        //
        //	//		GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//		ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_Signal_ID;
        //	//		ptrFileResp._reserved1 = itSignal.first;
        //	//		ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_Signal_ID;
        //	//		ptrFileResp._header._fileSize = signalSize;
        //	//		ptrFileResp._header._loginID = masterLogin;
        //	//		ptrFileResp._header._socketID = socketID;
        //	//		m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSigArr, signalSize);
        //	//	}
        //	//}
        //	//}//$$$$
        //
        //	////-----------sending all SM signals
        //	//Dictionary<int, Dictionary<int, List<SMSignal>>>.iterator it1;
        //	//Dictionary<int, List<SMSignal>>.iterator it2;
        //	//List<SMSignal>.iterator it3;
        //
        //	//lock (m_csMasterSignalSM) {//@@@@
        //	//for (it1 = m_mapMasterSignalSM.begin(); it1 != m_mapMasterSignalSM.end(); it1++)
        //	//{
        //	//	if (it1 != m_mapMasterSignalSM.end() && !m_isResetting)
        //	//	{
        //	//		for (it2 = it1.second.begin(); it2 != it1.second.end() && !m_isResetting; it2++)
        //	//		{
        //	//			if (it2.second.size() != 0)
        //	//			{
        //	//				size_t smSignalTotSize = sizeof(SMSignal) * it2.second.size();
        //	//				SMSignal ptrSmSigArr = (SMSignal)malloc(smSignalTotSize);
        //	//				//memset(ptrSmSigArr, 0, smSignalTotSize);
        //	//				cnt = 0;
        //	//				for (it3 = it2.second.begin(); it3 != it2.second.end(); it3++)
        //	//				{
        //	//					memcpy(out ptrSmSigArr[cnt], (*it3), sizeof(SMSignal));
        //	//					++cnt;
        //	//				}
        //	//				GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//				ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_SMSignal_ID;
        //	//				ptrFileResp._reserved1 = it2.first;
        //	//				ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_SMSignal_ID;
        //	//				ptrFileResp._header._fileSize = smSignalTotSize;
        //	//				ptrFileResp._header._loginID = masterLogin;
        //	//				ptrFileResp._header._socketID = socketID;
        //	//				m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSmSigArr, smSignalTotSize);
        //	//			}
        //	//		}
        //	//	}
        //	//}
        //	//}//$$$$
        //	//
        // //   ////-----------sending all SSP signals
        //	//Dictionary<int, Dictionary<int, SSPSignal>>.iterator itSSPSignal;
        //	//Dictionary<int, SSPSignal>.iterator itSSPSignal2;
        //	//lock (m_csMasterSignalSSP) {//@@@@
        //	//
        //	//for( itSSPSignal = m_mapMasterSignalSSP.begin() ; itSSPSignal != m_mapMasterSignalSSP.end() ; itSSPSignal++ )
        //	//{
        //	//	if (itSSPSignal.second.size() != 0 && !m_isResetting)
        //	//	{
        //	//		size_t totalSSPSize = sizeof(SSPSignal) * itSSPSignal.second.size();
        //	//		SSPSignal ptrSSPSigArr = (SSPSignal)malloc(totalSSPSize);
        //	//		//memset(ptrSSPSigArr, 0, totalSSPSize);
        //	//		cnt = 0;
        //	//		for (itSSPSignal2 = itSSPSignal.second.begin(); itSSPSignal2 != itSSPSignal.second.end(); itSSPSignal2++)
        //	//		{
        //	//			memcpy(out ptrSSPSigArr[cnt], itSSPSignal2.second, sizeof(SSPSignal));
        //	//			++cnt;
        //	//		}
        //	//		GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
        //	//		ptrFileResp._fileDataMessageType = clFileDataTypeID.FDMT_SSPSignal_ID;
        //	//		ptrFileResp._reserved1 = it2.first;
        //	//		ptrFileResp._header._fileDataMessageType = clFileDataTypeID.FDMT_SSPSignal_ID;
        //	//		ptrFileResp._header._fileSize = totalSSPSize;
        //	//		ptrFileResp._header._loginID = masterLogin;
        //	//		ptrFileResp._header._socketID = socketID;
        //	//		m_connectionMgr.SendAsFile(socketID, ptrFileResp, ptrSSPSigArr, totalSSPSize);
        //	//	}
        //	//}
        //	//}//$$$$
        //	m_ptrLogger.LogOk("END");
        //}
        //=================================================================================================
        void fetchAllSignal()
        {
            m_ptrLogger.LogOk("Fetching Signals START");

            Dictionary<int, Signal>.iterator itSignal;
            var ptrAllSignal = m_ptrMySqlWrapper.getAllSignals();
            int cnt = ptrAllSignal.Count;

            if (cnt > 0 && ptrAllSignal != null)
            {
                m_ptrLogger.LogOk("Total Signals %d", cnt);

                lock (m_SyncSignal)
                {//@@@@
                    for (int iLoop = 0; iLoop < cnt; iLoop++)
                    {
                        itSignal = m_mapSignal.find(ptrAllSignal[iLoop]._signalIndex);
                        if (itSignal == m_mapSignal.end())
                        {
                            Signal sig = (Signal)new Signal();
                            memcpy(ref sig, ptrAllSignal[iLoop]);
                            m_mapSignal.insert(new ValueTuple<int, Signal>(sig._signalIndex, sig));
                        }
                        else
                        {
                            m_ptrLogger.LogWarning("Duplicate Signal Index %d Name: %s", ptrAllSignal[iLoop]._signalIndex, ptrAllSignal[iLoop]._signalName);
                        }
                    }

                }//@@@@
                free(ptrAllSignal);
                ptrAllSignal = null;
            }
            else
            {
                m_ptrLogger.LogError("There is no signal");
            }
            m_ptrLogger.LogOk("Fetching Signals end");
        }
        //=================================================================================================
        void addSignal(Signal ptr)
        {
            Dictionary<int, Signal>.iterator itSignal;
            lock (m_SyncSignal)
            {//@@@@

                Signal sig = (Signal)new Signal();
                memcpy(ref sig, ptr);

                itSignal = m_mapSignal.find(sig._signalIndex);
                if (itSignal == m_mapSignal.end())
                {
                    m_mapSignal.insert(new ValueTuple<int, Signal>(sig._signalIndex, sig));
                }
                else
                {
                    free(sig);
                    sig = null;
                    m_ptrLogger.LogWarning("Duplicate Signal Index %d Name: %s", sig._signalIndex, sig._signalName);
                }

            }//@@@@
        }
        //=================================================================================================
        bool getSignal(Signal ptr, int signalIndex)
        {
            bool ret = false;
            Dictionary<int, Signal>.iterator itSignal;

            if (ptr != null)
            {
                lock (m_SyncSignal)
                {//@@@@

                    itSignal = m_mapSignal.find(signalIndex);
                    if (itSignal != m_mapSignal.end())
                    {
                        memcpy(ref ptr, itSignal.second);
                        ret = true;
                    }

                }//@@@@
            }

            return ret;
        }
        //=================================================================================================
        void fetchAllSSPSignal()
        {
            m_ptrLogger.LogOk("Fetching SSP Signals START");

            //Dictionary<int, ValueTuple<int, int>>.iterator itSSPSignal;
            var ptrAllSignalSSP = m_ptrMySqlWrapper.getAllSSPSignals();
            int cnt = ptrAllSignalSSP.Count;

            if (cnt > 0 && ptrAllSignalSSP != null)
            {
                m_ptrLogger.LogOk("TOTAL SSP Signals %d", cnt);

                lock (m_SyncSignalSSP)
                {
                    m_mapSignalSSP.Clear();
                    m_mapSSPMT4SignalIndex.Clear();
                }

                lock (m_SyncAccLinking)
                {
                    m_mapFollowers.Clear();
                }

                lock (m_SyncSignalSSP)
                {
                    m_mapSignalSSP.Clear();
                    m_mapSSPMT4SignalIndex.Clear();
                    for (int iLoop = 0; iLoop < cnt; iLoop++)
                    {
                        SSPSignal ptrSSP = ptrAllSignalSSP[iLoop];

                        var itSSPSignal = m_mapSignalSSP.find(ptrSSP._signalIndex);
                        if (itSSPSignal == m_mapSignalSSP.end())
                        {
                            var pr = (ptrSSP._symbolIndex, ptrSSP._strategyType);
                            m_mapSignalSSP.insert((ptrSSP._signalIndex, pr));
                            m_mapSSPMT4SignalIndex.insert(new ValueTuple<int, int>(ptrSSP._sspMT4Login, ptrSSP._signalIndex));
                        }
                        else
                        {
                            m_ptrLogger.LogWarning("Duplicate entry found for signal Index: %d", ptrSSP._signalIndex);
                        }
                        //addFollowerVolume(SRV_TYPE_DEMO, ptrSSP._sspMT4Login, 1);
                        fetchSSPFollower(ptrSSP);
                    }

                }

                //lock (m_SyncOpenOrderSSP)
                //{
                //    for (int iLoop = 0; iLoop < cnt; iLoop++)
                //    {
                //        SSPSignal ptrSSP = ptrAllSignalSSP[iLoop];
                //        if (m_mapSSPMT4OpenOrder.find(ptrSSP._sspMT4Login) == m_mapSSPMT4OpenOrder.end())
                //        {
                //            HashSet<int> st = new HashSet<int>();
                //            m_mapSSPMT4OpenOrder.insert(new ValueTuple<int, HashSet<int>>(ptrSSP._sspMT4Login, st));
                //        }
                //    }
                //}

                free(ptrAllSignalSSP);
                ptrAllSignalSSP = null;

            }
            else
            {
                m_ptrLogger.LogError("There is no SSP Signals");
            }

            m_ptrLogger.LogOk("Fetching SSP Signals END");
        }
        //=================================================================================================
        void addSSPSignal(SSPSignal ptr)
        {
            //Dictionary<int, ValueTuple<int, int>>.iterator itSSPSignal;

            lock (m_SyncSignalSSP)
            {
                var itSSPSignal = m_mapSignalSSP.find(ptr._signalIndex);
                if (itSSPSignal == m_mapSignalSSP.end())
                {
                    var pr = (ptr._symbolIndex, ptr._strategyType);
                    m_mapSignalSSP.insert((ptr._signalIndex, pr));
                    m_mapSSPMT4SignalIndex.insert(new ValueTuple<int, int>(ptr._sspMT4Login, ptr._signalIndex));
                }
                else
                {
                    m_ptrLogger.LogWarning("Duplicate entry found for signal Index: %d", ptr._signalIndex);
                }
            }

            //lock (m_SyncOpenOrderSSP)
            //{
            //    if (m_mapSSPMT4OpenOrder.find(ptr._sspMT4Login) == m_mapSSPMT4OpenOrder.end())
            //    {
            //        HashSet<int> st = new HashSet<int>();
            //        m_mapSSPMT4OpenOrder.insert(new ValueTuple<int, HashSet<int>>(ptr._sspMT4Login, st));
            //    }
            //}
        }
        //=================================================================================================
        void fetchAllSMSignal()
        {
            m_ptrLogger.LogOk("Fetching SM Signals START");
            var ptrAllSignalSM = m_ptrMySqlWrapper.getAllSMSignals();
            int cnt = ptrAllSignalSM.Count;
            if (cnt > 0 && ptrAllSignalSM != null)
            {
                m_ptrLogger.LogOk("TOTAL SM Signals %d", cnt);

                lock (m_SyncAccLinking)
                {

                    for (int iLoop = 0; iLoop < cnt; iLoop++)
                    {
                        SMSignal ptrSig = ptrAllSignalSM[iLoop];

                        if (m_setSM_MT4Login.find(ptrSig._smMT4Login) == m_setSM_MT4Login.end())
                        {
                            m_setSM_MT4Login.insert(ptrSig._smMT4Login);
                            m_mapSMSignalIndex.insert(new ValueTuple<int, int>(ptrSig._smMT4Login, ptrSig._signalIndex));
                        }

                        fetchSMFollower(ptrSig);
                    }

                }

                free(ptrAllSignalSM);
                ptrAllSignalSM = null;

            }
            else
            {
                m_ptrLogger.LogError("There is no SM Signal");
                ptrAllSignalSM = null;
            }

            m_ptrLogger.LogOk("Fetching SM Signals END");
        }
        //=================================================================================================
        void addSMSignal(SMSignal ptr)
        {
            lock (m_SyncAccLinking)
            {
                if (m_setSM_MT4Login.find(ptr._smMT4Login) == m_setSM_MT4Login.end())
                {
                    m_setSM_MT4Login.insert(ptr._smMT4Login);

                    addFollower(ptr._sspMT4Login, ptr._smMT4Login, SRV_TYPE_DEMO);
                    //addFollowerVolume(ptr. SRV_TYPE_DEMO, ptr._smMT4Login, 1);

                }
                else
                {
                    m_ptrLogger.LogWarning("Duplicate SM MT4 login %d", ptr._smMT4Login);
                }

            }
        }
        //=================================================================================================
        void fetchSSPFollower(SSPSignal ptr)
        {
            int cnt;

            Dictionary<int, List<ValueTuple<bool, int>>>.iterator it1;
            //List<ValueTuple<bool, int>>.iterator it2;

            //Dictionary<bool, Dictionary<int, Dictionary<int, int>>>.iterator it3;
            //Dictionary<int, Dictionary<int, int>>.iterator it4;
            //Dictionary<int, int>.iterator it5;

            if (ptr != null)
            {
                lock (m_SyncAccLinking)
                {
                    it1 = m_mapFollowers.find(ptr._sspMT4Login);
                    if (it1 == m_mapFollowers.end())
                    {
                        List<ValueTuple<bool, int>> vec = new List<(bool, int)>();
                        m_mapFollowers.insert(new ValueTuple<int, List<ValueTuple<bool, int>>>(ptr._sspMT4Login, vec));
                        it1 = m_mapFollowers.find(ptr._sspMT4Login);
                    }
                    else
                    {
                        m_ptrLogger.LogError("SSP MT4 must be unique..... SSP MasterLogin: %d MT4Login: %d", ptr._sspMasterLogin, ptr._sspMT4Login);
                    }
                }

                var arrFoll = m_ptrMySqlWrapper.getAllSSPFollowers(ptr._sspMT4Login, ptr._sspMT4ServerIndex);
                cnt = arrFoll.Count;
                if (arrFoll != null && cnt > 0)
                {
                    for (int iLoop = 0; iLoop < cnt; iLoop++)
                    {

                        addFollower(ptr._sspMT4Login, arrFoll[iLoop]._followerMT4Login, arrFoll[iLoop]._followerMT4ServerIndex);
                        addFollowerVolume(ptr._signalIndex, arrFoll[iLoop]._followerMT4ServerIndex, arrFoll[iLoop]._followerMT4Login, arrFoll[iLoop]._followervolume);
                    }
                    free(arrFoll);
                    arrFoll = null;
                }
            }
        }
        //=================================================================================================
        void fetchSMFollower(SMSignal ptr)
        {
            int cnt;
            //Dictionary<int, Dictionary<int, List<ValueTuple<bool, int>>>>  m_mapFollowers;
            Dictionary<int, List<ValueTuple<bool, int>>>.iterator it1;
            //List<ValueTuple<bool, int>>.iterator it2;

            //Dictionary<bool, Dictionary<int, Dictionary<int, int>>>.iterator it3;
            //Dictionary<int, Dictionary<int, int>>.iterator it4;
            //Dictionary<int, int>.iterator it5;
            bool isThisSMFirstTime = false;

            if (ptr != null)
            {
                //SM is also follower of SSP
                addFollower(ptr._sspMT4Login, ptr._smMT4Login, ptr._smMT4ServerIndex);
                addFollowerVolume(ptr._sspSignalIndex, ptr._smMT4ServerIndex, ptr._smMT4Login, m_masterUserSetting._signalTradeVolume);

                lock (m_SyncAccLinking)
                {

                    it1 = m_mapFollowers.find(ptr._smMT4Login);
                    if (it1 == m_mapFollowers.end())
                    {
                        List<ValueTuple<bool, int>> vec = new List<(bool, int)>();
                        m_mapFollowers.insert(new ValueTuple<int, List<ValueTuple<bool, int>>>(ptr._smMT4Login, vec));
                        it1 = m_mapFollowers.find(ptr._smMT4Login);
                        isThisSMFirstTime = true;
                    }

                }

                if (isThisSMFirstTime)
                {
                    var arrFoll = m_ptrMySqlWrapper.getAllSMFollowers(ptr._smMT4Login, ptr._smMT4ServerIndex);
                    cnt = arrFoll.Count;
                    if (arrFoll != null && cnt > 0)
                    {
                        for (int iLoop = 0; iLoop < cnt; iLoop++)
                        {
                            addFollower(ptr._smMT4Login, arrFoll[iLoop]._followerMT4Login, arrFoll[iLoop]._followerMT4ServerIndex);
                            addFollowerVolume(ptr._signalIndex, arrFoll[iLoop]._followerMT4ServerIndex, arrFoll[iLoop]._followerMT4Login, arrFoll[iLoop]._followervolume);
                        }
                        free(arrFoll);
                        arrFoll = null;
                    }
                }
            }
        }
        //=================================================================================================
        void addFollower(int sourceMT4Login, int followerMT4Login, eMT4ServerType followerServerIndex)
        {
            Dictionary<int, List<ValueTuple<bool, int>>>.iterator it1;
            List<ValueTuple<bool, int>>.iterator it2;


            lock (m_SyncAccLinking)
            {

                it1 = m_mapFollowers.find(sourceMT4Login);
                if (it1 == m_mapFollowers.end())
                {
                    List<ValueTuple<bool, int>> vec = new List<(bool, int)>();
                    m_mapFollowers.insert(new ValueTuple<int, List<ValueTuple<bool, int>>>(sourceMT4Login, vec));
                    it1 = m_mapFollowers.find(sourceMT4Login);
                }

                if (followerServerIndex == SRV_TYPE_DEMO)
                {
                    it2 = find(it1.second.begin(), it1.second.end(), (true, followerMT4Login));
                    if (it2 == it1.second.end())
                    {
                        it1.second.push_back((true, followerMT4Login));
                    }
                }
                else
                {
                    it2 = find(it1.second.begin(), it1.second.end(), (false, followerMT4Login));
                    if (it2 == it1.second.end())
                    {
                        it1.second.push_back((false, followerMT4Login));
                    }
                }

            }
        }

        void removeFollower(int sourceMT4Login, int followerMT4Login, eMT4ServerType followerServerIndex)
        {
            Dictionary<int, List<ValueTuple<bool, int>>>.iterator it1;
            List<ValueTuple<bool, int>>.iterator it2;
            lock (m_SyncAccLinking)
            {
                it1 = m_mapFollowers.find(sourceMT4Login);
                if (it1 != m_mapFollowers.end())
                {
                    if (followerServerIndex == SRV_TYPE_DEMO)
                        it2 = find(it1.second.begin(), it1.second.end(), (true, followerMT4Login));
                    else
                        it2 = find(it1.second.begin(), it1.second.end(), (false, followerMT4Login));
                    if (it2 != it1.second.end())
                    {
                        it1.second.erase(it2);
                    }
                }//if (it1 != m_mapFollowers.end())
            }
        }
        //=================================================================================================
        void addFollowerVolume(int signalIndex, eMT4ServerType serverIndex, int mt4login, int volume)
        {
            Dictionary<bool, Dictionary<int, Dictionary<int, int>>>.iterator it3;
            Dictionary<int, Dictionary<int, int>>.iterator it4;
            Dictionary<int, int>.iterator it5;

            lock (m_SyncfollowerTradeVol)
            {

                if (serverIndex == SRV_TYPE_DEMO)
                {
                    it3 = m_mapfollowerTradeVol.find(true);
                    if (it3 == m_mapfollowerTradeVol.end())
                    {
                        Dictionary<int, Dictionary<int, int>> mp = new Dictionary<int, Dictionary<int, int>>();
                        m_mapfollowerTradeVol.insert(new ValueTuple<bool, Dictionary<int, Dictionary<int, int>>>(true, mp));
                        it3 = m_mapfollowerTradeVol.find(true);
                    }
                    it4 = it3.second.find(signalIndex);
                    if (it4 == it3.second.end())
                    {
                        Dictionary<int, int> mp2 = new Dictionary<int, int>();
                        it3.second.insert(new ValueTuple<int, Dictionary<int, int>>(signalIndex, mp2));
                        it4 = it3.second.find(signalIndex);
                    }
                    it5 = it4.second.find(mt4login);
                    if (it5 == it4.second.end())
                    {
                        it4.second.insert(new ValueTuple<int, int>(mt4login, volume));
                    }
                    else
                    {
                        m_ptrLogger.LogWarning("Duplicate Entry SignalIndex: %d MT4Login: %d ServerIndex: %d Volume: %d", signalIndex, mt4login, serverIndex, volume);
                    }
                }
                else
                {
                    it3 = m_mapfollowerTradeVol.find(false);
                    if (it3 == m_mapfollowerTradeVol.end())
                    {
                        Dictionary<int, Dictionary<int, int>> mp = new Dictionary<int, Dictionary<int, int>>();
                        m_mapfollowerTradeVol.insert(new ValueTuple<bool, Dictionary<int, Dictionary<int, int>>>(false, mp));
                        it3 = m_mapfollowerTradeVol.find(false);
                    }
                    it4 = it3.second.find(signalIndex);
                    if (it4 == it3.second.end())
                    {
                        Dictionary<int, int> mp2 = new Dictionary<int, int>();
                        it3.second.insert(new ValueTuple<int, Dictionary<int, int>>(signalIndex, mp2));
                        it4 = it3.second.find(signalIndex);
                    }
                    it5 = it4.second.find(mt4login);
                    if (it5 == it4.second.end())
                    {
                        it4.second.insert(new ValueTuple<int, int>(mt4login, volume));
                    }
                    else
                    {
                        m_ptrLogger.LogWarning("Duplicate Entry SignalIndex: %d MT4Login: %d ServerIndex: %d Volume: %d", signalIndex, mt4login, serverIndex, volume);
                    }
                }
            }
        }

        void updateFollowerVolume(int signalIndex, eMT4ServerType serverIndex, int mt4login, int volume)
        {
            Dictionary<bool, Dictionary<int, Dictionary<int, int>>>.iterator it3;
            Dictionary<int, Dictionary<int, int>>.iterator it4;
            Dictionary<int, int>.iterator it5;

            lock (m_SyncfollowerTradeVol)
            {

                if (serverIndex == SRV_TYPE_DEMO)
                    it3 = m_mapfollowerTradeVol.find(true);
                else
                    it3 = m_mapfollowerTradeVol.find(false);

                if (it3 != m_mapfollowerTradeVol.end())
                {
                    it4 = it3.second.find(signalIndex);
                    if (it4 != it3.second.end())
                    {
                        it5 = it4.second.find(mt4login);
                        if (it5 != it4.second.end())
                        {
                            it4.second.erase(it5);
                            it4.second.insert(new ValueTuple<int, int>(mt4login, volume));
                        }
                    }
                }
            }
        }

        void removeFollowerVolume(int signalIndex, eMT4ServerType serverIndex, int mt4login, int volume)
        {
            Dictionary<bool, Dictionary<int, Dictionary<int, int>>>.iterator it3;
            Dictionary<int, Dictionary<int, int>>.iterator it4;
            Dictionary<int, int>.iterator it5;

            lock (m_SyncfollowerTradeVol)
            {

                if (serverIndex == SRV_TYPE_DEMO)
                    it3 = m_mapfollowerTradeVol.find(true);
                else
                    it3 = m_mapfollowerTradeVol.find(false);

                if (it3 != m_mapfollowerTradeVol.end())
                {
                    it4 = it3.second.find(signalIndex);
                    if (it4 != it3.second.end())
                    {
                        it5 = it4.second.find(mt4login);
                        if (it5 != it4.second.end())
                        {
                            it4.second.erase(it5);
                        }
                    }
                }
            }
        }
        //=================================================================================================
        void fetchSymbolSetting()
        {
            var ptrSSSArr = m_ptrMySqlWrapper.getAllSignalSymbolSettings();
            int cnt = ptrSSSArr.Count;
            if (cnt > 0)
            {
                lock (m_SyncSymbolSetting)
                {

                    //Dictionary<int, Dictionary<int, SignalSymbolSetting>>.iterator it1;
                    //Dictionary<int, SignalSymbolSetting>.iterator it2;
                    for (int iLoop = 0; iLoop < cnt; iLoop++)
                    {
                        var it1 = m_mapSignalSymSetting.find(ptrSSSArr[iLoop]._symbolIndex);
                        if (it1 == m_mapSignalSymSetting.end())
                        {
                            var mp = new Dictionary<eStrategyType, SignalSymbolSetting>();
                            m_mapSignalSymSetting.insert((ptrSSSArr[iLoop]._symbolIndex, mp));
                            it1 = m_mapSignalSymSetting.find(ptrSSSArr[iLoop]._symbolIndex);
                        }
                        var it2 = it1.second.find(ptrSSSArr[iLoop]._strategyType);
                        if (it2 == it1.second.end())
                        {
                            SignalSymbolSetting ptrSett = (SignalSymbolSetting)new SignalSymbolSetting();
                            memcpy(ref ptrSett, ptrSSSArr[iLoop]);
                            it1.second.insert((ptrSSSArr[iLoop]._strategyType, ptrSett));
                        }
                        else
                        {
                            m_ptrLogger.LogWarning("Duplicate Entry SymbolIndex: %d Strategy Type: %d", ptrSSSArr[iLoop]._symbolIndex, ptrSSSArr[iLoop]._strategyType);
                        }
                    }

                }

                free(ptrSSSArr);
                ptrSSSArr = null;
            }
        }
        //=================================================================================================
        /*
        1) For opening :
           a) Check trade disable for SSP and SM
           b) Check Symbol name for SSP. It is valid or not
           c) SL & TP are ok with respect to MT4
           c) Check Max SL for opening order for SM or SSP
        */
        /*
        https://book.mql4.com/appendix/limits
        BUY =>   TP > BID > SL     OpenPrice = Ask
        SELL=>   SL > ASK > TP     OpenPrice = Bid

        Bid > ASK =>  ASK = BID + SPREAD

        ASK LINE COLOR = RED
        BID LINE COLOR = BLUE

        Always BID / ASK

        In chart always we show BID PRICE

        BUY STOP =  Open Price   > BID
        BUY LIMIT = Open Price   < ASK

        SELL STOP =  Open Price  < ASK
        SELL LIMIT = Open Price  > BID

        Buy Limit — Buy provided the future "ASK" price is equal to the pre-defined value.
                    The current price level is higher than the value of the placed order.
                    Orders of this type are usually placed in anticipation of that the security price, having fallen to a certain level, will increase;
        Buy Stop —  Buy provided the future "ASK" price is equal to the pre-defined value.
                    The current price level is lower than the value of the placed order.
                    Orders of this type are usually placed in anticipation of that the security price, having reached a certain level, will keep on increasing;
        Sell Limit — Sell provided the future "BID" price is equal to the pre-defined value.
                     The current price level is lower than the value of the placed order.
                     Orders of this type are usually placed in anticipation of that the security price, having increased to a certain level, will fall;
        Sell Stop —  Sell provided the future "BID" price is equal to the pre-defined value.
                     The current price level is higher than the value of the placed order.
                     Orders of this type are usually placed in anticipation of that the security price, having reached a certain level, will keep on falling.
        */
        bool isOrderValidated(SocialOrderRequest ptr, ref eReturnCode reason, ref bool isSSP, ref bool isSM)
        {
            bool ret = false;

            //var onlineUser = m_connectionMgr.GetOnlineUser(ptr._header._socketID);
            ret = ptr._header._loginID == ptr._orderReq._masterLogin;
            //if (onlineUser != null && onlineUser.LoginID == ptr._header._loginID && onlineUser.LoginID == ptr._orderReq._masterLogin)
            //{
            //    ret = true;
            //}

            if (!ret)
            {
                reason = RC_INVALID_PARAMETER;
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PARAMETER MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }

            int masterLogin = getMasterLogin(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login);
            if (masterLogin != ptr._orderReq._masterLogin)
            {
                reason = RC_INVALID_PARAMETER;
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PARAMETER MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }

            isSSP = isClientSSP(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login);
            isSM = isClientSM(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login);

            if (ptr._orderReq._mt4ServerIndex == SRV_TYPE_DEMO)
            {
                if (!m_ptrDemoMT4Manager.isMT4Connected())
                {
                    reason = RC_LP_NOTCONNECTED;
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_LP_NOTCONNECTED MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }
            }
            else
            {
                if (!m_ptrLiveMT4Manager.isMT4Connected())
                {
                    reason = RC_LP_NOTCONNECTED;
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_LP_NOTCONNECTED MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }

            }

            if (isSSP)
            {
                ret = false;
                reason = RC_INVALID_PARAMETER;
                lock (m_SyncSignalSSP)
                {
                    Dictionary<int, int>.iterator itsspSig = m_mapSSPMT4SignalIndex.find(ptr._orderReq._mt4Login);
                    if (itsspSig != m_mapSSPMT4SignalIndex.end())
                    {
                        if (itsspSig.second == ptr._orderReq._signalIndex)
                        {
                            ret = true;
                        }
                    }
                }

                if (!ret)
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! INVALID SIGNAL INDEX MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }

                ret = false;
                // Fixed error with max trades open when a trade is being closed.
                if (ptr._orderReq._orderTransMode == (int)ORD_TRANS_OPEN && m_ptrDemoMT4Manager.getTotalOpenOrdersCount(ptr._orderReq._mt4Login,
                    m_masterUserSetting._demoGroup) >= m_masterUserSetting._maxSSPOpenOrders)
                {
                    reason = RC_MAX_SSP_ORDER;
                    m_ptrLogger.LogError("!! Order Validation Failed !! MasterLogin: %d MT4Login: %d SSP MAX ORDER REACHED", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login);
                    return false;
                }
                //Dictionary<int, HashSet<int>>.iterator itSSP
                //lock (m_SyncOpenOrderSSP) {
                //}
            }

            if (isSM)
            {
                ret = false;
                reason = RC_INVALID_PARAMETER;
                lock (m_SyncAccLinking)
                {
                    Dictionary<int, int>.iterator itsmSig = m_mapSMSignalIndex.find(ptr._orderReq._mt4Login);
                    if (itsmSig != m_mapSMSignalIndex.end())
                    {
                        if (itsmSig.second == ptr._orderReq._signalIndex)
                        {
                            ret = true;
                        }
                    }
                }

                if (!ret)
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! INVALID SIGNAL INDEX MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }
            }
            if (ptr._orderReq._orderTransMode != ORD_TRANS_OPEN && ptr._orderReq._orderID == 0)
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_ORDERID MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                reason = RC_INVALID_ORDERID;
                return false;
            }
            if (!isOrderTypeAndSymbolSupported(ptr._orderReq, ref reason))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SYMBOL OR RC_INVALID_ORDER_CMD MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }
            if (!isOrderTransSupported(ptr._orderReq, ref reason))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_ORDER_TRANS_MODE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }

            int symDigit = 5;
            int stopLevel = 4;
            double symPoint = 0.00001;

            if (!getSymbolProperty(ptr._orderReq._symbol, ref symDigit, ref stopLevel, ref symPoint))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_SYMBOL_NOT_FOUND MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                reason = RC_SYMBOL_NOT_FOUND;
                return false;
            }

            ptr._orderReq._price = NormalizeDouble(ptr._orderReq._price, symDigit);
            ptr._orderReq._sl = NormalizeDouble(ptr._orderReq._sl, symDigit);
            ptr._orderReq._tp = NormalizeDouble(ptr._orderReq._tp, symDigit);
            symPoint = NormalizeDouble(symPoint, symDigit);

            ptr._orderReq._price = round_off(ptr._orderReq._price, symDigit);
            ptr._orderReq._sl = round_off(ptr._orderReq._sl, symDigit);
            ptr._orderReq._tp = round_off(ptr._orderReq._tp, symDigit);
            symPoint = round_off(symPoint, symDigit);

            //round_off()
            double server_min_stop = convertPipToValue(stopLevel, symDigit, symPoint);

            server_min_stop = NormalizeDouble(server_min_stop, symDigit);

            double currBid, currAsk;
            currBid = currAsk = 0.0;

            if (!getLatestBidAsk(ptr._orderReq._symbol, ref currBid, ref currAsk))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_UNABLE_TO_GET_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                reason = RC_UNABLE_TO_GET_PRICE;
                return false;
            }

            if (currBid == 0.0 || currAsk == 0.0)
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_UNABLE_TO_GET_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                reason = RC_UNABLE_TO_GET_PRICE;
                return false;
            }



            if (isSSP || isSM)
            {
                DateTimeOffset currTime = DateTimeOffset.UtcNow;
                if (m_ptrMySqlWrapper.isTradingDisable(ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._mt4ServerIndex, currTime))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_TRADE_DISABLE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_TRADE_DISABLE;
                    return false;
                }
                if (isSSP && (ptr._orderReq._orderTransMode == ORD_TRANS_OPEN || ptr._orderReq._orderTransMode == ORD_TRANS_MODIFY))
                {
                    int maxSlinPips = 2;
                    if (getSSPSignalDetail(ptr._orderReq._mt4ServerIndex, ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._signalIndex, ptr._orderReq._symbol, ref maxSlinPips, ref reason))
                    {
                        if (ptr._orderReq._sl != 0.0)
                        {
                            int orderSLInPips = abs(ConvertInPips(ptr._orderReq._price, symDigit, symPoint) - ConvertInPips(ptr._orderReq._sl, symDigit, symPoint));
                            if (orderSLInPips > maxSlinPips)
                            {
                                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SIGNAL_SL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                                reason = RC_INVALID_SIGNAL_SL;
                                return false;
                            }
                        }
                        else
                        {
                            double slVal = convertPipToValue(maxSlinPips, symDigit, symPoint);
                            switch (ptr._orderReq._orderType)
                            {
                                case ORD_TYPE_BUY:
                                    {
                                        ptr._orderReq._sl = currBid - slVal;

                                    }
                                    break;
                                case ORD_TYPE_BUYLIMIT:
                                case ORD_TYPE_BUYSTOP:
                                    {
                                        ptr._orderReq._sl = ptr._orderReq._price - slVal;
                                    }
                                    break;
                                case ORD_TYPE_SELL:
                                    {
                                        ptr._orderReq._sl = currAsk + slVal;

                                    }
                                    break;
                                case ORD_TYPE_SELLLIMIT:
                                case ORD_TYPE_SELLSTOP:
                                    {
                                        ptr._orderReq._sl = ptr._orderReq._price + slVal;
                                    }
                                    break;
                            }//switch (ptr._orderReq._orderType)
                        }
                    }
                    else
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SIGNAL_SYMBOL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return false;
                    }
                }
            }
            //Correcting Precision
            //ptr._orderReq._price = NormalizeDouble(ptr._orderReq._price, symDigit);
            //ptr._orderReq._sl = NormalizeDouble(ptr._orderReq._sl, symDigit);
            //ptr._orderReq._tp = .NormalizeDouble(ptr._orderReq._tp, symDigit);

            //BUY =>   TP > BID > SL     OpenPrice = Ask
            if (ptr._orderReq._orderType == ORD_TYPE_BUY && ptr._orderReq._orderTransMode == ORD_TRANS_OPEN)
            {
                ptr._orderReq._price = currAsk;
                ptr._orderReq._price = NormalizeDouble(ptr._orderReq._price, symDigit);
            }
            else if (ptr._orderReq._orderType == ORD_TYPE_SELL && ptr._orderReq._orderTransMode == ORD_TRANS_OPEN)
            {
                ptr._orderReq._price = currBid;
                ptr._orderReq._price = NormalizeDouble(ptr._orderReq._price, symDigit);
            }


            //Ensure SL is ok
            /*if (ptr._orderReq._sl != 0.0 && (ptr._orderReq._orderTransMode == ORD_TRANS_OPEN || ptr._orderReq._orderTransMode == ORD_TRANS_MODIFY))
            {
                if (ptr._orderReq._orderType == ORD_TYPE_BUY && !((currBid - ptr._orderReq._sl) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_SL;
                    return false;
                }
                if (ptr._orderReq._orderType == ORD_TYPE_SELL && !((ptr._orderReq._sl - currAsk) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_SL;
                    return false;
                }
                if ((ptr._orderReq._orderType == ORD_TYPE_BUYSTOP || ptr._orderReq._orderType == ORD_TYPE_BUYLIMIT) &&
                    !((ptr._orderReq._price - ptr._orderReq._sl) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_SL;
                    return false;
                }
                if ((ptr._orderReq._orderType == ORD_TYPE_SELLLIMIT || ptr._orderReq._orderType == ORD_TYPE_SELLSTOP) &&
                    !((ptr._orderReq._sl - ptr._orderReq._price) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_SL;
                    return false;
                }
            }*/

            //Ensure TP is ok
            /*if (ptr._orderReq._tp != 0.0 && (ptr._orderReq._orderTransMode == ORD_TRANS_OPEN || ptr._orderReq._orderTransMode == ORD_TRANS_MODIFY))
            {
                if (ptr._orderReq._orderType == ORD_TYPE_BUY &&
                    !((ptr._orderReq._tp - currBid) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_TP MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_TP;
                    return false;
                }
                if (ptr._orderReq._orderType == ORD_TYPE_SELL &&
                    !((currAsk - ptr._orderReq._tp) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_TP MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_TP;
                    return false;
                }
                if ((ptr._orderReq._orderType == ORD_TYPE_BUYSTOP || ptr._orderReq._orderType == ORD_TYPE_BUYLIMIT) &&
                    !((ptr._orderReq._tp - ptr._orderReq._price) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_TP MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_TP;
                    return false;
                }
                if ((ptr._orderReq._orderType == ORD_TYPE_SELLLIMIT || ptr._orderReq._orderType == ORD_TYPE_SELLSTOP) &&
                    !((ptr._orderReq._price - ptr._orderReq._tp) >= server_min_stop))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_TP MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    reason = RC_INVALID_TP;
                    return false;
                }
            }*/

            if (ptr._orderReq._price == 0.0)
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                reason = RC_INVALID_PRICE;
                return false;
            }
            else
            {
                if (ptr._orderReq._orderTransMode == ORD_TRANS_OPEN || ptr._orderReq._orderTransMode == ORD_TRANS_MODIFY)
                {
                    if (ptr._orderReq._orderType == ORD_TYPE_BUYLIMIT && !(ptr._orderReq._price < currAsk))//Open Price   < ASK 
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        reason = RC_INVALID_PRICE;
                        return false;
                    }
                    else if (ptr._orderReq._orderType == ORD_TYPE_SELLLIMIT && !(ptr._orderReq._price > currBid))//Open Price  > BID
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        reason = RC_INVALID_PRICE;
                        return false;
                    }
                    else if (ptr._orderReq._orderType == ORD_TYPE_BUYSTOP && !(ptr._orderReq._price > currAsk))
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        reason = RC_INVALID_PRICE;
                        return false;
                    }
                    else if (ptr._orderReq._orderType == ORD_TYPE_SELLSTOP && !(ptr._orderReq._price < currBid))
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        reason = RC_INVALID_PRICE;
                        return false;
                    }
                }
            }

            return true;
        }
        //=================================================================================================
        public eReturnCode handleCloseOrderRequest(SocialOrderRequest ptr)
        {
            return handleModifyOrderRequest(ptr, MT4REQMODE.CLOSE_TRADE);
        }
        //=================================================================================================
        public eReturnCode handleDeleteOrderRequest(SocialOrderRequest ptr)
        {
            return handleModifyOrderRequest(ptr, MT4REQMODE.DELETE_TRADE);
        }
        //=================================================================================================
        public eReturnCode handleModifyOrderRequest(SocialOrderRequest ptr, MT4REQMODE reqMode = (MT4REQMODE)'M')
        {
            List<int> vecFollowerOrderID = new List<int>();
            List<uint> vecFollowerTransID = new List<uint>();
            List<int> vecFollowerMT4Login = new List<int>();
            List<bool> vecServerType = new List<bool>();
            bool isOrderInDemoSrv = false;
            int outMT4Login = 0;
            uint outTransID = 0;
            bool isThisSignalOrder = false;

            getLinkedOrderID(vecFollowerOrderID, vecFollowerTransID, vecFollowerMT4Login, vecServerType,
                ref isOrderInDemoSrv, ref outMT4Login, ref outTransID, ref isThisSignalOrder, ptr._orderReq._orderID);

            //Stage-1: Trade Recv
            //SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
            //ptrResp._header._loginID = ptr._orderReq._masterLogin;
            //ptrResp._header._MT4loginID = ptr._orderReq._mt4Login;
            //ptrResp._header._socketID = ptr._header._socketID;
            //ptrResp._clientReqID = ptr._clientReqID;
            //ptrResp._serverTransID = outTransID;
            //ptrResp._requestMode = reqMode;
            //ptrResp._retCode = (int)RC_TRADE_RECVD;
            //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, ptrResp._header._socketID);

            bool isSSP = false;
            bool isSM = false;
            eReturnCode reason = RC_TRADE_ACCEPTED;
            if (isOrderValidated(ptr, ref reason, ref isSSP, ref isSM))
            {
                if (isThisSignalOrder)
                {
                    MT4Request ptrSignaOrdlReq = (MT4Request)new MT4Request();
                    //memset(ptrSignaOrdlReq, 0, sizeof(MT4Request));
                    ptrSignaOrdlReq.reqType = MT4REQ_TRADE;
                    ptrSignaOrdlReq.requestMode = reqMode;
                    //ptrSignaOrdlReq.socketID = ptr._header._socketID;
                    ptrSignaOrdlReq.status = ORD_STAT_RECVD;
                    ptrSignaOrdlReq.serverTransID = outTransID;

                    ptrSignaOrdlReq.ptrData = new MT4OrderInfo();
                    //memset(ptrSignaOrdlReq.ptrData, 0, sizeof(MT4OrderInfo));

                    MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrSignaOrdlReq.ptrData;
                    memcpy(ref ptrSignaOrdlReq.ptrData, ptr._orderReq);
                    ptrOrd._volume = m_masterUserSetting._signalTradeVolume;
                    ptrOrd._mt4Login = outMT4Login;
                    ptrOrd._mt4ServerIndex = SRV_TYPE_DEMO;
                    ptrSignaOrdlReq.masterLogin = getMasterLogin(SRV_TYPE_DEMO, outMT4Login);
                    ptrSignaOrdlReq.newLoginOrOrderID = ptrOrd._orderID;
                    m_ptrDemoMT4Manager.insertMT4Request(ptrSignaOrdlReq);

                    int loopCnt = vecFollowerOrderID.size();
                    if (loopCnt == vecFollowerTransID.size() &&
                        loopCnt == vecFollowerMT4Login.size() &&
                        loopCnt == vecServerType.size())
                    {
                        for (int iLoop = 0; iLoop < loopCnt; iLoop++)
                        {
                            MT4Request ptrFollowerOrdlReq = (MT4Request)new MT4Request();
                            //memset(ptrFollowerOrdlReq, 0, sizeof(MT4Request));
                            ptrFollowerOrdlReq.reqType = MT4REQ_TRADE;
                            ptrFollowerOrdlReq.requestMode = reqMode;
                            //ptrFollowerOrdlReq.socketID = ptr._header._socketID;
                            ptrFollowerOrdlReq.status = ORD_STAT_RECVD;
                            ptrFollowerOrdlReq.serverTransID = vecFollowerTransID.ElementAt(iLoop);

                            ptrFollowerOrdlReq.ptrData = new MT4OrderInfo();
                            //memset(ptrFollowerOrdlReq.ptrData, 0, sizeof(MT4OrderInfo));

                            MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrFollowerOrdlReq.ptrData;
                            memcpy(ref ptrFollowerOrdlReq.ptrData, ptr._orderReq);
                            ptrOrdInfo._mt4Login = vecFollowerMT4Login.ElementAt(iLoop);
                            ptrOrdInfo._orderID = vecFollowerOrderID.ElementAt(iLoop);
                            ptrFollowerOrdlReq.newLoginOrOrderID = ptrOrdInfo._orderID;
                            if (vecServerType.ElementAt(iLoop))
                            {
                                ptrOrdInfo._mt4ServerIndex = SRV_TYPE_DEMO;
                                ptrFollowerOrdlReq.masterLogin = getMasterLogin(SRV_TYPE_DEMO, ptrOrdInfo._mt4Login);
                                m_ptrDemoMT4Manager.insertMT4Request(ptrFollowerOrdlReq);
                            }
                            else
                            {
                                ptrOrdInfo._mt4ServerIndex = SRV_TYPE_LIVE;
                                ptrFollowerOrdlReq.masterLogin = getMasterLogin(SRV_TYPE_LIVE, ptrOrdInfo._mt4Login);
                                m_ptrLiveMT4Manager.insertMT4Request(ptrFollowerOrdlReq);
                            }
                        }
                    }

                }
                else
                {
                    MT4Request ptrNonSignalReq = (MT4Request)new MT4Request();
                    //memset(ptrNonSignalReq, 0, sizeof(MT4Request));
                    ptrNonSignalReq.reqType = MT4REQ_TRADE;
                    ptrNonSignalReq.requestMode = reqMode;
                    //ptrNonSignalReq.socketID = ptr._header._socketID;
                    ptrNonSignalReq.status = ORD_STAT_RECVD;
                    ptrNonSignalReq.serverTransID = outTransID;

                    ptrNonSignalReq.ptrData = new MT4OrderInfo();
                    //memset(ptrNonSignalReq.ptrData, 0, sizeof(MT4OrderInfo));

                    MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrNonSignalReq.ptrData;
                    memcpy(ref ptrNonSignalReq.ptrData, ptr._orderReq);
                    ptrOrd._volume = m_masterUserSetting._signalTradeVolume;
                    ptrOrd._mt4Login = outMT4Login;
                    ptrOrd._signalIndex = 0;
                    ptrNonSignalReq.newLoginOrOrderID = ptrOrd._orderID;

                    if (isOrderInDemoSrv)
                    {
                        ptrOrd._mt4ServerIndex = SRV_TYPE_DEMO;
                        ptrNonSignalReq.masterLogin = getMasterLogin(SRV_TYPE_DEMO, outMT4Login);
                        m_ptrDemoMT4Manager.insertMT4Request(ptrNonSignalReq);
                    }
                    else
                    {
                        ptrOrd._mt4ServerIndex = SRV_TYPE_LIVE;
                        ptrNonSignalReq.masterLogin = getMasterLogin(SRV_TYPE_LIVE, outMT4Login);
                        m_ptrLiveMT4Manager.insertMT4Request(ptrNonSignalReq);
                    }
                }
            }
            else
            {
                //Last Stage some issue in order validation
                //SocialOrderResponse socialResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                //socialResp._header._loginID = ptr._orderReq._masterLogin;
                //socialResp._header._MT4loginID = ptr._orderReq._mt4Login;
                //socialResp._header._socketID = ptr._header._socketID;
                //socialResp._clientReqID = ptr._clientReqID;
                //socialResp._requestMode = reqMode;
                //socialResp._serverTransID = outTransID;
                //socialResp._retCode = (int)reason;
                //SentDataUsingSocketID(socialResp, MT_SocialOrderResponse_ID, socialResp._header._socketID);
            }//else of if (isOrderValidated(ptr, reason, isSSP, isSM))
            return reason;
        }
        //=================================================================================================
        public eReturnCode handleOpenOrderRequest(SocialOrderRequest ptr)
        {
            uint uniqueReqID = getUniqueRequestID();
            //Stage-1: Trade Recv
            //SocialOrderResponse ptrResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
            //ptrResp._header._loginID = ptr._orderReq._masterLogin;
            //ptrResp._header._MT4loginID = ptr._orderReq._mt4Login;
            //ptrResp._header._socketID = ptr._header._socketID;
            //ptrResp._clientReqID = ptr._clientReqID;
            //ptrResp._requestMode = MT4REQMODE.OPEN_TRADE;
            //ptrResp._serverTransID = uniqueReqID;
            //ptrResp._retCode = (int)RC_TRADE_RECVD;
            //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, ptrResp._header._socketID);

            bool isSSP = false;
            bool isSM = false;
            eReturnCode reason = RC_TRADE_ACCEPTED;
            if (isOrderValidated(ptr, ref reason, ref isSSP, ref isSM))
            {
                //Stage-2: Trade Accepted
                //SocialOrderResponse socialResp = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                //socialResp._header._loginID = ptr._orderReq._masterLogin;
                //socialResp._header._MT4loginID = ptr._orderReq._mt4Login;
                //socialResp._header._socketID = ptr._header._socketID;
                //socialResp._clientReqID = ptr._clientReqID;
                //socialResp._requestMode = MT4REQMODE.OPEN_TRADE;
                //socialResp._serverTransID = uniqueReqID;
                //socialResp._retCode = (int)RC_TRADE_ACCEPTED;
                //SentDataUsingSocketID(socialResp, MT_SocialOrderResponse_ID, socialResp._header._socketID);

                if (isSSP)
                {
                    Dictionary<int, List<ValueTuple<bool, int>>>.iterator itFoll1;
                    List<ValueTuple<bool, int>>.iterator itFoll2;

                    Dictionary<int, List<ValueTuple<bool, int>>>.iterator itSMFoll1;
                    List<ValueTuple<bool, int>>.iterator itSMFoll2;

                    //true = for demo 
                    List<ValueTuple<bool, MT4Request>> vecDemoLiveOrders = new List<ValueTuple<bool, MT4Request>>();
                    List<ValueTuple<bool, MT4Request>>.iterator itVecDemoLiveOrders;

                    MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                    //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                    ptrMT4Req4.masterLogin = getMasterLogin(SRV_TYPE_DEMO, ptr._orderReq._mt4Login);
                    ptrMT4Req4.reqType = MT4REQ_TRADE;
                    ptrMT4Req4.requestMode = MT4REQMODE.OPEN_TRADE;
                    //ptrMT4Req4.socketID = ptr._header._socketID;
                    ptrMT4Req4.status = ORD_STAT_RECVD;
                    ptrMT4Req4.serverTransID = uniqueReqID;
                    ptrMT4Req4.signalServerTransID = uniqueReqID;

                    ptrMT4Req4.ptrData = new MT4OrderInfo();
                    //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));

                    MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                    memcpy(ref ptrMT4Req4.ptrData, ptr._orderReq);
                    ptrOrd._volume = m_masterUserSetting._signalTradeVolume;
                    ptrOrd._mt4Login = ptr._orderReq._mt4Login;

                    //Adding SSP Order
                    vecDemoLiveOrders.Add((true, ptrMT4Req4));
                    insertSignalOrderLinking(ptrOrd._signalIndex, ptrOrd._mt4Login, ptrMT4Req4.masterLogin, true, uniqueReqID, -1);

                    lock (m_SyncAccLinking)
                    {

                        itFoll1 = m_mapFollowers.find(ptr._orderReq._mt4Login);
                        if (itFoll1 != m_mapFollowers.end())
                        {
                            for (itFoll2 = itFoll1.second.begin(); itFoll2 != itFoll1.second.end(); itFoll2++)
                            {
                                //unique id for ssp signal
                                uint uid = getUniqueRequestID();

                                if (itFoll2.first())//demo
                                {
                                    MT4Request ptrSSPDemoFollowerReq = (MT4Request)new MT4Request();
                                    //memset(ptrSSPDemoFollowerReq, 0, sizeof(MT4Request));
                                    ptrSSPDemoFollowerReq.masterLogin = getMasterLogin(SRV_TYPE_DEMO, itFoll2.second());
                                    ptrSSPDemoFollowerReq.reqType = MT4REQ_TRADE;
                                    ptrSSPDemoFollowerReq.requestMode = MT4REQMODE.OPEN_TRADE;
                                    //ptrSSPDemoFollowerReq.socketID = ptr._header._socketID;
                                    ptrSSPDemoFollowerReq.status = ORD_STAT_RECVD;
                                    ptrSSPDemoFollowerReq.serverTransID = uid;
                                    ptrSSPDemoFollowerReq.signalServerTransID = uniqueReqID;

                                    ptrSSPDemoFollowerReq.ptrData = new MT4OrderInfo();
                                    //memset(ptrSSPDemoFollowerReq.ptrData, 0, sizeof(MT4OrderInfo));

                                    MT4OrderInfo ptrOrd1 = (MT4OrderInfo)ptrSSPDemoFollowerReq.ptrData;
                                    memcpy(ref ptrSSPDemoFollowerReq.ptrData, ptr._orderReq);
                                    ptrOrd1._volume = getTradeVolume(ptr._orderReq._signalIndex, itFoll2.second(), true);
                                    ptrOrd1._mt4Login = itFoll2.second();
                                    if (ptrOrd1._volume != 0)
                                    {
                                        //Adding Follower Order
                                        vecDemoLiveOrders.Add((true, ptrSSPDemoFollowerReq));
                                        insertFollowerOrderLinking(uniqueReqID, itFoll2.second(), ptrSSPDemoFollowerReq.masterLogin, true, ptrOrd1._volume, uid);
                                        insertTransLinking(uniqueReqID, uid, true);
                                    }
                                    else
                                    {
                                        m_ptrLogger.LogWarning("Follower Volume is 0... order not mirrored");
                                    }
                                    //Check this follower is SM or not
                                    if (m_setSM_MT4Login.find(itFoll2.second()) != m_setSM_MT4Login.end() && m_mapSMSignalIndex.find(itFoll2.second()) != m_mapSMSignalIndex.end())
                                    {
                                        itSMFoll1 = m_mapFollowers.find(itFoll2.second());
                                        if (itSMFoll1 != m_mapFollowers.end())
                                        {
                                            //SM Signal Index
                                            int smSignalIndex = m_mapSMSignalIndex[itFoll2.second()];
                                            if (itSMFoll1.second.size() != 0)
                                            {
                                                insertSignalOrderLinking(smSignalIndex, itFoll2.second(), ptrSSPDemoFollowerReq.masterLogin, false, uid, ptr._orderReq._mt4Login);
                                            }

                                            for (itSMFoll2 = itSMFoll1.second.begin(); itSMFoll2 != itSMFoll1.second.end(); itSMFoll2++)
                                            {
                                                if (itSMFoll2.first())//demo
                                                {
                                                    MT4Request ptrSMDemoFollowerReq = (MT4Request)new MT4Request();
                                                    //memset(ptrSMDemoFollowerReq, 0, sizeof(MT4Request));
                                                    ptrSMDemoFollowerReq.masterLogin = getMasterLogin(SRV_TYPE_DEMO, itSMFoll2.second());
                                                    ptrSMDemoFollowerReq.reqType = MT4REQ_TRADE;
                                                    ptrSMDemoFollowerReq.requestMode = MT4REQMODE.OPEN_TRADE;
                                                    //ptrSMDemoFollowerReq.socketID = ptr._header._socketID;
                                                    ptrSMDemoFollowerReq.status = ORD_STAT_RECVD;
                                                    ptrSMDemoFollowerReq.serverTransID = getUniqueRequestID();
                                                    ptrSMDemoFollowerReq.signalServerTransID = uid;

                                                    ptrSMDemoFollowerReq.ptrData = new MT4OrderInfo();
                                                    //memset(ptrSMDemoFollowerReq.ptrData, 0, sizeof(MT4OrderInfo));

                                                    MT4OrderInfo ptrOrd2 = (MT4OrderInfo)ptrSMDemoFollowerReq.ptrData;
                                                    memcpy(ref ptrSMDemoFollowerReq.ptrData, ptr._orderReq);
                                                    ptrOrd2._volume = getTradeVolume(smSignalIndex, itSMFoll2.second(), true);
                                                    ptrOrd2._mt4Login = itSMFoll2.second();
                                                    if (ptrOrd2._volume != 0)
                                                    {
                                                        vecDemoLiveOrders.push_back((true, ptrSMDemoFollowerReq));
                                                        insertFollowerOrderLinking(ptrSMDemoFollowerReq.signalServerTransID,
                                                            ptrOrd2._mt4Login,
                                                            ptrSMDemoFollowerReq.masterLogin, true, ptrOrd2._volume,
                                                            ptrSMDemoFollowerReq.serverTransID);


                                                        insertTransLinking(uniqueReqID, ptrSMDemoFollowerReq.serverTransID, true);
                                                        insertTransLinking(ptrSMDemoFollowerReq.signalServerTransID, ptrSMDemoFollowerReq.serverTransID, true);
                                                    }
                                                    else
                                                    {
                                                        m_ptrLogger.LogWarning("Follower Volume is 0... order not mirrored");
                                                    }
                                                }
                                                else
                                                {
                                                    MT4Request ptrSMLiveFollowerReq = (MT4Request)new MT4Request();
                                                    //memset(ptrSMLiveFollowerReq, 0, sizeof(MT4Request));
                                                    ptrSMLiveFollowerReq.masterLogin = getMasterLogin(SRV_TYPE_LIVE, itSMFoll2.second());
                                                    ptrSMLiveFollowerReq.reqType = MT4REQ_TRADE;
                                                    ptrSMLiveFollowerReq.requestMode = MT4REQMODE.OPEN_TRADE;
                                                    //ptrSMLiveFollowerReq.socketID = ptr._header._socketID;
                                                    ptrSMLiveFollowerReq.status = ORD_STAT_RECVD;
                                                    ptrSMLiveFollowerReq.serverTransID = getUniqueRequestID();
                                                    ptrSMLiveFollowerReq.signalServerTransID = uid;

                                                    ptrSMLiveFollowerReq.ptrData = new MT4OrderInfo();
                                                    //memset(ptrSMLiveFollowerReq.ptrData, 0, sizeof(MT4OrderInfo));

                                                    MT4OrderInfo ptrOrdInfo = (MT4OrderInfo)ptrSMLiveFollowerReq.ptrData;
                                                    memcpy(ref ptrSMLiveFollowerReq.ptrData, ptr._orderReq);
                                                    ptrOrd._volume = getTradeVolume(smSignalIndex, itSMFoll2.second(), false);
                                                    ptrOrd._mt4Login = itSMFoll2.second();
                                                    if (ptrOrd._volume != 0)
                                                    {
                                                        vecDemoLiveOrders.push_back((false, ptrSMLiveFollowerReq));
                                                        insertFollowerOrderLinking(ptrSMLiveFollowerReq.signalServerTransID,
                                                            ptrOrd._mt4Login,
                                                            ptrSMLiveFollowerReq.masterLogin, false, ptrOrd._volume,
                                                            ptrSMLiveFollowerReq.serverTransID);

                                                        insertTransLinking(uniqueReqID, ptrSMLiveFollowerReq.serverTransID, false);
                                                        insertTransLinking(ptrSMLiveFollowerReq.signalServerTransID, ptrSMLiveFollowerReq.serverTransID, false);
                                                    }
                                                    else
                                                    {
                                                        m_ptrLogger.LogWarning("Follower Volume is 0... order not mirrored");
                                                    }
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    MT4Request ptrSSPLiveFollowerReq = (MT4Request)new MT4Request();
                                    //memset(ptrSSPLiveFollowerReq, 0, sizeof(MT4Request));
                                    ptrSSPLiveFollowerReq.masterLogin = getMasterLogin(SRV_TYPE_LIVE, itFoll2.second());
                                    ptrSSPLiveFollowerReq.reqType = MT4REQ_TRADE;
                                    ptrSSPLiveFollowerReq.requestMode = MT4REQMODE.OPEN_TRADE;
                                    // ptrSSPLiveFollowerReq.socketID = ptr._header._socketID;
                                    ptrSSPLiveFollowerReq.status = ORD_STAT_RECVD;
                                    ptrSSPLiveFollowerReq.serverTransID = uid;
                                    ptrSSPLiveFollowerReq.signalServerTransID = uniqueReqID;

                                    ptrSSPLiveFollowerReq.ptrData = new MT4OrderInfo();
                                    //memset(ptrSSPLiveFollowerReq.ptrData, 0, sizeof(MT4OrderInfo));

                                    MT4OrderInfo ptrOrd3 = (MT4OrderInfo)ptrSSPLiveFollowerReq.ptrData;
                                    memcpy(ref ptrSSPLiveFollowerReq.ptrData, ptr._orderReq);
                                    ptrOrd3._volume = getTradeVolume(ptr._orderReq._signalIndex, itFoll2.second(), false);
                                    ptrOrd3._mt4Login = itFoll2.second();
                                    if (ptrOrd._volume != 0)
                                    {
                                        vecDemoLiveOrders.push_back((false, ptrSSPLiveFollowerReq));
                                        insertFollowerOrderLinking(ptrSSPLiveFollowerReq.signalServerTransID,
                                            ptrOrd3._mt4Login,
                                            ptrSSPLiveFollowerReq.masterLogin, false, ptrOrd3._volume,
                                            ptrSSPLiveFollowerReq.serverTransID);

                                        insertTransLinking(ptrSSPLiveFollowerReq.signalServerTransID, ptrSSPLiveFollowerReq.serverTransID, false);
                                    }
                                    else
                                    {
                                        m_ptrLogger.LogWarning("Follower Volume is 0... order not mirrored");
                                    }
                                }
                            }//for (itFoll2 = itFoll1.second.begin(); itFoll2 != itFoll1.second.end(); itFoll2++)
                        }//if (itFoll1 != m_mapFollowers.end())

                    }

                    for (itVecDemoLiveOrders = vecDemoLiveOrders.begin(); itVecDemoLiveOrders != vecDemoLiveOrders.end(); itVecDemoLiveOrders++)
                    {
                        if (itVecDemoLiveOrders.first())
                        {
                            m_ptrDemoMT4Manager.insertMT4Request(itVecDemoLiveOrders.second());
                        }
                        else
                        {
                            m_ptrLiveMT4Manager.insertMT4Request(itVecDemoLiveOrders.second());
                        }
                    }

                }//if (isSSP)
                else
                {
                    MT4Request ptrSingleMT4Req = (MT4Request)new MT4Request();
                    //memset(ptrSingleMT4Req, 0, sizeof(MT4Request));
                    ptrSingleMT4Req.masterLogin = ptr._orderReq._masterLogin;
                    ptrSingleMT4Req.reqType = MT4REQ_TRADE;
                    ptrSingleMT4Req.requestMode = MT4REQMODE.OPEN_TRADE;
                    //ptrSingleMT4Req.socketID = ptr._header._socketID;
                    ptrSingleMT4Req.status = ORD_STAT_RECVD;
                    ptrSingleMT4Req.serverTransID = uniqueReqID;
                    ptrSingleMT4Req.ptrData = new MT4OrderInfo();
                    //memset(ptrSingleMT4Req.ptrData, 0, sizeof(MT4OrderInfo));
                    MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrSingleMT4Req.ptrData;
                    ptrOrd._mt4Login = ptr._orderReq._mt4Login;
                    memcpy(ref ptrSingleMT4Req.ptrData, ptr._orderReq);
                    if (ptr._orderReq._mt4ServerIndex == SRV_TYPE_DEMO)
                    {
                        m_ptrDemoMT4Manager.insertMT4Request(ptrSingleMT4Req);
                    }
                    else
                    {
                        m_ptrLiveMT4Manager.insertMT4Request(ptrSingleMT4Req);
                    }
                }//else of if (isSSP)
            }//if (isOrderValidated(ptr, reason, isSSP, isSM))
            else
            {
                //Last Stage some issue in order validation
                //SocialOrderResponse ptrResp2 = GetMessageObject<SocialOrderResponse>(MT_SocialOrderResponse_ID);
                //ptrResp2._header._loginID = ptr._orderReq._masterLogin;
                //ptrResp2._header._MT4loginID = ptr._orderReq._mt4Login;
                //ptrResp2._header._socketID = ptr._header._socketID;
                //ptrResp2._clientReqID = ptr._clientReqID;
                //ptrResp2._requestMode = MT4REQMODE.OPEN_TRADE;
                //ptrResp2._serverTransID = uniqueReqID;
                //ptrResp2._retCode = (int)reason;
                //SentDataUsingSocketID(ptrResp2, MT_SocialOrderResponse_ID, ptrResp2._header._socketID);
            }//else of if (isOrderValidated(ptr, reason, isSSP, isSM))

            uint lastuid = getUniqueRequestID();
            m_ptrMySqlWrapper.updateMasterSetting_UID(lastuid - 1);
            return reason;
        }
        //=================================================================================================
        void startTempSocialTradeThread()
        {
            m_ptrLogger.LogOk("START");
            stopTempSocialTradeThread();
            m_hTempSocialTradeThrdrEvnt = CreateEvent(false, false);

            m_iTempSocialTradeThrd = true;
            m_hTempSocialTradeThrd = _beginthreadex(tempSocialTradeThread);
            m_ptrLogger.LogOk("END");
            SetEvent(m_hTempSocialTradeThrdrEvnt);
        }
        //=================================================================================================
        void stopTempSocialTradeThread()
        {
            m_ptrLogger.LogOk("START");
            m_iTempSocialTradeThrd = false;

            if (m_hTempSocialTradeThrdrEvnt != null)
            {
                SetEvent(m_hTempSocialTradeThrdrEvnt);
            }

            if (m_hTempSocialTradeThrd != null)
            {
                WaitForSingleObject(m_hTempSocialTradeThrd, INFINITE);
            }

            if (m_hTempSocialTradeThrd != null)
            {
                CloseHandle(m_hTempSocialTradeThrd);
            }

            if (m_hTempSocialTradeThrdrEvnt != null)
            {
                CloseHandle(m_hTempSocialTradeThrdrEvnt);
            }
            m_hTempSocialTradeThrdrEvnt = null;
            m_hTempSocialTradeThrd = null;
            m_ptrLogger.LogOk("END");
        }
        //=================================================================================================
        void startDBTransmitThread()
        {
            m_ptrLogger.LogOk("START");
            stopDBTransmitThread();
            m_hTDBTransmithrdrEvnt = CreateEvent(false, false);

            m_iDBTransmitThrd = true;

            m_hDBTransmitThrd = _beginthreadex(dBTransmitThread);
            m_ptrLogger.LogOk("END");
            SetEvent(m_hTDBTransmithrdrEvnt);
        }
        //=================================================================================================
        void stopDBTransmitThread()
        {
            m_ptrLogger.LogOk("START");
            m_iDBTransmitThrd = false;

            if (m_hTDBTransmithrdrEvnt != null)
            {
                SetEvent(m_hTDBTransmithrdrEvnt);
            }

            if (m_hDBTransmitThrd != null)
            {
                WaitForSingleObject(m_hDBTransmitThrd, INFINITE);
            }

            if (m_hDBTransmitThrd != null)
            {
                CloseHandle(m_hDBTransmitThrd);
            }

            if (m_hTDBTransmithrdrEvnt != null)
            {
                CloseHandle(m_hTDBTransmithrdrEvnt);
            }
            m_hTDBTransmithrdrEvnt = null;
            m_hDBTransmitThrd = null;
            m_ptrLogger.LogOk("END");
        }
        //=================================================================================================
        void startRebateThread()
        {
            m_ptrLogger.LogOk("START");
            stopRebateThread();
            m_hRebateThrdEvnt = CreateEvent(false, false);

            m_iRebateThrd = true;
            m_hRebateThrd = _beginthreadex(rebateThread);
            m_ptrLogger.LogOk("END");
            SetEvent(m_hRebateThrdEvnt);
        }
        //=================================================================================================
        void stopRebateThread()
        {
            m_ptrLogger.LogOk("START");
            m_iRebateThrd = false;

            if (m_hRebateThrdEvnt != null)
            {
                SetEvent(m_hRebateThrdEvnt);
            }

            if (m_hRebateThrd != null)
            {
                WaitForSingleObject(m_hRebateThrd, INFINITE);
            }

            if (m_hRebateThrd != null)
            {
                CloseHandle(m_hRebateThrd);
            }

            if (m_hRebateThrdEvnt != null)
            {
                CloseHandle(m_hRebateThrdEvnt);
            }
            m_hRebateThrdEvnt = null;
            m_hRebateThrd = null;
            m_ptrLogger.LogOk("END");
        }
        //=================================================================================================
        //void startMarginLevelThread()
        //{
        //	m_ptrLogger.LogOk("START");
        //	stopTradeRecordThread();
        //	m_hMarginLevelThrdrEvnt = CreateEvent(false, false);
        //
        //	m_iMarginLevelThrd = true;
        //	m_hMarginLevelThrd = (HANDLE)_beginthreadex(null, null, marginLevelThread, this, null, null);
        //	m_ptrLogger.LogOk("END");
        //	SetEvent(m_hMarginLevelThrdrEvnt);
        //}

        //void stopMarginLevelThread()
        //{
        //	m_ptrLogger.LogOk("START");
        //	m_iMarginLevelThrd = false;
        //
        //	if (m_hMarginLevelThrdrEvnt != null)
        //	{
        //		SetEvent(m_hMarginLevelThrdrEvnt);
        //	}
        //
        //	if (m_hMarginLevelThrd != null)
        //	{
        //		WaitForSingleObject(m_hMarginLevelThrd, INFINITE);
        //	}
        //
        //	if (m_hMarginLevelThrd != null)
        //	{
        //		CloseHandle(m_hMarginLevelThrd);
        //	}
        //
        //	if (m_hMarginLevelThrdrEvnt != null)
        //	{
        //		CloseHandle(m_hMarginLevelThrdrEvnt);
        //	}
        //	m_hMarginLevelThrdrEvnt = null;
        //	m_hMarginLevelThrd = null;
        //	m_ptrLogger.LogOk("END");
        //}
        //=================================================================================================
        uint tempSocialTradeThread()
        {
            Queue<TempSocialRecord> tempQueue = new Queue<TempSocialRecord>();
            int sigLogin, follLogin;
            sigLogin = follLogin = 0;
            m_ptrLogger.LogOk("START");

            while (m_iTempSocialTradeThrd)
            {
                WaitForSingleObject(m_hTempSocialTradeThrdrEvnt, INFINITE);

                lock (m_csQueueTempSocialTrade)
                {
                    while (!m_queueTempSocialTrade.empty() && m_iTempSocialTradeThrd)
                    {
                        tempQueue.Enqueue(m_queueTempSocialTrade.front());
                        m_queueTempSocialTrade.pop();
                    }
                }

                while (!tempQueue.empty() && m_iTempSocialTradeThrd)
                {
                    TempSocialRecord ptrTemp = tempQueue.front();
                    SocialTradeRecordResponse ptr = GetMessageObject<SocialTradeRecordResponse>(MT_SocialTradeRecordResponse_ID);
                    ptr._tradeRcrd._transID = ptrTemp.transid;
                    ptr._tradeRcrd._signalTransID = ptrTemp.signalTransid;
                    ptr._tradeRcrd._signalIndex = ptrTemp.signalIndex;
                    ptr._tradeRcrd._signalMasterLogin = sigLogin = ptrTemp.signalMasterLogin;
                    ptr._tradeRcrd._signalMT4Login = ptrTemp.signalMT4Login;
                    ptr._tradeRcrd._signalOrderID = ptrTemp.signalOrderID;
                    ptr._tradeRcrd._isSSPSignal = ptrTemp.isSSP;
                    ptr._tradeRcrd._isServerDemo = ptrTemp.isTraderServerDemo;
                    ptr._tradeRcrd._orderID = ptrTemp.traderOrderid;
                    ptr._tradeRcrd._mT4Login = ptrTemp.traderMT4Login;
                    ptr._tradeRcrd._masterLogin = follLogin = ptrTemp.traderMasterLogin;

                    SocialTradeRecordResponse ptr2 = GetMessageObject<SocialTradeRecordResponse>(MT_SocialTradeRecordResponse_ID);
                    memcpy(ref ptr2, ptr);

                    SentDataUsingLoginID(ptr, MT_SocialTradeRecordResponse_ID, sigLogin);
                    SentDataUsingLoginID(ptr2, MT_SocialTradeRecordResponse_ID, follLogin);

                    m_ptrMySqlWrapper.insertSocialTradeRecord(ptrTemp.transid, ptrTemp.signalTransid, ptrTemp.signalIndex, ptrTemp.signalMasterLogin, ptrTemp.signalMT4Login, ptrTemp.signalOrderID, ptrTemp.isSSP, ptrTemp.isTraderServerDemo, ptrTemp.traderOrderid, ptrTemp.traderMT4Login, ptrTemp.traderMasterLogin);
                    free(ptrTemp);
                    ptrTemp = null;
                    tempQueue.pop();

                }
            }
            //while (!tempQueue.empty())
            //{
            //    free(tempQueue.front());
            //    //tempQueue.front() = null;
            //    tempQueue.pop();
            //}
            tempQueue.Clear();
            m_ptrLogger.LogOk("END");
            return 0;
        }
        //=================================================================================================
        uint dBTransmitThread()
        {
            Queue<ValueTuple<int, FileDataTypeID>> tempQueue = new Queue<(int, FileDataTypeID)>();
            int masterlogin = 0;
            FileDataTypeID dataid = 0;
            DateTimeOffset strSysTime;


            m_ptrLogger.LogOk("START");

            while (m_iDBTransmitThrd)
            {
                WaitForSingleObject(m_hTDBTransmithrdrEvnt, INFINITE);

                //SYSTEMTIME st;
                //GetLocalTime(&st);
                //time_t curr;
                //Utilities.DateTime.SystemTimeToTime_t(&st, &curr);
                //curr = curr - 10;
                //strSysTime = Utilities.DateTime.Convert_time_t_to_MySQLDateTime(curr);
                strSysTime = DateTimeOffset.UtcNow.AddSeconds(-10);

                lock (m_csQueueDBTransmit)
                {
                    while (!m_queueDBTransmit.empty() && m_iDBTransmitThrd)
                    {
                        tempQueue.Enqueue(m_queueDBTransmit.front());
                        m_queueDBTransmit.pop();
                    }
                }
                int cnt = 0;
                while (!tempQueue.empty() && m_iDBTransmitThrd)
                {
                    masterlogin = tempQueue.front().first();
                    dataid = tempQueue.front().second();
                    //var vecSocketID = m_connectionMgr.GetOnlineUsers().Select(i => i.SocketID).ToList();

                    //foreach (var itVec in vecSocketID)
                    //{
                    if (dataid == 0)//send all data
                    {

                    }//if (dataid == 0)//send all data
                    else
                    {
                        switch (dataid)
                        {
                            case FDMT_MasterUser_ID:
                                {
                                    var ptrMasterArr = m_ptrMySqlWrapper.getAllUpdatedMasterAccounts(strSysTime);
                                    cnt = ptrMasterArr.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_MasterUser_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_MasterUser_ID;
                                        //ptrFileResp._header._fileSize = sizeof(MasterUser) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrMasterArr);
                                    }
                                }
                                break;
                            case FDMT_MT4Account_ID:
                                {
                                    var ptrAccArr = m_ptrMySqlWrapper.getAllMT4AccountsForMasterLogin(masterlogin, strSysTime);
                                    cnt = ptrAccArr.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_MT4Account_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_MT4Account_ID;
                                        //ptrFileResp._header._fileSize = sizeof(MT4Account) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrAccArr);
                                    }
                                }
                                break;
                            case FDMT_Signal_ID:
                                {
                                    var ptrAllSignal = m_ptrMySqlWrapper.getAllSignals(masterlogin, strSysTime);
                                    cnt = ptrAllSignal.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_Signal_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_Signal_ID;
                                        //ptrFileResp._header._fileSize = sizeof(Signal) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrAllSignal);
                                    }
                                }
                                break;
                            case FDMT_SMSignal_ID:
                                {
                                    var ptrAllSMSignal = m_ptrMySqlWrapper.getAllSMSignals(masterlogin, strSysTime);
                                    cnt = ptrAllSMSignal.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_SMSignal_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_SMSignal_ID;
                                        //ptrFileResp._header._fileSize = sizeof(SMSignal) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrAllSMSignal);
                                    }
                                }
                                break;
                            case FDMT_SSPSignal_ID:
                                {
                                    var ptrAllSSPSignal = m_ptrMySqlWrapper.getAllSSPSignals(masterlogin, strSysTime);
                                    cnt = ptrAllSSPSignal.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_SSPSignal_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_SSPSignal_ID;
                                        //ptrFileResp._header._fileSize = sizeof(SSPSignal) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrAllSSPSignal);
                                    }
                                }
                                break;
                            case FDMT_SMFollower_ID:
                                {
                                    var ptrAllSMSignal = m_ptrMySqlWrapper.getSMSubscribedSignals(masterlogin, strSysTime);
                                    cnt = ptrAllSMSignal.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_SMFollower_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_SMFollower_ID;
                                        //ptrFileResp._header._fileSize = sizeof(SMFollower) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrAllSMSignal);
                                    }
                                }
                                break;
                            case FDMT_SSPFollower_ID:
                                {
                                    var ptrAllSSPFollower = m_ptrMySqlWrapper.getSSPSubscribedSignals(masterlogin, strSysTime);
                                    cnt = ptrAllSSPFollower.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_SSPFollower_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_SSPFollower_ID;
                                        //ptrFileResp._header._fileSize = sizeof(SSPFollower) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrAllSSPFollower);
                                    }
                                }
                                break;
                            case FDMT_MT4TradeDisableInfo_ID:
                                {
                                    var ptrMt4DisableTrades = m_ptrMySqlWrapper.getAllTradeDisableInfos(masterlogin, strSysTime);
                                    cnt = ptrMt4DisableTrades.Count;
                                    if (cnt > 0)
                                    {
                                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                                        ptrFileResp._fileDataMessageType = FDMT_MT4TradeDisableInfo_ID;
                                        ptrFileResp._header._fileDataMessageType = FDMT_MT4TradeDisableInfo_ID;
                                        //ptrFileResp._header._fileSize = sizeof(MT4TradeDisableInfo) * cnt;
                                        ptrFileResp._header._loginID = masterlogin;
                                        //ptrFileResp._header._socketID = itVec;
                                        m_connectionMgr.SendAsFile(/*itVec,*/ ptrFileResp, ptrMt4DisableTrades);
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }// else of if (dataid == 0)//send all data
                    //}//for (itVec = vecSocketID.begin(); itVec != vecSocketID.end(); itVec++)
                    tempQueue.pop();
                }//while (!tempQueue.empty() && m_iDBTransmitThrd)
            }//while (m_iDBTransmitThrd)

            tempQueue.Clear();
            m_ptrLogger.LogOk("END");
            return 0;
        }
        //=================================================================================================
        uint rebateThread()
        {
            Queue<RebateData> tempQueue = new Queue<RebateData>();
            int masterlogin, rebateMt4Acc;
            float signalRebateAmt, sspPer, totalRebateAmt, sspRebate, smRebate;

            masterlogin = rebateMt4Acc = -1;
            signalRebateAmt = sspPer = 0.0F;
            Signal sig = new Signal();

            m_ptrLogger.LogOk("START");

            while (m_iRebateThrd)
            {
                WaitForSingleObject(m_hRebateThrdEvnt, INFINITE);

                lock (m_csQueueRebate)
                {
                    while (!m_queueRebate.empty() && m_iRebateThrd)
                    {
                        tempQueue.Enqueue(m_queueRebate.front());
                        m_queueRebate.pop();
                    }
                }

                while (!tempQueue.empty() && m_iRebateThrd)
                {
                    RebateData ptrRebate = tempQueue.front();
                    //Insert Data in DB for rebate
                    m_ptrMySqlWrapper.insertRebateData(ptrRebate.signalIndex,
                        ptrRebate.signalMT4Login,
                        ptrRebate.signlaOrderID,
                        ptrRebate.followerOrderID,
                        ptrRebate.followerVolume,
                        ptrRebate.isSSP,
                        ptrRebate.sspAccForSM);
                    //memset(&sig, 0, sizeof(Signal));
                    if (ptrRebate.isSSP)
                    {
                        if (getSignal(sig, ptrRebate.signalIndex))
                        {
                            masterlogin = getMasterLogin(SRV_TYPE_DEMO, ptrRebate.signalMT4Login);
                            rebateMt4Acc = getRebateAccount(masterlogin);
                            signalRebateAmt = sig._rebateAmount;
                            totalRebateAmt = signalRebateAmt * (ptrRebate.followerVolume / 100.0F);

                            m_ptrLogger.LogOk("MasterLogin: %d RebateMt4Acc: %d RebateAmt: %0.02f FollowerVol: %d IsSSP: %s SSPAccForSM: %d",
                                masterlogin, rebateMt4Acc, signalRebateAmt, ptrRebate.followerVolume, (ptrRebate.isSSP == true ? "YES" : "NO"), ptrRebate.sspAccForSM);

                            MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                            //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                            ptrMT4Req4.masterLogin = masterlogin;
                            ptrMT4Req4.reqType = MT4REQ_BALANCE;
                            ptrMT4Req4.status = ORD_STAT_RECVD;
                            ptrMT4Req4.serverTransID = getUniqueRequestID();
                            ptrMT4Req4.signalServerTransID = (uint)ptrRebate.followerOrderID; //TODO Claudia: remove the cast and refactor all OrderId and TransId, the type should be int everywhere
                            ptrMT4Req4.ptrData = new MT4OrderInfo();
                            //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));
                            MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                            ptrOrd._accountType = ACC_TYPE_REBATE;
                            ptrOrd._masterLogin = masterlogin;
                            ptrOrd._mt4Login = rebateMt4Acc;
                            ptrOrd._mt4ServerIndex = SRV_TYPE_DEMO;
                            ptrOrd._orderTransMode = ORD_TRANS_CLOSE;
                            ptrOrd._orderType = ORD_TYPE_BALANCE;
                            ptrOrd._price = (double)totalRebateAmt;
                            ptrOrd._price = round_off(ptrOrd._price, 2);
                            m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req4);


                        }
                        else
                        {
                            m_ptrLogger.LogError("Unable to get signal for rebate SigMt4 : %d SigIndex: %d Order: %d", ptrRebate.signalMT4Login, ptrRebate.signalIndex, ptrRebate.followerOrderID);
                        }
                    }
                    else
                    {
                        if (getSignal(sig, ptrRebate.signalIndex))
                        {
                            masterlogin = getMasterLogin(SRV_TYPE_DEMO, ptrRebate.signalMT4Login);
                            rebateMt4Acc = getRebateAccount(masterlogin);
                            signalRebateAmt = sig._rebateAmount;
                            sspPer = sig._sspPercentage;
                            totalRebateAmt = signalRebateAmt * (ptrRebate.followerVolume / 100.0F);
                            sspRebate = (totalRebateAmt * sspPer) / 100.0F;
                            smRebate = totalRebateAmt - sspRebate;


                            MT4Request ptrMT4Req4 = (MT4Request)new MT4Request();
                            //memset(ptrMT4Req4, 0, sizeof(MT4Request));
                            ptrMT4Req4.masterLogin = masterlogin;
                            ptrMT4Req4.reqType = MT4REQ_BALANCE;
                            ptrMT4Req4.status = ORD_STAT_RECVD;
                            ptrMT4Req4.serverTransID = getUniqueRequestID();
                            ptrMT4Req4.signalServerTransID = (uint)ptrRebate.followerOrderID; //TODO Claudia: remove the cast and refactor all OrderId and TransId, the type should be int everywhere
                            ptrMT4Req4.ptrData = new MT4OrderInfo();
                            //memset(ptrMT4Req4.ptrData, 0, sizeof(MT4OrderInfo));
                            MT4OrderInfo ptrOrd = (MT4OrderInfo)ptrMT4Req4.ptrData;
                            ptrOrd._accountType = ACC_TYPE_REBATE;
                            ptrOrd._masterLogin = masterlogin;
                            ptrOrd._mt4Login = rebateMt4Acc;
                            ptrOrd._mt4ServerIndex = SRV_TYPE_DEMO;
                            ptrOrd._orderTransMode = ORD_TRANS_CLOSE;
                            ptrOrd._orderType = ORD_TYPE_BALANCE;
                            ptrOrd._price = (double)smRebate;
                            ptrOrd._price = round_off(ptrOrd._price, 2);
                            m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req4);


                            masterlogin = getMasterLogin(SRV_TYPE_DEMO, ptrRebate.sspAccForSM);
                            rebateMt4Acc = getRebateAccount(masterlogin);

                            MT4Request ptrMT4Req5 = (MT4Request)new MT4Request();
                            //memset(ptrMT4Req5, 0, sizeof(MT4Request));
                            ptrMT4Req5.masterLogin = masterlogin;
                            ptrMT4Req5.reqType = MT4REQ_BALANCE;
                            ptrMT4Req5.status = ORD_STAT_RECVD;
                            ptrMT4Req5.serverTransID = getUniqueRequestID();
                            ptrMT4Req5.ptrData = new MT4OrderInfo();
                            //memset(ptrMT4Req5.ptrData, 0, sizeof(MT4OrderInfo));
                            MT4OrderInfo ptrOrd2 = (MT4OrderInfo)ptrMT4Req5.ptrData;
                            ptrOrd2._accountType = ACC_TYPE_REBATE;
                            ptrOrd2._masterLogin = masterlogin;
                            ptrOrd2._mt4Login = rebateMt4Acc;
                            ptrOrd2._mt4ServerIndex = SRV_TYPE_DEMO;
                            ptrOrd2._orderTransMode = ORD_TRANS_CLOSE;
                            ptrOrd2._orderType = ORD_TYPE_BALANCE;
                            ptrOrd2._price = (double)sspRebate;
                            ptrOrd._price = round_off(ptrOrd._price, 2);
                            m_ptrDemoMT4Manager.insertMT4Request(ptrMT4Req5);
                        }
                        else
                        {
                            m_ptrLogger.LogError("Unable to get signal for rebate SigMt4 : %d SigIndex: %d Order: %d", ptrRebate.signalMT4Login, ptrRebate.signalIndex, ptrRebate.followerOrderID);
                        }

                    }
                    free(ptrRebate);
                    tempQueue.pop();
                }//while (!tempQueue.empty() && m_iDBTransmitThrd)
            }//while (m_iDBTransmitThrd)

            //while (!tempQueue.empty())
            //{
            //    free(tempQueue.front());
            //    tempQueue.pop();
            //}//while (!tempQueue.empty())
            tempQueue.Clear();
            m_ptrLogger.LogOk("END");
            return 0;
        }
        //=================================================================================================
        //uint 	marginLevelThread(object arg)
        //{
        //	WitFXMT4ServerBL pThis = (WitFXMT4ServerBL)arg;
        //	Queue<ValueTuple<MarginLevel*, bool>> tempQueue;
        //
        //	m_ptrLogger.LogOk("START");
        //
        //	while (m_iMarginLevelThrd)
        //	{
        //		WaitForSingleObject(m_hMarginLevelThrdrEvnt, INFINITE);
        //
        //		lock (m_csQueueMarginLevel) {
        //		while (!m_queueMarginLevel.empty() && m_iMarginLevelThrd)
        //		{
        //			tempQueue.Enqueue(m_queueMarginLevel.front());
        //			m_queueMarginLevel.pop();
        //		}
        //		}
        //
        //		while (!tempQueue.empty() && m_iMarginLevelThrd)
        //		{
        //
        //			tempQueue.pop();
        //
        //		}
        //	}
        //	while (!tempQueue.empty())
        //	{
        //		free(tempQueue.front().first);
        //		tempQueue.pop();
        //	}
        //	m_ptrLogger.LogOk("END");
        //	return 0;
        //}
        //=================================================================================================
        void insertTempSocialTrade(TempSocialRecord ptrData)
        {
            lock (m_csQueueTempSocialTrade)
            {
                m_queueTempSocialTrade.Enqueue(ptrData);
            }

            SetEvent(m_hTempSocialTradeThrdrEvnt);
        }
        //=================================================================================================
        void insertDBTransmitData(int masterLogin, FileDataTypeID data_ID)
        {
            lock (m_csQueueDBTransmit)
            {

                m_queueDBTransmit.Enqueue((masterLogin, data_ID));

            }

            SetEvent(m_hTDBTransmithrdrEvnt);
        }
        ////=================================================================================================
        void insertRebateData(int signalIndex, int signalMT4Login, int followerOrderID, int followerVolume, bool isSSP, int sspAccForSM, int sigOrderID)
        {
            RebateData ptrData = (RebateData)new RebateData();
            //memset(ptrData, 0, sizeof(RebateData));
            ptrData.followerOrderID = followerOrderID;
            ptrData.signlaOrderID = sigOrderID;
            ptrData.followerVolume = followerVolume;
            ptrData.isSSP = isSSP;
            ptrData.signalIndex = signalIndex;
            ptrData.signalMT4Login = signalMT4Login;
            ptrData.sspAccForSM = sspAccForSM;


            lock (m_csQueueRebate)
            {

                m_queueRebate.Enqueue(ptrData);

            }

            SetEvent(m_hRebateThrdEvnt);
        }
        ////=================================================================================================
        void startRankingThread()
        {
            m_ptrLogger.LogOk("START");
            stopRankingThread();
            m_hRankingThrdEvnt = CreateEvent(true, false);
            m_iRankingThrd = true;
            m_hRankingThrd = _beginthreadex(rankingThread);

            m_ptrLogger.LogOk("END");
        }

        void stopRankingThread()
        {
            m_ptrLogger.LogOk("START");

            m_iRankingThrd = false;

            if (m_hRankingThrdEvnt != null)
            {
                SetEvent(m_hRankingThrdEvnt);
            }

            if (m_hRankingThrd != null)
            {
                WaitForSingleObject(m_hRankingThrd, INFINITE);
            }

            if (m_hRankingThrd != null)
            {
                CloseHandle(m_hRankingThrd);
            }

            if (m_hRankingThrdEvnt != null)
            {
                CloseHandle(m_hRankingThrdEvnt);
            }
            m_hRankingThrdEvnt = null;
            m_hRankingThrd = null;
            m_ptrLogger.LogOk("END");
        }

        public volatile static Sys.IReadOnlyList<Ranking> RankingCache = Array.Empty<Ranking>();

        uint rankingThread()
        {
            var waitMiliSec = m_iRankingThrdPeriod * 1000;

            while (m_iRankingThrd && !m_isResetting)
            {
                RankingCache = m_ptrMySqlWrapper.readSignalD1data(90);

                if (WaitForSingleObject(m_hRankingThrdEvnt, waitMiliSec) == WAIT_TIMEOUT)
                {
                    if (!m_iRankingThrd || m_isResetting)
                    {
                        break;
                    }
                    m_ptrMySqlWrapper.syncD1ReportData(m_ptrDemoMT4Manager.getServerTime());
                    /////////////////////////////////////////////////////////////////////
                }//if (WaitForSingleObject(m_hOpenTradeAnalyThrdEvnt, waitMiliSec) == WAIT_TIMEOUT)
                waitMiliSec = m_iRankingThrdPeriod = m_masterUserSetting._rankingRefreshInSec * 100;
            }//while (m_iOpenTradeAnalyThrd && !m_isClosing)
            return 0;
        }//uint 	MT4ServerConnector.openTradeAnalyserThread(object arg)
         ////=================================================================================================
        void startIsTradingDisabledThread()
        {
            m_ptrLogger.LogOk("START");
            stopIsTradingDisabledThread();
            m_hIsTradingDisabledThrdEvnt = CreateEvent(true, false);
            m_iIsTradingDisabledThrd = true;
            m_hIsTradingDisabledThrd = _beginthreadex(isTradingDisabledThread);

            m_ptrLogger.LogOk("END");
        }

        void stopIsTradingDisabledThread()
        {
            m_ptrLogger.LogOk("START");

            m_iIsTradingDisabledThrd = false;

            if (m_hIsTradingDisabledThrdEvnt != null)
            {
                SetEvent(m_hIsTradingDisabledThrdEvnt);
            }

            if (m_hIsTradingDisabledThrd != null)
            {
                WaitForSingleObject(m_hIsTradingDisabledThrd, INFINITE);
            }

            if (m_hIsTradingDisabledThrd != null)
            {
                CloseHandle(m_hIsTradingDisabledThrd);
            }

            if (m_hIsTradingDisabledThrdEvnt != null)
            {
                CloseHandle(m_hIsTradingDisabledThrdEvnt);
            }
            m_hIsTradingDisabledThrdEvnt = null;
            m_hIsTradingDisabledThrd = null;
            m_ptrLogger.LogOk("END");
        }

        uint isTradingDisabledThread()
        {
            var waitMiliSec = m_iIsTradingDisabledThrdPeriod * 1000;

            while (m_iIsTradingDisabledThrd && !m_isResetting)
            {
                if (WaitForSingleObject(m_hIsTradingDisabledThrdEvnt, waitMiliSec) == WAIT_TIMEOUT)
                {
                    if (!m_iIsTradingDisabledThrd || m_isResetting)
                    {
                        break;
                    }
                    Sys.IReadOnlyList<MT4TradeDisableInfo> ptrMt4DisableTrades = m_ptrMySqlWrapper.getAllTradeDisableInfo();
                    var cnt = ptrMt4DisableTrades.Count;
                    if (cnt > 0)
                    {
                        GenericFileDataResponse ptrFileResp = GetFileMessageObject<GenericFileDataResponse>(MT_GenericFileDataResponse_ID);
                        ptrFileResp._fileDataMessageType = FDMT_MT4TradeDisableInfo_ID;
                        ptrFileResp._header._fileDataMessageType = FDMT_MT4TradeDisableInfo_ID;
                        //ptrFileResp._header._fileSize = sizeof(MT4TradeDisableInfo) * cnt;

                        //foreach (var onlineUser in m_connectionMgr.GetOnlineUsers())
                        //    if (!m_isResetting)
                        //    {
                        //        ptrFileResp._header._loginID = onlineUser.LoginID;
                        //        ptrFileResp._header._socketID = onlineUser.SocketID;
                        m_connectionMgr.SendAsFile(/*onlineUser.SocketID,*/ ptrFileResp, ptrMt4DisableTrades);
                        //    }
                    }//end if (cnt > 0)

                }
                waitMiliSec = m_iIsTradingDisabledThrdPeriod * 1000;
            }
            return 0;
        }

        /*************************************************************************************/
        int getSymbolIndex(string symbol)
        {
            int ret = -1;
            Dictionary<string, int>.iterator it;

            //lock (m_csSymbolIndexName) {
            it = m_mapSymbolNameIndex.find(symbol);
            if (it != m_mapSymbolNameIndex.end())
            {
                ret = it.second;
            }
            //}

            return ret;
        }
        //================================================================================================//
        string getSymbolName(int index)
        {
            string ret = "";
            Dictionary<int, string>.iterator it;

            //lock (m_csSymbolIndexName) {
            it = m_mapSymbolIndexName.find(index);
            if (it != m_mapSymbolIndexName.end())
            {
                ret = it.second;
            }
            //}
            return ret;
        }
        //================================================================================================//
        uint getUniqueRequestID()
        {
            uint id = 0;

            lock (m_csUniqueID)
            {
                id = (++m_iUniqueID);
            }

            return id;
        }
        //================================================================================================//
        bool isClientSSP(eMT4ServerType serverIndex, int mt4login)
        {
            bool ret = false;
            if (serverIndex == SRV_TYPE_DEMO)
            {
                lock (m_SyncAccLinking)
                {
                    if (m_setSM_MT4Login.find(mt4login) == m_setSM_MT4Login.end() && m_mapFollowers.find(mt4login) != m_mapFollowers.end())
                    {
                        ret = true;
                    }
                }
            }

            return ret;
        }
        //================================================================================================//
        bool isThisAdmin(int masterlogin)
        {
            if (m_setAdminLogin.find(masterlogin) != m_setAdminLogin.end())
            {
                return true;
            }
            return false;
        }
        //================================================================================================//
        bool getSSPSignalDetail(eMT4ServerType serverIndex, int masterlogin, int mt4login, int signalIndex, string symbol, ref int maxSLinPips, ref eReturnCode errorCode)
        {
            errorCode = eReturnCode.RC_INVALID_SIGNAL_SYMBOL;
            bool ret = false;
            //Dictionary<int, ValueTuple<int, int>>.iterator itSSPSignal;

            lock (m_SyncSignalSSP)
            {

                var itSSPSignal = m_mapSignalSSP.find(signalIndex);
                if (itSSPSignal != m_mapSignalSSP.end())
                {
                    int symIndex = itSSPSignal.second.first();
                    var strategyType = itSSPSignal.second.second();
                    if (getSymbolName(symIndex) == symbol)
                    {
                        //Dictionary<int, Dictionary<int, SignalSymbolSetting>>.iterator itSymSett1;
                        //Dictionary<int, SignalSymbolSetting>.iterator itSymSett12;
                        lock (m_SyncSymbolSetting)
                        {
                            var itSymSett1 = m_mapSignalSymSetting.find(symIndex);
                            if (itSymSett1 != m_mapSignalSymSetting.end())
                            {
                                var itSymSett12 = itSymSett1.second.find(strategyType);
                                if (itSymSett12 != itSymSett1.second.end())
                                {
                                    errorCode = eReturnCode.RC_OK;
                                    maxSLinPips = itSymSett12.second._maxSLInPips;
                                    ret = true;
                                }
                            }
                        }
                    }
                }

            }

            return ret;
        }
        //================================================================================================//
        int ConvertInPips(double val, int digit, double pointValue)
        {
            double dAdjPoint = pointValue;

            if (digit == 5 || digit == 3)
            {
                dAdjPoint = pointValue * 10.0;
            }
            int ret = (int)(val / dAdjPoint);
            return ret;
        }
        //================================================================================================//
        double convertPipToValue(int pips, int digit, double pointValue)
        {
            double dAdjPoint = pointValue;

            if (digit == 5 || digit == 3)
            {
                dAdjPoint = pointValue * 10.0;
            }
            double ret = (double)pips * dAdjPoint;
            return ret;

        }
        //================================================================================================//
        bool isOrderTypeAndSymbolSupported(MT4OrderInfo ptr, ref eReturnCode reason)
        {
            if (getSymbolIndex(ptr._symbol) == -1)
            {
                reason = RC_INVALID_SYMBOL;
                return false;
            }
            if (!(ptr._orderType >= ORD_TYPE_BUY && ptr._orderType <= ORD_TYPE_SELLSTOP))
            {
                reason = RC_INVALID_ORDER_CMD;
                return false;
            }
            return true;
        }
        //================================================================================================//
        bool isOrderTransSupported(MT4OrderInfo ptr, ref eReturnCode reason)
        {
            if (!(ptr._orderTransMode >= ORD_TRANS_OPEN && ptr._orderTransMode <= ORD_TRANS_CLOSE))
            {
                reason = RC_INVALID_ORDER_TRANS_MODE;
                return false;
            }
            return true;
        }
        //================================================================================================//
        bool isClientSM(eMT4ServerType serverIndex, int mt4login)
        {
            bool ret = false;
            if (serverIndex == SRV_TYPE_DEMO)
            {
                lock (m_SyncAccLinking)
                {
                    if (m_setSM_MT4Login.find(mt4login) != m_setSM_MT4Login.end())
                    {
                        ret = true;
                    }
                }
            }

            return ret;
        }
        //================================================================================================//
        bool getLatestBidAsk(string symbol, ref double bid, ref double ask)
        {
            bool ret = false;
            int symIndex = getSymbolIndex(symbol);

            Dictionary<int, MarketData>.iterator it;

            lock (m_SyncQuoteBook)
            {
                it = m_mapQuoteBook.find(symIndex);
                if (it != m_mapQuoteBook.end())
                {
                    bid = it.second._bid;
                    ask = it.second._ask;
                    ret = true;
                }
            }

            return ret;
        }
        //================================================================================================//
        bool getSymbolProperty(string symbol, ref int digit, ref int stopLevel, ref double pointValue)
        {
            Dictionary<string, MT4SymbolInfo>.iterator it = m_mapSymbolSpec.find(symbol);
            if (it != m_mapSymbolSpec.end())
            {
                digit = it.second._digits;
                stopLevel = it.second._stops_level;
                pointValue = it.second._point;
                return true;
            }
            return false;
        }
        //================================================================================================//
        int getTradeVolume(int signalIndex, int mt4login, bool isDemoServer)
        {
            int vol = 0;
            Dictionary<bool, Dictionary<int, Dictionary<int, int>>>.iterator it3;
            Dictionary<int, Dictionary<int, int>>.iterator it4;
            Dictionary<int, int>.iterator it5;

            lock (m_SyncfollowerTradeVol)
            {
                it3 = m_mapfollowerTradeVol.find(isDemoServer);
                if (it3 != m_mapfollowerTradeVol.end())
                {
                    it4 = it3.second.find(signalIndex);
                    if (it4 != it3.second.end())
                    {
                        it5 = it4.second.find(mt4login);
                        if (it5 != it4.second.end())
                        {
                            vol = it5.second;
                        }
                    }
                }
            }

            return vol;
        }
        //=========================================================================================================================================================//
        int getMasterLogin(eMT4ServerType serverIndex, int mt4Login)
        {
            int ret = 0;

            Dictionary<bool, Dictionary<int, int>>.iterator it1;
            Dictionary<int, int>.iterator it2;

            lock (m_SyncMapMt4Master)
            {

                bool isDemoServer = serverIndex == SRV_TYPE_DEMO ? true : false;
                it1 = m_mapMT4MasterLogin.find(isDemoServer);
                if (it1 != m_mapMT4MasterLogin.end())
                {
                    it2 = it1.second.find(mt4Login);
                    if (it2 != it1.second.end())
                    {
                        ret = it2.second;
                    }
                }

            }

            return ret;
        }
        //=========================================================================================================================================================//
        int getRebateAccount(int masterlogin)
        {
            int ret = -1;
            Dictionary<int, int>.iterator it2;

            lock (m_SyncMapMt4Master)
            {
                it2 = m_mapMasterRebateAcc.find(masterlogin);
                if (it2 != m_mapMasterRebateAcc.end())
                {
                    ret = it2.second;
                }
            }

            return ret;
        }
        //=========================================================================================================================================================//
        void insertTransLinking(uint signalTransID, uint followerTransID, bool isFollowerDemo)
        {
            m_ptrLogger.LogOk("Signal Trans ID: %u FollowerTransID: %u IsFollowerDemo: %s", signalTransID, followerTransID, (isFollowerDemo == true ? "YES" : "NO"));

            lock (m_SyncOrderLinking)
            {

                Dictionary<uint, List<uint>>.iterator it1;
                List<uint>.iterator it2;
                Dictionary<uint, ValueTuple<int, bool>>.iterator it4;

                it1 = m_mapTransLinking.find(signalTransID);
                if (it1 == m_mapTransLinking.end())
                {
                    List<uint> vec = new List<uint>();
                    m_mapTransLinking.insert(new ValueTuple<uint, List<uint>>(signalTransID, vec));
                    it1 = m_mapTransLinking.find(signalTransID);
                }

                it2 = find(it1.second.begin(), it1.second.end(), followerTransID);
                if (it2 == it1.second.end())
                {
                    it1.second.push_back(followerTransID);
                }
                else
                {
                    m_ptrLogger.LogWarning("Follower Trans ID: %u already exist for SigTransID : %u",
                        followerTransID, signalTransID);
                }

                it4 = m_mapTransIDOrderID.find(followerTransID);

                if (it4 == m_mapTransIDOrderID.end())
                {
                    m_mapTransIDOrderID.insert(new ValueTuple<uint, ValueTuple<int, bool>>(followerTransID, (-1, isFollowerDemo)));
                }
                else
                {
                    m_ptrLogger.LogWarning("Follower Trans ID: %u already exist in m_mapTransIDOrderID", followerTransID);
                }


                it4 = m_mapTransIDOrderID.find(signalTransID);

                if (it4 == m_mapTransIDOrderID.end())
                {
                    m_mapTransIDOrderID.insert(new ValueTuple<uint, ValueTuple<int, bool>>(signalTransID, (-1, true)));
                }
                else
                {
                    m_ptrLogger.LogWarning("Signal Trans ID: %u already exist in m_mapTransIDOrderID", signalTransID);
                }


            }
        }
        //=========================================================================================================================================================//
        void updateTransLinking(uint transID, int orderID)
        {
            m_ptrLogger.LogOk("TransID: %u OrderID: %d", transID, orderID);

            lock (m_SyncOrderLinking)
            {

                Dictionary<uint, ValueTuple<int, bool>>.iterator it4;
                Dictionary<bool, Dictionary<int, uint>>.iterator it5;
                Dictionary<int, uint>.iterator it6;

                it4 = m_mapTransIDOrderID.find(transID);

                if (it4 != m_mapTransIDOrderID.end())
                {
                    it4.second = (orderID, it4.second.second());

                    it5 = m_mapOrderIDTransID.find(it4.second.second());
                    if (it5 == m_mapOrderIDTransID.end())
                    {
                        Dictionary<int, uint> mp = new Dictionary<int, uint>();
                        m_mapOrderIDTransID.insert(new ValueTuple<bool, Dictionary<int, uint>>(it4.second.second(), mp));
                        it5 = m_mapOrderIDTransID.find(it4.second.second());
                    }

                    it6 = it5.second.find(orderID);
                    if (it6 == it5.second.end())
                    {
                        it5.second.insert(new ValueTuple<int, uint>(orderID, transID));
                        ///////////////////////////////////////////////////////////////////
                        Dictionary<uint, FollowerOrderLinking>.iterator itFollowerLink;
                        Dictionary<uint, SignalOrderLinking>.iterator itSignalLink;

                        itFollowerLink = m_mapFollowerTransDetail.find(transID);
                        if (itFollowerLink != m_mapFollowerTransDetail.end())
                        {
                            itSignalLink = m_mapSignalTransDetail.find(itFollowerLink.second._signal_transID);
                            if (itSignalLink != m_mapSignalTransDetail.end())
                            {
                                /*TempSocialRecord ptr = (TempSocialRecord)new TempSocialRecord();
                                //memset(ptr, 0, sizeof(TempSocialRecord));
                                ptr.isSSP = itSignalLink.second._signal_IsSSP;
                                ptr.isTraderServerDemo = itFollowerLink.second._isFollowerDemo;
                                ptr.signalIndex = itSignalLink.second._signal_index;
                                ptr.signalMasterLogin = itSignalLink.second._signal_masterAcc;
                                ptr.signalMT4Login = itSignalLink.second._signal_mt4Acc;
                                ptr.signalTransid = itSignalLink.second._signal_transID;
                                ptr.traderMasterLogin = itFollowerLink.second._follower_masterAcc;
                                ptr.traderMT4Login = itFollowerLink.second._follower_mt4Acc;
                                ptr.traderOrderid = orderID;
                                ptr.transid = transID; */
                                int sigOrdId = -1;
                                it4 = m_mapTransIDOrderID.find(itSignalLink.second._signal_transID);
                                if (it4 != m_mapTransIDOrderID.end())
                                {
                                    sigOrdId = it4.second.first();
                                }
                                //insertTempSocialTrade(ptr);
                                /////////////////////////////////////////////////////////////////
                                if (!itFollowerLink.second._isFollowerDemo)
                                {
                                    insertRebateData(itSignalLink.second._signal_index, itSignalLink.second._signal_mt4Acc, orderID, itFollowerLink.second._followerVolume, itSignalLink.second._signal_IsSSP, itSignalLink.second._sspMt4AccForSM, sigOrdId);
                                }
                                /////////////////////////////////////////////////////////////////

                            }
                        }
                        //else
                        //{
                        //	/*itSignalLink = m_mapSignalTransDetail.find(transID);
                        //	if (itSignalLink != m_mapSignalTransDetail.end())
                        //	{
                        //		TempSocialRecord ptr = (TempSocialRecord)new TempSocialRecord();
                        //		//memset(ptr, 0, sizeof(TempSocialRecord));
                        //		ptr.isSSP = itSignalLink.second._signal_IsSSP;
                        //		ptr.isTraderServerDemo = true;
                        //		ptr.signalIndex = itSignalLink.second._signal_index;
                        //		ptr.signalMasterLogin = itSignalLink.second._signal_masterAcc;
                        //		ptr.signalMT4Login = itSignalLink.second._signal_mt4Acc;
                        //		ptr.signalTransid = transID;
                        //		ptr.traderOrderid = orderID;
                        //		ptr.signalOrderID = orderID;
                        //		ptr.transid = transID;
                        //		insertTempSocialTrade(ptr);
                        //	} */
                        //}
                        ///////////////////////////////////////////////////////////////////

                    }
                    else
                    {
                        m_ptrLogger.LogError("Duplicate Trans ID: %u for order %d in m_mapOrderIDTransID", transID, orderID);
                    }
                }
                else
                {
                    m_ptrLogger.LogError("Trans ID: %u  for order %d does not exist in m_mapTransIDOrderID", transID, orderID);
                }

            }
        }
        //=========================================================================================================================================================//
        void removeTransLinking(uint transID)
        {
            m_ptrLogger.LogOk("TransID: %u", transID);
            lock (m_SyncOrderLinking)
            {

                Dictionary<uint, List<uint>>.iterator it1;
                List<uint>.iterator it2;
                Dictionary<uint, ValueTuple<int, bool>>.iterator it4;
                Dictionary<bool, Dictionary<int, uint>>.iterator it5;
                Dictionary<int, uint>.iterator it6;

                it1 = m_mapTransLinking.find(transID);

                if (it1 != m_mapTransLinking.end())
                {
                    if (it1.second.size() == 0)
                    {
                        removeSignalOrderLinking(transID);
                        m_mapTransLinking.erase(it1);
                    }
                }

                List<uint> vecSignalTransID = new List<uint>();
                vecSignalTransID.Clear();
                for (it1 = m_mapTransLinking.begin(); it1 != m_mapTransLinking.end(); it1++)
                {

                    it2 = find(it1.second.begin(), it1.second.end(), transID);
                    if (it2 != it1.second.end())
                    {
                        removeFollowerOrderLinking(transID);
                        it1.second.erase(it2);
                    }
                    if (it1.second.size() == 0)
                    {
                        removeSignalOrderLinking(it1.first);
                        vecSignalTransID.push_back(it1.first);
                    }
                }
                for (it2 = vecSignalTransID.begin(); it2 != vecSignalTransID.end(); it2++)
                {
                    it1 = m_mapTransLinking.find(it2);
                    if (it1 != m_mapTransLinking.end())
                    {
                        m_mapTransLinking.erase(it1);
                    }
                }

                it4 = m_mapTransIDOrderID.find(transID);
                if (it4 != m_mapTransIDOrderID.end())
                {
                    bool isDemo = it4.second.second();
                    int orderID = it4.second.first();

                    m_mapTransIDOrderID.erase(it4);

                    it5 = m_mapOrderIDTransID.find(isDemo);

                    if (it5 != m_mapOrderIDTransID.end())
                    {
                        it6 = it5.second.find(orderID);
                        if (it6 != it5.second.end())
                        {
                            it5.second.erase(it6);
                        }
                    }
                }

            }
        }
        //=========================================================================================================================================================//
        void insertFollowerOrderLinking(uint signal_transID, int follower_mt4Acc, int follower_masterAcc, bool isFollowerDemo, int followerTradeVol, uint follower_transID)
        {
            lock (m_SyncOrderLinking)
            {

                Dictionary<uint, FollowerOrderLinking>.iterator it3;

                it3 = m_mapFollowerTransDetail.find(follower_transID);
                if (it3 == m_mapFollowerTransDetail.end())
                {
                    FollowerOrderLinking ptrLink = (FollowerOrderLinking)new FollowerOrderLinking();
                    //memset(ptrLink, 0, sizeof(FollowerOrderLinking));
                    ptrLink._signal_transID = signal_transID;
                    ptrLink._follower_mt4Acc = follower_mt4Acc;
                    ptrLink._follower_masterAcc = follower_masterAcc;
                    ptrLink._isFollowerDemo = isFollowerDemo;
                    ptrLink._follower_transID = follower_transID;
                    ptrLink._followerVolume = followerTradeVol;
                    m_mapFollowerTransDetail.insert(new ValueTuple<uint, FollowerOrderLinking>(follower_transID, ptrLink));
                }

            }
        }
        //=========================================================================================================================================================//
        void insertSignalOrderLinking(int signal_index, int signal_mt4Acc, int signal_masterAcc, bool signal_IsSSP, uint signal_transID, int sspAccForSM)
        {
            lock (m_SyncOrderLinking)
            {

                Dictionary<uint, SignalOrderLinking>.iterator it3;
                //Dictionary<uint, int>.iterator it4;

                it3 = m_mapSignalTransDetail.find(signal_transID);
                if (it3 == m_mapSignalTransDetail.end())
                {
                    SignalOrderLinking ptrLink = (SignalOrderLinking)new SignalOrderLinking();
                    //memset(ptrLink, 0, sizeof(SignalOrderLinking));
                    ptrLink._signal_index = signal_index;
                    ptrLink._signal_mt4Acc = signal_mt4Acc;
                    ptrLink._signal_masterAcc = signal_masterAcc;
                    ptrLink._signal_IsSSP = signal_IsSSP;
                    ptrLink._signal_transID = signal_transID;
                    ptrLink._sspMt4AccForSM = sspAccForSM;
                    m_mapSignalTransDetail.insert(new ValueTuple<uint, SignalOrderLinking>(signal_transID, ptrLink));
                }
                else
                {
                    m_ptrLogger.LogWarning("Signal Trans ID %u already exist for MasterAcc: %d MT4Acc: %d SignalIndex: %d",
                                       signal_transID, signal_masterAcc, signal_mt4Acc, signal_index);
                }

            }
        }
        //=========================================================================================================================================================//
        void removeFollowerOrderLinking(uint follower_transID)
        {
            Dictionary<uint, FollowerOrderLinking>.iterator it3;

            it3 = m_mapFollowerTransDetail.find(follower_transID);
            if (it3 != m_mapFollowerTransDetail.end())
            {
                free(it3.second);
                it3.second = null;
                m_mapFollowerTransDetail.erase(it3);
            }//if (it3 != m_mapFollowerTransDetail.end())
        }
        //=========================================================================================================================================================//
        //HIGH ALERT : DON'T USE IT ANYWHERE THIS FUNCTION CAN ONLY USED BY removeFollowerOrderLinking//
        void removeSignalOrderLinking(uint signal_transID)
        {
            Dictionary<uint, SignalOrderLinking>.iterator it3;

            it3 = m_mapSignalTransDetail.find(signal_transID);
            if (it3 != m_mapSignalTransDetail.end())
            {
                free(it3.second);
                it3.second = null;
                m_mapSignalTransDetail.erase(it3);
            }//if (it3 != m_mapFollowerTransDetail.end())
        }
        //=========================================================================================================================================================//
        void getLinkedOrderID(List<int> vecFollowerOrderID, List<uint> vecFollowerTransID,
                                      List<int> vecFollowerMT4Login, List<bool> vecSrvType, ref bool isOrderInDemoSrv,
                                      ref int outMT4Login, ref uint outTransID, ref bool isThisSignalOrder, int inOrderID)
        {
            Dictionary<bool, Dictionary<int, uint>>.iterator it5;
            Dictionary<int, uint>.iterator it6;
            Dictionary<uint, List<uint>>.iterator it7;
            List<uint>.iterator it8;
            Dictionary<uint, ValueTuple<int, bool>>.iterator it9;
            Dictionary<uint, SignalOrderLinking>.iterator it10;
            Dictionary<uint, FollowerOrderLinking>.iterator it11;
            isOrderInDemoSrv = false;
            vecFollowerOrderID.Clear();
            vecFollowerTransID.Clear();
            vecSrvType.Clear();
            isThisSignalOrder = false;
            outMT4Login = 0; outTransID = 0;

            lock (m_SyncOrderLinking)
            {


                //Checking in demo server
                it5 = m_mapOrderIDTransID.find(true);
                if (it5 != m_mapOrderIDTransID.end())
                {
                    it6 = it5.second.find(inOrderID);
                    if (it6 != it5.second.end())
                    {
                        outTransID = it6.second;
                        isOrderInDemoSrv = true;
                    }
                }

                //Checking in live server
                if (!isOrderInDemoSrv)
                {
                    it5 = m_mapOrderIDTransID.find(false);
                    if (it5 != m_mapOrderIDTransID.end())
                    {
                        it6 = it5.second.find(inOrderID);
                        if (it6 != it5.second.end())
                        {
                            outTransID = it6.second;
                            isOrderInDemoSrv = false;
                        }
                    }
                }

                //if (isOrderInDemoSrv)
                //{
                it10 = m_mapSignalTransDetail.find(outTransID);
                if (it10 == m_mapSignalTransDetail.end())
                {
                    //It is in follower list
                    it11 = m_mapFollowerTransDetail.find(outTransID);
                    if (it11 != m_mapFollowerTransDetail.end())
                    {
                        isThisSignalOrder = false;
                        outMT4Login = it11.second._follower_mt4Acc;
                    }
                    else
                    {
                        //error
                    }
                }
                else
                {
                    isThisSignalOrder = true;
                    outMT4Login = it10.second._signal_mt4Acc;
                }
                //}


                //if (isThisSignalOrder && isOrderInDemoSrv)
                //{
                it7 = m_mapTransLinking.find(outTransID);

                if (it7 != m_mapTransLinking.end())
                {
                    for (it8 = it7.second.begin(); it8 != it7.second.end(); it8++)
                    {
                        it9 = m_mapTransIDOrderID.find(it8);
                        if (it9 != m_mapTransIDOrderID.end())
                        {
                            vecFollowerOrderID.push_back(it9.second.first());
                            vecFollowerTransID.push_back(it9.first);
                            it11 = m_mapFollowerTransDetail.find(it9.first);
                            vecSrvType.push_back(it9.second.second());
                            if (it11 != m_mapFollowerTransDetail.end())
                            {
                                vecFollowerMT4Login.push_back(it11.second._follower_mt4Acc);
                            }
                        }
                    }
                }
                //}

            }
        }

        ////=================================================================================================

        private static T GetMessageObject<T>(MessageTypeID msgType) where T : new()
        {
            var header = new MessageHeader();
            //header._structSize = MsgLenInBytes;
            //header._messageType = msgType;
            //header._splitPacketSeq = 1;
            //header._splitCount = 1;

            var message = new T();
            ((dynamic)message)._header = header;
            return message;
        }

        private static T GetFileMessageObject<T>(MessageTypeID msgType) where T : GenericFileDataResponse, new()
        {
            var header = new FileMessageHeader();
            //header._structSize = MsgLenInBytes;
            header._messageType = msgType;

            var message = new T();
            message._header = header;
            return message;
        }

        void SentDataUsingLoginID(object msg, MessageTypeID msgType, int loginID)
        {
            m_connectionMgr.SentDataUsingLoginID(msg, msgType, loginID);
        }

        //void SentDataToAll(object msg, MessageTypeID msgType)
        //{
        //    m_connectionMgr.SentDataToAll(msg, msgType);
        //}

        //void SentDataUsingSocketID(object msg, MessageTypeID msgType, uint socketID)
        //{
        //    m_connectionMgr.SentDataUsingSocketID(msg, msgType, socketID);
        //}

        public eReturnCode handleOrderRequest(SocialOrderRequest ptr)
        {
            var senderLoginID = ptr._header._loginID;
            Debug.Assert(senderLoginID > 0);

            switch (ptr._orderReq._orderTransMode)
            {
                case ORD_TRANS_CLOSE:
                    {
                        m_ptrLogger.LogInfo("Close Order Request Recv Login: %d MT4Login: %d OrderID: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return handleCloseOrderRequest(ptr);
                    }
                case ORD_TRANS_DELETE:
                    {
                        m_ptrLogger.LogInfo("Delete Order Request Recv Login: %d MT4Login: %d OrderID: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return handleDeleteOrderRequest(ptr);
                    }
                case ORD_TRANS_MODIFY:
                    {
                        m_ptrLogger.LogInfo("Modify Order Request Recv Login: %d MT4Login: %d OrderID: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return handleModifyOrderRequest(ptr);
                    }
                case ORD_TRANS_OPEN:
                    {
                        m_ptrLogger.LogInfo("Open Order Request Recv Login: %d MT4Login: %d Symbol: %s OrdType: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._symbol, ptr._orderReq._orderType);
                        return handleOpenOrderRequest(ptr);
                    }
                default:
                    {
                        //Invalid Trans Mode
                        //var ptrResp = GetMessageObject< SocialOrderResponse>(MT_SocialOrderResponse_ID);
                        //ptrResp._header._loginID = ptr._orderReq._masterLogin;
                        //ptrResp._header._MT4loginID = ptr._orderReq._mt4Login;
                        ////ptrResp._header._socketID = ptr._header._socketID;
                        //ptrResp._clientReqID = ptr._clientReqID;
                        //ptrResp._serverTransID = 0;
                        //ptrResp._retCode = RC_INVALID_ORDER_TRANS_MODE;
                        //SentDataUsingSocketID(ptrResp, MT_SocialOrderResponse_ID, ptrResp->_header._socketID);
                        return RC_INVALID_ORDER_TRANS_MODE;
                    }
            }
        }
    }
}
