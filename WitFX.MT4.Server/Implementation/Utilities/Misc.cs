using WitFX.MT4.Server.Implementation.Helpers;

namespace WitFX.MT4.Server.Implementation.Utilities
{
    internal static class Misc
    {
        internal static double round_off(double raw, int @decimal)
        {
            if (@decimal > 0)
            {
                if (@decimal < 10)
                {
                    double decfactor = CppHelper.pow((double)10, (double)@decimal);
                    double @base = CppHelper.floor(raw * decfactor) / decfactor;
                    double rest = raw - @base;
                    double round_v = rest >= 0.5 / decfactor ? 1.0 / decfactor : 0.0;

                    return @base + round_v;
                }
            }
            return raw;
        }
    }
}
