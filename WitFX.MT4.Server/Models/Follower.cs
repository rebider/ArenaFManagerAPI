using WitFX.Contracts;

namespace WitFX.MT4.Server.Models
{
    public sealed class Follower
    {
        public int MasterLogin;
        public eMT4ServerType ServerIndex;
        public int MT4Login;
    }
}
