using System;
using System.Diagnostics;

namespace WitFX.MT4
{
    public static class MT4Helper
    {
        public const uint USER_COLOR_NONE = 0xFF000000;
        public const int MAX_SEC_GROUPS = 32;

        #region Time

        private static readonly TimeZoneInfo _mt4Zone =
            TimeZoneInfo.FindSystemTimeZoneById("E. Europe Standard Time");

        public static DateTimeOffset GetMt4ZeroUtc()
        {
            Debug.Assert(_mt4Zone != null);
            var offset = _mt4Zone.GetUtcOffset(DateTime.UtcNow);
            return new DateTimeOffset(1970, 1, 1, 0, 0, 0, offset).ToUniversalTime();
        }

        public static DateTimeOffset? FromMT4Time(long mt4Time)
        {
            if (mt4Time == 0)
                return null;

            return GetMt4ZeroUtc().AddSeconds(mt4Time);
        }

        public static long ToMT4Time(DateTimeOffset? dateTime)
        {
            if (dateTime == null)
                return 0;

            return (long)(dateTime.Value - GetMt4ZeroUtc()).TotalSeconds;
        }

        #endregion
    }
}
