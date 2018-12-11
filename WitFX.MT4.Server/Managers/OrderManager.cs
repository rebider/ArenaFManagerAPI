using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.Contracts;
using WitFX.MT4.Server.cls;
using WitFX.MT4.Server.Events;
using WitFX.MT4.Server.Implementation;
using WitFX.MT4.Server.Implementation.Helpers;
using WitFX.MT4.Server.Implementation.Utilities;
using WitFX.MT4.Server.Models;
using WitFX.MT4.Server.MT4;
using WitFX.MT4.Server.Services;
using static WitFX.Contracts.eOrderStatus;
using static WitFX.Contracts.eMT4ServerType;
using static WitFX.Contracts.eMT4OrderTransMode;
using static WitFX.Contracts.eReturnCode;
using static WitFX.Contracts.eMT4OrderType;

namespace WitFX.MT4.Server.Managers
{
    public sealed class OrderManager
    {
        private readonly ServerLogger m_ptrLogger;
        private readonly MT4SymbolInfoService _symbolService;
        private readonly MT4AccountService _mt4AccountService;
        private readonly MasterUserSettingService _masterSettingsService;
        private readonly OrderService _orderService;
        private readonly FollowerService _followerService;
        private readonly SignalService _signalService;
        private readonly MT4Manager _mt4Manager;
        private readonly TradeDisableService _tradeDisableService;
        private readonly SignalSymbolSettingService _signalSymbolSettingService;
        private readonly IMessageEvents _connectionMgr;
        private readonly TradeRecordService _tradeRecordService;
        private readonly MarketDataService _marketDataService;

        public OrderManager(
            ILogger logger, MT4SymbolInfoService symbolService, MT4AccountService mt4AccountService,
            MasterUserSettingService masterSettingsService, OrderService orderService,
            FollowerService followerService,
            SignalService signalService, MT4Manager mt4Manager,
            TradeDisableService tradeDisableService, SignalSymbolSettingService signalSymbolSettingService,
            IMessageEvents connectionMgr, TradeRecordService tradeRecordService,
            MarketDataService marketDataService)
        {
            m_ptrLogger = new ServerLogger(logger, nameof(OrderManager));
            _symbolService = symbolService;
            _mt4AccountService = mt4AccountService;
            _masterSettingsService = masterSettingsService;
            _orderService = orderService;
            _followerService = followerService;
            _signalService = signalService;
            _mt4Manager = mt4Manager;
            _tradeDisableService = tradeDisableService;
            _signalSymbolSettingService = signalSymbolSettingService;
            _connectionMgr = connectionMgr;
            _tradeRecordService = tradeRecordService;
            _marketDataService = marketDataService;
        }

        public class SocialOrderResult
        {
            public eReturnCode Code;
            public readonly List<MT4Request> MT4Requests = new List<MT4Request>();
        }

        /// <summary>
        /// handleOrderRequest
        /// </summary>
        public async Task<SocialOrderResult> ProcessOrder(SocialOrderRequest ptr, CancellationToken cancellationToken)
        {
            var senderLoginID = ptr._header._loginID;
            Debug.Assert(senderLoginID > 0);

            switch (ptr._orderReq._orderTransMode)
            {
                case ORD_TRANS_CLOSE:
                    {
                        m_ptrLogger.LogInfo("Close Order Request Recv Login: %d MT4Login: %d OrderID: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return await CloseOrder(ptr, cancellationToken);
                    }
                case ORD_TRANS_DELETE:
                    {
                        m_ptrLogger.LogInfo("Delete Order Request Recv Login: %d MT4Login: %d OrderID: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return await DeleteOrder(ptr, cancellationToken);
                    }
                case ORD_TRANS_MODIFY:
                    {
                        m_ptrLogger.LogInfo("Modify Order Request Recv Login: %d MT4Login: %d OrderID: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        return await ModifyOrder(ptr, MT4REQMODE.MODIFY_TRADE, cancellationToken);
                    }
                case ORD_TRANS_OPEN:
                    {
                        m_ptrLogger.LogInfo("Open Order Request Recv Login: %d MT4Login: %d Symbol: %s OrdType: %d", senderLoginID, ptr._orderReq._mt4Login, ptr._orderReq._symbol, ptr._orderReq._orderType);
                        return await OpenOrder(ptr, cancellationToken);
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
                        return new SocialOrderResult { Code = RC_INVALID_ORDER_TRANS_MODE };
                    }
            }
        }

        /// <summary>
        /// handleCloseOrderRequest
        /// </summary>
        private async Task<SocialOrderResult> CloseOrder(SocialOrderRequest ptr, CancellationToken cancellationToken)
        {
            return await ModifyOrder(ptr, MT4REQMODE.CLOSE_TRADE, cancellationToken);
        }

        /// <summary>
        /// handleDeleteOrderRequest
        /// </summary>
        private async Task<SocialOrderResult> DeleteOrder(SocialOrderRequest ptr, CancellationToken cancellationToken)
        {
            return await ModifyOrder(ptr, MT4REQMODE.DELETE_TRADE, cancellationToken);
        }

        private async Task<Order> GetAndCheckOrderAsync(MT4OrderInfo request, CancellationToken cancellationToken)
        {
            if (request._mt4Login <= 0)
                throw new ArgumentException("Invalid MT4 login", nameof(request));

            var order = await _orderService.GetOrderAsync(request._mt4ServerIndex, request._orderID, cancellationToken);

            if (order == null)
            {
                throw new InvalidOperationException(
                    $"Order not found: serverIndex = {request._mt4ServerIndex}, orderId = {request._orderID}");
            }

            var isValid = order.MT4Login == request._mt4Login;

            if (!isValid)
                throw new InvalidOperationException(
                    $"MT4 login {order.MT4Login} of an order does not match mt4 login {request._mt4Login} of request");

            return order;
        }

        /// <summary>
        /// handleModifyOrderRequest
        /// </summary>
        private async Task<SocialOrderResult> ModifyOrder(
            SocialOrderRequest request, MT4REQMODE reqMode, CancellationToken cancellationToken)
        {
            Debug.Assert(request != null);
            var requestOrderInfo = request._orderReq;
            var result = new SocialOrderResult();
            var order = await GetAndCheckOrderAsync(request._orderReq, cancellationToken);

            var validation = new OrderValidationContext
            {
                isSSP = false,
                isSM = false,
                reason = RC_TRADE_ACCEPTED
            };

            if (await IsOrderValidated(request, validation, cancellationToken))
            {
                var m_masterUserSetting = await _masterSettingsService.GetCachedMasterSettingsAsync(cancellationToken);

                var orderRequest = new MT4Request();
                orderRequest.Order = order;
                orderRequest.serverTransID = order.TransId;
                orderRequest.reqType = MT4REQ.MT4REQ_TRADE;
                orderRequest.requestMode = reqMode;
                orderRequest.status = ORD_STAT_RECVD;

                var orderInfo = requestOrderInfo.Clone();
                orderRequest.ptrData = orderInfo;

                if (order.SignalIndex != null)
                {
                    Debug.Assert(m_masterUserSetting._signalTradeVolume > 0);
                    orderInfo._volume = m_masterUserSetting._signalTradeVolume;
                }
                else
                {
                    // 2017-08-26 Alexey: Giuseppe confirmed that volume of non-signal order can be modified manually by user
                    Debug.Assert(requestOrderInfo._volume > 0);
                }

                Debug.Assert(requestOrderInfo._mt4Login > 0);
                orderInfo._mt4Login = requestOrderInfo._mt4Login;
                orderInfo._mt4ServerIndex = requestOrderInfo._mt4ServerIndex;
                Debug.Assert(requestOrderInfo._masterLogin > 0);
                orderRequest.masterLogin = requestOrderInfo._masterLogin;
                Debug.Assert(orderInfo._orderID > 0);

                if (order.SignalIndex == null)
                    switch (requestOrderInfo._mt4ServerIndex)
                    {
                        case SRV_TYPE_DEMO:
                            orderRequest.accType = eAccountType.ACC_TYPE_FOLLOWER_DEMO;
                            break;
                        case SRV_TYPE_LIVE:
                            orderRequest.accType = eAccountType.ACC_TYPE_FOLLOWER_LIVE;
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                _mt4Manager.GetConnector(requestOrderInfo._mt4ServerIndex).insertMT4Request(orderRequest);
                result.MT4Requests.Add(orderRequest);

                if (order.SignalIndex != null)
                {
                    var followedOrders = await _orderService.GetFollowedOrdersAsync(order.TransId, cancellationToken);

                    foreach (var followerOrder in followedOrders)
                    {
                        var followerOrderRequest = new MT4Request();
                        followerOrderRequest.Order = followerOrder;
                        followerOrderRequest.serverTransID = followerOrder.TransId;
                        followerOrderRequest.reqType = MT4REQ.MT4REQ_TRADE;
                        followerOrderRequest.requestMode = reqMode;
                        followerOrderRequest.status = ORD_STAT_RECVD;

                        var followerOrderInfo = requestOrderInfo.Clone();
                        followerOrderRequest.ptrData = followerOrderInfo;
                        Debug.Assert(followerOrder.MT4Login > 0);
                        followerOrderInfo._mt4Login = followerOrder.MT4Login;
                        Debug.Assert(followerOrder.OrderId > 0);
                        followerOrderInfo._orderID = followerOrder.OrderId;
                        Debug.Assert(followerOrder.MasterLogin > 0);
                        followerOrderRequest.masterLogin = followerOrder.MasterLogin;
                        followerOrderInfo._mt4ServerIndex = followerOrder.MT4ServerIndex;
                        Debug.Assert(followerOrderInfo._volume > 0);

                        _mt4Manager.GetConnector(followerOrder.MT4ServerIndex).insertMT4Request(followerOrderRequest);
                        result.MT4Requests.Add(followerOrderRequest);
                    }
                }
            }

            result.Code = validation.reason;
            return result;
        }

        /// <summary>
        /// handleOpenOrderRequest
        /// </summary>
        private async Task<SocialOrderResult> OpenOrder(SocialOrderRequest request, CancellationToken cancellationToken)
        {
            var requestOrderInfo = request._orderReq;
            var result = new SocialOrderResult();

            var validation = new OrderValidationContext
            {
                isSSP = false,
                isSM = false,
                reason = RC_TRADE_ACCEPTED
            };

            if (await IsOrderValidated(request, validation, cancellationToken))
            {
                var m_masterUserSetting = await _masterSettingsService.GetCachedMasterSettingsAsync(cancellationToken);

                // Create MT4Request

                var orderRequest = new MT4Request();
                Debug.Assert(requestOrderInfo._masterLogin > 0);
                orderRequest.masterLogin = requestOrderInfo._masterLogin;
                orderRequest.reqType = MT4REQ.MT4REQ_TRADE;
                orderRequest.requestMode = MT4REQMODE.OPEN_TRADE;
                orderRequest.status = ORD_STAT_RECVD;

                // Create MT4OrderInfo

                var orderInfo = requestOrderInfo.Clone();
                orderRequest.ptrData = orderInfo;

                if (validation.isSSP)
                {
                    Debug.Assert(m_masterUserSetting._signalTradeVolume > 0);
                    orderInfo._volume = m_masterUserSetting._signalTradeVolume;
                }
                else
                    Debug.Assert(orderInfo._volume > 0);

                Debug.Assert(requestOrderInfo._mt4Login > 0);
                orderInfo._mt4Login = requestOrderInfo._mt4Login;

                // Create Order

                var order = new Order();
                orderRequest.Order = order;
                orderRequest.serverTransID = order.TransId = TransactionService.NewTransactionId();
                order.MT4ServerIndex = requestOrderInfo._mt4ServerIndex;
                Debug.Assert(requestOrderInfo._mt4Login > 0);
                order.MT4Login = requestOrderInfo._mt4Login;
                Debug.Assert(requestOrderInfo._masterLogin > 0);
                order.MasterLogin = requestOrderInfo._masterLogin;
                order.Comment = "direct trade";

                if (validation.isSSP)
                {
                    Debug.Assert(requestOrderInfo._signalIndex > 0);
                    order.SignalIndex = requestOrderInfo._signalIndex;
                    order.IsSSP = true;
                }
                else
                    Debug.Assert(requestOrderInfo._signalIndex <= 0);

                Debug.Assert(!validation.isSM);

                // Commit MT4Request

                _mt4Manager.GetConnector(requestOrderInfo._mt4ServerIndex).insertMT4Request(orderRequest);
                result.MT4Requests.Add(orderRequest);

                if (validation.isSSP)
                {
                    var signalName = validation.signalName;

                    var sspFollowers = await _followerService.GetSspFollowersAsync(requestOrderInfo._mt4Login, cancellationToken);

                    foreach (var sspFollower in sspFollowers)
                    {
                        Debug.Assert(sspFollower.MasterLogin > 0 && sspFollower.MT4Login > 0);

                        //Check this follower is SM or not

                        var smSignal =
                            sspFollower.ServerIndex == SRV_TYPE_DEMO
                                ? await _signalService.GetCachedSMSignalByMT4LoginAsync(sspFollower.MT4Login, cancellationToken)
                                : null;

                        var sspFollowerRequest = new MT4Request();
                        sspFollowerRequest.masterLogin = sspFollower.MasterLogin;
                        sspFollowerRequest.reqType = MT4REQ.MT4REQ_TRADE;
                        sspFollowerRequest.requestMode = MT4REQMODE.OPEN_TRADE;
                        sspFollowerRequest.status = ORD_STAT_RECVD;

                        var sspFollowerOrderInfo = requestOrderInfo.Clone();
                        sspFollowerRequest.ptrData = sspFollowerOrderInfo;
                        sspFollowerOrderInfo._volume = await _followerService.GetTradeVolumeAsync(requestOrderInfo._signalIndex, sspFollower.MT4Login, sspFollower.ServerIndex, cancellationToken);
                        sspFollowerOrderInfo._mt4Login = sspFollower.MT4Login;

                        var sspFollowerOrder = new Order();
                        sspFollowerRequest.Order = sspFollowerOrder;
                        sspFollowerRequest.serverTransID = sspFollowerOrder.TransId = TransactionService.NewTransactionId();
                        sspFollowerOrder.MT4ServerIndex = sspFollower.ServerIndex;
                        sspFollowerOrder.MT4Login = sspFollower.MT4Login;
                        sspFollowerOrder.MasterLogin = sspFollower.MasterLogin;
                        Debug.Assert(order.TransId != Guid.Empty);
                        sspFollowerOrder.SignalTransId = order.TransId;
                        sspFollowerOrder.Comment = signalName;

                        if (smSignal != null)
                        {
                            Debug.Assert(smSignal._signalIndex > 0);
                            sspFollowerOrder.SignalIndex = smSignal._signalIndex;
                            sspFollowerOrder.IsSSP = false;
                        }

                        if (sspFollowerOrderInfo._volume > 0)
                        {
                            _mt4Manager.GetConnector(sspFollower.ServerIndex).insertMT4Request(sspFollowerRequest);
                            result.MT4Requests.Add(sspFollowerRequest);
                        }
                        else
                            m_ptrLogger.LogWarning(
                                $"Order {order.TransId} not mirrored for ssp follower {sspFollower.ServerIndex} {sspFollower.MT4Login}, volume is zero");

                        if (smSignal != null)
                        {
                            var smFollowers = await _followerService.GetSmFollowersAsync(sspFollower.MT4Login, cancellationToken);

                            foreach (var smFollower in smFollowers)
                            {
                                Debug.Assert(smFollower.MasterLogin > 0 && smFollower.MT4Login > 0);

                                var smFollowerRequest = new MT4Request();
                                smFollowerRequest.masterLogin = smFollower.MasterLogin;
                                smFollowerRequest.reqType = MT4REQ.MT4REQ_TRADE;
                                smFollowerRequest.requestMode = MT4REQMODE.OPEN_TRADE;
                                smFollowerRequest.status = ORD_STAT_RECVD;

                                var smFollowerOrderInfo = request._orderReq.Clone();
                                smFollowerRequest.ptrData = smFollowerOrderInfo;
                                smFollowerOrderInfo._volume = await _followerService.GetTradeVolumeAsync(smSignal._signalIndex, smFollower.MT4Login, smFollower.ServerIndex, cancellationToken);
                                smFollowerOrderInfo._mt4Login = smFollower.MT4Login;

                                if (smFollowerOrderInfo._volume > 0)
                                {
                                    var smFollowerOrder = new Order();
                                    smFollowerOrder.Comment = $"{smSignal._signalName}|{signalName}";
                                    smFollowerRequest.Order = smFollowerOrder;
                                    smFollowerRequest.serverTransID = smFollowerOrder.TransId = TransactionService.NewTransactionId();
                                    smFollowerOrder.MT4ServerIndex = smFollower.ServerIndex;
                                    smFollowerOrder.MT4Login = smFollower.MT4Login;
                                    smFollowerOrder.MasterLogin = smFollower.MasterLogin;
                                    smFollowerOrder.SignalTransId = sspFollowerOrder.TransId;


                                    _mt4Manager.GetConnector(smFollower.ServerIndex).insertMT4Request(smFollowerRequest);
                                    result.MT4Requests.Add(smFollowerRequest);
                                }
                                else
                                    m_ptrLogger.LogWarning(
                                        $"Order {sspFollowerOrder.TransId} not mirrored for sm follower {smFollower.ServerIndex} {smFollower.MT4Login}, volume is zero");
                            }
                        }
                    }
                }
            }

            result.Code = validation.reason;
            return result;
        }

        private sealed class OrderValidationContext
        {
            public bool isSSP;
            public bool isSM;
            public eReturnCode reason;
            public string signalName;
        }

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
        private async Task<bool> IsOrderValidated(SocialOrderRequest ptr, OrderValidationContext context, CancellationToken cancellationToken)
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
                context.reason = RC_INVALID_PARAMETER;
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PARAMETER MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }

            int masterLogin = await _mt4AccountService.GetCachedMasterLoginAsync(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login, cancellationToken);
            if (masterLogin != ptr._orderReq._masterLogin)
            {
                context.reason = RC_INVALID_PARAMETER;
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PARAMETER MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }

            context.isSSP = await IsClientSSPAsync(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login, cancellationToken);
            context.isSM = await IsClientSMAsync(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login, cancellationToken);

            if (ptr._orderReq._mt4ServerIndex == SRV_TYPE_DEMO)
            {
                if (!_mt4Manager.Demo.isMT4Connected())
                {
                    context.reason = RC_LP_NOTCONNECTED;
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_LP_NOTCONNECTED MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }
            }
            else
            {
                if (!_mt4Manager.Live.isMT4Connected())
                {
                    context.reason = RC_LP_NOTCONNECTED;
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_LP_NOTCONNECTED MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }

            }

            if (context.isSSP)
            {
                ret = false;
                var originalReson = context.reason; // Alexey
                context.reason = RC_INVALID_PARAMETER;
                //lock (m_SyncSignalSSP)
                //{
                //Dictionary<int, int>.iterator itsspSig = m_mapSSPMT4SignalIndex.find(ptr._orderReq._mt4Login);

                var sspSignal = await _signalService.GetSignalByMt4Login(ptr._orderReq._mt4ServerIndex, ptr._orderReq._mt4Login, cancellationToken);

                // before
                //var sspSignalIndex = await _signalService.GetCachedSSPSignalIndexByMT4LoginAsync(ptr._orderReq._mt4Login, cancellationToken);
                var sspSignalIndex = sspSignal != null && sspSignal._isSSP ? sspSignal._signalIndex : 0; 

                //if (itsspSig != m_mapSSPMT4SignalIndex.end())
                if (sspSignalIndex > 0)
                {
                    //if (itsspSig.second == ptr._orderReq._signalIndex)
                    if (sspSignalIndex == ptr._orderReq._signalIndex)
                    {
                        ret = true;
                        context.reason = originalReson; // Alexey
                        context.signalName = sspSignal._signalName;
                    }
                }
                //}

                if (!ret)
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! INVALID SIGNAL INDEX MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }

                var m_masterUserSetting = await _masterSettingsService.GetCachedMasterSettingsAsync(cancellationToken);

                ret = false;
                // Fixed error with max trades open when a trade is being closed.
                if (ptr._orderReq._orderTransMode == (int)ORD_TRANS_OPEN && _mt4Manager.Demo.getTotalOpenOrdersCount(ptr._orderReq._mt4Login,
                    m_masterUserSetting._demoGroup) >= m_masterUserSetting._maxSSPOpenOrders)
                {
                    context.reason = RC_MAX_SSP_ORDER;
                    m_ptrLogger.LogError("!! Order Validation Failed !! MasterLogin: %d MT4Login: %d SSP MAX ORDER REACHED", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login);
                    return false;
                }
                //Dictionary<int, HashSet<int>>.iterator itSSP
                //lock (m_SyncOpenOrderSSP) {
                //}
            }

            if (context.isSM)
            {
                ret = false;
                var originalReason = context.reason; // Alexey
                context.reason = RC_INVALID_PARAMETER;
                //lock (m_SyncAccLinking)
                //{
                //Dictionary<int, int>.iterator itsmSig = m_mapSMSignalIndex.find(ptr._orderReq._mt4Login);
                var smSignal = await _signalService.GetCachedSMSignalByMT4LoginAsync(ptr._orderReq._mt4Login, cancellationToken);

                //if (itsmSig != m_mapSMSignalIndex.end())
                if (smSignal != null)
                {
                    //if (itsmSig.second == ptr._orderReq._signalIndex)
                    if (smSignal._signalIndex == ptr._orderReq._signalIndex)
                    {
                        ret = true;
                        context.reason = originalReason; // Alexey
                        context.signalName = smSignal._signalName;
                    }
                }
                //}

                if (!ret)
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! INVALID SIGNAL INDEX MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    return false;
                }
            }
            if (ptr._orderReq._orderTransMode != ORD_TRANS_OPEN && ptr._orderReq._orderID == 0)
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_ORDERID MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                context.reason = RC_INVALID_ORDERID;
                return false;
            }
            if (!IsOrderTypeAndSymbolSupported(ptr._orderReq, ref context.reason))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SYMBOL OR RC_INVALID_ORDER_CMD MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }
            if (!IsOrderTransSupported(ptr._orderReq, ref context.reason))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_ORDER_TRANS_MODE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                return false;
            }

            int symDigit = 5;
            int stopLevel = 4;
            double symPoint = 0.00001;

            if (!_mt4Manager.GetSymbolProperty(ptr._orderReq._symbol, ref symDigit, ref stopLevel, ref symPoint))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_SYMBOL_NOT_FOUND MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                context.reason = RC_SYMBOL_NOT_FOUND;
                return false;
            }

            ptr._orderReq._price = Common.NormalizeDouble(ptr._orderReq._price, symDigit);
            ptr._orderReq._sl = Common.NormalizeDouble(ptr._orderReq._sl, symDigit);
            ptr._orderReq._tp = Common.NormalizeDouble(ptr._orderReq._tp, symDigit);
            symPoint = Common.NormalizeDouble(symPoint, symDigit);

            ptr._orderReq._price = Misc.round_off(ptr._orderReq._price, symDigit);
            ptr._orderReq._sl = Misc.round_off(ptr._orderReq._sl, symDigit);
            ptr._orderReq._tp = Misc.round_off(ptr._orderReq._tp, symDigit);
            symPoint = Misc.round_off(symPoint, symDigit);

            //round_off()
            double server_min_stop = convertPipToValue(stopLevel, symDigit, symPoint);

            server_min_stop = Common.NormalizeDouble(server_min_stop, symDigit);

            double currBid, currAsk;
            currBid = currAsk = 0.0;

            if (!getLatestBidAsk(ptr._orderReq._symbol, ref currBid, ref currAsk))
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_UNABLE_TO_GET_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                context.reason = RC_UNABLE_TO_GET_PRICE;
                return false;
            }

            if (currBid == 0.0 || currAsk == 0.0)
            {
                m_ptrLogger.LogError("!! Order Validation Failed !! RC_UNABLE_TO_GET_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                context.reason = RC_UNABLE_TO_GET_PRICE;
                return false;
            }



            if (context.isSSP || context.isSM)
            {
                DateTimeOffset currTime = DateTimeOffset.UtcNow;
                if (await _tradeDisableService.IsTradingDisable(ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._mt4ServerIndex, currTime, cancellationToken))
                {
                    m_ptrLogger.LogError("!! Order Validation Failed !! RC_TRADE_DISABLE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                    context.reason = RC_TRADE_DISABLE;
                    return false;
                }
                if (context.isSSP && (ptr._orderReq._orderTransMode == ORD_TRANS_OPEN || ptr._orderReq._orderTransMode == ORD_TRANS_MODIFY))
                {
                    int maxSlinPips = 2;
                    var details = await GetSSPSignalDetailAsync(ptr._orderReq._mt4ServerIndex, ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._signalIndex, ptr._orderReq._symbol, cancellationToken);
                    context.reason = details.errorCode;
                    if (details.Success)
                    {
                        maxSlinPips = details.maxSLinPips;
                        if (ptr._orderReq._sl != 0.0)
                        {
                            int orderSLInPips = CppHelper.abs(ConvertInPips(ptr._orderReq._price, symDigit, symPoint) - ConvertInPips(ptr._orderReq._sl, symDigit, symPoint));
                            if (orderSLInPips > maxSlinPips + 1) // TODO: temporary HACK 
                            {
                                m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_SIGNAL_SL MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                                context.reason = RC_INVALID_SIGNAL_SL;
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
                ptr._orderReq._price = Common.NormalizeDouble(ptr._orderReq._price, symDigit);
            }
            else if (ptr._orderReq._orderType == ORD_TYPE_SELL && ptr._orderReq._orderTransMode == ORD_TRANS_OPEN)
            {
                ptr._orderReq._price = currBid;
                ptr._orderReq._price = Common.NormalizeDouble(ptr._orderReq._price, symDigit);
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
                context.reason = RC_INVALID_PRICE;
                return false;
            }
            else
            {
                if (ptr._orderReq._orderTransMode == ORD_TRANS_OPEN || ptr._orderReq._orderTransMode == ORD_TRANS_MODIFY)
                {
                    if (ptr._orderReq._orderType == ORD_TYPE_BUYLIMIT && !(ptr._orderReq._price < currAsk))//Open Price   < ASK 
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        context.reason = RC_INVALID_PRICE;
                        return false;
                    }
                    else if (ptr._orderReq._orderType == ORD_TYPE_SELLLIMIT && !(ptr._orderReq._price > currBid))//Open Price  > BID
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        context.reason = RC_INVALID_PRICE;
                        return false;
                    }
                    else if (ptr._orderReq._orderType == ORD_TYPE_BUYSTOP && !(ptr._orderReq._price > currAsk))
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        context.reason = RC_INVALID_PRICE;
                        return false;
                    }
                    else if (ptr._orderReq._orderType == ORD_TYPE_SELLSTOP && !(ptr._orderReq._price < currBid))
                    {
                        m_ptrLogger.LogError("!! Order Validation Failed !! RC_INVALID_PRICE MasterLogin: %d MT4Login: %d OrdID: %d", ptr._orderReq._masterLogin, ptr._orderReq._mt4Login, ptr._orderReq._orderID);
                        context.reason = RC_INVALID_PRICE;
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// isOrderTypeAndSymbolSupported
        /// </summary>
        bool IsOrderTypeAndSymbolSupported(MT4OrderInfo ptr, ref eReturnCode reason)
        {
            if (_symbolService.GetCachedSymbolIndex(ptr._symbol) == -1)
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

        /// <summary>
        /// isOrderTransSupported
        /// </summary>
        bool IsOrderTransSupported(MT4OrderInfo ptr, ref eReturnCode reason)
        {
            if (!(ptr._orderTransMode >= ORD_TRANS_OPEN && ptr._orderTransMode <= ORD_TRANS_CLOSE))
            {
                reason = RC_INVALID_ORDER_TRANS_MODE;
                return false;
            }
            return true;
        }

        /// <summary>
        /// getSymbolProperty
        /// </summary>
        bool GetSymbolProperty(string symbol, ref int digit, ref int stopLevel, ref double pointValue)
        {
            var symbolInfo = _symbolService.GetCachedSymbolInfo(symbol);
            if (symbolInfo != null)
            {
                digit = symbolInfo._digits;
                stopLevel = symbolInfo._stops_level;
                pointValue = symbolInfo._point;
                return true;
            }
            return false;
        }

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

        bool getLatestBidAsk(string symbol, ref double bid, ref double ask)
        {
            bool ret = false;

            //Dictionary<int, MarketData>.iterator it;

            //lock (m_SyncQuoteBook)
            //{
            //it = m_mapQuoteBook.find(symIndex);
            var marketData = _marketDataService.GetMarketData(symbol);
            //if (it != m_mapQuoteBook.end())
            if (marketData != null)
            {
                bid = marketData._bid;
                ask = marketData._ask;
                ret = true;
            }
            //}

            return ret;
        }

        private struct SSpSignalDetails
        {
            public bool Success;
            public int maxSLinPips;
            public eReturnCode errorCode;
        }

        private async Task<SSpSignalDetails> GetSSPSignalDetailAsync(
            eMT4ServerType serverIndex, int masterlogin, int mt4login, int signalIndex, string symbol,
            CancellationToken cancellationToken)
        {
            var maxSLinPips = 0;
            var errorCode = eReturnCode.RC_INVALID_SIGNAL_SYMBOL;
            bool ret = false;
            //Dictionary<int, ValueTuple<int, int>>.iterator itSSPSignal;

            //lock (m_SyncSignalSSP)
            //{

            //var itSSPSignal = m_mapSignalSSP.find(signalIndex);
            var itSSPSignal = await _signalService.GetSSPSignalShortBySignalIndex(signalIndex, cancellationToken);
            //if (itSSPSignal != m_mapSignalSSP.end())
            if (itSSPSignal != null)
            {
                int symIndex = itSSPSignal.Value.symbolIndex;
                var strategyType = itSSPSignal.Value.strategyType;
                //if (getSymbolName(symIndex) == symbol)
                if (_symbolService.GetCachedSymbolIndex(symbol) == symIndex && symIndex > 0)
                {
                    //Dictionary<int, Dictionary<int, SignalSymbolSetting>>.iterator itSymSett1;
                    //Dictionary<int, SignalSymbolSetting>.iterator itSymSett12;
                    //lock (m_SyncSymbolSetting)
                    //{
                    //var itSymSett1 = m_mapSignalSymSetting.find(symIndex);
                    //if (itSymSett1 != m_mapSignalSymSetting.end())
                    //{
                    //    var itSymSett12 = itSymSett1.second.find(strategyType);
                    //    if (itSymSett12 != itSymSett1.second.end())
                    //    {
                    var settings = await _signalSymbolSettingService.GetSignalSymbolSettingAsync(symIndex, strategyType, cancellationToken);
                    errorCode = eReturnCode.RC_OK;
                    maxSLinPips = settings._maxSLInPips;
                    ret = true;
                    //    }
                    //}
                    //}
                }
            }
            //}

            return new SSpSignalDetails
            {
                Success = ret,
                maxSLinPips = maxSLinPips,
                errorCode = errorCode
            };
        }

        private async Task<bool> IsClientSSPAsync(eMT4ServerType serverIndex, int mt4login, CancellationToken cancellationToken)
        {
            bool ret = false;
            if (serverIndex == SRV_TYPE_DEMO)
            {
                //lock (m_SyncAccLinking)
                //{
                var sspSignalIndex = await _signalService.GetCachedSSPSignalIndexByMT4LoginAsync(mt4login, cancellationToken);
                //if (m_setSM_MT4Login.find(mt4login) == m_setSM_MT4Login.end() && m_mapFollowers.find(mt4login) != m_mapFollowers.end())
                if (sspSignalIndex > 0)
                {
                    ret = true;
                }
                //}
            }

            return ret;
        }

        private async Task<bool> IsClientSMAsync(eMT4ServerType serverIndex, int mt4login, CancellationToken cancellationToken)
        {
            bool ret = false;
            if (serverIndex == SRV_TYPE_DEMO)
            {
                //lock (m_SyncAccLinking)
                //{
                var smSignal = await _signalService.GetCachedSMSignalByMT4LoginAsync(mt4login, cancellationToken);
                //if (m_setSM_MT4Login.find(mt4login) != m_setSM_MT4Login.end())
                if (smSignal != null)
                {
                    ret = true;
                }
                //}
            }

            return ret;
        }

        /// <summary>
        /// WitFXMT4ServerBL.onTradeResponse
        /// </summary>
        internal async Task OnTradeAsync(
            eMT4ServerType serverIndex, TradeRecord ptrTrade, TransType transType, CancellationToken cancellationToken)
        {
            var ptr = new MyTradeRecordResponse(); // MT_MyTradeRecordResponse_ID
            ptr._trans = transType;
            ptr._isDemoServer = serverIndex == SRV_TYPE_DEMO ? true : false;
            CppHelper.memcpy(ref ptr._trade, ptrTrade);
            int masterlogin = await _mt4AccountService.GetCachedMasterLoginAsync(serverIndex, ptrTrade.login, cancellationToken);
            _connectionMgr.SentDataUsingLoginID(ptr, MessageTypeID.MT_MyTradeRecordResponse_ID, masterlogin);

            if (transType == TransType.TRANS_DELETE && (ptrTrade.cmd == (int)TradeCommand.OP_BUY || ptrTrade.cmd == (int)TradeCommand.OP_SELL || ptrTrade.cmd == (int)TradeCommand.OP_BALANCE))
            {
                if (serverIndex == SRV_TYPE_DEMO)
                {
                    await _tradeRecordService.InsertDemoCloseTrades(ptrTrade, cancellationToken);
                }
                else
                {
                    await _tradeRecordService.InsertLiveCloseTrades(ptrTrade, cancellationToken);
                }
                if (ptrTrade.profit < 0)
                {
                    //m_ptrMySqlWrapper.getSSPSignal(ptrTrade.login, out var symbolIndex, out var strategyType);
                    var result = await _signalService.GetSSPSignalShort(ptrTrade.login, cancellationToken);
                    if (result != null)
                    {
                        var symbolIndex = result.Value.symbolIndex;
                        var strategyType = result.Value.strategyType;
                        SignalSymbolSetting symbolSettings = await _signalSymbolSettingService.GetSignalSymbolSettingAsync(symbolIndex, strategyType, cancellationToken);

                        if (symbolSettings != null)
                        {
                            double lossinpips = 0.0;
                            if (CppHelper.strstr(ptrTrade.symbol, "JPY"))
                            {
                                lossinpips = CppHelper.abs(ptrTrade.open_price - ptrTrade.close_price) * 100;
                            }
                            else
                                lossinpips = CppHelper.abs(ptrTrade.open_price - ptrTrade.close_price) * 10000;
                            if (lossinpips > symbolSettings._maxLossInPips)
                            {
                                var startTime = ptrTrade.close_time.Value;
                                //tm* tmTime = localtime(&startTime);
                                //tmTime.tm_hour = tmTime.tm_hour + symbolSettings._blockTimeInHrs;
                                DateTimeOffset endTime = startTime.AddHours(symbolSettings._blockTimeInHrs);
                                await _tradeDisableService.InsertTradeDisableRow(masterlogin, ptrTrade.login, serverIndex, (int)ptrTrade.cmd, startTime, endTime, cancellationToken);
                                //send update to service
                                var tradingptr = new TradeDisableResponse(); // MT_TradeDisableResponse_ID
                                tradingptr._serverTime = DateTimeOffset.UtcNow;
                                MT4TradeDisableInfo ptrInfo = null;
                                ptrInfo = (MT4TradeDisableInfo)new MT4TradeDisableInfo();
                                //memset(ptrInfo, 0, sizeof(MT4TradeDisableInfo));
                                ptrInfo._masterLogin = masterlogin;
                                ptrInfo._mt4ServerIndex = serverIndex;
                                ptrInfo._mt4Login = ptrTrade.login;
                                ptrInfo._isDisabled = true;
                                CppHelper.memcpy(ref tradingptr._tradeDisableInfo, ptrInfo);
                                _connectionMgr.SentDataUsingLoginID(tradingptr, MessageTypeID.MT_TradeDisableResponse_ID, masterlogin);
                            }
                        }//if (blocktime != 0)
                    }//if (symbolIndex != 0)
                }//if (ptrTrade.profit < 0)
            }
        }
    }
}
