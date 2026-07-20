using System.Collections.Generic;

namespace ForkPlus.Git.Diff.Parsing.Tokens
{
	public static class StringHelper
	{
		public static Range[] LineRanges(this string text, bool includeEmptyLine = false)
		{
			List<Range> list = new List<Range>(1024);
			int num = 0;
			int num2 = 0;
			int length = text.Length;
			while (num2 < length)
			{
				if (text[num2] == '\n')
				{
					num2++;
					list.Add(new Range(num, num2));
					num = num2;
				}
				else
				{
					num2++;
				}
			}
			if (num != num2 || includeEmptyLine)
			{
				list.Add(new Range(num, num2));
			}
			return list.ToArray();
		}
	}
}
