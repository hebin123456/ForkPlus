using System.Text.RegularExpressions;

namespace ForkPlus
{
	public static class RegexExtensions
	{
		[Null]
		public static Match FirstMatch(this Regex regex, string input)
		{
			Match match = regex.Match(input);
			if (match.Success)
			{
				return match;
			}
			return null;
		}
	}
}
