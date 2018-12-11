using System;
using System.Diagnostics;
using WitFX.Backend.Infrastructure.Logging;
using WitFX.MT4.Server.Implementation.Helpers;

namespace WitFX.MT4.Server.Implementation
{
    internal sealed class ServerLogger
    {
        private readonly ILogger _logger;
        private readonly string _prefix;

        public ServerLogger(ILogger logger, string prefix)
        {
            Debug.Assert(logger != null);
            Debug.Assert(!string.IsNullOrEmpty(prefix));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger;
            _prefix = prefix;
        }

        private string GetPrefix()
        {
            var callStack = new StackTrace();
            var frame = callStack.GetFrame(2);
            return _prefix + "." + frame.GetMethod().Name + ": ";
        }

        public void LogInfo(string message, params object[] args)
            => _logger.LogInfo(GetPrefix() + CppHelper.PrintF(message, args));

        public void LogWarning(string message, params object[] args)
            => _logger.LogWarning(GetPrefix() + CppHelper.PrintF(message, args));

        public void LogError(string message, Exception exception)
            => _logger.LogError(GetPrefix() + message, exception);

        public void LogError(string message, params object[] args)
            => _logger.LogError(GetPrefix() + CppHelper.PrintF(message, args));

        public void LogException(Exception exception)
            => _logger.LogError(GetPrefix() + exception.GetType().Name + ": " + exception.Message, exception);

        public void LogOk(string message, params object[] args)
            => ((TextLogger)_logger).Log("OK", GetPrefix() + CppHelper.PrintF(message, args));

        public void LogTrade(string message, params object[] args)
            => ((TextLogger)_logger).Log("TRADE", GetPrefix() + CppHelper.PrintF(message, args));
    }
}
