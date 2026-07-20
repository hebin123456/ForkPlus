using System;
using Xunit;

namespace ForkPlus.Tests
{
	public class DateTimeExtensionsTests
	{
		[Fact]
		public void UnixStartTime_YearIs1970()
		{
			Assert.Equal(1970, DateTimeExtensions.UnixStartTime.Year);
		}

		[Fact]
		public void TimeIntervalSince1970_UnixStartTimeReturnsZero()
		{
			Assert.Equal(0, DateTimeExtensions.UnixStartTime.TimeIntervalSince1970());
		}

		[Fact]
		public void TimeIntervalSince1970_OneSecondLaterReturnsOne()
		{
			DateTime dt = DateTimeExtensions.UnixStartTime.AddSeconds(1);
			Assert.Equal(1, dt.TimeIntervalSince1970());
		}

		[Fact]
		public void TimeIntervalSince1970_KnownTimestamp()
		{
			DateTime dt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			Assert.Equal(1577836800, dt.TimeIntervalSince1970());
		}

		[Fact]
		public void MillisecondsSince1970_UnixStartTimeReturnsZero()
		{
			Assert.Equal(0L, DateTimeExtensions.UnixStartTime.MillisecondsSince1970());
		}

		[Fact]
		public void MillisecondsSince1970_OneMillisecondLaterReturnsOne()
		{
			DateTime dt = DateTimeExtensions.UnixStartTime.AddMilliseconds(1);
			Assert.Equal(1L, dt.MillisecondsSince1970());
		}
	}
}
