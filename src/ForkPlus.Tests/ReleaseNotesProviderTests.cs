// 单元测试（v4.1.0，首次启动"更新内容"弹窗数据源 ReleaseNotesProvider）：
// 锁定章节解析契约——"## v{版本}" 标题行到下一个 "## " 标题之间的正文；版本号归一
//（v/V 前缀剥离、忽略大小写整行匹配、容忍首尾空白）；CRLF 与 LF 等价；"###" 子标题
// 是正文不是章节边界；空章节/未知版本/无效版本/空文本一律返回 null（调用方不弹窗）。
// 另锁随包分发契约：Docs/RELEASE_NOTE.md 必须随 csproj Content 复制到输出目录
//（与 BundledRelativePath 一致，引用工程的测试输出同样拿到），否则弹窗数据源
// 静默缺失——"首次启动不弹窗"这类症状只能靠此契约测试提前拦截。
using System;
using System.IO;
using ForkPlus;
using Xunit;

namespace ForkPlus.Tests
{
	public class ReleaseNotesProviderTests
	{
		private const string SampleMarkdown =
			"# Release Notes\n" +
			"\n" +
			"## v4.1.0\n" +
			"\n" +
			"- Auto update pipeline\n" +
			"### 修复\n" +
			"- fix crash\n" +
			"\n" +
			"## v4.0.12\n" +
			"\n" +
			"> crash fixes\n" +
			"\n" +
			"## v4.0.11\n" +
			"\n" +
			"- older\n";

		[Fact]
		public void Extract_MiddleSection_CollectsBodyUntilNextVersionHeading()
		{
			string section = ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown, "4.1.0");
			Assert.Equal("- Auto update pipeline\n### 修复\n- fix crash", section);
		}

		[Fact]
		public void Extract_LastSection_CollectsBodyUntilEndOfFile()
		{
			string section = ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown, "4.0.11");
			Assert.Equal("- older", section);
		}

		[Theory]
		[InlineData("4.1.0")]
		[InlineData("v4.1.0")]
		[InlineData("V4.1.0")]
		[InlineData(" 4.1.0 ")]
		public void Extract_VersionPrefixAndSurroundingSpace_AreNormalized(string version)
		{
			Assert.NotNull(ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown, version));
		}

		[Fact]
		public void Extract_HeadingMatch_IgnoresCase()
		{
			string markdown = "## V4.2.0\n- body\n";
			Assert.Equal("- body", ReleaseNotesProvider.ExtractVersionSection(markdown, "4.2.0"));
		}

		[Fact]
		public void Extract_CrlfLineEndings_EquivalentToLf()
		{
			string lf = ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown, "4.0.12");
			string crlf = ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown.Replace("\n", "\r\n"), "4.0.12");
			Assert.Equal(lf, crlf);
		}

		[Fact]
		public void Extract_SubHeading_IsContentNotSectionBoundary()
		{
			string markdown = "## v1.0\n### Sub\n- one\n## v2.0\n- two\n";
			// "### Sub" 收进 1.0 正文（不终结章节）
			Assert.Equal("### Sub\n- one", ReleaseNotesProvider.ExtractVersionSection(markdown, "1.0"));
			// 也不开启章节：以 "Sub" 查版本必然落空
			Assert.Null(ReleaseNotesProvider.ExtractVersionSection(markdown, "Sub"));
		}

		[Fact]
		public void Extract_NonVersionH2Heading_TerminatesSection()
		{
			string markdown = "## v1.0\n- one\n## Roadmap\n- future\n";
			Assert.Equal("- one", ReleaseNotesProvider.ExtractVersionSection(markdown, "1.0"));
		}

		[Fact]
		public void Extract_EmptySectionBody_ReturnsNull()
		{
			string markdown = "## v1.0\n\n## v2.0\n- two\n";
			Assert.Null(ReleaseNotesProvider.ExtractVersionSection(markdown, "1.0"));
		}

		[Theory]
		[InlineData("3.0.0")]
		[InlineData("")]
		[InlineData("v")]
		public void Extract_UnknownOrInvalidVersion_ReturnsNull(string version)
		{
			Assert.Null(ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown, version));
		}

		[Fact]
		public void Extract_NullMarkdownOrVersion_EmptyMarkdown_ReturnsNull()
		{
			Assert.Null(ReleaseNotesProvider.ExtractVersionSection(null, "4.1.0"));
			Assert.Null(ReleaseNotesProvider.ExtractVersionSection(SampleMarkdown, null));
			Assert.Null(ReleaseNotesProvider.ExtractVersionSection("", "4.1.0"));
		}

		[Fact]
		public void BundledMarkdown_IsPackagedIntoTestOutputDirectory()
		{
			string path = Path.Combine(AppContext.BaseDirectory, ReleaseNotesProvider.BundledRelativePath);
			Assert.True(File.Exists(path), "Docs/RELEASE_NOTE.md 应随包复制到输出目录: " + path);
		}

		[Fact]
		public void BundledMarkdown_HistoricalSectionExtracts()
		{
			// 用真实随包文件锁格式兼容：仓库 RELEASE_NOTE.md 自 v1.3.0 起维护，
			// 上一版 v4.0.12 章节必须可提取（标题行格式回归即失败）
			string notes = ReleaseNotesProvider.GetBundledNotesForVersion("4.0.12");
			Assert.NotNull(notes);
			Assert.NotEqual("", notes);
		}

		[Fact]
		public void GetBundledNotes_UnknownVersion_ReturnsNull()
		{
			Assert.Null(ReleaseNotesProvider.GetBundledNotesForVersion("0.0.1"));
		}
	}
}
