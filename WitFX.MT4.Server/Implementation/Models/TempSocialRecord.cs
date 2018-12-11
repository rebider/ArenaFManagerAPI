namespace WitFX.MT4.Server.Implementation.Models
{
    public sealed class TempSocialRecord
    {
        public uint transid;
        public uint signalTransid;
        public int signalIndex;
        public int signalMasterLogin;
        public int signalMT4Login;
        public int signalOrderID;
        public bool isSSP;
        public bool isTraderServerDemo;
        public int traderOrderid;
        public int traderMT4Login;
        public int traderMasterLogin;
    }
}
