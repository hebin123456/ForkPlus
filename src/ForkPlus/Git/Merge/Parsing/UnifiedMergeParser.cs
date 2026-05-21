using System.Collections.Generic;
using ForkPlus.Git.Merge.Parsing.Tokenizer;

namespace ForkPlus.Git.Merge.Parsing
{
	public class UnifiedMergeParser
	{
		private static readonly UnifiedMergeTokenizer _tokenizer = new UnifiedMergeTokenizer();

		public bool TryParse(string filePath, string input, bool noNewLineAtEndOfFile, out MergeConflict result)
		{
			MergeToken[] tokens = _tokenizer.GetTokens(input);
			if (tokens == null)
			{
				Log.Error("Cannot create tokens for '" + filePath + "'");
				result = null;
				return false;
			}
			List<MergeConflict.Chunk> list = new List<MergeConflict.Chunk>();
			for (int i = 0; i < tokens.Length; i++)
			{
				if (!TryParseChunk(tokens, ref i, out var result2))
				{
					result = null;
					return false;
				}
				list.Add(result2);
			}
			result = new MergeConflict(filePath, list.ToArray(), noNewLineAtEndOfFile);
			return true;
		}

		private bool TryParseChunk(MergeToken[] tokens, ref int cursor, out MergeConflict.Chunk result)
		{
			while (cursor < tokens.Length)
			{
				MergeToken mergeToken = tokens[cursor];
				if (mergeToken is ContextToken contextToken)
				{
					if (contextToken.RemoteType == ContextType.None && contextToken.LocalType == ContextType.None)
					{
						return ParseContextChunk(tokens, ref cursor, out result);
					}
					return ParseChangeChunk(tokens, ref cursor, out result);
				}
				if (mergeToken is ConflictStartToken)
				{
					return ParseConflictChunk(tokens, ref cursor, out result);
				}
				if (!(mergeToken is UnknownToken))
				{
					Log.Error("Unexpected token '" + mergeToken.GetType().Name + "'");
					result = null;
					return false;
				}
				cursor++;
			}
			result = null;
			return false;
		}

		private static bool ParseContextChunk(MergeToken[] tokens, ref int cursor, out MergeConflict.Chunk result)
		{
			List<MergeConflict.Line> list = new List<MergeConflict.Line>();
			while (cursor < tokens.Length)
			{
				if (tokens[cursor] is ContextToken contextToken)
				{
					if (contextToken.RemoteType == ContextType.None && contextToken.LocalType == ContextType.None)
					{
						list.Add(new MergeConflict.Line(ContextType.None, contextToken.ContextString));
						cursor++;
						continue;
					}
					cursor--;
					break;
				}
				if (list.Count == 0)
				{
					Log.Error("Unexpected call of ParseContextChunk");
					result = null;
					return false;
				}
				cursor--;
				break;
			}
			result = new MergeConflict.ContextChunk(list.ToArray());
			return true;
		}

		private static bool ParseChangeChunk(MergeToken[] tokens, ref int cursor, out MergeConflict.Chunk result)
		{
			List<MergeConflict.SelectableLine> list = new List<MergeConflict.SelectableLine>();
			List<MergeConflict.SelectableLine> list2 = new List<MergeConflict.SelectableLine>();
			while (cursor < tokens.Length)
			{
				if (tokens[cursor] is ContextToken contextToken)
				{
					if (contextToken.LocalType == ContextType.Add)
					{
						list2.Add(new MergeConflict.SelectableLine(MergeConflictPart.Remote, contextToken.LocalType, contextToken.ContextString));
					}
					else if (contextToken.RemoteType == ContextType.Remove)
					{
						list2.Add(new MergeConflict.SelectableLine(MergeConflictPart.Remote, contextToken.RemoteType, contextToken.ContextString));
					}
					else if (contextToken.LocalType == ContextType.Remove)
					{
						list.Add(new MergeConflict.SelectableLine(MergeConflictPart.Local, contextToken.LocalType, contextToken.ContextString));
					}
					else
					{
						if (contextToken.RemoteType != ContextType.Add)
						{
							cursor--;
							break;
						}
						list.Add(new MergeConflict.SelectableLine(MergeConflictPart.Local, contextToken.RemoteType, contextToken.ContextString));
					}
					cursor++;
					continue;
				}
				if (list2.Count == 0 && list.Count == 0)
				{
					Log.Error("Unexpected call of ParseChangeChunk");
					result = null;
					return false;
				}
				cursor--;
				break;
			}
			result = new MergeConflict.ChangeChunk(list.ToArray(), list2.ToArray(), selectAddedLines: true);
			return true;
		}

		private static bool ParseConflictChunk(MergeToken[] tokens, ref int cursor, out MergeConflict.Chunk result)
		{
			string remoteName = "";
			string localName = "";
			List<MergeConflict.SelectableLine> list = new List<MergeConflict.SelectableLine>();
			List<MergeConflict.SelectableLine> list2 = new List<MergeConflict.SelectableLine>();
			bool flag = false;
			bool flag2 = false;
			while (cursor < tokens.Length)
			{
				MergeToken mergeToken = tokens[cursor];
				if (mergeToken is ConflictStartToken conflictStartToken)
				{
					if (list.Count != 0 || list2.Count != 0)
					{
						Log.Error("Conflict parsing error. Unexpected conflict start");
						result = null;
						return false;
					}
					localName = conflictStartToken.LocalName.TrimEnd();
				}
				else if (mergeToken is ContextToken contextToken)
				{
					if (flag)
					{
						if (contextToken.LocalType != 0)
						{
							list.Add(new MergeConflict.SelectableLine(MergeConflictPart.Remote, contextToken.RemoteType, contextToken.ContextString));
						}
					}
					else if (!flag2 && contextToken.RemoteType != 0)
					{
						list2.Add(new MergeConflict.SelectableLine(MergeConflictPart.Local, contextToken.LocalType, contextToken.ContextString));
					}
				}
				else if (mergeToken is ConflictSeparatorToken)
				{
					if (list.Count != 0 || flag)
					{
						Log.Error("Conflict parsing error.Unexpected conflict separator");
						result = null;
						return false;
					}
					if (list2.Count == 0)
					{
						list2.Add(new MergeConflict.EmptyLine(MergeConflictPart.Local));
					}
					flag = true;
				}
				else if (mergeToken is BaseStartToken)
				{
					if (list.Count != 0 || flag)
					{
						Log.Error("Conflict parsing error.Unexpected base start");
						result = null;
						return false;
					}
					flag2 = true;
				}
				else if (mergeToken is BaseEndToken)
				{
					flag2 = false;
				}
				else if (mergeToken is ConflictEndToken conflictEndToken)
				{
					if (list.Count == 0 && list2.Count == 0)
					{
						Log.Error("Conflict parsing error.Unexpected conflict end");
						result = null;
						return false;
					}
					remoteName = conflictEndToken.RemoteName.TrimEnd();
					if (list.Count == 0)
					{
						list.Add(new MergeConflict.EmptyLine(MergeConflictPart.Remote));
					}
					break;
				}
				cursor++;
			}
			result = new MergeConflict.ConflictChunk(remoteName, list.ToArray(), localName, list2.ToArray());
			return true;
		}
	}
}
