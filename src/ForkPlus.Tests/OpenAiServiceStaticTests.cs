using System.Linq;
using Newtonsoft.Json.Linq;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.2.0 OpenAiService 静态纯逻辑方法单元测试。覆盖 ParseWipCommitPlan、StripCodeFences、
	/// MatchesCommitMessageRegex、DecodeServiceError。重点测试 Release Note 明确点名的 JSON 鲁棒性。
	/// </summary>
	public class OpenAiServiceStaticTests
	{
		// ===================== ParseWipCommitPlan =====================

		[Fact]
		public void ParseWipCommitPlan_NullInput_ReturnsNull()
		{
			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(null, new ChangedFile[0]);
			Assert.Null(plan);
		}

		[Fact]
		public void ParseWipCommitPlan_WhitespaceInput_ReturnsNull()
		{
			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan("   \n  ", new ChangedFile[0]);
			Assert.Null(plan);
		}

		[Fact]
		public void ParseWipCommitPlan_NoJsonArray_ReturnsNull()
		{
			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan("Sorry, I cannot help with that.", new ChangedFile[0]);
			Assert.Null(plan);
		}

		[Fact]
		public void ParseWipCommitPlan_EmptyJsonArray_ReturnsNull()
		{
			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan("[]", new ChangedFile[0]);
			Assert.Null(plan);
		}

		[Fact]
		public void ParseWipCommitPlan_AllSubjectsBlank_ReturnsNull()
		{
			string ai = "[{\"subject\":\"\",\"files\":[\"a.cs\"]},{\"subject\":\"   \",\"files\":[\"b.cs\"]}]";
			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, new ChangedFile[0]);
			Assert.Null(plan);
		}

		[Fact]
		public void ParseWipCommitPlan_DirectArray_ParsesGroups()
		{
			ChangedFile[] staged = new[]
			{
				new ChangedFile("a.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true),
				new ChangedFile("b.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true)
			};
			string ai = "[{\"subject\":\"feat: a\",\"body\":\"details\",\"reason\":\"test\",\"files\":[\"a.cs\",\"b.cs\"]}]";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal(1, plan.Groups.Count);
			Assert.Equal("feat: a", plan.Groups[0].Subject);
			Assert.Equal("details", plan.Groups[0].Body);
			Assert.Equal("test", plan.Groups[0].Reason);
			Assert.Equal(2, plan.Groups[0].MatchedFileCount);
			Assert.True(plan.IsComplete);
		}

		[Fact]
		public void ParseWipCommitPlan_GroupsWrapper_ParsesGroups()
		{
			ChangedFile[] staged = new[]
			{
				new ChangedFile("a.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true)
			};
			string ai = "{\"groups\":[{\"subject\":\"feat: a\",\"files\":[\"a.cs\"]}]}";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal(1, plan.Groups.Count);
			Assert.Equal("feat: a", plan.Groups[0].Subject);
		}

		[Fact]
		public void ParseWipCommitPlan_MarkdownCodeFence_ParsesCorrectly()
		{
			ChangedFile[] staged = new[]
			{
				new ChangedFile("a.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true)
			};
			string ai = "Here is the plan:\n```json\n[{\"subject\":\"feat\",\"files\":[\"a.cs\"]}]\n```\nLet me know!";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal(1, plan.Groups.Count);
		}

		[Fact]
		public void ParseWipCommitPlan_SubjectIsTrimmed()
		{
			ChangedFile[] staged = new ChangedFile[] { };
			string ai = "[{\"subject\":\"  feat: trimmed  \",\"files\":[]}]";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal("feat: trimmed", plan.Groups[0].Subject);
		}

		[Fact]
		public void ParseWipCommitPlan_FilesContainWhitespace_FilteredOut()
		{
			ChangedFile[] staged = new[]
			{
				new ChangedFile("a.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true)
			};
			string ai = "[{\"subject\":\"feat\",\"files\":[\"a.cs\",\"\",\"  \",null]}]";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal(1, plan.Groups[0].MatchedFileCount);
			Assert.Single(plan.Groups[0].Files);  // 空白被过滤
		}

		[Fact]
		public void ParseWipCommitPlan_FilesAreTrimmed()
		{
			ChangedFile[] staged = new[]
			{
				new ChangedFile("a.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true)
			};
			string ai = "[{\"subject\":\"feat\",\"files\":[\"  a.cs  \"]}]";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal("a.cs", plan.Groups[0].Files[0]);
			Assert.Equal(1, plan.Groups[0].MatchedFileCount);
		}

		[Fact]
		public void ParseWipCommitPlan_NonJObjectArrayElement_Skipped()
		{
			ChangedFile[] staged = new ChangedFile[] { };
			// 数组里含字符串和数字，应被跳过
			string ai = "[\"not a group\",42,{\"subject\":\"valid\",\"files\":[]}]";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal(1, plan.Groups.Count);
			Assert.Equal("valid", plan.Groups[0].Subject);
		}

		[Fact]
		public void ParseWipCommitPlan_MissingBodyAndReason_DefaultsToEmpty()
		{
			ChangedFile[] staged = new ChangedFile[] { };
			string ai = "[{\"subject\":\"feat\",\"files\":[]}]";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal("", plan.Groups[0].Body);
			Assert.Equal("", plan.Groups[0].Reason);
		}

		[Fact]
		public void ParseWipCommitPlan_InvalidJson_ReturnsNull()
		{
			// 起始 [ 但内部不是合法 JSON
			ChangedFile[] staged = new ChangedFile[] { };
			string ai = "[this is not valid json";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.Null(plan);
		}

		[Fact]
		public void ParseWipCommitPlan_JsonArrayInProseText_ExtractedCorrectly()
		{
			ChangedFile[] staged = new[]
			{
				new ChangedFile("a.cs", StatusType.Modified, StatusType.None, ChangeType.Modified, true)
			};
			// AI 输出中嵌套字符串字面量里有 ]，确保状态机正确处理
			string ai = "I analyzed the changes. [{\"subject\":\"feat\",\"files\":[\"a.cs\"],\"reason\":\"fixed [critical] bug\"}] Hope this helps!";

			WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(ai, staged);

			Assert.NotNull(plan);
			Assert.Equal(1, plan.Groups.Count);
			Assert.Equal("fixed [critical] bug", plan.Groups[0].Reason);
		}

		// ===================== StripCodeFences =====================

		[Fact]
		public void StripCodeFences_Null_ReturnsNull()
		{
			Assert.Null(OpenAiService.StripCodeFences(null));
		}

		[Fact]
		public void StripCodeFences_EmptyString_ReturnsEmpty()
		{
			Assert.Equal("", OpenAiService.StripCodeFences(""));
		}

		[Fact]
		public void StripCodeFences_NoFence_ReturnsOriginalTrimmed()
		{
			// 注意：实现是 Not fence → return text;（返回原文，不 trim）
			string text = "hello world";
			Assert.Equal("hello world", OpenAiService.StripCodeFences(text));
		}

		[Fact]
		public void StripCodeFences_StandardJsonFence_Stripped()
		{
			string text = "```json\n{\"key\":\"value\"}\n```";
			Assert.Equal("{\"key\":\"value\"}", OpenAiService.StripCodeFences(text));
		}

		[Fact]
		public void StripCodeFences_FenceWithoutLang_Stripped()
		{
			string text = "```\ncontent\n```";
			Assert.Equal("content", OpenAiService.StripCodeFences(text));
		}

		[Fact]
		public void StripCodeFences_OpenFenceOnlyNoClose_StripsPrefix()
		{
			// 开头有 ``` 但没有换行：去掉前 3 字符
			string text = "```content";
			Assert.Equal("content", OpenAiService.StripCodeFences(text));
		}

		[Fact]
		public void StripCodeFences_OpenFenceWithNewlineNoClose_StripsPrefix()
		{
			string text = "```\ncontent without close";
			Assert.Equal("content without close", OpenAiService.StripCodeFences(text));
		}

		// ===================== MatchesCommitMessageRegex =====================

		[Fact]
		public void MatchesCommitMessageRegex_NullPattern_ReturnsTrueAndNoError()
		{
			bool ok = OpenAiService.MatchesCommitMessageRegex("anything", null, out string error);
			Assert.True(ok);
			Assert.Null(error);
		}

		[Fact]
		public void MatchesCommitMessageRegex_EmptyPattern_ReturnsTrueAndNoError()
		{
			bool ok = OpenAiService.MatchesCommitMessageRegex("anything", "", out string error);
			Assert.True(ok);
			Assert.Null(error);
		}

		[Fact]
		public void MatchesCommitMessageRegex_WhitespacePattern_ReturnsTrue()
		{
			bool ok = OpenAiService.MatchesCommitMessageRegex("anything", "   ", out string error);
			Assert.True(ok);
			Assert.Null(error);
		}

		[Fact]
		public void MatchesCommitMessageRegex_MatchingPattern_ReturnsTrue()
		{
			// ^feat: 开头
			bool ok = OpenAiService.MatchesCommitMessageRegex("feat: add login", "^feat:", out string error);
			Assert.True(ok);
			Assert.Null(error);
		}

		[Fact]
	public void MatchesCommitMessageRegex_InvalidPattern_ReturnsTrueFallback()
	{
		// 非法正则不应抛异常，容错返回 true（catch 块不调用 PreferencesLocalization）
		bool ok = OpenAiService.MatchesCommitMessageRegex("anything", "[", out string error);
		Assert.True(ok);
		Assert.Null(error);
	}

		[Fact]
		public void MatchesCommitMessageRegex_MultiLineMessage_NormalizesCrlf()
		{
			// title + description，pattern 用 Singleline 模式应能跨行匹配
			string message = "feat: title\r\n\r\ndescription body";
			bool ok = OpenAiService.MatchesCommitMessageRegex(message, "feat: title.*description", out string error);
			Assert.True(ok);
			Assert.Null(error);
		}

		// ===================== DecodeServiceError =====================

		[Fact]
		public void DecodeServiceError_Null_ReturnsNull()
		{
			Assert.Null(OpenAiService.DecodeServiceError(null));
		}

		[Fact]
		public void DecodeServiceError_ErrorObjectWithMessage_ReturnsMessage()
		{
			JObject json = JObject.Parse(@"{""error"":{""message"":""rate limit exceeded""}}");
			string result = OpenAiService.DecodeServiceError(json);
			Assert.Contains("rate limit exceeded", result);
		}

		[Fact]
		public void DecodeServiceError_TopLevelMessage_ReturnsMessage()
		{
			JObject json = JObject.Parse(@"{""message"":""service unavailable""}");
			string result = OpenAiService.DecodeServiceError(json);
			Assert.Contains("service unavailable", result);
		}

		[Fact]
		public void DecodeServiceError_WithHttpStatusCode_PrefixesHttpCode()
		{
			JObject json = JObject.Parse(@"{""error"":{""message"":""too many requests""},""__http_status_code__"":429}");
			string result = OpenAiService.DecodeServiceError(json);
			Assert.Contains("[HTTP 429]", result);
			Assert.Contains("too many requests", result);
		}

		[Fact]
		public void DecodeServiceError_HttpCodeUnder300_NoPrefix()
		{
			JObject json = JObject.Parse(@"{""error"":{""message"":""ok""},""__http_status_code__"":200}");
			string result = OpenAiService.DecodeServiceError(json);
			Assert.DoesNotContain("[HTTP", result);
			Assert.Contains("ok", result);
		}

		[Fact]
	public void DecodeServiceError_JArray_MergesMessages()
	{
		JArray array = JArray.Parse(@"[""error one"",""error two""]");
		string result = OpenAiService.DecodeServiceError(array);
		Assert.NotNull(result);
		Assert.Contains("error one", result);
		Assert.Contains("error two", result);
	}
	}
}
