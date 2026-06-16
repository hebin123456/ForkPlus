using System;
using ForkPlus.Settings;

namespace ForkPlus.Git
{
	public class DateTimeHelper
	{
		public static readonly DateTime UnixStartTime = ForkPlus.Git.UnixTime.UnixStartTime;

		public static bool TryParseUnixDate(string date, out DateTime result)
		{
			if (long.TryParse(date, out var result2))
			{
				result = UnixStartTime.AddSeconds(result2).ToLocalTime();
				return true;
			}
			result = default(DateTime);
			return false;
		}

		public static string ToRelativeString(DateTime dateTime)
		{
			TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks - dateTime.Ticks);
			double num = Math.Abs(timeSpan.TotalSeconds);
			if (num < 60.0)
			{
				if (timeSpan.Seconds != 1)
				{
					return FormatRelative(timeSpan.Seconds, "seconds ago", "秒前", "秒前");
				}
				return FormatSingularRelative("one second ago", "1 秒前", "1 秒前");
			}
			if (num < 120.0)
			{
				return FormatSingularRelative("a minute ago", "1 分钟前", "1 分鐘前");
			}
			if (num < 2700.0)
			{
				return FormatRelative(timeSpan.Minutes, "minutes ago", "分钟前", "分鐘前");
			}
			if (num < 5400.0)
			{
				return FormatSingularRelative("an hour ago", "1 小时前", "1 小時前");
			}
			if (num < 86400.0)
			{
				return FormatRelative(timeSpan.Hours, "hours ago", "小时前", "小時前");
			}
			if (num < 172800.0)
			{
				return FormatSingularRelative("yesterday", "昨天", "昨天");
			}
			if (num < 2592000.0)
			{
				return FormatRelative(timeSpan.Days, "days ago", "天前", "天前");
			}
			if (num < 31104000.0)
			{
				int num2 = Convert.ToInt32(Math.Floor((double)timeSpan.Days / 30.0));
				if (num2 > 1)
				{
					return FormatRelative(num2, "months ago", "个月前", "個月前");
				}
				return FormatSingularRelative("one month ago", "1 个月前", "1 個月前");
			}
			int num3 = Convert.ToInt32(Math.Floor((double)timeSpan.Days / 365.0));
			if (num3 > 1)
			{
				return FormatRelative(num3, "years ago", "年前", "年前");
			}
			return FormatSingularRelative("one year ago", "1 年前", "1 年前");
		}

		private static string FormatRelative(int value, string englishSuffix, string simplifiedSuffix, string traditionalSuffix)
		{
			if (IsSimplifiedChinese())
			{
				return value + " " + simplifiedSuffix;
			}
			if (IsTraditionalChinese())
			{
				return value + " " + traditionalSuffix;
			}
			return value + " " + englishSuffix;
		}

		private static string FormatSingularRelative(string english, string simplified, string traditional)
		{
			if (IsSimplifiedChinese())
			{
				return simplified;
			}
			if (IsTraditionalChinese())
			{
				return traditional;
			}
			return english;
		}

		private static bool IsSimplifiedChinese()
		{
			return ForkPlusSettings.Default.UiLanguage == "zh-Hans";
		}

		private static bool IsTraditionalChinese()
		{
			return ForkPlusSettings.Default.UiLanguage == "zh-Hant";
		}
	}
}
