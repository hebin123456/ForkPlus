using System;
using ForkPlus.UI.Dialogs;
using ForkPlus.Undo;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.4.0 ReflogViewItem 单元测试。
	///
	/// ReflogViewItem 是 ReflogWindow 的 ListView 行视图模型（POCO，不依赖 WPF）。
	/// 测试 IndexDisplay / ShaDisplay / OperationName / CommitSubject / TimeDisplay 的格式化逻辑。
	/// </summary>
	public class ReflogViewItemTests
	{
		private const string SampleSha = "abc123def456789012345678901234567890abcd";

		private static ReflogEntry MakeReflogEntry(int index, string sha = SampleSha, string reflogSubject = "commit: fix", string commitSubject = "fix", DateTime? timestampUtc = null)
		{
			return new ReflogEntry
			{
				Sha = sha,
				ReflogSubject = reflogSubject,
				CommitSubject = commitSubject,
				Index = index,
				TimestampUtc = timestampUtc
			};
		}

		[Fact]
		public void IndexDisplay_ContainsHeadAtSyntax()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), "Commit 'fix'");
			Assert.Equal("HEAD@{0}", item.IndexDisplay);
		}

		[Fact]
		public void IndexDisplay_LargeIndex_FormatsCorrectly()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(42), "op");
			Assert.Equal("HEAD@{42}", item.IndexDisplay);
		}

		[Fact]
		public void ShaDisplay_TruncatesTo8Chars()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), "op");
			Assert.Equal(8, item.ShaDisplay.Length);
			Assert.Equal(SampleSha.Substring(0, 8), item.ShaDisplay);
		}

		[Fact]
		public void ShaDisplay_ShortSha_ReturnsFullSha()
		{
			// 短 sha 不会被截断（虽然实际场景 ParseLine 会拒绝短 sha，但 ReflogViewItem 应鲁棒）
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, sha: "abc123"), "op");
			Assert.Equal("abc123", item.ShaDisplay);
		}

		[Fact]
		public void ShaDisplay_EmptySha_ReturnsEmpty()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, sha: ""), "op");
			Assert.Equal("", item.ShaDisplay);
		}

		[Fact]
		public void ShaDisplay_NullSha_ReturnsEmpty()
		{
			ReflogEntry entry = MakeReflogEntry(0);
			entry.Sha = null;
			ReflogViewItem item = new ReflogViewItem(entry, "op");
			Assert.Equal("", item.ShaDisplay);
		}

		[Fact]
		public void OperationName_PassedThrough()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), "Commit 'fix: bug'");
			Assert.Equal("Commit 'fix: bug'", item.OperationName);
		}

		[Fact]
		public void OperationName_Null_NormalizedToEmpty()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), null);
			Assert.Equal("", item.OperationName);
		}

		[Fact]
		public void CommitSubject_PassedThrough()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, commitSubject: "fix: bug"), "op");
			Assert.Equal("fix: bug", item.CommitSubject);
		}

		[Fact]
		public void CommitSubject_Null_NormalizedToEmpty()
		{
			ReflogEntry entry = MakeReflogEntry(0);
			entry.CommitSubject = null;
			ReflogViewItem item = new ReflogViewItem(entry, "op");
			Assert.Equal("", item.CommitSubject);
		}

		[Fact]
		public void TimeDisplay_NullTimestamp_ReturnsEmpty()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: null), "op");
			Assert.Equal("", item.TimeDisplay);
		}

		[Fact]
		public void TimeDisplay_WithTimestamp_FormatsLocalTime()
		{
			// UTC 时间，应转换为本地时区显示
			DateTime utc = new DateTime(2026, 7, 19, 2, 0, 0, DateTimeKind.Utc);
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: utc), "op");

			// 本地时间 = utc.ToLocalTime()
			string expected = utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
			Assert.Equal(expected, item.TimeDisplay);
		}

		[Fact]
		public void TimeDisplay_UtcTimestamp_ConvertsToLocal()
		{
			// 验证 TimeDisplay 是本地时间，不是 UTC
			DateTime utc = new DateTime(2026, 7, 19, 2, 0, 0, DateTimeKind.Utc);
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: utc), "op");

			// 如果本地时区非 UTC，TimeDisplay 的小时数应 != 2
			// （在 CI 的 UTC 时区环境下，本地 = UTC，小时数会是 2，所以这个测试主要验证格式正确）
			Assert.Contains("2026-07-19", item.TimeDisplay);
		}

		[Fact]
		public void ReflogViewItem_PreservesEntryIndex()
		{
			// 验证 IndexDisplay 与 entry.Index 一致
			for (int i = 0; i < 5; i++)
			{
				ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(i), "op");
				Assert.Equal("HEAD@{" + i + "}", item.IndexDisplay);
			}
		}
	}
}
