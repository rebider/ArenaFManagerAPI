using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Contracts;
using WitFX.MT4.Server.cls;
using WitFX.MT4.Server.Implementation;
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
    public sealed class AccountManager
    {
        private readonly ServerLogger m_ptrLogger;
        private readonly MasterUserService _masterUserService;
        private readonly ProductService _productService;
        private readonly MT4Manager _mt4Manager;

        public AccountManager(
            ILogger logger, MasterUserService masterUserService, ProductService productService,
            MT4Manager mt4Manager)
        {
            m_ptrLogger = new ServerLogger(logger, nameof(AccountManager));
            _masterUserService = masterUserService;
            _productService = productService;
            _mt4Manager = mt4Manager;
        }

        /// <summary>
        /// handleNewAccountRequest; used only by WitFX; old name was CreateAccount
        /// </summary>
        public async Task<NewAccountResponse> SetAccountsByProduct(
            NewAccountRequest ptr, CancellationToken cancellationToken)
        {
            m_ptrLogger.LogOk("START");
            Debug.Assert(ptr._usrDetails != null);
            await _masterUserService.InsertMasterAccount(ptr._usrDetails, cancellationToken);
            bool ret = true;
            NewAccountResponse response;
            if (ret)
            {
                NewAccountResponse ptrResp = new NewAccountResponse(); //MT_NewAccountResponse_ID
                ptrResp._usrDetails = ptr._usrDetails;
                ptrResp._retCode = RC_OK;
                response = ptrResp; //m_connectionMgr.SendResponseToQueue(ptr._header._socketID, ptrResp, MT_NewAccountResponse_ID);

                var ptrproductDetails = await _productService.GetProductDetails(ptr._productid, cancellationToken);
                var cnt = ptrproductDetails.Count;
                for (int it = 0; it < cnt; it++)
                {
                    MT4Request ptrMT4Req = (MT4Request)new MT4Request();
                    //memset(ptrMT4Req, 0, sizeof(MT4Request));
                    ptrMT4Req.accType = ptrproductDetails[it]._accounttype;
                    ptrMT4Req.group = ptrproductDetails[it]._group;
                    ptrMT4Req.deposit = ptrproductDetails[it]._deposit;
                    ptrMT4Req.leverage = ptr._leverage;
                    ptrMT4Req.masterLogin = ptr._usrDetails._login;
                    ptrMT4Req.reqType = MT4REQ.MT4REQ_NEW_ACCOUNT;
                    ptrMT4Req.requestMode = MT4REQMODE.NEW_ACCOUNT;
                    //ptrMT4Req.socketID = ptr._header._socketID;
                    ptrMT4Req.status = ORD_STAT_RECVD;
                    ptrMT4Req.serverTransID = TransactionService.NewTransactionId();
                    //ptrMT4Req.ptrData = new MasterUser();
                    ptrMT4Req.ptrData = ptr._usrDetails;
                    if (ptrproductDetails[it]._serverid == SRV_TYPE_DEMO)
                        _mt4Manager.Demo.insertMT4Request(ptrMT4Req);
                    else
                        _mt4Manager.Live.insertMT4Request(ptrMT4Req);

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
                NewAccountResponse ptrResp = new NewAccountResponse(); // MT_NewAccountResponse_ID
                //memcpy(ref ptrResp._usrDetails, ptr._usrDetails);
                ptrResp._usrDetails = ptr._usrDetails;
                ptrResp._retCode = RC_ERROR;
                response = ptrResp; //  m_connectionMgr.SendResponseToQueue(ptr._header._socketID, ptrResp, MT_NewAccountResponse_ID);
            }//else of if (ret)
            m_ptrLogger.LogOk("END");
            Debug.Assert(response._usrDetails._login > 0);
            //TODO: insertDBTransmitData(response._usrDetails._login, FDMT_MasterUser_ID);
            //send email to user
            //sendRegistrationEmail(ptr->_usrDetails._login, ptr->_usrDetails._password, ptr-            
            return response;
        }

        /// <summary>
        /// this function only add a master user into the db; u
        /// </summary>
        public async Task<NewAccountResponse> RegistrationByUser(
            NewAccountRequest ptr, CancellationToken cancellationToken)
        {
            m_ptrLogger.LogOk("START");
            Debug.Assert(ptr._usrDetails != null);
            await _masterUserService.InsertMasterAccount(ptr._usrDetails, cancellationToken);
            NewAccountResponse response = new NewAccountResponse()
            {
                _usrDetails = ptr._usrDetails,
                _retCode = RC_OK
            };
            m_ptrLogger.LogOk("END");
            Debug.Assert(response._usrDetails._login > 0);
            return response;
        }

        /// <summary>
        /// this function only add accounts based on user request
        /// </summary>
        public async Task<NewAccountResponse> SetAccountsByUser(
            IReadOnlyList<NewAccountDetails> accountDetails, MasterUser user, CancellationToken cancellationToken)
        {
            m_ptrLogger.LogOk("START");
            Debug.Assert(user != null);
            NewAccountResponse response = new NewAccountResponse()
            {
                _usrDetails = user,
                _retCode = RC_OK
            };

            foreach (NewAccountDetails details in accountDetails)
            {
                MT4Request ptrMT4Req = (MT4Request)new MT4Request();
                ptrMT4Req.accType = eAccountType.ACC_TYPE_STANDARD_DEMO;
                ptrMT4Req.group = details.Group;
                ptrMT4Req.leverage = details.Leverage;
                ptrMT4Req.deposit = details.Deposit;
                ptrMT4Req.masterLogin = user._login;
                ptrMT4Req.reqType = MT4REQ.MT4REQ_NEW_ACCOUNT;
                ptrMT4Req.requestMode = MT4REQMODE.NEW_ACCOUNT;
                ptrMT4Req.status = ORD_STAT_RECVD;
                ptrMT4Req.serverTransID = TransactionService.NewTransactionId();
                ptrMT4Req.ptrData = user;
                _mt4Manager.Demo.insertMT4Request(ptrMT4Req);
                response.MT4Requests.Add(ptrMT4Req);
            }

            m_ptrLogger.LogOk("END");
            Debug.Assert(response._usrDetails._login > 0);
            return response;
        }

    }
}
