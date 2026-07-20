namespace ForkPlus
{
	public static class StringExtensions
	{
		[Null]
		public static string Abbreviated(this string input, int length = 7)
		{
			if (input.Length < length)
			{
				return input;
			}
			return input.Substring(0, length);
		}

		public static string Quotify(this string input)
		{
			return "\"" + (input ?? "").Replace("\"", "\\\"") + "\"";
		}

		public static string EscapeSpaces(this string input)
		{
			return input.Replace(" ", "\\ ").Replace("(", "\\(").Replace(")", "\\)")
				.Replace("$", "\\$");
		}

		public static string EscapeQuotes(this string input)
		{
			return input.Replace("\"", "\\\"");
		}

		public static string Substring(this string input, Range range)
		{
			return input.Substring(range.Start, range.Length);
		}

		public static string Replace(this string input, Range range, string value)
		{
			return input.Remove(range.Start, range.Length).Insert(range.Start, value);
		}

		public static string TrimStart(this string input, string prefix)
		{
			if (input.StartsWith(prefix))
			{
				return input.Substring(prefix.Length);
			}
			return input;
		}

		public static string TrimEnd(this string input, string suffix)
		{
			if (input.EndsWith(suffix))
			{
				return input.Substring(0, input.Length - suffix.Length);
			}
			return input;
		}
	}
}
