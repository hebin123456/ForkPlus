using System.Collections.Generic;
using System.Text;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff.Parsing.Tokens;

namespace ForkPlus.Git.Diff.Parsing
{
	public class PatchParser
	{
		public GitCommandResult<Patch> Parse(string input, string srcPrefix = "forkSrcPrefix/", string dstPrefix = "forkDstPrefix/")
		{
			byte[] bytes = Encoding.UTF8.GetBytes(input);
			byte[] bytes2 = Encoding.UTF8.GetBytes(srcPrefix);
			byte[] bytes3 = Encoding.UTF8.GetBytes(dstPrefix);
			GitCommandResult<Token[]> gitCommandResult = ReadTokens(bytes, bytes2, bytes3);
			if (!gitCommandResult.Succeeded)
			{
				return null;
			}
			Token[] result = gitCommandResult.Result;
			List<Diff> list = new List<Diff>();
			for (int i = 0; i < result.Length; i++)
			{
				Token token = result[i];
				if (token.Type == TokenType.DiffHeaderTitle)
				{
					GitCommandResult<Diff> gitCommandResult2 = ParseDiff(bytes, result, ref i);
					if (!gitCommandResult2.Succeeded)
					{
						return GitCommandResult<Patch>.Failure(gitCommandResult2.Error);
					}
					list.Add(gitCommandResult2.Result);
				}
			}
			return GitCommandResult<Patch>.Success(new Patch(list.ToArray()));
		}

		private static GitCommandResult<Token[]> ReadTokens(byte[] diffUtf8, byte[] srcPrefixUtf8, byte[] dstPrefixUtf8)
		{
			return BtRequest.Run(() => default(BtParsePatchResult), delegate(ref BtParsePatchResult x)
			{
				return Bt.bt_parse_patch(diffUtf8, (ulong)diffUtf8.Length, srcPrefixUtf8, (ulong)srcPrefixUtf8.Length, dstPrefixUtf8, (ulong)dstPrefixUtf8.Length, ref x);
			}, delegate(ref BtParsePatchResult x)
			{
				return Into(ref x);
			}, delegate(ref BtParsePatchResult x)
			{
				Bt.bt_release_parse_patch(ref x);
			});
		}

		private static GitCommandResult<Token[]> Into(ref BtParsePatchResult btParsePatchResult)
		{
			return GitCommandResult<Token[]>.Success(btParsePatchResult.tokens.GetStructArray(btParsePatchResult.tokens_len, delegate(BtPatchToken btPatchToken)
			{
				byte kind = btPatchToken.kind;
				Range range = new Range((int)btPatchToken.start, (int)btPatchToken.end);
				return new Token((TokenType)kind, range);
			}));
		}

		private GitCommandResult<Diff> ParseDiff(byte[] diffUtf8, Token[] tokens, ref int cursor)
		{
			bool flag = false;
			string text = null;
			string text2 = null;
			FileMode? fileMode = null;
			FileMode? fileMode2 = null;
			string text3 = null;
			string text4 = null;
			int? similarity = null;
			List<string> list = new List<string>();
			List<Chunk> list2 = new List<Chunk>();
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			bool flag5 = false;
			for (; cursor < tokens.Length; cursor++)
			{
				Token token = tokens[cursor];
				switch (token.Type)
				{
				case TokenType.DiffHeaderTitle:
					if (flag)
					{
						cursor--;
						break;
					}
					flag = true;
					continue;
				case TokenType.DiffHeaderTitleSrcPath:
					text = Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length);
					continue;
				case TokenType.DiffHeaderTitleDstPath:
					text2 = Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length);
					continue;
				case TokenType.DiffHeaderDeletedFileMode:
				{
					flag2 = true;
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out var result3))
					{
						fileMode = new FileMode(result3);
						if (fileMode == FileMode.Submodule)
						{
							flag5 = true;
						}
						continue;
					}
					return GitCommandResult<Diff>.Failure(new GitCommandError.ParseError("Cannot parse deleted file mode in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				}
				case TokenType.DiffHeaderNewFileMode:
				{
					flag3 = true;
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out var result))
					{
						fileMode2 = new FileMode(result);
						if (fileMode2 == FileMode.Submodule)
						{
							flag5 = true;
						}
						continue;
					}
					return GitCommandResult<Diff>.Failure(new GitCommandError.ParseError("Cannot parse new file mode in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				}
				case TokenType.DiffHeaderOldMode:
				{
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out var result5))
					{
						fileMode = new FileMode(result5);
						if (fileMode == FileMode.Submodule)
						{
							flag5 = true;
						}
						continue;
					}
					return GitCommandResult<Diff>.Failure(new GitCommandError.ParseError("Cannot parse old mode in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				}
				case TokenType.DiffHeaderNewMode:
				{
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out var result6))
					{
						fileMode2 = new FileMode(result6);
						if (fileMode2 == FileMode.Submodule)
						{
							flag5 = true;
						}
						continue;
					}
					return GitCommandResult<Diff>.Failure(new GitCommandError.ParseError("Cannot parse new mode in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				}
				case TokenType.DiffHeaderIndexSrcObject:
					text3 = Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length);
					if (text3 == null || text3.Length != 40)
					{
						Log.Warn("Incomplete tree sha in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length));
					}
					continue;
				case TokenType.DiffHeaderIndexDstObject:
					text4 = Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length);
					if (text4 == null || text4.Length != 40)
					{
						Log.Warn("Incomplete tree sha in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length));
					}
					continue;
				case TokenType.DiffHeaderIndexFileMode:
				{
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out var result4))
					{
						fileMode = new FileMode(result4);
						fileMode2 = new FileMode(result4);
						if (fileMode == FileMode.Submodule)
						{
							flag5 = true;
						}
						continue;
					}
					return GitCommandResult<Diff>.Failure(new GitCommandError.ParseError("Cannot parse index file mode in:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				}
				case TokenType.DiffHeaderSimilarity:
				{
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out var result2))
					{
						similarity = result2;
					}
					continue;
				}
				case TokenType.BinaryFilesDiffer:
					flag4 = true;
					continue;
				case TokenType.ChunkHeaderTitle:
				{
					GitCommandResult<Chunk> gitCommandResult = ParseChunk(diffUtf8, tokens, list, ref cursor);
					if (!gitCommandResult.Succeeded)
					{
						return GitCommandResult<Diff>.Failure(gitCommandResult.Error);
					}
					list2.Add(gitCommandResult.Result);
					continue;
				}
				default:
					cursor--;
					break;
				case TokenType.DiffHeaderCopyFrom:
				case TokenType.DiffHeaderCopyTo:
				case TokenType.DiffHeaderRenameFrom:
				case TokenType.DiffHeaderRenameTo:
				case TokenType.AddedLine:
				case TokenType.DeletedLine:
				case TokenType.Other:
					continue;
				}
				break;
			}
			if (text == null || text2 == null)
			{
				return GitCommandResult<Diff>.Failure(new GitCommandError.ParseError("File patch parsing failed! File path not found"));
			}
			string oldFilepath = text;
			string newFilepath = text2;
			if (flag3)
			{
				oldFilepath = null;
			}
			else if (flag2)
			{
				newFilepath = null;
			}
			Chunk[] chunks = list2.ToArray();
			Diff.FileType type = (flag5 ? Diff.FileType.Submodule : ((!flag4) ? Diff.FileType.Text : Diff.FileType.Binary));
			bool isMinified = AreChunksMinified(chunks, list);
			return GitCommandResult<Diff>.Success(new Diff(oldFilepath, newFilepath, fileMode, fileMode2, text3, text4, list.ToArray(), chunks, similarity, type, isMinified));
		}

		private static bool AreChunksMinified(Chunk[] chunks, List<string> lines)
		{
			int num = 2048;
			for (int i = 0; i < chunks.Length; i++)
			{
				SubChunk[] subChunks = chunks[i].SubChunks;
				foreach (SubChunk subChunk in subChunks)
				{
					for (int k = subChunk.PreContext.Start; k < subChunk.PreContext.End; k++)
					{
						if (lines[k].Length >= num)
						{
							return true;
						}
					}
					for (int l = subChunk.Deleted.Start; l < subChunk.Deleted.End; l++)
					{
						if (lines[l].Length >= num)
						{
							return true;
						}
					}
					for (int m = subChunk.Added.Start; m < subChunk.Added.End; m++)
					{
						if (lines[m].Length >= num)
						{
							return true;
						}
					}
					for (int n = subChunk.PostContext.Start; n < subChunk.PostContext.End; n++)
					{
						if (lines[n].Length >= num)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private GitCommandResult<Chunk> ParseChunk(byte[] diffUtf8, Token[] tokens, List<string> lines, ref int cursor)
		{
			int result = -1;
			int result2 = 1;
			int result3 = -1;
			int result4 = 1;
			string contextString = null;
			for (; IsChunkToken(tokens[cursor].Type); cursor++)
			{
				Token token = tokens[cursor];
				switch (tokens[cursor].Type)
				{
				case TokenType.ChunkHeaderOldStart:
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out result))
					{
						continue;
					}
					return GitCommandResult<Chunk>.Failure(new GitCommandError.ParseError("Cannot parse old start in chunk header:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				case TokenType.ChunkHeaderOldLength:
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out result2))
					{
						continue;
					}
					return GitCommandResult<Chunk>.Failure(new GitCommandError.ParseError("Cannot parse old length in chunk header:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				case TokenType.ChunkHeaderNewStart:
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out result3))
					{
						continue;
					}
					return GitCommandResult<Chunk>.Failure(new GitCommandError.ParseError("Cannot parse new start in chunk header:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				case TokenType.ChunkHeaderNewLength:
					if (TryParseInt(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length), out result4))
					{
						continue;
					}
					return GitCommandResult<Chunk>.Failure(new GitCommandError.ParseError("Cannot parse new length in chunk header:\n" + Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length)));
				case TokenType.ChunkHeaderContext:
					if (tokens[cursor].Range.Start == tokens[cursor].Range.End)
					{
						return GitCommandResult<Chunk>.Failure(new GitCommandError.ParseError("Parsed empty header title"));
					}
					contextString = Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length);
					continue;
				case TokenType.ChunkHeaderTitle:
					continue;
				}
				break;
			}
			if (result == -1 || result3 == -1)
			{
				return GitCommandResult<Chunk>.Failure(new GitCommandError.ParseError("Could not parse old length and new length in chunk header"));
			}
			List<SubChunk> list = new List<SubChunk>();
			while (cursor < tokens.Length)
			{
				Token token2 = tokens[cursor];
				TokenType type = token2.Type;
				if (type - 22 <= TokenType.DiffHeaderTitleDstPath)
				{
					GitCommandResult<SubChunk> gitCommandResult = ParseSubChunk(diffUtf8, tokens, lines, ref cursor);
					list.Add(gitCommandResult.Result);
					cursor++;
					continue;
				}
				cursor--;
				break;
			}
			return GitCommandResult<Chunk>.Success(new Chunk(result, result2, result3, result4, contextString, list.ToArray()));
		}

		private bool IsChunkToken(TokenType tokenType)
		{
			return tokenType switch
			{
				TokenType.ChunkHeaderTitle => true, 
				TokenType.ChunkHeaderOldStart => true, 
				TokenType.ChunkHeaderOldLength => true, 
				TokenType.ChunkHeaderNewStart => true, 
				TokenType.ChunkHeaderNewLength => true, 
				TokenType.ChunkHeaderContext => true, 
				_ => false, 
			};
		}

		private GitCommandResult<SubChunk> ParseSubChunk(byte[] diffUtf8, Token[] tokens, List<string> lines, ref int cursor)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			NoNewLineAtEndOfFile noNewLineAtEndOfFile = NoNewLineAtEndOfFile.None;
			int count = lines.Count;
			for (; cursor < tokens.Length; cursor++)
			{
				Token token = tokens[cursor];
				switch (token.Type)
				{
				case TokenType.ContextLine:
					if (num3 == 0 && num2 == 0)
					{
						lines.Add(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length));
						num++;
					}
					else
					{
						lines.Add(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length));
						num4++;
					}
					continue;
				case TokenType.DeletedLine:
					if (num4 != 0)
					{
						cursor--;
						break;
					}
					lines.Add(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length));
					num2++;
					continue;
				case TokenType.AddedLine:
					if (num4 != 0)
					{
						cursor--;
						break;
					}
					lines.Add(Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length));
					num3++;
					continue;
				case TokenType.Pragma:
					if (Encoding.UTF8.GetString(diffUtf8, token.Range.Start, token.Range.Length).StartsWith("No newline at end of file"))
					{
						if (num4 > 0)
						{
							noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Context;
						}
						else if (num3 > 0)
						{
							noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Added;
						}
						else if (num2 > 0)
						{
							noNewLineAtEndOfFile |= NoNewLineAtEndOfFile.Deleted;
						}
					}
					continue;
				default:
					cursor--;
					break;
				}
				break;
			}
			Range preContext = new Range(count, count + num);
			Range deleted = new Range(preContext.End, preContext.End + num2);
			Range added = new Range(deleted.End, deleted.End + num3);
			return GitCommandResult<SubChunk>.Success(new SubChunk(postContext: new Range(added.End, added.End + num4), preContext: preContext, deleted: deleted, added: added, noNewLineAtEndOfFile: noNewLineAtEndOfFile));
		}

		private static bool TryParseInt(string input, out int result)
		{
			if (input == "")
			{
				result = 0;
				return true;
			}
			return int.TryParse(input, out result);
		}
	}
}
