using WitFX.Backend.Extensions;
using WitFX.Backend.Infrastructure.Extensions;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Backend.Services;
using WitFX.MT4;
using WitFX.MT4Server.Extensions;
using WitFX.MT4Server.Models;
using WitFX.MT4Server.Services;
using WitFXTCPStructDotNet.cls;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using static WitFX.Backend.Helpers.DatabaseHelper;
using Cpp = WitFX.MT4Server.Implementation.Collections;
using MyTradeRecord = WitFX.MT4.TradeRecord;

namespace WitFX.MT4Server.Implementation
{
    public sealed class MYSQLWrapper : IDisposable
    {
        private readonly DatabaseService _database;
        private readonly FollowerService _followerService;
        private readonly LogService _logService;
        private readonly MasterUserSettingService _masterUserSettingService;
        private readonly MasterUserService _masterUserService;
        private readonly MT4AccountService _mt4AccountService;
        private readonly MT4SymbolInfoService _mt4SymbolInfoService;
        private readonly ProductService _productService;
        private readonly RankingService _rankingService;
        private readonly RebateService _rebateService;
        private readonly SignalService _signalService;
        private readonly SignalSymbolSettingService _signalSymbolSettingService;
        private readonly TradeDisableService _tradeDisableService;
        private readonly TradeRecordService _tradeRecordService;
        private readonly ILogger _logger;

        public MYSQLWrapper(
            DatabaseService database, FollowerService followerService, LogService logService,
            MasterUserSettingService masterUserSettingService, MasterUserService masterUserService,
            MT4AccountService mt4AccountService,
            MT4SymbolInfoService mt4SymbolInfoService,
            ProductService productService, RankingService rankingService, RebateService rebateService,
            SignalService signalService, SignalSymbolSettingService sygnalSymbolSettingService,
            TradeDisableService tradeDisableService, TradeRecordService tradeRecordService,
            ILogger logger)
        {
            _database = database;
            _followerService = followerService;
            _logService = logService;
            _masterUserService = masterUserService;
            _masterUserSettingService = masterUserSettingService;
            _mt4AccountService = mt4AccountService;
            _mt4SymbolInfoService = mt4SymbolInfoService;
            _productService = productService;
            _rankingService = rankingService;
            _rebateService = rebateService;
            _signalService = signalService;
            _signalSymbolSettingService = sygnalSymbolSettingService;
            _tradeDisableService = tradeDisableService;
            _tradeRecordService = tradeRecordService;
            _logger = logger;
        }

        public void Dispose()
        {
        }

        #region Database

        private T? ExecuteValueOrNull<T>(string query, Func<MySqlDataReader, T> factory)
            where T : struct
            => _database.ExecuteValueOrNullAsync(query, factory, CancellationToken.None).Result;

        private T ExecuteRecordOrNull<T>(string query, Func<MySqlDataReader, T> factory)
            where T : class
            => _database.ExecuteRecordOrNullAsync(query, factory, CancellationToken.None).Result;

        private T ExecuteRecordRequred<T>(string query, Func<MySqlDataReader, T> factory)
            => _database.ExecuteRecordRequredAsync(query, factory, CancellationToken.None).Result;

        private IReadOnlyList<T> ExecuteRecords<T>(string query, Func<MySqlDataReader, T> factory)
            => _database.ExecuteRecordsAsync(query, factory, CancellationToken.None).Result;

        private void ExecuteEachRecords(string query, Action<MySqlDataReader> iterator)
            => _database.ExecuteEachRecordsAsync(query, iterator, CancellationToken.None).Wait();

        private void ExecuteNonQuery(string query)
            => _database.ExecuteNonQueryAsync(query, CancellationToken.None).Wait();

        #endregion

        #region MasterUserSetting

        public bool updateMasterSetting_MaxSMAccount(double value) { throw new NotImplementedException(); }
        public bool updateMasterSetting_MaxSSPAccount(double value) { throw new NotImplementedException(); }
        public bool updateMasterSetting_MaxSSPInSM(double value) { throw new NotImplementedException(); }
        public bool updateMasterSetting_SignalTradeVolume(double value) { throw new NotImplementedException(); }
        public bool updateMasterSetting_SSPMaxOpenOrders(double value) { throw new NotImplementedException(); }
        public void updateMasterSetting_UID(uint value)
        => _masterUserSettingService.UpdateMasterSetting_UID(value, CancellationToken.None).Wait();

        public bool updateMasterSetting_DemoGroupName(string value) { throw new NotImplementedException(); }
        public bool updateMasterSetting_LiveGroupName(string value) { throw new NotImplementedException(); }
        public void updateMasterSetting(MasterUserSetting ptrSetting)
           => _masterUserSettingService.UpdateMasterSetting(ptrSetting, CancellationToken.None).Wait();

        public MasterUserSetting getMasterSetting()
            => _masterUserSettingService.GetMasterSetting(CancellationToken.None).Result;

        #endregion

        #region MasterUser

        public void insertMasterAccount(MasterUser ptrUsr)
            => _masterUserService.InsertMasterAccount(ptrUsr, CancellationToken.None).Wait();

        public void updateMasterAccount(MasterUser ptrUsr)
            => _masterUserService.UpdateMasterAccount(ptrUsr, CancellationToken.None).Wait();

        public void changeMasterAccountPassword(string alias, string oldPassword,
            string newPassword)
        => _masterUserService.ChangeMasterAccountPassword(
            alias, oldPassword, newPassword, CancellationToken.None).Wait();

        public void resetMasterAccountPassword(int masterLogin, string newPassword)
        => _masterUserService.ResetMasterAccountPassword(
            masterLogin, newPassword, CancellationToken.None).Wait();

        public void updateLastDateTimeInMasterAcc(int masterLogin)
         => _masterUserService.UpdateLastDateTimeInMasterAcc(masterLogin,
             CancellationToken.None).Wait();

        public MasterAccountPreview isMasterAccountPwdOk(int masterLogin, string pwd)
        => _masterUserService.IsMasterAccountPwdOk(masterLogin, pwd, CancellationToken.None).Result;

        public MasterAccountPreview isMasterAccountPwdOk(string loginalias, string pwd)
            => _masterUserService.IsMasterAccountPwdOk(loginalias, pwd, CancellationToken.None).Result;

        public MasterUser getMasterAccount(int masterLogin)
            => _masterUserService.GetMasterAccount(masterLogin, CancellationToken.None).Result;

        public IReadOnlyList<MasterUser> getAllMasterAccounts()
            => _masterUserService.GetAllMasterAccounts(CancellationToken.None).Result;

        public IReadOnlyList<MasterUser> getAllUpdatedMasterAccounts(DateTimeOffset? updateTime)
            => _masterUserService.GetAllUpdatedMasterAccounts(updateTime, CancellationToken.None).Result;

        #endregion

        #region MTAccount

        public bool insertMT4Account(MT4Account ptr)
        {
            _mt4AccountService.InsertMT4Account(ptr, CancellationToken.None).Wait();
            return true;
        }

        public void deleteMT4Account(MT4Account ptr)
        => _mt4AccountService.DeleteMT4Account(ptr, CancellationToken.None).Wait();

        public IReadOnlyList<MT4Account> getAllMT4Accounts()
         => _mt4AccountService.GetAllMT4Accounts(CancellationToken.None).Result;

        public void setAllMT4AccountsInMap(Cpp.Dictionary<bool, Cpp.Dictionary<int, int>> mp, Cpp.Dictionary<int, int> mpMasterRebate)
        {
            ExecuteEachRecords(
                   "SELECT * FROM  `mt4account` ",
                   i =>
                   {
                       var masterLogin = i.GetInt32("masterlogin");
                       var mt4Login = i.GetInt32("mt4login");

                       if (i.GetInt32("accounttype") == (int)eAccountType.ACC_TYPE_REBATE)
                       {
                           Debug.Assert(!mpMasterRebate.ContainsKey(masterLogin));
                           mpMasterRebate.Add(masterLogin, mt4Login);
                       }

                       bool isDemoServer = i.GetInt32("mt4serverindex") == (int)eMT4ServerType.SRV_TYPE_DEMO ? true : false;
                       if (!mp.TryGetValue(isDemoServer, out Cpp.Dictionary<int, int> mp2))
                       {
                           mp.Add(isDemoServer, mp2 = new Cpp.Dictionary<int, int>());
                       }
                       Debug.Assert(!mp2.ContainsKey(mt4Login));
                       mp2.Add(mt4Login, masterLogin);

                   });

        }

        public IReadOnlyList<MT4Account> getAllMT4AccountsForMasterLogin(int masterLogin, DateTimeOffset? updateTime = null)
         => _mt4AccountService.GetAllMT4AccountsForMasterLogin(masterLogin, updateTime, CancellationToken.None).Result;

        public IReadOnlyList<MT4Account> getAllSMAccounts()
       => _mt4AccountService.GetAllSMAccounts(CancellationToken.None).Result;

        public IReadOnlyList<MT4Account> getAllSSPAccounts()
        => _mt4AccountService.GetAllSSPAccounts(CancellationToken.None).Result;

        public IReadOnlyList<MT4Account> getAllFollowerDemoAccounts()
        => _mt4AccountService.GetAllFollowerDemoAccounts(CancellationToken.None).Result;

        public IReadOnlyList<MT4Account> getAllFollowerLiveAccounts()
        => _mt4AccountService.GetAllFollowerLiveAccounts(CancellationToken.None).Result;

        public IReadOnlyList<MT4Account> getAllRebateAccounts()
        => _mt4AccountService.GetAllRebateAccounts(CancellationToken.None).Result;

        #endregion

        #region MT4SymbolInfo

        public void insertMT4Symbol(MT4SymbolInfo ptr)
        => _mt4SymbolInfoService.InsertMT4Symbol(ptr, CancellationToken.None).Wait();

        public void insertUpdateMT4Symbol(MT4SymbolInfo ptr)
        => _mt4SymbolInfoService.InsertUpdateMT4Symbol(ptr, CancellationToken.None).Wait();

        public IReadOnlyList<MT4SymbolInfo> getAllMT4Symbols()
        => _mt4SymbolInfoService.GetAllMT4Symbols(CancellationToken.None).Result;

        public void setSymbolMap(Cpp.Dictionary<int, string> mp, Cpp.Dictionary<string, int> mp2)
        {
            ExecuteEachRecords(
                  "SELECT * FROM  `mt4symbolinfo` ",
                  i =>
                  {
                      mp.Add(i.GetInt32("symbolIndex"), i.GetStringOrNull("symbol"));
                      mp2.Add(i.GetStringOrNull("symbol"), i.GetInt32("symbolIndex"));
                  });
        }

        public string getSymbolName(int symbolIndex)
        => _mt4SymbolInfoService.GetSymbolName(symbolIndex, CancellationToken.None).Result;

        #endregion

        #region SignalSymbolSetting

        public void insertSignalSymbolSetting(SignalSymbolSetting ptr)
       => _signalSymbolSettingService.InsertSignalSymbolSetting(
           ptr, CancellationToken.None).Wait();

        public void deleteSignalSymbolSetting(SignalSymbolSetting ptr)
       => _signalSymbolSettingService.DeleteSignalSymbolSetting(
           ptr, CancellationToken.None).Wait();

        public SignalSymbolSetting getSignalSymbolBlockTime(int symbolIndex, eStrategyType strategyType)
            => _signalSymbolSettingService.GetSignalSymbolBlockTime(
                symbolIndex, strategyType, CancellationToken.None).Result;

        public IReadOnlyList<SignalSymbolSetting> getAllSignalSymbolSettings()
            => _signalSymbolSettingService.GetAllSignalSymbolSettings(CancellationToken.None).Result;

        #endregion

        #region Signal

        public void insertSignal(Signal ptr)
                => _signalService.InsertSignal(ptr, CancellationToken.None).Wait();

        public void updateSignalMT4Login(uint uid, int mt4login, bool isRemove = false)
        => _signalService.UpdateSignalMT4Login(
            uid, mt4login, isRemove, CancellationToken.None).Wait();

        public bool isSignalNameExist(string signalName)
        => _signalService.IsSignalNameExist(
            signalName, CancellationToken.None).Result;

        public IReadOnlyList<Signal> getAllSignals(int masterLogin = -1, DateTimeOffset? updateTime = null)
               => _signalService.GetAllSignals(masterLogin, updateTime, CancellationToken.None).Result;

        public Signal getSignal(int signalIndex)
                => _signalService.GetSignal(signalIndex, CancellationToken.None).Result;

        #endregion

        #region SMSignal

        public void insertSMSignal(SMSignal ptr)
        => _signalService.InsertSMSignal(ptr, CancellationToken.None).Wait();

        public void updateSMSignalMT4Login(uint uid, int mt4login, bool isRemove = false)
         => _signalService.UpdateSMSignalMT4Login(
            uid, mt4login, isRemove, CancellationToken.None).Wait();

        public IReadOnlyList<SMSignal> getAllSMSignals(int masterLogin = -1, DateTimeOffset? updateTime = null)
         => _signalService.GetAllSMSignals(masterLogin, updateTime, CancellationToken.None).Result;

        public SMSignal getSMSignalAllDetails(int signalIndex)
        => _signalService.GetSMSignalAllDetails(signalIndex, CancellationToken.None).Result;

        public int getSMSignalCount(int masterLogin)
        => _signalService.GetSMSignalCount(masterLogin, CancellationToken.None).Result;

        public int getSSPCountForSMSignal(int masterLogin, int signalIndex)
         => _signalService.GetSSPCountForSMSignal(masterLogin, signalIndex, CancellationToken.None).Result;

        public void deleteSSPinSM(SMSignal ptr)
             => _signalService.DeleteSSPinSM(ptr, CancellationToken.None).Wait();

        #endregion

        #region SSPSignal

        public void insertSSPSignal(SSPSignal ptr)
                => _signalService.InsertSSPSignal(ptr, CancellationToken.None).Wait();

        public void updateSSPSignalMT4Login(uint uid, int mt4login, bool isRemove = false)
        => _signalService.UpdateSSPSignalMT4Login(
            uid, mt4login, isRemove, CancellationToken.None).Wait();

        public IReadOnlyList<SSPSignal> getAllSSPSignals(int masterLogin = -1, DateTimeOffset? updateTime = null)
        => _signalService.GetAllSSPSignals(masterLogin, updateTime, CancellationToken.None).Result;

        public SSPSignal getSSPSignal(int signalIndex)
                => _signalService.GetSSPSignal(signalIndex, CancellationToken.None).Result;

        public void getSSPSignal(int mt4account, out int symbolIndex, out eStrategyType strategyType)
        {
            var result = _signalService.GetSSPSignalShort(mt4account, CancellationToken.None).Result;

            if (result != null)
            {
                symbolIndex = result.Value.symbolIndex;
                strategyType = result.Value.strategyType;
            }
            else
            {
                symbolIndex = 0;
                strategyType = (eStrategyType)0;
            }
        }

        public int getSSPSignalCount(int masterLogin)
        => _signalService.GetSSPSignalCount(masterLogin, CancellationToken.None).Result;

        #endregion

        #region SMFollower / SMFollowerUser

        public void insertSMFollower(SMFollower ptr)
            => _followerService.InsertSMFollower(ptr, CancellationToken.None).Wait();

        public void modifySMFollower(SMFollower ptr)
             => _followerService.ModifySMFollower(ptr, CancellationToken.None).Wait();

        public void deleteSMFollower(SMFollower ptr)
             => _followerService.DeleteSMFollower(ptr, CancellationToken.None).Wait();

        public IReadOnlyList<SMFollower> getSMSubscribedSignals(int masterLogin = -1, DateTimeOffset? updateTime = null)
            => _followerService.GetSMSubscribedSignals(masterLogin, updateTime, CancellationToken.None).Result;

        public IReadOnlyList<SMFollower> getAllSMFollowers()
             => _followerService.GetAllSMFollowers(CancellationToken.None).Result;

        public IReadOnlyList<SMFollower> getAllSMFollowers(int smMT4Login, eMT4ServerType smMT4ServerIndex)
            => _followerService.GetAllSMFollowers(smMT4Login, smMT4ServerIndex, CancellationToken.None).Result;

        public IReadOnlyList<SMFollowerUser> getAllSMFollowerUsers(int smMT4Login, int followermasterlogin)
             => _followerService.GetAllSMFollowerUsers(smMT4Login, followermasterlogin, CancellationToken.None).Result;

        public int getAllSMFollowersCount(int smMT4Login, int smMT4ServerIndex)
            => _followerService.GetAllSMFollowersCount(smMT4Login, smMT4ServerIndex, CancellationToken.None).Result;

        #endregion

        #region SSPFollower / SSPFollowerUser

        public void insertSSPFollower(SSPFollower ptr)
             => _followerService.InsertSSPFollower(ptr, CancellationToken.None).Wait();

        public void deleteSSPFollower(SSPFollower ptr)
             => _followerService.DeleteSSPFollower(ptr, CancellationToken.None).Wait();

        public void modifySSPFollower(SSPFollower ptr)
             => _followerService.ModifySSPFollower(ptr, CancellationToken.None).Wait();

        public IReadOnlyList<SSPFollower> getSSPSubscribedSignals(int masterLogin = -1, DateTimeOffset? updateTime = null)
        => _followerService.GetSSPSubscribedSignals(masterLogin, updateTime, CancellationToken.None).Result;

        public IReadOnlyList<SSPFollower> getAllSSPFollowers()
         => _followerService.GetAllSSPFollowers(CancellationToken.None).Result;

        public IReadOnlyList<SSPFollower> getAllSSPFollowers(int sspMT4Login, eMT4ServerType sspMT4ServerIndex)
         => _followerService.GetAllSSPFollowers(sspMT4Login, sspMT4ServerIndex, CancellationToken.None).Result;

        public IReadOnlyList<SSPFollowerUser> getAllSSPFollowerUsers(int sspMT4Login, int followermasterlogin)
         => _followerService.GetAllSSPFollowerUsers(sspMT4Login, followermasterlogin, CancellationToken.None).Result;

        public int getAllSSPFollowersCount(int sspMT4Login, int sspMT4ServerIndex)
        => _followerService.GetAllSSPFollowersCount(sspMT4Login, sspMT4ServerIndex, CancellationToken.None).Result;

        #endregion

        #region TradeDisable

        public void insertTradeDisableRow(int masterLogin, int mt4Acc, eMT4ServerType mt4ServerIndex,
            int accountType, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            try
            {
                _tradeDisableService.InsertTradeDisableRow(masterLogin, mt4Acc, mt4ServerIndex,
                accountType, startTime, endTime, CancellationToken.None).Wait();
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
            }
        }

        public void deleteTradeDisableRow(int masterLogin, int mt4Acc, int mt4ServerIndex)
        => _tradeDisableService.DeleteTradeDisableRow(masterLogin, mt4Acc, mt4ServerIndex,
             CancellationToken.None).Wait();

        public bool isTradingDisable(int masterLogin, int mt4Acc, eMT4ServerType mt4ServerIndex,
            DateTimeOffset mt4Time)
        => _tradeDisableService.IsTradingDisable(
            masterLogin, mt4Acc, mt4ServerIndex, mt4Time, CancellationToken.None).Result;

        public IReadOnlyList<MT4TradeDisableInfo> getAllTradeDisableInfos(int masterLogin,
            DateTimeOffset? updateTime = null)
         => _tradeDisableService.GetAllTradeDisableInfos(masterLogin, updateTime,
             CancellationToken.None).Result;

        public IReadOnlyList<MT4TradeDisableInfo> getAllTradeDisableInfo()
        => _tradeDisableService.GetAllTradeDisableInfo(CancellationToken.None).Result;

        #endregion

        #region Log

        public void insertLog(eLogType logType, string msg)
        => _logService.InsertLog(logType, msg, CancellationToken.None).Wait();

        #endregion

        #region Mirroring

        public void insertFollowerOrderLinking(
            uint signalTransID, uint followerTransID, int followerMT4Acc,
            int followerMasterAcc, int followerTradeVol, bool isFollowerDemo)
        {
            ExecuteNonQuery(
                "INSERT INTO  `followerorderlinking` ( `signal_transid`, " +
                "`follower_transid`,`follower_mt4acc`, `follower_masteracc`,`isfollowerdemo`," +
                "`follower_volume` ) VALUES( " +
                signalTransID + "," +
                followerTransID + "," +
                followerMT4Acc + "," +
                followerMasterAcc + "," +
                isFollowerDemo + "," +
                followerTradeVol + " );");
        }

        public void insertSignalOrderLinking(
            uint signalTransID, int signalIndex, int signalMT4Acc, int signalMasterAcc,
            bool isSSP, int sspMt4AccForSM)
        {
            ExecuteNonQuery(
                "INSERT INTO  `signalorderlinking` ( `signal_transid`, " +
                "`signal_index`,`signal_mt4acc`, `signal_masteracc`,`signal_isssp`," +
                "`sspmt4accforsm` ) VALUES( " +
                signalTransID + "," +
                signalIndex + "," +
                signalMT4Acc + "," +
                signalMasterAcc + "," +
                isSSP + "," +
                sspMt4AccForSM + " );");
        }

        public void loadFollowerOrderLinking(
            Cpp.List<uint> signalTransID, Cpp.List<uint> followerTransID, Cpp.List<int> followerMT4Acc,
            Cpp.List<int> followerMasterAcc, Cpp.List<bool> isFollowerDemo, Cpp.List<int> followerVolume)
        {
            ExecuteEachRecords(
                   "SELECT * FROM  `followerorderlinking` ",
                   i =>
                   {
                       signalTransID.Add((uint)i.GetInt32("signal_transid"));
                       followerTransID.Add((uint)i.GetInt32("follower_transid"));
                       followerMT4Acc.Add(i.GetInt32("follower_mt4acc"));
                       followerMasterAcc.Add(i.GetInt32("follower_masteracc"));
                       isFollowerDemo.Add(i.GetBoolean("isfollowerdemo"));
                       followerVolume.Add(i.GetInt32("follower_volume"));
                   });
        }

        public void loadSignalOrderLinking(
            Cpp.List<uint> signalTransID, Cpp.List<int> signalIndex, Cpp.List<int> signalMT4Acc,
            Cpp.List<int> signalMasterAcc, Cpp.List<bool> isSSP, Cpp.List<int> sspMt4AccForSM)
        {
            ExecuteEachRecords(
                "SELECT * FROM  `signalorderlinking` ",
                reader =>
                {
                    signalTransID.Add((uint)reader.GetInt32("signal_transid"));
                    signalIndex.Add(reader.GetInt32("signal_index"));
                    signalMT4Acc.Add(reader.GetInt32("signal_mt4acc"));
                    signalMasterAcc.Add(reader.GetInt32("signal_masteracc"));
                    isSSP.Add(reader.GetBoolean("signal_isssp"));
                    sspMt4AccForSM.Add(reader.GetInt32("sspmt4accforsm"));
                });
        }

        public void insertTransLinking(Cpp.Dictionary<uint, Cpp.List<uint>> mapTransLinking)
        {
            var query = new StringBuilder(
                "INSERT INTO  `transidlinking` ( `signaltransid`, " +
                "`followertransid` ) VALUES ");

            var isFirstRow = true;

            foreach (var it1 in mapTransLinking)
                foreach (var it2 in it1.Value)
                {
                    if (isFirstRow)
                        isFirstRow = false;
                    else
                        query.Append(",");

                    query.Append($"({it1.Key},{it2})");
                }

            ExecuteNonQuery(query.ToString());
        }

        public void loadTransLinking(Cpp.Dictionary<uint, Cpp.List<uint>> mapTransLinking)
        {
            ExecuteEachRecords(
                "SELECT * FROM  `transidlinking` ",
                reader =>
                {
                    uint signalTransID = (uint)reader.GetInt32("signaltransid");
                    uint followertransid = (uint)reader.GetInt32("followertransid");

                    if (!mapTransLinking.TryGetValue(signalTransID, out var list))
                    {
                        mapTransLinking.Add(signalTransID, list = new Cpp.List<uint>());
                    }
                    list.Add(followertransid);
                });
        }

        public void insertTransIDOrderID(Cpp.Dictionary<uint, ValueTuple<int, bool>> mapTransIDOrderID)
        {
            var query = new StringBuilder(
                "INSERT INTO  `transidorderidlinking` ( `transid`, " +
                "`orderid` , `isdemoserver` ) VALUES ");

            var isFirstRow = true;

            foreach (var it1 in mapTransIDOrderID)
            {
                if (isFirstRow)
                    isFirstRow = false;
                else
                    query.Append(",");

                query.Append($"({it1.Key},{it1.Value.Item1},{SerializeBool(it1.Value.Item2)})");
            }


            ExecuteNonQuery(query.ToString());
        }

        public void loadTransIDOrderID(
            Cpp.Dictionary<uint, ValueTuple<int, bool>> mapTransIDOrderID,
            Cpp.Dictionary<bool, Cpp.Dictionary<int, uint>> mapOrderIDTransID)
        {
            ExecuteEachRecords(
                "SELECT * FROM  `transidorderidlinking` ",
                reader =>
                {
                    uint transid = (uint)reader.GetInt32("transid");
                    int orderid = reader.GetInt32("orderid");
                    bool isdemoserver = reader.GetBoolean("isdemoserver");

                    mapTransIDOrderID.Add(transid, (orderid, isdemoserver));

                    if (!mapOrderIDTransID.TryGetValue(isdemoserver, out Cpp.Dictionary<int, uint> dictionary))
                    {
                        mapOrderIDTransID.Add(isdemoserver, dictionary = new Cpp.Dictionary<int, uint>());
                    }
                    dictionary.Add(orderid, transid);
                });
        }

        public void clearTransIdOrderidLinkingTable()
        {
            ExecuteNonQuery("truncate transidorderidlinking");
        }

        public void clearTransIdLinkingTable()
        {
            ExecuteNonQuery("truncate transidlinking");
        }

        public void clearFollowerOrderLinkingTable()
        {
            ExecuteNonQuery("truncate followerorderlinking");
        }

        public void clearSignalOrderLinkingTable()
        {
            ExecuteNonQuery("truncate signalorderlinking");
        }

        #endregion

        #region SocialTradeRecord

        public void insertSocialTradeRecord(uint transid, uint signalTransid, int signalIndex,
            int signalMasterLogin, int signalMT4Login, int signalOrderID, bool isSSP,
            bool isTraderServerDemo, int traderOrderid, int traderMT4Login, int traderMasterLogin)
        => _tradeRecordService.InsertSocialTradeRecord(transid, signalTransid, signalIndex,
             signalMasterLogin, signalMT4Login, signalOrderID, isSSP,
             isTraderServerDemo, traderOrderid, traderMT4Login, traderMasterLogin,
             CancellationToken.None).Wait();

        public void updateSocialTradeRecord(TradeRecord ptr, bool isServerDemo)
        => _tradeRecordService.UpdateSocialTradeRecord(
            ptr, isServerDemo, CancellationToken.None).Wait();

        #endregion

        #region TradeRecord

        public void insertDemoCloseTrades(TradeRecord ptr)
        => _tradeRecordService.InsertDemoCloseTrades(ptr, CancellationToken.None).Wait();

        public void insertLiveCloseTrades(TradeRecord ptr)
        {
            try
            {
                _tradeRecordService.InsertLiveCloseTrades(ptr, CancellationToken.None).Wait();
            }
            catch(Exception exception)
            {
                _logger.LogException(exception);
            }
        }

        #endregion

        #region MyTradeRecord

        public IReadOnlyList<MyTradeRecord> getDemoCloseTrades(Cpp.List<int> vecLogin)
         => _tradeRecordService.GetDemoCloseTrades(vecLogin, CancellationToken.None).Result;
        public IReadOnlyList<MyTradeRecord> getDemoCloseTradesPvt(Cpp.List<int> vecLogin, DateTimeOffset from, DateTimeOffset to)
        => _tradeRecordService.GetDemoCloseTradesPvt(vecLogin, from, to, CancellationToken.None).Result;
        public IReadOnlyList<MyTradeRecord> getLiveCloseTrades(Cpp.List<int> vecLogin)
         => _tradeRecordService.GetLiveCloseTrades(vecLogin, CancellationToken.None).Result;

        #endregion

        #region Rebate

        public void insertRebateData(int signalIndex, int signalMt4Login, int signalOrderID,
            int followerOrderID, int followerVolume, bool isSSP, int sspAccForSM)
        {
            try
            {
                _rebateService.InsertRebateData(
                    signalIndex, signalMt4Login, signalOrderID,
                    followerOrderID, followerVolume, isSSP, sspAccForSM, CancellationToken.None).Wait();
            }
            catch (Exception exception)
            {
                _logger.LogException(exception);
            }
        }

        #endregion

        #region Ranking

        public void syncD1ReportData(DateTimeOffset currTime)
        {
            Cpp.List<bool> vecIsSSP = new Cpp.List<bool>();
            Cpp.List<int> vecSignalIndex = new Cpp.List<int>();
            Cpp.List<int> vecMT4Login = new Cpp.List<int>();
            Cpp.List<string> vecSignalName = new Cpp.List<string>();
            ExecuteEachRecords(
                   "SELECT * FROM  `signal` ",
                   i =>
                   {
                       vecIsSSP.Add(i.GetBoolean("isssp"));
                       vecSignalIndex.push_back(i.GetInt32("signalindex"));
                       vecMT4Login.push_back(i.GetInt32("mt4login"));
                       vecSignalName.push_back(i.GetString("signalname"));
                   });

            int arrayCnt = vecMT4Login.Count;
            int tradeCnt = -1;
            DateTimeOffset fromDate = currTime;
            DateTimeOffset toDate = currTime;

            Cpp.List<int> temp = new Cpp.List<int>();
            int rankPos = 0;
            int totalVolume = 0;
            int totalfollowervolume = 0;
            int totalclosetrades = 0;
            int totalwinningtrades = 0;
            int totallosetrades = 0;
            double totalprofitinpips = 0.0;
            double totalwinnningtradeprofitinpips = 0.0;
            double totallosetradeprofitinpips = 0.0;
            double avg_positionholdingtime = 0.0;
            double winningratio = 0.0;
            double avg_pipspertrade = 0.0;
            double avg_winningtradepips = 0.0;
            double avg_losetradepips = 0.0;
            double avg_lowpertrade = 0.0;
            double riskreward = 0.0;

            for (int iSignalLoop = 0; iSignalLoop < arrayCnt; iSignalLoop++)
            {
                temp.push_back(vecMT4Login.ElementAt(iSignalLoop));
                tradeCnt = -1;
                IReadOnlyList<MyTradeRecord> ptrTradeArr = getDemoCloseTradesPvt(temp, fromDate, toDate);
                tradeCnt = ptrTradeArr.Count;
                rankPos = 0;
                totalVolume = 0;
                totalfollowervolume = 0;
                totalclosetrades = 0;
                totalwinningtrades = 0;
                totallosetrades = 0;
                totalprofitinpips = 0.0;
                totalwinnningtradeprofitinpips = 0.0;
                totallosetradeprofitinpips = 0.0;
                avg_positionholdingtime = 0.0;

                winningratio = 0.0;
                avg_pipspertrade = 0.0;
                avg_winningtradepips = 0.0;
                avg_losetradepips = 0.0;
                avg_lowpertrade = 0.0;
                riskreward = 0.0;

                for (int iTradeLoop = 0; iTradeLoop < tradeCnt; iTradeLoop++)
                {
                    totalVolume += ptrTradeArr[iTradeLoop].volume;
                    ++totalclosetrades;
                    double profit = 0.0;
                    if (ptrTradeArr[iTradeLoop].symbol == "JPY")
                    {
                        profit += Math.Abs(ptrTradeArr[iTradeLoop].open_price - ptrTradeArr[iTradeLoop].close_price) * 100;
                    }
                    else
                        profit += Math.Abs(ptrTradeArr[iTradeLoop].open_price - ptrTradeArr[iTradeLoop].close_price) * 10000;
                    //totalprofitinpips += ptrTradeArr[iTradeLoop].profit;
                    totalprofitinpips += profit;
                    if (ptrTradeArr[iTradeLoop].profit >= 0.0)
                    {
                        ++totalwinningtrades;
                        //totalwinnningtradeprofitinpips += ptrTradeArr[iTradeLoop].profit;
                        totalwinnningtradeprofitinpips += profit;
                    }
                    else
                    {
                        ++totallosetrades;
                        //totallosetradeprofitinpips += ptrTradeArr[iTradeLoop].profit;
                        totallosetradeprofitinpips += profit;
                    }
                    avg_positionholdingtime += (ptrTradeArr[iTradeLoop].close_time - ptrTradeArr[iTradeLoop].open_time).Value.TotalSeconds;
                }
                if (totalclosetrades != 0)
                {
                    avg_positionholdingtime = avg_positionholdingtime / totalclosetrades;
                    winningratio = totalwinningtrades / totalclosetrades * 100;
                    avg_pipspertrade = (totalwinnningtradeprofitinpips + totallosetradeprofitinpips) / totalclosetrades;
                    avg_winningtradepips = totalwinnningtradeprofitinpips / totalclosetrades;
                    avg_losetradepips = totallosetradeprofitinpips / totalclosetrades;
                    if (totallosetradeprofitinpips != 0)
                        riskreward = totalprofitinpips / totallosetradeprofitinpips * 100;
                    else
                        riskreward = 0;
                }
                else
                {
                    avg_positionholdingtime = 0;
                }

                insertSignalD1data(vecSignalIndex.ElementAt(iSignalLoop),
                    vecSignalName.ElementAt(iSignalLoop),
                    vecIsSSP.ElementAt(iSignalLoop),
                    vecMT4Login.ElementAt(iSignalLoop), rankPos, totalVolume, totalfollowervolume, totalclosetrades, totalwinningtrades, totallosetrades,
                    totalprofitinpips, totalwinnningtradeprofitinpips, totallosetradeprofitinpips, avg_positionholdingtime, toDate,
                    winningratio, avg_pipspertrade, avg_winningtradepips, avg_losetradepips, avg_lowpertrade, riskreward);
            }
        }

        public void insertSignalD1data(int signalindex, string signal, bool isssp, int mt4login,
                                    int rankposition, int totalvolume, int totalfollowervolume,
                                    int totalclosetrades, int totalwinningtrades,
                                    int totallosetrades, double totalprofitinpips,
                                    double totalwinnningtradeprofitinpips,
                                    double totallosetradeprofitinpips,
                                    double avg_positionholdingtime, DateTimeOffset d1_time,
                                    double winningratio, double avg_pipspertrade,
                                    double avg_winningtradepips, double avg_losetradepips,
                                    double avg_lowpertrade, double riskreward)
            => _rankingService.InsertSignalD1data(signalindex, signal, isssp, mt4login,
                                     rankposition, totalvolume, totalfollowervolume,
                                     totalclosetrades, totalwinningtrades,
                                     totallosetrades, totalprofitinpips,
                                     totalwinnningtradeprofitinpips,
                                     totallosetradeprofitinpips,
                                     avg_positionholdingtime, d1_time,
                                     winningratio, avg_pipspertrade,
                                     avg_winningtradepips, avg_losetradepips,
                                     avg_lowpertrade, riskreward, CancellationToken.None).Wait();

        public IReadOnlyList<Ranking> readSignalD1data(int days)
            => _rankingService.ReadSignalD1data(days, CancellationToken.None).Result;


        public int getTradingDaysForRankingCalculation(int mt4login)
             => _rankingService.GetTradingDaysForRankingCalculation(mt4login, CancellationToken.None).Result;


        public RankingService.FollowerInfo getLiveSSPFollowersInfo(int sspMT4Login)
            => _rankingService.GetLiveSSPFollowersInfo(sspMT4Login, CancellationToken.None).Result;

        public RankingService.FollowerInfo getLiveSMFollowersInfo(int smMT4Login)
             => _rankingService.GetLiveSMFollowersInfo(smMT4Login, CancellationToken.None).Result;

        public double getDrawDown(int mt4login)
             => _rankingService.GetDrawDown(mt4login, CancellationToken.None).Result;

        public void updateDailyRanking()
        => _rankingService.UpdateDailyRanking(CancellationToken.None).Wait();

        public IReadOnlyList<Ranking> getDataForGraph(int signalindex)
        => _rankingService.GetDataForGraph(signalindex, CancellationToken.None).Result;

        #endregion

        #region Product

        public IReadOnlyList<Product> getAllProducts()
            => _productService.GetAllProducts(CancellationToken.None).Result;
        public IReadOnlyList<ProductDetails> getProductDetails(int productId)
            => _productService.GetProductDetails(productId, CancellationToken.None).Result;

        #endregion
    }
}
