using System;

namespace WitFX.MT4.Server.Implementation.Utilities
{
    internal static class Common
    {
        internal static double NormalizeDouble(double val, int digits)
        {
            return Math.Round(val, digits);
            //if (digits < 0) digits = 0;
            //if (digits > 8) digits = 8;
            ////----
            //const double p = ExtDecimalArray[digits];
            //return ((val >= 0.0) ? (double(__int64(val * p + 0.5000001)) / p) : (double(__int64(val * p - 0.5000001)) / p));
        }
    }
}
