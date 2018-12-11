using System;
using System.Diagnostics;

namespace WitFX.MT4
{
    public static class ManagerHost
    {
        public static IManagerHost Current;

        public static CManagerFactory CreateFactory(Action<Exception> exceptionHandler)
        {
            Debug.Assert(Current != null);

            if (Current == null)
                throw new InvalidOperationException("MT4 infrastructure is not initialized");

            if (exceptionHandler == null)
                throw new ArgumentNullException(nameof(exceptionHandler));

            return Current.CreateFactory(exceptionHandler);
        }
    }

    public interface IManagerHost
    {
        CManagerFactory CreateFactory(Action<Exception> exceptionHandler);
    }
}
