using System;
using WitFX.Backend.Infrastructure.Logging;

namespace WitFX.MT4.Server.Implementation
{
    public class ServerConsoleLogger : ConsoleLogger
    {
        protected override ConsoleColor GetColor(string level)
        {
            switch (level)
            {
                case "OK":
                    return ConsoleColor.DarkGray;
                case "TRADE":
                    return ConsoleColor.Green;
                default:
                    return base.GetColor(level);
            }
        }
    }
}
