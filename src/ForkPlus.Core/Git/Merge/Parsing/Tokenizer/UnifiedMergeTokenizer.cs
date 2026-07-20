using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Merge.Parsing.Tokenizer
{
	public class UnifiedMergeTokenizer
	{
		private static readonly char NewLineSymbol = '\n';

		public MergeToken[] GetTokens(string input)
		{
			List<MergeToken> list = new List<MergeToken>(1024);
			int num = 0;
			bool flag = false;
			while (num < input.Length)
			{
				Range range = ReadString(input, num);
				string text = input.Substring(range);
				if (text.StartsWith("++<<<<<<<"))
				{
					string text2 = ParseConflictTitle(text);
					if (text2 == null)
					{
						return null;
					}
					list.Add(new ConflictStartToken(range, text2));
					num = range.End;
				}
				else if (text.StartsWith("++======="))
				{
					list.Add(new ConflictSeparatorToken(range));
					num = range.End;
				}
				else if (text.StartsWith("++>>>>>>>"))
				{
					string text3 = ParseConflictTitle(text);
					if (text3 == null)
					{
						return null;
					}
					list.Add(new ConflictEndToken(range, text3));
					num = range.End;
				}
				else if (text.StartsWith("++|||||||"))
				{
					string text4 = ParseBaseSha(text);
					if (text4 == null)
					{
						return null;
					}
					list.Add(new BaseStartToken(range, text4));
					num = range.End;
				}
				else if (text.StartsWith("++======="))
				{
					list.Add(new BaseEndToken(range));
					num = range.End;
				}
				else if (IsContextLine(text) && flag)
				{
					if (!ParseContextLine(text, out var remote, out var local, out var contextString))
					{
						return null;
					}
					list.Add(new ContextToken(range, remote, local, contextString));
					num = range.End;
				}
				else
				{
					if (text.StartsWith("@@@"))
					{
						flag = true;
					}
					list.Add(new UnknownToken(range));
					num = range.End;
				}
			}
			return list.ToArray();
		}

		private static bool ParseContextLine(string input, out ContextType remote, out ContextType local, out string contextString)
		{
			if (input.Length < 2)
			{
				remote = ContextType.None;
				local = ContextType.None;
				contextString = null;
				Log.Error("Cannot parse context line in '" + input + "'");
				return false;
			}
			remote = ParseChangeType(input[0]);
			local = ParseChangeType(input[1]);
			contextString = input.Substring(2);
			return true;
		}

		private static ContextType ParseChangeType(char character)
		{
			return character switch
			{
				'-' => ContextType.Remove, 
				'+' => ContextType.Add, 
				' ' => ContextType.None, 
				_ => throw new InvalidOperationException(), 
			};
		}

		private static bool IsContextLine(string line)
		{
			if (line.Length < 2)
			{
				return false;
			}
			char c = line[0];
			if (c != ' ' && c != '+' && c != '-')
			{
				return false;
			}
			char c2 = line[1];
			if (c2 != ' ' && c2 != '+' && c2 != '-')
			{
				return false;
			}
			return true;
		}

		private static string ParseConflictTitle(string line)
		{
			int num = 10;
			if (line.Length < num)
			{
				Log.Error("Cannot parse conflict title in '" + line + "'");
				return null;
			}
			return line.Substring(num);
		}

		private static string ParseBaseSha(string line)
		{
			int num = 10;
			if (line.Length < num)
			{
				Log.Error("Cannot parse base sha in '" + line + "'");
				return null;
			}
			return line.Substring(num);
		}

		private static Range ReadString(string input, int start)
		{
			int i;
			for (i = start; i < input.Length; i++)
			{
				if (input[i] == NewLineSymbol)
				{
					return new Range(start, i + 1);
				}
			}
			return new Range(start, i);
		}
	}
}
