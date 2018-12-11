using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Contracts;
using WitFX.MT4.Server.cls;
using WitFX.MT4.Server.Implementation;
using WitFX.MT4.Server.Implementation.Helpers;
using WitFX.MT4.Server.Implementation.Models;
using WitFX.MT4.Server.MT4;
using WitFX.MT4.Server.Services;
using static WitFX.MT4.Server.Implementation.Helpers.CppHelper;
using static WitFX.Contracts.eOrderStatus;
using static WitFX.Contracts.eMT4ServerType;
using static WitFX.MT4.Server.Implementation.MT4REQ;
using static WitFX.Contracts.eAccountType;
using static WitFX.Contracts.eReturnCode;
using static WitFX.Contracts.eDataTransMode;

namespace WitFX.MT4.Server.Managers
{
    public sealed class FollowingManager
    {
        private readonly ServerLogger m_ptrLogger;
        private readonly MasterUserSettingService _masterSettingsService;
        private readonly FollowerService _followerService;
        private readonly SignalService _signalService;
        private readonly MT4Manager _mt4Manager;
        private readonly MasterUserService _masterUserService;

        public FollowingManager(
            ILogger logger,
            MasterUserSettingService masterSettingsService,
            FollowerService followerService,
            SignalService signalService, MT4Manager mt4Manager,
            MasterUserService masterUserService)
        {
            m_ptrLogger = new ServerLogger(logger, nameof(FollowingManager));
            _masterSettingsService = masterSettingsService;
            _followerService = followerService;
            _signalService = signalService;
            _mt4Manager = mt4Manager;
            _masterUserService = masterUserService;
        }
        //TODO: handleSSPFollowerRequest
        public async Task<AddSSPFollowerResponse> AddSSPFollower(
            AddSSPFollowerRequest ptr, CancellationToken cancellationToken)
        {
            AddSSPFollowerResponse ptrResp = null;
            m_ptrLogger.LogOk("START");

            if (ptr._dataTransMode == DT_TRANS_ADD)
            {
                await _followerService.InsertSSPFollower(ptr._sspfollower, cancellationToken);
                bool ret = true;
                ptrResp = new AddSSPFollowerResponse(); //MT_AddSSPFollowerResponse_ID
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                CppHelper.memcpy(ref ptrResp._sspfollower, ptr._sspfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSSPFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    //await AddFollowerAsync(ptr._sspfollower._sspMT4Login, ptr._sspfollower._followerMT4Login, 
                    //    ptr._sspfollower._followerMT4ServerIndex, cancellationToken);
                    //await AddFollowerVolumeAsync(ptr._sspfollower._sspSignalIndex, 
                    //    ptr._sspfollower._followerMT4ServerIndex, ptr._sspfollower._followerMT4Login, 
                    //    ptr._sspfollower._followervolume, cancellationToken);

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
                await _followerService.ModifySSPFollower(ptr._sspfollower, cancellationToken);
                bool ret = true;
                ptrResp = new AddSSPFollowerResponse(); // MT_AddSSPFollowerResponse_ID
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                CppHelper.memcpy(ref ptrResp._sspfollower, ptr._sspfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSSPFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    //await UpdateFollowerVolumeAsync(ptr._sspfollower._sspSignalIndex, ptr._sspfollower._followerMT4ServerIndex, 
                    //    ptr._sspfollower._followerMT4Login, ptr._sspfollower._followervolume, cancellationToken);

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
                await _followerService.DeleteSSPFollower(ptr._sspfollower, cancellationToken);
                bool ret = true;
                ptrResp = new AddSSPFollowerResponse(); // MT_AddSSPFollowerResponse_ID
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                CppHelper.memcpy(ref ptrResp._sspfollower, ptr._sspfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSSPFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    //await RemoveFollowerAsync(ptr._sspfollower._sspMT4Login, 
                    //    ptr._sspfollower._followerMT4Login, 
                    //    ptr._sspfollower._followerMT4ServerIndex, cancellationToken);
                    //await RemoveFollowerVolumeAsync(ptr._sspfollower._sspSignalIndex, 
                    //    ptr._sspfollower._followerMT4ServerIndex, ptr._sspfollower._followerMT4Login, 
                    //    ptr._sspfollower._followervolume, cancellationToken);

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


        //TODO: handleSMFollowerRequest
        public async Task<AddSMFollowerResponse> AddSMFollower(
            AddSMFollowerRequest ptr, CancellationToken cancellationToken)
        {
            AddSMFollowerResponse ptrResp = null;
            m_ptrLogger.LogOk("START");

            if (ptr._dataTransMode == DT_TRANS_ADD)
            {
                await _followerService.InsertSMFollower(ptr._smfollower, cancellationToken);
                bool ret = true;
                ptrResp = new AddSMFollowerResponse(); // MT_AddSMFollowerResponse_ID
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                CppHelper.memcpy(ref ptrResp._smfollower, ptr._smfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSMFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    //await AddFollowerAsync(ptr._smfollower._smMT4Login, ptr._smfollower._followerMT4Login, 
                    //    ptr._smfollower._followerMT4ServerIndex, cancellationToken);
                    //await AddFollowerVolumeAsync(ptr._smfollower._smSignalIndex, ptr._smfollower._followerMT4ServerIndex, 
                    //    ptr._smfollower._followerMT4Login, ptr._smfollower._followervolume, cancellationToken);

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
                await _followerService.ModifySMFollower(ptr._smfollower, cancellationToken);
                bool ret = true;
                ptrResp = new AddSMFollowerResponse(); //MT_AddSMFollowerResponse_ID
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                CppHelper.memcpy(ref ptrResp._smfollower, ptr._smfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSMFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    //await  UpdateFollowerVolumeAsync(ptr._smfollower._smSignalIndex, 
                    //    ptr._smfollower._followerMT4ServerIndex, ptr._smfollower._followerMT4Login, 
                    //    ptr._smfollower._followervolume, cancellationToken);
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
                await _followerService.DeleteSMFollower(ptr._smfollower, cancellationToken);
                bool ret = true;
                ptrResp = new AddSMFollowerResponse(); //MT_AddSMFollowerResponse_ID
                ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
                ptrResp._dataTransMode = ptr._dataTransMode;
                CppHelper.memcpy(ref ptrResp._smfollower, ptr._smfollower);
                //SentDataUsingSocketID(ptrResp, MT_AddSMFollowerResponse_ID, ptr._header._socketID);
                if (ret)
                {
                    //await RemoveFollowerAsync(ptr._smfollower._smMT4Login, ptr._smfollower._followerMT4Login, 
                    //    ptr._smfollower._followerMT4ServerIndex, cancellationToken);
                    //await RemoveFollowerVolumeAsync(ptr._smfollower._smSignalIndex, 
                    //    ptr._smfollower._followerMT4ServerIndex, ptr._smfollower._followerMT4Login, 
                    //    ptr._smfollower._followervolume, cancellationToken);

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

        //TODO: handleRemoveSSPinSMRequest
        public async Task<SMSignalRemoveResponse> RemoveSSPinSM(
            SMSignalRemoveRequest ptr, CancellationToken cancellationToken)
        {
            m_ptrLogger.LogOk("START");

            await _signalService.DeleteSSPinSM(ptr._smSignal, cancellationToken);
            bool ret = true;
            SMSignalRemoveResponse ptrResp = new SMSignalRemoveResponse();//MT_RemoveSMSignalResponse_ID
            ptrResp._retCode = ret == true ? RC_OK : RC_ERROR;
            ptrResp._dataTransMode = ptr._dataTransMode;
            CppHelper.memcpy(ref ptrResp._smSignal, ptr._smSignal);
            //SentDataUsingSocketID(ptrResp, MT_RemoveSMSignalResponse_ID, ptr._header._socketID);
            if (ret)
            {
                //await RemoveFollowerAsync(ptr._smSignal._smMT4Login, ptr._smSignal._sspMT4Login, 
                //    ptr._smSignal._sspMT4ServerIndex);
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

        //TODO: handleSMAccountRequest
        public async Task<SMSignalCreateResponse> CreateSMAccount(
            SMSignalCreateRequest ptr, CancellationToken cancellationToken)
        {
            var ptrResp = new SMSignalCreateResponse();
            m_ptrLogger.LogOk("START");

            var m_masterUserSetting = await _masterSettingsService.GetMasterSetting(cancellationToken);

            if (ptr._isSignalCreated)
            {
                if (await _signalService.GetSSPCountForSMSignal(ptr._smSignal._smMasterLogin,
                    ptr._smSignal._signalIndex, cancellationToken) >=
                    m_masterUserSetting._maxSSPInSM)
                {
                    m_ptrLogger.LogInfo("Max SSP Signal reached for SM signal index %d. Ignoring SM request", ptr._smSignal._signalIndex);
                    return null;
                }

                //Alexey: additional code
                var signalOfSM = await _signalService.GetSignal(ptr._smSignal._signalIndex, cancellationToken);
                Debug.Assert(signalOfSM != null);

                SMSignal smSig = new SMSignal();
                //memset(&smSig, 0, sizeof(SMSignal));

                smSig._signalIndex = ptr._smSignal._signalIndex;
                smSig._smMasterLogin = ptr._smSignal._smMasterLogin;
                smSig._smMT4Login = ptr._smSignal._smMT4Login;
                Debug.Assert(signalOfSM.AccountTransId != Guid.Empty);
                smSig.SmAccountTransId = signalOfSM.AccountTransId;
                smSig._smMT4ServerIndex = SRV_TYPE_DEMO;
                smSig._sspMasterLogin = ptr._smSignal._sspMasterLogin;
                smSig._sspMT4Login = ptr._smSignal._sspMT4Login;
                smSig._sspMT4ServerIndex = ptr._smSignal._sspMT4ServerIndex;
                smSig._sspSignalIndex = ptr._smSignal._sspSignalIndex;

                await _signalService.InsertSMSignal(smSig, cancellationToken);
                bool res = true;

                if (res)
                {
                    //ptrResp = new SMSignalCreateResponse(); //MT_SMSignalCreateResponse_ID
                    //ptrResp._header._loginID = ptr._header._loginID;
                    //ptrResp._header._socketID = ptr._header._socketID;
                    ptrResp._retCode = RC_OK;
                    //memcpy(ref ptrResp._smSignal, smSig);
                    ptrResp._smSignal = smSig;
                    //SentDataUsingSocketID(ptrResp, MT_SSPSignalCreateResponse_ID, ptrResp._header._socketID);

                    //addFollower(smSig._sspMT4Login, smSig._smMT4Login, smSig._smMT4ServerIndex);
                    //addFollowerVolume(smSig._sspSignalIndex, smSig._smMT4ServerIndex, smSig._smMT4Login, m_masterUserSetting._signalTradeVolume);
                }
                else
                {
                    m_ptrLogger.LogError("Unable to insert SMSignal for signal Index: %d master login: %d MT4 Login: %d", smSig._signalIndex, ptr._smSignal._smMasterLogin, smSig._smMT4Login);
                }
            }
            else
            {
                if (await _signalService.GetSMSignalCount(ptr._smSignal._smMasterLogin, cancellationToken) >=
                    m_masterUserSetting._maxSMAccount)
                {
                    m_ptrLogger.LogWarning("Max SM Signal reached. Ignoring SM request");
                    return null;
                }

                if (await _signalService.IsSignalNameExist(ptr._signalName, cancellationToken))
                {
                    m_ptrLogger.LogWarning("Signal name already exist.... %s", ptr._signalName);
                    return null;
                }


                MasterUser ptrUser = await _masterUserService.GetMasterAccount(ptr._smSignal._smMasterLogin,
                    cancellationToken);
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
                    ptrMT4Req2.reqType = MT4REQ.MT4REQ_NEW_ACCOUNT;
                    //ptrMT4Req2.socketID = ptr._header._socketID;
                    ptrMT4Req2.status = ORD_STAT_RECVD;
                    ptrMT4Req2.serverTransID = TransactionService.NewTransactionId();
                    ptrMT4Req2.ptrData = (MasterUser)new MasterUser();
                    memcpy(ref ptrMT4Req2.ptrData, ptrUser);

                    sig.AccountTransId = ptrMT4Req2.serverTransID;
                    sig._rebateAmount = m_masterUserSetting._rebate_SM;
                    sig._sspPercentage = m_masterUserSetting._ssp_per;
                    m_ptrLogger.LogInfo("Inserting SM signal in DB Signal: %s MasterLogin: %d Dummy MT4 Login: %d", ptr._signalName, ptr._smSignal._smMasterLogin, sig._mt4Login);

                    await _signalService.InsertSignal(sig, cancellationToken);
                    ptrResp.Signal = sig;
                    bool res2 = true;

                    if (res2)
                    {
                        if (ptr._smSignal._sspMT4Login != 0)
                        {
                            SMSignal smSig = new SMSignal();
                            //memset(&smSig, 0, sizeof(SMSignal));
                            smSig._signalIndex = sig._signalIndex;
                            smSig._smMasterLogin = sig._masterLogin;
                            Debug.Assert(sig.AccountTransId != Guid.Empty);
                            smSig.SmAccountTransId = sig.AccountTransId;
                            smSig._smMT4Login = sig._mt4Login;
                            smSig._smMT4ServerIndex = SRV_TYPE_DEMO;
                            smSig._sspMasterLogin = ptr._smSignal._sspMasterLogin;
                            smSig._sspMT4Login = ptr._smSignal._sspMT4Login;
                            smSig._sspMT4ServerIndex = ptr._smSignal._sspMT4ServerIndex;
                            smSig._sspSignalIndex = ptr._smSignal._sspSignalIndex;

                            await _signalService.InsertSMSignal(smSig, cancellationToken);
                            bool res3 = true;

                            if (res3)
                            {
                                //ptrResp = new SMSignalCreateResponse(); //MT_SMSignalCreateResponse_ID
                                //ptrResp._header._loginID = ptr._header._loginID;
                                //ptrResp._header._socketID = ptr._header._socketID;
                                ptrResp._retCode = RC_OK;
                                //memcpy(ref ptrResp._smSignal, smSig);
                                ptrResp._smSignal = smSig;
                                ptrResp.Signal = sig;
                                //SentDataUsingSocketID(ptrResp, MT_SSPSignalCreateResponse_ID, ptrResp._header._socketID);

                                //addFollower(smSig._sspMT4Login, smSig._smMasterLogin, smSig._smMT4ServerIndex);
                                //addFollowerVolume(smSig._sspSignalIndex, smSig._smMT4ServerIndex, smSig._smMasterLogin, m_masterUserSetting._signalTradeVolume);
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

                    //if (ptrResp == null)
                    //{
                    //    ptrResp = new SMSignalCreateResponse(); // { _header = new MessageHeader { _loginID = ptr._header._loginID } };
                    //}

                    ptrResp.MT4Request = ptrMT4Req2;
                    CppHelper.free(ptrUser);
                    ptrUser = null;
                }
            }
            m_ptrLogger.LogOk("END");
            return ptrResp;
        }

        private WitFXServerConnector m_ptrDemoMT4Manager => _mt4Manager.Demo;
        private WitFXServerConnector m_ptrLiveMT4Manager => _mt4Manager.Live;

        //TODO: handleSSPAccountRequest
        public async Task<SSPSignalCreateResult> CreateSSPAccount(
            SSPSignalCreateRequest ptr, CancellationToken cancellationToken)
        {
            m_ptrLogger.LogOk("START");
            var m_masterUserSetting = await _masterSettingsService.GetMasterSetting(cancellationToken);

            if (await _signalService.GetSSPSignalCount(ptr._masterLogin, cancellationToken) >=
                m_masterUserSetting._maxSSPAccount)
            {
                m_ptrLogger.LogWarning("Max SSP Signal reached. Ignoring SSP request");
                return null;
            }
            if (await _signalService.IsSignalNameExist(ptr._signalName, cancellationToken))
            {
                m_ptrLogger.LogWarning("Signal name already exist.... %s", ptr._signalName);
                return null;
            }

            MasterUser ptrUser = await _masterUserService.GetMasterAccount(ptr._masterLogin,
                    cancellationToken);
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
                ptrMT4Req2.reqType = MT4REQ.MT4REQ_NEW_ACCOUNT;
                //ptrMT4Req2.socketID = ptr._header._socketID;
                ptrMT4Req2.status = ORD_STAT_RECVD;
                ptrMT4Req2.serverTransID = TransactionService.NewTransactionId();
                ptrMT4Req2.ptrData = (MasterUser)new MasterUser();
                memcpy(ref ptrMT4Req2.ptrData, ptrUser);

                sig.AccountTransId = ptrMT4Req2.serverTransID;
                sig._rebateAmount = m_masterUserSetting._rebate_SSP;
                sig._sspPercentage = 100.0F;
                m_ptrLogger.LogInfo("Inserting SSP signal in DB Signal: %s MasterLogin: %d Dummy MT4 Login: %d", ptr._signalName, ptr._masterLogin, sig._mt4Login);
                await _signalService.InsertSignal(sig, cancellationToken);
                bool res = true;
                if (res)
                {
                    SSPSignal ssp = new SSPSignal();
                    ssp._signalIndex = sig._signalIndex;
                    ssp._sspMasterLogin = sig._masterLogin;
                    ssp._sspMT4Login = sig._mt4Login;
                    Debug.Assert(sig.AccountTransId != Guid.Empty);
                    ssp.SspAccountTransId = sig.AccountTransId;
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
                    await _signalService.InsertSSPSignal(ssp, cancellationToken);
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

                CppHelper.free(ptrUser);
                ptrUser = null;

            }
            m_ptrLogger.LogOk("END");
            return result;
        }
    }
}
