using System;
using System.Text.RegularExpressions;

namespace ForkPlus
{
	public class BugtrackerLinkDefinition
	{
		public string Name { get; }

		public string RegexString { get; }

		public Level Level { get; }

		public Regex Regex { get; }

		public string UrlString { get; }

		[Null]
		public static BugtrackerLinkDefinition Create(string name, Level level, string regexString, string urlString)
		{
			try
			{
				Regex regex = new Regex(regexString, RegexOptions.Multiline);
				return new BugtrackerLinkDefinition(name, level, regexString, regex, urlString);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to parse bugtrack link definition", ex);
				return null;
			}
		}

		private BugtrackerLinkDefinition(string name, Level level, string regexString, Regex regex, string urlString)
		{
			Name = name ?? "";
			UrlString = urlString ?? "";
			RegexString = regexString;
			Regex = regex;
			Level = level;
		}

		public bool BugtrackerEquals(BugtrackerLinkDefinition bugtracker)
		{
			if (Name == bugtracker.Name && RegexString == bugtracker.RegexString && Level == bugtracker.Level)
			{
				return UrlString == bugtracker.UrlString;
			}
			return false;
		}
	}
}
