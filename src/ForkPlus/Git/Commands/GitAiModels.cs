using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// git-ai agent 标识（agent_id）：生成代码的工具、会话 id 与模型。
	/// </summary>
	public class GitAiAgentId
	{
		public string Tool { get; }

		public string Id { get; }

		public string Model { get; }

		public GitAiAgentId(string tool, string id, string model)
		{
			Tool = tool ?? "";
			Id = id ?? "";
			Model = model ?? "";
		}
	}

	/// <summary>
	/// git-ai 会话信息（sessions 节点）：agent 标识 + 提交该会话代码的人类作者。
	/// </summary>
	public class GitAiSession
	{
		public GitAiAgentId AgentId { get; }

		public string HumanAuthor { get; }

		public GitAiSession(GitAiAgentId agentId, string humanAuthor)
		{
			AgentId = agentId;
			HumanAuthor = humanAuthor ?? "";
		}
	}

	/// <summary>
	/// 单个文件的一行级 AI 归属区间：新文件行号 [StartLine, EndLine]（闭区间）
	/// 由 tool/model 的 agent 会话生成。
	/// </summary>
	public class GitAiLineAttribution
	{
		public int StartLine { get; }

		public int EndLine { get; }

		public string Tool { get; }

		public string Model { get; }

		public string HumanAuthor { get; }

		public GitAiLineAttribution(int startLine, int endLine, string tool, string model, string humanAuthor)
		{
			StartLine = startLine;
			EndLine = endLine;
			Tool = tool ?? "";
			Model = model ?? "";
			HumanAuthor = humanAuthor ?? "";
		}

		/// <summary>显示名：tool（未知模型省略 model 段），如 "claude" / "cursor · gpt-5"。</summary>
		public string DisplayName => IsModelKnown ? Tool + " · " + Model : Tool;

		private bool IsModelKnown => !string.IsNullOrEmpty(Model) && Model != "unknown";

		/// <summary>该行号是否落在本归属区间内。</summary>
		public bool Contains(int lineNumber)
		{
			return lineNumber >= StartLine && lineNumber <= EndLine;
		}
	}

	/// <summary>
	/// <c>git-ai diff &lt;commit&gt; --json</c> 的解析结果：
	/// 按文件组织的行级 AI 归属数据（人类写的行不产生归属条目）。
	/// JSON 结构（Git AI Standard authorship/3.0.0，git-ai 1.x 实测）：
	/// <code>
	/// {
	///   "files": { "&lt;path&gt;": { "annotations": { "&lt;promptId&gt;": [[start,end],...] } } },
	///   "sessions": { "&lt;sessionId&gt;": { "agent_id": { "tool","id","model" }, "human_author" } },
	///   "hunks": [ { "file_path", "start_line", "end_line", "hunk_kind", "session_id"?, "prompt_id"?, "human_id"? } ]
	/// }
	/// </code>
	/// </summary>
	public class GitAiDiffAttribution
	{
		/// <summary>sessionId → 会话信息。</summary>
		public Dictionary<string, GitAiSession> Sessions { get; }

		/// <summary>文件路径（git 原始 unix 路径）→ AI 归属区间列表。</summary>
		public Dictionary<string, List<GitAiLineAttribution>> Files { get; }

		public GitAiDiffAttribution(Dictionary<string, GitAiSession> sessions, Dictionary<string, List<GitAiLineAttribution>> files)
		{
			Sessions = sessions;
			Files = files;
		}

		public static GitAiDiffAttribution Empty => new GitAiDiffAttribution(new Dictionary<string, GitAiSession>(), new Dictionary<string, List<GitAiLineAttribution>>());

		/// <summary>是否完全没有任何 AI 归属数据（空提交 / 全人类提交 / 仓库未使用 git-ai）。</summary>
		public bool IsEmpty => Files.Count == 0 || Files.Values.All((List<GitAiLineAttribution> x) => x.Count == 0);

		/// <summary>
		/// 取指定文件的 AI 归属区间。路径按 unix 规范化后不区分大小写比较（Windows 仓库路径大小写可能不一致）；
		/// 未命中返回空列表（不返回 null）。
		/// </summary>
		public List<GitAiLineAttribution> GetAttributions(string filePath)
		{
			if (string.IsNullOrEmpty(filePath) || Files.Count == 0)
			{
				return new List<GitAiLineAttribution>();
			}
			string normalized = ForkPlus.PathHelper.NormalizeUnix(filePath);
			foreach (KeyValuePair<string, List<GitAiLineAttribution>> kv in Files)
			{
				if (string.Equals(ForkPlus.PathHelper.NormalizeUnix(kv.Key), normalized, StringComparison.OrdinalIgnoreCase))
				{
					return kv.Value;
				}
			}
			return new List<GitAiLineAttribution>();
		}

		/// <summary>解析 git-ai diff --json 输出。解析失败抛出异常（由调用方转为 GitCommandError）。</summary>
		public static GitAiDiffAttribution Decode(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return Empty;
			}
			JObject root = JObject.Parse(json);
			Dictionary<string, GitAiSession> sessions = DecodeSessions(root["sessions"] as JObject);
			// 优先用 hunks（带行号区间 + 直接关联 session），annotations 作为兜底。
			Dictionary<string, List<GitAiLineAttribution>> files = new Dictionary<string, List<GitAiLineAttribution>>(StringComparer.OrdinalIgnoreCase);
			JArray hunks = root["hunks"] as JArray;
			if (hunks != null)
			{
				foreach (JToken hunkToken in hunks)
				{
					DecodeHunk(hunkToken as JObject, sessions, files);
				}
			}
			if (files.Count == 0)
			{
				DecodeAnnotations(root["files"] as JObject, sessions, files);
			}
			return new GitAiDiffAttribution(sessions, files);
		}

		/// <summary>解析单个 hunk。带 session_id/prompt_id 的 hunk 是 AI 写的；带 human_id 的是人类写的（跳过）。</summary>
		private static void DecodeHunk(JObject hunk, Dictionary<string, GitAiSession> sessions, Dictionary<string, List<GitAiLineAttribution>> files)
		{
			if (hunk == null)
			{
				return;
			}
			string sessionId = hunk["session_id"]?.Value<string>();
			string promptId = hunk["prompt_id"]?.Value<string>();
			// promptId 形如 "<sessionId>::<toolCallId>"，session_id 缺失时从中还原
			if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(promptId))
			{
				int separatorIndex = promptId.IndexOf("::", StringComparison.Ordinal);
				if (separatorIndex > 0)
				{
					sessionId = promptId.Substring(0, separatorIndex);
				}
			}
			if (string.IsNullOrEmpty(sessionId))
			{
				return;
			}
			string filePath = hunk["file_path"]?.Value<string>();
			int startLine = hunk["start_line"]?.Value<int>() ?? 0;
			int endLine = hunk["end_line"]?.Value<int>() ?? 0;
			if (string.IsNullOrEmpty(filePath) || startLine <= 0 || endLine < startLine)
			{
				return;
			}
			GitAiSession session;
			sessions.TryGetValue(sessionId, out session);
			string tool = session?.AgentId?.Tool ?? "";
			string model = session?.AgentId?.Model ?? "";
			string humanAuthor = session?.HumanAuthor ?? "";
			List<GitAiLineAttribution> list;
			if (!files.TryGetValue(filePath, out list))
			{
				list = new List<GitAiLineAttribution>();
				files[filePath] = list;
			}
			list.Add(new GitAiLineAttribution(startLine, endLine, tool, model, humanAuthor));
		}

		/// <summary>兜底：从 files.annotations 解析（promptId → 行区间列表）。</summary>
		private static void DecodeAnnotations(JObject filesNode, Dictionary<string, GitAiSession> sessions, Dictionary<string, List<GitAiLineAttribution>> files)
		{
			if (filesNode == null)
			{
				return;
			}
			foreach (KeyValuePair<string, JToken> fileKv in filesNode)
			{
				JObject fileNode = fileKv.Value as JObject;
				JObject annotations = fileNode?["annotations"] as JObject;
				if (annotations == null || annotations.Count == 0)
				{
					continue;
				}
				List<GitAiLineAttribution> list = new List<GitAiLineAttribution>();
				foreach (KeyValuePair<string, JToken> annotationKv in annotations)
				{
					string promptId = annotationKv.Key;
					int separatorIndex = promptId.IndexOf("::", StringComparison.Ordinal);
					string sessionId = separatorIndex > 0 ? promptId.Substring(0, separatorIndex) : promptId;
					GitAiSession session;
					sessions.TryGetValue(sessionId, out session);
					JArray ranges = annotationKv.Value as JArray;
					if (ranges == null)
					{
						continue;
					}
					foreach (JToken rangeToken in ranges)
					{
						JArray range = rangeToken as JArray;
						if (range == null || range.Count < 2)
						{
							continue;
						}
						int start = range[0].Value<int>();
						int end = range[1].Value<int>();
						if (start > 0 && end >= start)
						{
							list.Add(new GitAiLineAttribution(start, end, session?.AgentId?.Tool ?? "", session?.AgentId?.Model ?? "", session?.HumanAuthor ?? ""));
						}
					}
				}
				if (list.Count > 0)
				{
					files[fileKv.Key] = list;
				}
			}
		}

		private static Dictionary<string, GitAiSession> DecodeSessions(JObject sessionsNode)
		{
			Dictionary<string, GitAiSession> sessions = new Dictionary<string, GitAiSession>();
			if (sessionsNode == null)
			{
				return sessions;
			}
			foreach (KeyValuePair<string, JToken> kv in sessionsNode)
			{
				JObject sessionNode = kv.Value as JObject;
				if (sessionNode == null)
				{
					continue;
				}
				JObject agentIdNode = sessionNode["agent_id"] as JObject;
				GitAiAgentId agentId = new GitAiAgentId(
					agentIdNode?["tool"]?.Value<string>() ?? "",
					agentIdNode?["id"]?.Value<string>() ?? "",
					agentIdNode?["model"]?.Value<string>() ?? "");
				sessions[kv.Key] = new GitAiSession(agentId, sessionNode["human_author"]?.Value<string>() ?? "");
			}
			return sessions;
		}
	}

	/// <summary>git-ai stats 按 tool::model 维度的细分。</summary>
	public class GitAiToolStats
	{
		public string Tool { get; }

		public string Model { get; }

		public long AiAdditions { get; }

		public long AiAccepted { get; }

		public GitAiToolStats(string tool, string model, long aiAdditions, long aiAccepted)
		{
			Tool = tool ?? "";
			Model = model ?? "";
			AiAdditions = aiAdditions;
			AiAccepted = aiAccepted;
		}

		/// <summary>显示名："tool · model"（模型未知时只显示 tool）。</summary>
		public string DisplayName => string.IsNullOrEmpty(Model) || Model == "unknown" ? Tool : Tool + " · " + Model;
	}

	/// <summary>
	/// <c>git-ai stats --json</c> 的解析结果。同时兼容两种 JSON 形态（git-ai 1.7 实测）：
	/// 单提交：根级 { human_additions, unknown_additions, ai_additions, ai_accepted, ..., tool_model_breakdown }；
	/// 区间：{ authorship_stats: { total_commits, commits_with_authorship, ... }, range_stats: { 同单提交 } }。
	/// </summary>
	public class GitAiStats
	{
		public long HumanAdditions { get; }

		/// <summary>无作者归属数据的新增行数（提交未记录 git-ai authorship note）。</summary>
		public long UnknownAdditions { get; }

		public long AiAdditions { get; }

		/// <summary>AI 生成且未被人类改动直接提交的行数（纯 AI 行）。</summary>
		public long AiAccepted { get; }

		public long GitDiffDeletedLines { get; }

		public long GitDiffAddedLines { get; }

		/// <summary>区间统计时的提交总数；单提交统计为 null。</summary>
		public long? TotalCommits { get; }

		/// <summary>区间统计时带 authorship note 的提交数；单提交统计为 null。</summary>
		public long? CommitsWithAuthorship { get; }

		public GitAiToolStats[] Breakdown { get; }

		public GitAiStats(long humanAdditions, long unknownAdditions, long aiAdditions, long aiAccepted, long gitDiffDeletedLines, long gitDiffAddedLines, long? totalCommits, long? commitsWithAuthorship, GitAiToolStats[] breakdown)
		{
			HumanAdditions = humanAdditions;
			UnknownAdditions = unknownAdditions;
			AiAdditions = aiAdditions;
			AiAccepted = aiAccepted;
			GitDiffDeletedLines = gitDiffDeletedLines;
			GitDiffAddedLines = gitDiffAddedLines;
			TotalCommits = totalCommits;
			CommitsWithAuthorship = commitsWithAuthorship;
			Breakdown = breakdown ?? new GitAiToolStats[0];
		}

		/// <summary>新增行中 AI 占比（0-100，无新增行时 0）。</summary>
		public double AiPercentage
		{
			get
			{
				long added = GitDiffAddedLines;
				if (added <= 0)
				{
					return 0.0;
				}
				return Math.Round(AiAdditions * 100.0 / added, 1);
			}
		}

		/// <summary>纯人类行数（human_additions 减去 mixed 部分）。</summary>
		public long PureHumanAdditions
		{
			get
			{
				return HumanAdditions;
			}
		}

		/// <summary>解析 git-ai stats --json 输出。解析失败抛出异常（由调用方转为 GitCommandError）。</summary>
		public static GitAiStats Decode(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				throw new FormatException("git-ai stats returned empty output");
			}
			JObject root = JObject.Parse(json);
			// 区间形态：真实数据嵌在 range_stats 下
			JObject statsNode = root["range_stats"] as JObject;
			long? totalCommits = null;
			long? commitsWithAuthorship = null;
			if (statsNode != null)
			{
				JObject authorshipNode = root["authorship_stats"] as JObject;
				totalCommits = authorshipNode?["total_commits"]?.Value<long>();
				commitsWithAuthorship = authorshipNode?["commits_with_authorship"]?.Value<long>();
			}
			else
			{
				statsNode = root;
			}
			long humanAdditions = statsNode["human_additions"]?.Value<long>() ?? 0L;
			long unknownAdditions = statsNode["unknown_additions"]?.Value<long>() ?? 0L;
			long aiAdditions = statsNode["ai_additions"]?.Value<long>() ?? 0L;
			long aiAccepted = statsNode["ai_accepted"]?.Value<long>() ?? 0L;
			long deletedLines = statsNode["git_diff_deleted_lines"]?.Value<long>() ?? 0L;
			long addedLines = statsNode["git_diff_added_lines"]?.Value<long>() ?? 0L;
			List<GitAiToolStats> breakdown = new List<GitAiToolStats>();
			JObject breakdownNode = statsNode["tool_model_breakdown"] as JObject;
			if (breakdownNode != null)
			{
				foreach (KeyValuePair<string, JToken> kv in breakdownNode)
				{
					// key 形如 "claude_code::claude-sonnet-4-5-20250929"，"::" 前是 tool，后是 model
					string tool = kv.Key;
					string model = "";
					int separatorIndex = kv.Key.IndexOf("::", StringComparison.Ordinal);
					if (separatorIndex >= 0)
					{
						tool = kv.Key.Substring(0, separatorIndex);
						model = kv.Key.Substring(separatorIndex + 2);
					}
					JObject entry = kv.Value as JObject;
					breakdown.Add(new GitAiToolStats(
						tool,
						model,
						entry?["ai_additions"]?.Value<long>() ?? 0L,
						entry?["ai_accepted"]?.Value<long>() ?? 0L));
				}
			}
			breakdown.Sort((GitAiToolStats a, GitAiToolStats b) => b.AiAdditions.CompareTo(a.AiAdditions));
			return new GitAiStats(humanAdditions, unknownAdditions, aiAdditions, aiAccepted, deletedLines, addedLines, totalCommits, commitsWithAuthorship, breakdown.ToArray());
		}
	}
}
