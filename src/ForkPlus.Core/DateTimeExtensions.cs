using System;

namespace ForkPlus
{
	public static class DateTimeExtensions
	{
		public static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public static int TimeIntervalSince1970(this DateTime value)
		{
			return (int)(value - UnixStartTime).TotalSeconds;
		}

		public static long MillisecondsSince1970(this DateTime value)
		{
			return (long)(value - UnixStartTime).TotalMilliseconds;
		}
	}
}
