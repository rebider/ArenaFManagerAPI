using System;

namespace WitFX.MT4
{
    //+------------------------------------------------------------------+
    //| Symbol groups                                                    |
    //+------------------------------------------------------------------+
    public sealed class ConSymbolGroup
    {
        public string name;                  // group name
        public string description;           // group description
    };

    //+------------------------------------------------------------------+
    //| Selected symbol information                                      |
    //+------------------------------------------------------------------+
    public sealed class SymbolInfo
    {
        public string symbol;                // symbol name
        public int digits;                // floating point digits
        public int count;                 // symbol counter
        public int visible;               // visibility
                                          //--- äàííûå äëÿ ïåðåðàñ÷¸òà ñïðåäà
        public int type;                  // symbol type (symbols group index)
        public double point;                 // symbol point=1/pow(10,digits)
        public int spread;                // symbol spread
        public int spread_balance;        // spread balance
                                          //---
        public int direction;             // direction
        public int updateflag;            // update flag
        public DateTimeOffset? lasttime;              // last tick time
        public double bid, ask;              // bid, ask
        public double high, low;             // high, low
        public double commission;            // commission
        public int comm_type;             // commission type
    };

    //+------------------------------------------------------------------+
    //| Trade Record                                                     |
    //+------------------------------------------------------------------+
    public sealed class TradeRecord
    {
        public int order;                 // order ticket
        public int login;                 // owner's login
        public string symbol;                // security
        public int digits;                // security precision
        public int cmd;                   // trade command
        public int volume;                // volume
                                          //---
        public DateTimeOffset? open_time;             // open time
        public int state;                 // reserved
        public double open_price;            // open price
        public double sl, tp;                // stop loss & take profit
        public DateTimeOffset? close_time;            // close time
        public int gw_volume;             // gateway order volume
        public DateTimeOffset? expiration;            // pending order's expiration time
        public char reason;                // trade reason
                                           //char              conv_reserv[3];		   // reserved fields
                                           //double            conv_rates[2];		   // convertation rates from profit currency to group deposit currency
                                           // (first element-for open time, second element-for close time)
        public double commission;            // commission
        public double commission_agent;      // agent commission
        public double storage;               // order swaps
        public double close_price;           // close price
        public double profit;                // profit
        public double profitinpips;
        public double taxes;                 // taxes
        public int magic;                 // special value used by client experts
        public string comment;               // comment
        public int gw_order;              // gateway order ticket
        public int activation;            // used by MT Manager
        public short gw_open_price;         // gateway order price deviation (pips) from order open price
        public short gw_close_price;        // gateway order price deviation (pips) from order close price
        public double margin_rate;           // margin convertation rate (rate of convertation from margin currency to deposit one)
        public DateTimeOffset? timestamp;             // timestamp
                                                     //int               api_data[4];           // for api usage
                                                     //TradeRecord *__ptr32 next;               // internal data
    };

    //+------------------------------------------------------------------+
    //| Margin level of the user                                         |
    //+------------------------------------------------------------------+
    public sealed class MarginLevel
    {
        public int login;            // user login
        public string group;            // user group
        public int leverage;         // user leverage
        public int updated;          // (internal)
        public double balance;          // balance+credit
        public double equity;           // equity
        public int volume;           // lots
        public double margin;           // margin requirements
        public double margin_free;      // free margin
        public double margin_level;     // margin level
        public int margin_type;      // margin controlling type (percent/currency)
        public int level_type;       // level type(ok/margincall/stopout)
    };

    //+------------------------------------------------------------------+
    //| Symbol configuration                                             |
    //+------------------------------------------------------------------+
    public sealed class ConSymbol
    {
        //--- common settings
        public string symbol;                      // name
        public string description;                 // description
        public string source;                      // synonym
        public string currency;                    // currency
        public int type;                        // security group (see ConSymbolGroup)
        public int digits;                      // security precision
        public int trade;                       // trade mode
                                                //--- external settings
                                                //COLORREF          background_color;            // background color
        public int count;                       // symbols index
        public int count_original;              // symbols index in market watch
                                                //int               external_unused[7];
                                                //--- sessions
        public int realtime;                    // allow real time quotes
        public DateTimeOffset? starting;                    // trades starting date (UNIX time)
        public DateTimeOffset? expiration;                  // trades end date      (UNIX time)
                                                           //ConSessions       sessions[7];                 // quote & trade sessions
                                                           //--- profits
        public int profit_mode;                 // profit calculation mode
        public int profit_reserved;             // reserved
                                                //--- filtration
        public int filter;                      // filter value
        public int filter_counter;              // filtration parameter
        public double filter_limit;                // max. permissible deviation from last quote (percents)
        public int filter_smoothing;            // smoothing
        public float filter_reserved;             // reserved
        public int logging;                     // enable to log quotes
                                                //--- spread & swaps
        public int spread;                      // spread
        public int spread_balance;              // spread balance
        public int exemode;                     // execution mode
        public int swap_enable;                 // enable swaps
        public int swap_type;                   // swap type
        public double swap_long, swap_short;        // swaps values for long & short postions
        public int swap_rollover3days;          // triple rollover day-0-Monday,1-Tuesday...4-Friday
        public double contract_size;               // contract size
        public double tick_value;                  // one tick value
        public double tick_size;                   // one tick size
        public int stops_level;                 // stops deviation value
                                                //---            îâåðíàéòû è ïðî÷èå ñâîïû
        public int gtc_pendings;                // GTC mode { ORDERS_DAILY, ORDERS_GTC, ORDERS_DAILY_NO_STOPS }
                                                //--- margin calculation
        public int margin_mode;                 // margin calculation mode
        public double margin_initial;              // initial margin
        public double margin_maintenance;          // margin maintenance
        public double margin_hedged;               // hedged margin
        public double margin_divider;              // margin divider
                                                   //--- calclulated variables (internal data)
        public double point;                       // point size-(1/(10^digits)
        public double multiply;                    // multiply 10^digits
        public double bid_tickvalue;               // tickvalue for bid
        public double ask_tickvalue;               // tickvalue for ask
                                                   //---
        public int long_only;                   // allow only BUY positions
        public int instant_max_volume;          // max. volume for Instant Execution
                                                //---
        public string margin_currency;             // currency of margin requirments
        public int freeze_level;                // modification freeze level
        public int margin_hedged_strong;        // strong hedged margin mode
        public DateTimeOffset? value_date;                  // value date
        public int quotes_delay;                // quotes delay after session start
        public int swap_openprice;                // use open price at swaps calculation in SWAP_BY_INTEREST mode
        public int swap_variation_margin;       // charge variation margin on rollover
                                                //---
                                                //int               unused[21];             	  // reserved
    };

    //+------------------------------------------------------------------+
    //| User Record                                                      |
    //+------------------------------------------------------------------+
    public sealed class UserRecord
    {
        //--- common settings
        public int login;                      // login
        public string group;                  // group
        public string password;               // password
                                              //--- access flags
        public bool enable;                     // enable
        public bool enable_change_password;     // allow to change password
        public bool enable_read_only;           // allow to open/positions (TRUE-may not trade)
        public bool enable_otp;                 // allow to use one-time password
                                                //int               enable_reserved[2];         // for future use
                                                //---
        public string password_investor;      // read-only mode password
        public string password_phone;         // phone password
        public string name;                   // name
        public string first_name;             // first name
        public string last_name;              // last name
        public string referee_login_id;       // referee's login id
        public string country;                // country
        public string city;                   // city
        public string state;                  // state
        public string zipcode;                // zipcode
        public string address;                // address
        public string lead_source;            // lead source
        public string phone;                  // phone
        public string email;                  // email
        public string comment;                // comment
        public string id;                     // SSN (IRD)
        public string status;                 // status
        public DateTimeOffset? regdate;                    // registration date
        public DateTimeOffset? lastdate;                   // last connection time
                                                          //--- trade settings
        public int leverage;                   // leverage
        public int agent_account;              // agent account
        public DateTimeOffset? timestamp;                  // timestamp
        public int last_ip;                    // last visit ip
                                               //---            òîðãîâûå äàííûå
        public double balance;                    // balance
        public double prevmonthbalance;           // previous month balance
        public double prevbalance;                // previous day balance
        public double credit;                     // credit
        public double interestrate;               // accumulated interest rate
        public double taxes;                      // taxes
        public double prevmonthequity;            // previous month equity
        public double prevequity;                 // previous day equity
                                                  //double            reserved2[2];               // for future use
                                                  //---
        public string otp_secret;             // one-time password secret
        public string secure_reserved;       // secure data reserved
        public bool send_reports;               // enable send reports by email
        public uint mqid;                       // MQ client identificator
        public ulong user_color;                 // color got to client (used by MT Manager)
                                                 //---
                                                 //char              unused[40];                 // for future use
                                                 //char              api_data[16];               // for API usage
    };

    //+------------------------------------------------------------------+
    //| Trade transaction                                                |
    //+------------------------------------------------------------------+
    public sealed class TradeTransInfo
    {
        public TradeTransType type;             // trade transaction type
        public byte flags;            // flags
        public short cmd;              // trade command
        public int order, orderby;   // order, order by
        public string symbol;           // trade symbol
        public int volume;           // trade volume
        public double price;            // trade price
        public double sl, tp;           // stoploss, takeprofit
        public int ie_deviation;     // deviation on IE
        public string comment;          // comment
        public DateTimeOffset? expiration;       // pending order expiration time
        public int crc;              // crc
    };
}
