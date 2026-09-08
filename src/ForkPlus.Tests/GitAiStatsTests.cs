using System;
using System.Collections.Generic;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// git-ai stats --json 解析测试。JSON 样本来自 git-ai 1.7.1 实测输出
	/// （单提交根级形态 / 区间嵌套形态两种）。
	/// </summary>
	public class GitAiStatsTests
	{
		/// <summary>git-ai 1.7.1 单提交实测输出（未使用 git-ai 的仓库，全部行为 unknown）。</summary>
		private const string SingleCommitJson =
			"{\"human_additions\":0,\"unknown_additions\":1067,\"ai_additions\":0,\"ai_accepted\":0," +
			"\"git_diff_deleted_lines\":1,\"git_diff_added_lines\":1067,\"tool_model_breakdown\":{}}";

		/// <summary>git-ai 1.7.1 区间实测输出骨架（保留关键字段，省略超长提交清单）。</summary>
		private const string RangeJson =
			"{\"authorship_stats\":{\"total_commits\":100,\"commits_with_authorship\":0," +
			"\"authors_committing_authorship\":[],\"authors_not_committing_authorship\":[\"Test User <test@example.com>\"]," +
			"\"commits_without_authorship\":[\"05cf320\",\"d8d213d\"]}," +
			"\"range_stats\":{\"human_additions\":12,\"unknown_additions\":27686,\"ai_additions\":5300,\"ai_accepted\":4100," +
			"\"git_diff_deleted_lines\":12261,\"git_diff_added_lines\":32998," +
			"\"tool_model_breakdown\":{\"claude_code::claude-sonnet-4-5\":{\"ai_additions\":3300,\"ai_accepted\":2500}," +
			"\"cursor::gpt-5\":{\"ai_additions\":2000,\"ai_accepted\":1600}}}}";

		[Fact]
		public void Decode_SingleCommit_ParsesRootLevelFields()
		{
			GitAiStats stats = GitAiStats.Decode(SingleCommitJson);

			Assert.Equal(0, stats.HumanAdditions);
			Assert.Equal(1067, stats.UnknownAdditions);
			Assert.Equal(0, stats.AiAdditions);
			Assert.Equal(0, stats.AiAccepted);
			Assert.Equal(1067, stats.GitDiffAddedLines);
			Assert.Equal(1, stats.GitDiffDeletedLines);
			Assert.Null(stats.TotalCommits);
			Assert.Null(stats.CommitsWithAuthorship);
			Assert.Empty(stats.Breakdown);
		}

		[Fact]
		public void Decode_Range_ParsesNestedRangeStatsAndAuthorship()
		{
			GitAiStats stats = GitAiStats.Decode(RangeJson);

			// 区间模式：行数来自 range_stats
			Assert.Equal(12, stats.HumanAdditions);
			Assert.Equal(27686, stats.UnknownAdditions);
			Assert.Equal(5300, stats.AiAdditions);
			Assert.Equal(4100, stats.AiAccepted);
			Assert.Equal(32998, stats.GitDiffAddedLines);
			// 提交数来自 authorship_stats
			Assert.Equal(100, stats.TotalCommits);
			Assert.Equal(0, stats.CommitsWithAuthorship);
		}

		[Fact]
		public void Decode_Range_BreakdownSortedByAiAdditionsDescending()
		{
			GitAiStats stats = GitAiStats.Decode(RangeJson);

			Assert.Equal(2, stats.Breakdown.Length);
			Assert.Equal("claude_code", stats.Breakdown[0].Tool);
			Assert.Equal("claude-sonnet-4-5", stats.Breakdown[0].Model);
			Assert.Equal(3300, stats.Breakdown[0].AiAdditions);
			Assert.Equal("claude_code · claude-sonnet-4-5", stats.Breakdown[0].DisplayName);
			Assert.Equal("cursor · gpt-5", stats.Breakdown[1].DisplayName);
			// 未知模型的 DisplayName 只显示 tool
			Assert.Equal("cursor", new GitAiToolStats("cursor", "unknown", 1, 1).DisplayName);
		}

		[Fact]
		public void AiPercentage_ComputedAgainstAddedLines()
		{
			GitAiStats stats = GitAiStats.Decode(RangeJson);

			Assert.Equal(16.1, stats.AiPercentage, 1);
			Assert.Equal(0.0, GitAiStats.Decode(SingleCommitJson).AiPercentage);
		}

		[Fact]
		public void Decode_EmptyOutput_ThrowsFormatException()
		{
			Assert.Throws<FormatException>(() => GitAiStats.Decode(""));
			Assert.Throws<FormatException>(() => GitAiStats.Decode("   "));
		}

		[Fact]
		public void Decode_MalformedJson_Throws()
		{
			Assert.ThrowsAny<Exception>(() => GitAiStats.Decode("not json at all"));
		}

		[Fact]
		public void DiffAttribution_DecodeHunks_MapsSessionToolAndModel()
		{
			string json =
				"{\"files\":{}," +
				"\"sessions\":{\"sess-1\":{\"agent_id\":{\"tool\":\"claude_code\",\"id\":\"sess-1\",\"model\":\"claude-sonnet-4-5\"},\"human_author\":\"Dev <dev@example.com>\"}}," +
				"\"hunks\":[{\"file_path\":\"src/a.cs\",\"start_line\":3,\"end_line\":10,\"hunk_kind\":\"Add\",\"session_id\":\"sess-1\"}," +
				"{\"file_path\":\"src/b.cs\",\"start_line\":1,\"end_line\":5,\"hunk_kind\":\"Add\",\"prompt_id\":\"sess-1::call-9\"}," +
				"{\"file_path\":\"src/human.cs\",\"start_line\":1,\"end_line\":5,\"hunk_kind\":\"Add\",\"human_id\":\"u-1\"}]}";
			GitAiDiffAttribution attribution = GitAiDiffAttribution.Decode(json);

			Assert.False(attribution.IsEmpty);
			List<GitAiLineAttribution> a = attribution.GetAttributions("src/a.cs");
			Assert.Single(a);
			Assert.Equal(3, a[0].StartLine);
			Assert.Equal(10, a[0].EndLine);
			Assert.Equal("claude_code", a[0].Tool);
			Assert.Equal("claude-sonnet-4-5", a[0].Model);
			Assert.Equal("Dev <dev@example.com>", a[0].HumanAuthor);
			// prompt_id 还原 sessionId（session_id 缺失时取 "::" 前段）
			List<GitAiLineAttribution> b = attribution.GetAttributions("src/b.cs");
			Assert.Single(b);
			Assert.Equal("claude_code", b[0].Tool);
			// 纯人类 hunk（human_id，无 session）不产生归属条目
			Assert.Empty(attribution.GetAttributions("src/human.cs"));
		}

		[Fact]
		public void DiffAttribution_GetAttributions_MatchesPathCaseInsensitively()
		{
			string json =
				"{\"files\":{}," +
				"\"sessions\":{\"s\":{\"agent_id\":{\"tool\":\"t\",\"id\":\"s\",\"model\":\"m\"},\"human_author\":\"\"}}," +
				"\"hunks\":[{\"file_path\":\"Src/App.CS\",\"start_line\":1,\"end_line\":2,\"hunk_kind\":\"Add\",\"session_id\":\"s\"}]}";
			GitAiDiffAttribution attribution = GitAiDiffAttribution.Decode(json);

			Assert.Single(attribution.GetAttributions("src/app.cs"));
			Assert.Empty(attribution.GetAttributions("src/other.cs"));
			Assert.Empty(attribution.GetAttributions(""));
		}
}
}
