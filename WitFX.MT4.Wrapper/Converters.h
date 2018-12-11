#pragma once

#include "Stdafx.h"

inline __time32_t ToMT4Time(Nullable<DateTimeOffset> managed)
{
	return (__time32_t)WitFX::MT4::MT4Helper::ToMT4Time(managed);
}

inline Nullable<DateTimeOffset>FromMT4Time(__time32_t mt4Time)
{
	return WitFX::MT4::MT4Helper::FromMT4Time(mt4Time);
}

#define TO_MANAGED(FIELD) managed->FIELD = native->FIELD;
#define TO_MANAGED_STRING(FIELD) managed->FIELD = marshal_as<String^, char*>(native->FIELD);
#define TO_MANAGED_TIME(FIELD) managed->FIELD = FromMT4Time(native->FIELD);

#define TO_NATIVE(FIELD) native->FIELD = managed->FIELD;
#define TO_NATIVE_CAST(FIELD,TYPE) native->FIELD = (TYPE)managed->FIELD;
#define TO_NATIVE_CHARS(FIELD) String^ FIELD = managed->FIELD; if (FIELD != nullptr) memcpy(native->FIELD, marshal_as<std::string, String^>(FIELD).c_str(), sizeof(native->FIELD));
#define TO_NATIVE_TIME(FIELD) native->FIELD = ToMT4Time(managed->FIELD);

template<typename T>
array<T>^ CreateArrayOrEmpty(int count)
{
	if (!count)
		return Array::Empty<T>();

	return gcnew array<T>(count);
}

MANAGED ConSymbol^ ToManagedConSymbol(NATIVE ConSymbol* native)
{
	auto managed = gcnew MANAGED ConSymbol();
	TO_MANAGED_STRING(symbol);
	TO_MANAGED_STRING(description);
	TO_MANAGED_STRING(source);
	TO_MANAGED_STRING(currency);
	TO_MANAGED(type);
	TO_MANAGED(digits);
	TO_MANAGED(trade);
	// background_color
	TO_MANAGED(count);
	TO_MANAGED(count_original);
	// external_unused
	TO_MANAGED(realtime);
	TO_MANAGED_TIME(starting);
	TO_MANAGED_TIME(expiration);
	//sessions 
	TO_MANAGED(profit_mode);
	TO_MANAGED(profit_reserved);
	TO_MANAGED(filter);
	TO_MANAGED(filter_counter);
	TO_MANAGED(filter_limit);
	TO_MANAGED(filter_smoothing);
	TO_MANAGED(filter_reserved);
	TO_MANAGED(logging);
	TO_MANAGED(spread);
	TO_MANAGED(spread_balance);
	TO_MANAGED(exemode);
	TO_MANAGED(swap_enable);
	TO_MANAGED(swap_type);
	TO_MANAGED(swap_long);
	TO_MANAGED(swap_short);
	TO_MANAGED(swap_rollover3days);
	TO_MANAGED(contract_size);
	TO_MANAGED(tick_value);
	TO_MANAGED(tick_size);
	TO_MANAGED(stops_level);
	TO_MANAGED(gtc_pendings);
	TO_MANAGED(margin_mode);
	TO_MANAGED(margin_initial);
	TO_MANAGED(margin_maintenance);
	TO_MANAGED(margin_hedged);
	TO_MANAGED(margin_divider);
	TO_MANAGED(point);
	TO_MANAGED(multiply);
	TO_MANAGED(bid_tickvalue);
	TO_MANAGED(ask_tickvalue);
	TO_MANAGED(long_only);
	TO_MANAGED(instant_max_volume);
	TO_MANAGED_STRING(margin_currency);
	TO_MANAGED(freeze_level);
	TO_MANAGED(margin_hedged_strong);
	TO_MANAGED_TIME(value_date);
	TO_MANAGED(quotes_delay);
	TO_MANAGED(swap_openprice);
	TO_MANAGED(swap_variation_margin);
	//unused[21];     
	return managed;
}

MANAGED UserRecord^ ToManagedUserRecord(NATIVE UserRecord* native)
{
	auto managed = gcnew MANAGED UserRecord();
	TO_MANAGED(login);
	TO_MANAGED_STRING(group);
	TO_MANAGED_STRING(password);
	TO_MANAGED(enable);
	TO_MANAGED(enable_change_password);
	TO_MANAGED(enable_read_only);
	TO_MANAGED(enable_otp);
	//enable_reserved
	TO_MANAGED_STRING(password_investor);
	TO_MANAGED_STRING(password_phone);
	TO_MANAGED_STRING(name);
	TO_MANAGED_STRING(country);
	TO_MANAGED_STRING(city);
	TO_MANAGED_STRING(state);
	TO_MANAGED_STRING(zipcode);
	TO_MANAGED_STRING(address);
	TO_MANAGED_STRING(lead_source);
	TO_MANAGED_STRING(phone);
	TO_MANAGED_STRING(email);
	TO_MANAGED_STRING(comment);
	TO_MANAGED_STRING(id);
	TO_MANAGED_STRING(status);
	TO_MANAGED_TIME(regdate);
	TO_MANAGED_TIME(lastdate);
	TO_MANAGED(leverage);
	TO_MANAGED(agent_account);
	TO_MANAGED_TIME(timestamp);
	TO_MANAGED(last_ip);
	TO_MANAGED(balance);
	TO_MANAGED(prevmonthbalance);
	TO_MANAGED(prevbalance);
	TO_MANAGED(credit);
	TO_MANAGED(interestrate);
	TO_MANAGED(taxes);
	TO_MANAGED(prevmonthequity);
	TO_MANAGED(prevequity);
	//reserved2
	TO_MANAGED_STRING(otp_secret);
	TO_MANAGED_STRING(secure_reserved);
	TO_MANAGED(send_reports);
	TO_MANAGED(mqid);
	TO_MANAGED(user_color);
	//unused
	//api_data  
	return managed;
}

void ToNativeUserRecord(MANAGED UserRecord^ managed, NATIVE UserRecord* native)
{
	memset(native, 0, sizeof(NATIVE UserRecord));
	TO_NATIVE(login);
	TO_NATIVE_CHARS(group);
	TO_NATIVE_CHARS(password);
	TO_NATIVE(enable);
	TO_NATIVE(enable_change_password);
	TO_NATIVE(enable_read_only);
	TO_NATIVE(enable_otp);
	//enable_reserved
	TO_NATIVE_CHARS(password_investor);
	TO_NATIVE_CHARS(password_phone);
	TO_NATIVE_CHARS(name);
	TO_NATIVE_CHARS(country);
	TO_NATIVE_CHARS(city);
	TO_NATIVE_CHARS(state);
	TO_NATIVE_CHARS(zipcode);
	TO_NATIVE_CHARS(address);
	TO_NATIVE_CHARS(lead_source);
	TO_NATIVE_CHARS(phone);
	TO_NATIVE_CHARS(email);
	TO_NATIVE_CHARS(comment);
	TO_NATIVE_CHARS(id);
	TO_NATIVE_CHARS(status);
	TO_NATIVE_TIME(regdate);
	TO_NATIVE_TIME(lastdate);
	TO_NATIVE(leverage);
	TO_NATIVE(agent_account);
	TO_NATIVE_TIME(timestamp);
	TO_NATIVE(last_ip);
	TO_NATIVE(balance);
	TO_NATIVE(prevmonthbalance);
	TO_NATIVE(prevbalance);
	TO_NATIVE(credit);
	TO_NATIVE(interestrate);
	TO_NATIVE(taxes);
	TO_NATIVE(prevmonthequity);
	TO_NATIVE(prevequity);
	//reserved2
	TO_NATIVE_CHARS(otp_secret);
	TO_NATIVE_CHARS(secure_reserved);
	TO_NATIVE(send_reports);
	TO_NATIVE(mqid);
	TO_NATIVE_CAST(user_color, COLORREF);
	//unused
	//api_data  
}

MANAGED TradeRecord^ ToManagedTradeRecord(NATIVE TradeRecord* native)
{
	auto managed = gcnew MANAGED TradeRecord();
	TO_MANAGED(order);               
	TO_MANAGED(login);               
	TO_MANAGED_STRING(symbol);
	TO_MANAGED(digits);              
	TO_MANAGED(cmd);                 
	TO_MANAGED(volume);              
	TO_MANAGED_TIME(open_time);
	TO_MANAGED(state);                
	TO_MANAGED(open_price);           
	TO_MANAGED(sl);     
	TO_MANAGED(tp);
	TO_MANAGED_TIME(close_time);
	TO_MANAGED(gw_volume);            
	TO_MANAGED_TIME(expiration);
	TO_MANAGED(reason);
	//conv_reserv
	//conv_rates
	TO_MANAGED(commission);           
	TO_MANAGED(commission_agent);      
	TO_MANAGED(storage);              
	TO_MANAGED(close_price);          
	TO_MANAGED(profit);              
	// profitinpips : - this is not a native MT$'s property. this is high-level property. and this prop initialized only at reading from db in MYSQLWRAPPER class.
	TO_MANAGED(taxes);                
	TO_MANAGED(magic);                
	TO_MANAGED_STRING(comment);
	TO_MANAGED(gw_order);            
	TO_MANAGED(activation);            
	TO_MANAGED(gw_open_price);        
	TO_MANAGED(gw_close_price);      
	TO_MANAGED(margin_rate);         
	TO_MANAGED_TIME(timestamp);
	//api_data
	//next;  
	return managed;
}

MANAGED SymbolInfo^ ToManagedSymbolInfo(NATIVE SymbolInfo* native)
{
	auto managed = gcnew MANAGED SymbolInfo();
	TO_MANAGED_STRING(symbol);                
	TO_MANAGED(digits);               
	TO_MANAGED(count);                
	TO_MANAGED(visible);             
	TO_MANAGED(type);                
	TO_MANAGED(point);               
	TO_MANAGED(spread);               
	TO_MANAGED(spread_balance);      
	TO_MANAGED(direction);            
	TO_MANAGED(updateflag);          
	TO_MANAGED_TIME(lasttime);            
	TO_MANAGED(bid); 
	TO_MANAGED(ask); 
	TO_MANAGED(high); 
	TO_MANAGED(low);
	TO_MANAGED(commission);          
	TO_MANAGED(comm_type);
	return managed;
}

void ToNativeTradeTransInfo(MANAGED TradeTransInfo^ managed, NATIVE TradeTransInfo* native)
{
	memset(native, 0, sizeof(NATIVE TradeTransInfo));
	TO_NATIVE_CAST(type, UCHAR);
	TO_NATIVE(flags);
	TO_NATIVE(cmd);
	TO_NATIVE(order);
	TO_NATIVE(orderby);
	TO_NATIVE_CHARS(symbol);
	TO_NATIVE(volume);
	TO_NATIVE(price);
	TO_NATIVE(sl);
	TO_NATIVE(tp);
	TO_NATIVE(ie_deviation);
	TO_NATIVE_CHARS(comment);
	TO_NATIVE_TIME(expiration);
	TO_NATIVE(crc);
}

MANAGED ConSymbolGroup^ ToManagedConSymbolGroup(NATIVE ConSymbolGroup* native)
{
	auto managed = gcnew MANAGED ConSymbolGroup();
	TO_MANAGED_STRING(name);
	TO_MANAGED_STRING(description);
	return managed;
}

MANAGED MarginLevel^ ToManagedMarginLevel(NATIVE MarginLevel* native)
{
	auto managed = gcnew MANAGED MarginLevel();
	TO_MANAGED(login);
	TO_MANAGED_STRING(group);
	TO_MANAGED(leverage);
	TO_MANAGED(updated);
	TO_MANAGED(balance);
	TO_MANAGED(equity);
	TO_MANAGED(volume);
	TO_MANAGED(margin);
	TO_MANAGED(margin_free);
	TO_MANAGED(margin_level);
	TO_MANAGED(margin_type);
	TO_MANAGED(level_type);
	return managed;
}
