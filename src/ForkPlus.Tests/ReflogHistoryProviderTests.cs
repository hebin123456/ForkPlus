using ForkPlus.Undo;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.3.0 ReflogHistoryProvider 解析逻辑单元测试。
	///
	/// 测试 ParseLine 静态方法（已改为 internal 供测试直接调用）。
	/// 不依赖 GitModule / git 进程 / WPF。
	///
	/// reflog 输出格式（由 ReflogHistoryProvider.ReadHeadReflog 请求）：
	///   %H%x00%gs%x00%s
	/// 即：sha + NUL + reflog subject + NUL + commit subject
	/// NUL = '\0'（%x00）
	/// </summary>
	public class ReflogHistoryProviderTests
	{
		// 一个真实的 40 字符 sha，用于测试
		private const string SampleSha = "abc123def456789012345678901234567890abcd";

		[Fact]
		public void ParseLine_ValidLineWithAllFields_ReturnsEntry()
		{
			string line = SampleSha + "\0" + "commit: fix: bug" + "\0" + "fix: bug";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 3);

			Assert.NotNull(entry);
			Assert.Equal(SampleSha, entry.Sha);
			Assert.Equal("commit: fix: bug", entry.ReflogSubject);
			Assert.Equal("fix: bug", entry.CommitSubject);
			Assert.Equal(3, entry.Index);
		}

		[Fact]
		public void ParseLine_LineWithOnlySha_StillParsesSubjectFieldsAsEmpty()
		{
			// 极端情况：只有一个 sha，没有 NUL 分隔符
			string line = SampleSha;
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 0);

			Assert.NotNull(entry);
			Assert.Equal(SampleSha, entry.Sha);
			Assert.Equal("", entry.ReflogSubject);
			Assert.Equal("", entry.CommitSubject);
		}

		[Fact]
		public void ParseLine_LineWithShaAndOneSubject_SecondSubjectIsEmpty()
		{
			// sha + NUL + reflog subject（commit subject 缺失）
			string line = SampleSha + "\0" + "reset: moving to HEAD~1";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 1);

			Assert.NotNull(entry);
			Assert.Equal(SampleSha, entry.Sha);
			Assert.Equal("reset: moving to HEAD~1", entry.ReflogSubject);
			Assert.Equal("", entry.CommitSubject);
		}

		[Fact]
		public void ParseLine_EmptyReflogSubject_PreservesEmpty()
		{
			// sha + NUL + NUL + commit subject（reflog subject 为空）
			string line = SampleSha + "\0" + "" + "\0" + "fix: bug";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 0);

			Assert.NotNull(entry);
			Assert.Equal(SampleSha, entry.Sha);
			Assert.Equal("", entry.ReflogSubject);
			Assert.Equal("fix: bug", entry.CommitSubject);
		}

		[Fact]
		public void ParseLine_EmptyLine_ReturnsNull()
		{
			Assert.Null(ReflogHistoryProvider.ParseLine("", 0));
		}

		[Fact]
		public void ParseLine_WhitespaceLine_ReturnsNull()
		{
			Assert.Null(ReflogHistoryProvider.ParseLine("   \t  ", 0));
		}

		[Fact]
		public void ParseLine_NullLine_ReturnsNull()
		{
			Assert.Null(ReflogHistoryProvider.ParseLine(null, 0));
		}

		[Fact]
		public void ParseLine_ShortSha_ReturnsNull()
		{
			// 非 40 字符的 sha 视为无效（git rev-parse 返回的总是 40 字符完整 sha）
			string line = "abc123" + "\0" + "commit: foo";
			Assert.Null(ReflogHistoryProvider.ParseLine(line, 0));
		}

		[Fact]
		public void ParseLine_ExtraFieldsAfterCommitSubject_AreIgnored()
		{
			// 万一 commit subject 中意外含 NUL（理论上 %s 不会，但解析应鲁棒）
			// split 后 parts.Length > 3，但 ParseLine 只取 parts[0..2]
			string line = SampleSha + "\0" + "subj1" + "\0" + "subj2" + "\0" + "extra";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 0);

			Assert.NotNull(entry);
			Assert.Equal(SampleSha, entry.Sha);
			Assert.Equal("subj1", entry.ReflogSubject);
			Assert.Equal("subj2", entry.CommitSubject);
		}

		[Fact]
		public void ParseLine_CheckoutOperationWithFromTo_SubjectPreserved()
		{
			// checkout 类操作的 reflog subject 形如 "checkout: moving from main to feature/x"
			string line = SampleSha + "\0" + "checkout: moving from main to feature/x" + "\0" + "";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 5);

			Assert.NotNull(entry);
			Assert.Equal("checkout: moving from main to feature/x", entry.ReflogSubject);
			Assert.Equal("", entry.CommitSubject);
			Assert.Equal(5, entry.Index);
		}

		[Fact]
		public void ParseLine_IndexZero_MostRecentOperation()
		{
			// 索引 0 = 最近一次操作（与 git reflog HEAD@{0} 一致）
			string line = SampleSha + "\0" + "commit: latest" + "\0" + "latest commit";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 0);

			Assert.NotNull(entry);
			Assert.Equal(0, entry.Index);
		}

		[Fact]
		public void ParseLine_AmendOperationType_StillParses()
		{
			// amend 类操作
			string line = SampleSha + "\0" + "commit (amend): amend msg" + "\0" + "amended subject";
			ReflogEntry entry = ReflogHistoryProvider.ParseLine(line, 2);

			Assert.NotNull(entry);
			Assert.Equal("commit (amend): amend msg", entry.ReflogSubject);
			Assert.Equal("amended subject", entry.CommitSubject);
		}
	}
}
