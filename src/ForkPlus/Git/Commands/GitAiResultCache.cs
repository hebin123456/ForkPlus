using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// git-ai 查询结果的进程内 LRU 缓存。
	/// <para>
	/// 每次 git-ai 调用都要 spawn 进程（首次还要冷启动 daemon），秒级耗时；
	/// 而同一提交的行级归属、同一区间的统计结果是 immutable 的，缓存命中可做到零开销。
	/// 这直接消除两类重复开销：
	/// </para>
	/// <list type="bullet">
	/// <item>同一提交反复打开 Blame 不再重复跑 git-ai diff</item>
	/// <item>每次打开统计页 / 切回同一区间不再重复跑 git-ai stats</item>
	/// </list>
	/// <para>
	/// 失败结果一律不缓存——可能是 daemon 冷启动等瞬时原因，值得下次重试；
	/// 成功的空结果（仓库未使用 git-ai、提交无 AI 代码）会缓存（负缓存），
	/// 避免在没用 git-ai 的仓库上每次都白跑一遍。
	/// </para>
	/// </summary>
	internal static class GitAiResultCache
	{
		/// <summary>每张缓存表的最大条目数。归属/统计对象都不大，64 条足够覆盖常用的浏览回溯。</summary>
		private const int MaxEntries = 64;

		private static readonly object Lock = new object();

		private static readonly LinkedList<Entry> DiffEntries = new LinkedList<Entry>();

		private static readonly LinkedList<Entry> StatsEntries = new LinkedList<Entry>();

		private sealed class Entry
		{
			public string Key;

			public object Value;
		}

		/// <summary>取缓存的 git-ai diff 归属。未命中返回 null。</summary>
		[Null]
		public static GitAiDiffAttribution GetDiffAttribution(string repoPath, string sha)
		{
			return (GitAiDiffAttribution)Get(DiffEntries, BuildKey(repoPath, sha));
		}

		/// <summary>缓存 git-ai diff 归属（含空结果）。</summary>
		public static void PutDiffAttribution(string repoPath, string sha, GitAiDiffAttribution value)
		{
			if (value != null)
			{
				Put(DiffEntries, BuildKey(repoPath, sha), value);
			}
		}

		/// <summary>取缓存的 git-ai 统计。未命中返回 null。</summary>
		[Null]
		public static GitAiStats GetStats(string repoPath, string revSpec)
		{
			return (GitAiStats)Get(StatsEntries, BuildKey(repoPath, revSpec));
		}

		/// <summary>缓存 git-ai 统计。</summary>
		public static void PutStats(string repoPath, string revSpec, GitAiStats value)
		{
			if (value != null)
			{
				Put(StatsEntries, BuildKey(repoPath, revSpec), value);
			}
		}

		[Null]
		private static object Get(LinkedList<Entry> entries, string key)
		{
			lock (Lock)
			{
				for (LinkedListNode<Entry> node = entries.First; node != null; node = node.Next)
				{
					if (string.Equals(node.Value.Key, key, StringComparison.Ordinal))
					{
						// 命中后移到队首（LRU）
						if (node != entries.First)
						{
							entries.Remove(node);
							entries.AddFirst(node);
						}
						return node.Value.Value;
					}
				}
			}
			return null;
		}

		private static void Put(LinkedList<Entry> entries, string key, object value)
		{
			lock (Lock)
			{
				for (LinkedListNode<Entry> node = entries.First; node != null; node = node.Next)
				{
					if (string.Equals(node.Value.Key, key, StringComparison.Ordinal))
					{
						node.Value.Value = value;
						if (node != entries.First)
						{
							entries.Remove(node);
							entries.AddFirst(node);
						}
						return;
					}
				}
				entries.AddFirst(new Entry
				{
					Key = key,
					Value = value
				});
				while (entries.Count > MaxEntries)
				{
					entries.RemoveLast();
				}
			}
		}

		/// <summary>缓存键：仓库路径（忽略大小写，Windows 路径大小写可能不一致）+ 仓库内唯一标识（sha / revSpec）。</summary>
		private static string BuildKey(string repoPath, string id)
		{
			return (repoPath ?? "").ToLowerInvariant() + "\n" + (id ?? "");
		}
	}
}
