using System.Linq;

namespace ForkPlus.UI.Dialogs
{
	internal static class GitMmCommandPreviewHelper
	{
		public static string Format(string[] args)
		{
			return "git mm " + string.Join(" ", args.Select(QuoteIfNeeded));
		}

		private static string QuoteIfNeeded(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return "\"\"";
			}
			if (value.IndexOfAny(new char[2] { ' ', '\t' }) < 0)
			{
				return value;
			}
			return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		}
	}
}
