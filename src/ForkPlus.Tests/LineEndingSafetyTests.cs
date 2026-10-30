// v5.2.4：换行符安全检查（core.safecrlf）拒绝暂存的错误识别回归。
// StageFileGitCommand 失败时按 stderr 文案归类为 LineEndingSafetyRejected，
// ErrorWindow 据此提供"允许转换并暂存"（一次性 -c core.safecrlf=false 重试）。
// 本测试锁定识别规则（英文/中文 git 输出）与误报边界（普通错误不归类）。
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	public class LineEndingSafetyTests
	{
		[Fact]
		public void Test_EnglishFatal_Matches()
		{
			string stderr = "fatal: LF would be replaced by CRLF in src/App.cs";
			Assert.True(GitCommandError.LineEndingSafetyRejected.Test(stderr));
		}

		[Fact]
		public void Test_EnglishFatalReverseDirection_Matches()
		{
			string stderr = "fatal: CRLF would be replaced by LF in README.md";
			Assert.True(GitCommandError.LineEndingSafetyRejected.Test(stderr));
		}

		[Fact]
		public void Test_ChineseFatal_Matches()
		{
			// zh_CN.po 已翻译 convert.c 的消息："fatal: LF 将被 CRLF 替换"
			string stderr = "fatal: LF 将被 CRLF 替换 src/App.cs";
			Assert.True(GitCommandError.LineEndingSafetyRejected.Test(stderr));
		}

		[Fact]
		public void Test_WarningWithoutFatal_DoesNotMatch()
		{
			// safecrlf=warn 时是 warning 不致命——warn 场景 git 正常暂存成功，不走失败分支，
			// 但仍要求识别器不把 warning 误报为 fatal（保持语义精确）。
			string stderr = "warning: LF will be replaced by CRLF in src/App.cs. The file will have its original line endings in your working directory";
			Assert.False(GitCommandError.LineEndingSafetyRejected.Test(stderr));
		}

		[Fact]
		public void Test_OtherErrors_DoesNotMatch()
		{
			Assert.False(GitCommandError.LineEndingSafetyRejected.Test(""));
			Assert.False(GitCommandError.LineEndingSafetyRejected.Test(null));
			Assert.False(GitCommandError.LineEndingSafetyRejected.Test("fatal: Unable to create index.lock': File exists."));
			Assert.False(GitCommandError.LineEndingSafetyRejected.Test("error: pathspec 'x' did not match any file(s) known to git"));
		}

		[Fact]
		public void Constructor_KeepsFilesForRetry()
		{
			ChangedFile[] files = new ChangedFile[]
			{
				new ChangedFile("src/App.cs", StatusType.Modified),
				new ChangedFile("README.md", StatusType.Added)
			};
			var error = new GitCommandError.LineEndingSafetyRejected("full", "stderr", files);
			Assert.Same(files, error.Files);
			Assert.False(string.IsNullOrWhiteSpace(error.FriendlyDescription));
		}

		[Fact]
		public void Constructor_NullFiles_FallsBackToEmpty()
		{
			var error = new GitCommandError.LineEndingSafetyRejected("full", "stderr", null);
			Assert.NotNull(error.Files);
			Assert.Empty(error.Files);
		}
	}
}
