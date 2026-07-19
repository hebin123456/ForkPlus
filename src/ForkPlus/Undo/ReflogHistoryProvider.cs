using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Undo
{
	/// <summary>
	/// v3.3.0：读取 git reflog 并解析为历史列表。
	///
	/// reflog 是 git 原生持久化的（.git/logs/HEAD），默认保留 90 天。
	/// 用作 Undo/Redo 的真相源：跨会话保留 + CLI 操作天然兼容 + 无栈深度限制。
	///
	/// 输出格式：索引 0 = 最近一次操作，索引 N = N 步之前的操作（与 git reflog HEAD@{N} 一致）。
	/// v3.4.0：新增 TimestampUtc 字段，供 Reflog 视图显示时间。
	/// </summary>
	public class ReflogHistoryProvider
	{
		/// <summary>默认读取的最大 reflog 条目数（防止超大 reflog 拖慢 UI）。</summary>
		public const int DefaultMaxCount = 200;

		/// <summary>
		/// 读取 HEAD reflog 列表。失败时返回空列表（永不抛出）。
		/// 输出顺序：索引 0 = 最近一次，索引 N = N 步前。
		/// </summary>
		public List<ReflogEntry> ReadHeadReflog(GitModule gitModule, int maxCount = DefaultMaxCount)
		{
			List<ReflogEntry> result = new List<ReflogEntry>();
			if (gitModule == null || maxCount <= 0)
			{
				return result;
			}
			try
			{
				// 用 %x00 (NUL) 分隔字段，避免 commit message 含换行干扰解析
				// 字段顺序：%H（sha） %x00 %gs（reflog subject） %x00 %s（commit subject） %x00 %ci（committer date iso）
				GitRequestResult r = new GitRequest(gitModule)
					.Command("reflog", "HEAD", "--pretty=format:%H%x00%gs%x00%s%x00%ci", $"--max-count={maxCount}")
					.Execute(silent: true);
				if (!r.Success || string.IsNullOrEmpty(r.Stdout))
				{
					return result;
				}
				// reflog 输出每行一条；commit subject 不会跨行（被 %s 限制为单行 subject）
				string[] lines = r.Stdout.Split(Consts.Chars.NewLine);
				for (int i = 0; i < lines.Length; i++)
				{
					ReflogEntry entry = ParseLine(lines[i], i);
					if (entry != null)
					{
						result.Add(entry);
					}
				}
			}
			catch
			{
				// 静默：reflog 读取失败不阻断 Undo/Redo
			}
			return result;
		}

		/// <summary>解析单行 reflog 输出。失败返回 null。internal 供单元测试直接调用。</summary>
		internal static ReflogEntry ParseLine(string line, int index)
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				return null;
			}
			string[] parts = line.Split('\0');
			if (parts.Length < 1 || parts[0].Length != 40)
			{
				return null;
			}
			ReflogEntry entry = new ReflogEntry
			{
				Sha = parts[0],
				ReflogSubject = parts.Length > 1 ? parts[1] : "",
				CommitSubject = parts.Length > 2 ? parts[2] : "",
				Index = index
			};
			// v3.4.0：解析 %ci（committer date iso，形如 "2026-07-19 10:00:00 +0800"）
			if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
			{
				entry.TimestampUtc = ParseIsoDate(parts[3]);
			}
			return entry;
		}

		/// <summary>
		/// 解析 git %ci 输出（形如 "2026-07-19 10:00:00 +0800"）为 UTC DateTime。
		/// 失败返回 null。
		/// </summary>
		private static DateTime? ParseIsoDate(string s)
		{
			try
			{
				// DateTime.Parse 支持 "yyyy-MM-dd HH:mm:ss zz" 格式
				DateTime local = DateTime.Parse(s, null, System.Globalization.DateTimeStyles.AssumeLocal);
				return local.ToUniversalTime();
			}
			catch
			{
				return null;
			}
		}
	}

	/// <summary>reflog 单条记录。</summary>
	public sealed class ReflogEntry
	{
		/// <summary>commit sha（40 字符）。</summary>
		public string Sha { get; set; }

		/// <summary>reflog subject，例如 "commit: fix: bug" / "reset: moving to HEAD~1" / "checkout: moving from main to feature/x"。</summary>
		public string ReflogSubject { get; set; }

		/// <summary>commit 自身的 subject（first line）。checkout/reset 类操作可能为空。</summary>
		public string CommitSubject { get; set; }

		/// <summary>reflog 索引（HEAD@{N}）。0 = 最近一次。</summary>
		public int Index { get; set; }

		/// <summary>v3.4.0：reflog 记录时间（UTC）。可能为 null（解析失败或老格式）。</summary>
		public DateTime? TimestampUtc { get; set; }
	}
}
