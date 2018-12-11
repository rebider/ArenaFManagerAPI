using System;

namespace WitFX.MT4
{
    public interface CManagerFactory : IDisposable
    {
        int WinsockStartup();
        bool IsValid();
        CManagerInterface Create();
    }
}
