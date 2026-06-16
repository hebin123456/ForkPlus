using System;

namespace ForkPlus.Git
{
    public static class UnixTime
    {
        public static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static bool TryParseUnixDate(string date, out DateTime result)
        {
            if (long.TryParse(date, out long unixSeconds))
            {
                result = UnixStartTime.AddSeconds(unixSeconds);
                return true;
            }
            result = DateTime.MinValue;
            return false;
        }
    }
}
