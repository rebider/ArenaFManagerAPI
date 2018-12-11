namespace WitFX.MT4
{
    public enum ReturnCode
    {
        //--- common errors
        RET_OK = 0,        // all OK
        RET_OK_NONE,                     // all OK-no operation
        RET_ERROR,                       // general error
        RET_INVALID_DATA,                // invalid data
        RET_TECH_PROBLEM,                // server technical problem
        RET_OLD_VERSION,                 // old client terminal
        RET_NO_CONNECT,                  // no connection
        RET_NOT_ENOUGH_RIGHTS,           // no enough rights
        RET_TOO_FREQUENT,                // too frequently access to server
        RET_MALFUNCTION,                 // mulfunctional operation
        RET_GENERATE_KEY,                // need to send public key
        RET_SECURITY_SESSION,            // security session start
                                         //--- account status
        RET_ACCOUNT_DISABLED = 64,       // account blocked
        RET_BAD_ACCOUNT_INFO,            // bad account info
        RET_PUBLIC_KEY_MISSING,          // îòñóòñòâóå?êëþ÷
                                         //--- trade
        RET_TRADE_TIMEOUT = 128,      // trade transatcion timeou expired
        RET_TRADE_BAD_PRICES,            // order has wrong prices
        RET_TRADE_BAD_STOPS,             // wrong stops level
        RET_TRADE_BAD_VOLUME,            // wrong lot size
        RET_TRADE_MARKET_CLOSED,         // market closed
        RET_TRADE_DISABLE,               // trade disabled
        RET_TRADE_NO_MONEY,              // no enough money for order execution
        RET_TRADE_PRICE_CHANGED,         // price changed
        RET_TRADE_OFFQUOTES,             // no quotes
        RET_TRADE_BROKER_BUSY,           // broker is busy
        RET_TRADE_REQUOTE,               // requote
        RET_TRADE_ORDER_LOCKED,          // order is proceed by dealer and cannot be changed
        RET_TRADE_LONG_ONLY,             // allowed only BUY orders
        RET_TRADE_TOO_MANY_REQ,          // too many requests from one client
                                         //--- order status notification
        RET_TRADE_ACCEPTED,              // trade request accepted by server and placed in request queue
        RET_TRADE_PROCESS,               // trade request accepted by dealerd
        RET_TRADE_USER_CANCEL,           // trade request canceled by client
                                         //--- additional return codes
        RET_TRADE_MODIFY_DENIED,         // trade modification denied
        RET_TRADE_CONTEXT_BUSY,          // trade context is busy (used in client terminal)
        RET_TRADE_EXPIRATION_DENIED,     // using expiration date denied
        RET_TRADE_TOO_MANY_ORDERS,       // too many orders
        RET_TRADE_HEDGE_PROHIBITED,      // hedge is prohibited
        RET_TRADE_PROHIBITED_BY_FIFO     // prohibited by fifo rule
    };

    public enum TradeTransType
    {
        //---
        TT_PRICES_GET,                      // prices requets
        TT_PRICES_REQUOTE,                  // requote
                                            //--- client trade transaction
        TT_ORDER_IE_OPEN = 64,                // open order (Instant Execution)
        TT_ORDER_REQ_OPEN,                  // open order (Request Execution)
        TT_ORDER_MK_OPEN,                   // open order (Market Execution)
        TT_ORDER_PENDING_OPEN,              // open pending order
                                            //---
        TT_ORDER_IE_CLOSE,                  // close order (Instant Execution)
        TT_ORDER_REQ_CLOSE,                 // close order (Request Execution)
        TT_ORDER_MK_CLOSE,                  // close order (Market Execution)
                                            //---
        TT_ORDER_MODIFY,                    // modify pending order
        TT_ORDER_DELETE,                    // delete pending order
        TT_ORDER_CLOSE_BY,                  // close order by order
        TT_ORDER_CLOSE_ALL,                 // close all orders by symbol
                                            //--- broker trade transactions
        TT_BR_ORDER_OPEN,                   // open order
        TT_BR_ORDER_CLOSE,                  // close order
        TT_BR_ORDER_DELETE,                 // delete order (ANY OPEN ORDER!!!)
        TT_BR_ORDER_CLOSE_BY,               // close order by order
        TT_BR_ORDER_CLOSE_ALL,              // close all orders by symbol
        TT_BR_ORDER_MODIFY,                 // modify open price, stoploss, takeprofit etc. of order
        TT_BR_ORDER_ACTIVATE,               // activate pending order
        TT_BR_ORDER_COMMENT,                // modify comment of order
        TT_BR_BALANCE                       // balance/credit
    };

    //--- trade commands
    public enum TradeCommand { OP_BUY = 0, OP_SELL, OP_BUY_LIMIT, OP_SELL_LIMIT, OP_BUY_STOP, OP_SELL_STOP, OP_BALANCE, OP_CREDIT };

    //+------------------------------------------------------------------+
    //| Transaction types                                                |
    //+------------------------------------------------------------------+
    public enum TransType { TRANS_ADD, TRANS_DELETE, TRANS_UPDATE, TRANS_CHANGE_GRP };

    //+------------------------------------------------------------------+
    //| Pumping notification codes                                       |
    //+------------------------------------------------------------------+
    public enum PumpCode
    {
        PUMP_START_PUMPING = 0,      // pumping started
        PUMP_UPDATE_SYMBOLS,       // update symbols
        PUMP_UPDATE_GROUPS,        // update groups
        PUMP_UPDATE_USERS,         // update users
        PUMP_UPDATE_ONLINE,        // update online users
        PUMP_UPDATE_BIDASK,        // update bid/ask
        PUMP_UPDATE_NEWS,          // update news
        PUMP_UPDATE_NEWS_BODY,     // update news body
        PUMP_UPDATE_MAIL,          // update news
        PUMP_UPDATE_TRADES,        // update trades
        PUMP_UPDATE_REQUESTS,      // update trade requests
        PUMP_UPDATE_PLUGINS,       // update server plugins
        PUMP_UPDATE_ACTIVATION,    // new order for activation (sl/sp/stopout)
        PUMP_UPDATE_MARGINCALL,    // new margin calls
        PUMP_STOP_PUMPING,         // pumping stopped
        PUMP_PING,                 // ping
        PUMP_UPDATE_NEWS_NEW,      // update news in new format (NewsTopicNew structure)
    };
}
