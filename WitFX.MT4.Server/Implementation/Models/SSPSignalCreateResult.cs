using WitFX.Contracts;

namespace WitFX.MT4.Server.Implementation.Models
{
    public sealed class SSPSignalCreateResult
    {
        public MT4Request MT4Request;
        public Signal Signal;
        public SSPSignal SSPSignal;
    }
}
