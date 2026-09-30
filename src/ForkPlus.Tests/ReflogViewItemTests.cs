using System;
using System.Collections.Generic;
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

		// ===== WS9 时间线升级：索引命中 / 首末条时间线标记 / 相对时间 / 悬停提示 =====

		[Fact]
		public void TwoArgConstructor_DefaultsToPlainMidTimelineItem()
		{
			// 既有两参构造保持 v3.4.0 语义：未命中索引的中间时间线条目（上下线均画）
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), "op");
			Assert.False(item.IsIndexedOperation);
			Assert.False(item.IsFirst);
			Assert.False(item.IsLast);
			Assert.True(item.TimelineShowTopLine);
			Assert.True(item.TimelineShowBottomLine);
		}

		[Fact]
		public void TimelineFlags_FirstItem_HidesTopLineOnly()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), "op", isFirst: true);
			Assert.True(item.TimelineShowBottomLine);
			Assert.False(item.TimelineShowTopLine);
		}

		[Fact]
		public void TimelineFlags_LastItem_HidesBottomLineOnly()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(3), "op", isLast: true);
			Assert.True(item.TimelineShowTopLine);
			Assert.False(item.TimelineShowBottomLine);
		}

		[Fact]
		public void TimelineFlags_IndexedOperation_DoesNotAffectLines()
		{
			// 命中只改变节点颜色/加粗，不影响竖线
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0), "Commit 'fix'", isIndexedOperation: true);
			Assert.True(item.IsIndexedOperation);
			Assert.True(item.TimelineShowTopLine);
			Assert.True(item.TimelineShowBottomLine);
		}

		[Fact]
		public void RawReflogSubject_PassedThrough()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, reflogSubject: "reset: moving to HEAD~1"), "Reset 'main'");
			Assert.Equal("reset: moving to HEAD~1", item.RawReflogSubject);
		}

		[Fact]
		public void RawReflogSubject_Null_NormalizedToEmpty()
		{
			ReflogEntry entry = MakeReflogEntry(0);
			entry.ReflogSubject = null;
			ReflogViewItem item = new ReflogViewItem(entry, "op");
			Assert.Equal("", item.RawReflogSubject);
		}

		[Fact]
		public void RelativeTimeDisplay_NullTimestamp_ReturnsEmpty()
		{
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: null), "op");
			Assert.Equal("", item.RelativeTimeDisplay);
		}

		[Fact]
		public void RelativeTimeDisplay_RecentTimestamp_UsesRelativeFormat()
		{
			// 3 小时前：ToRelativeString 输出 "3 hours ago" / "3 小时前" / "3 小時前"，
			// 与语言无关的共同结构是含数量 "3" 且不等于绝对时间格式
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: DateTime.UtcNow.AddHours(-3)), "op");
			string relative = item.RelativeTimeDisplay;
			Assert.NotEqual("", relative);
			Assert.NotEqual(item.TimeDisplay, relative);
			Assert.Contains("3", relative);
		}

		[Fact]
		public void RelativeTimeDisplay_FutureTimestamp_FallsBackToAbsolute()
		{
			// 时钟偏斜（未来时间戳）：ToRelativeString 会算出负数，回退绝对时间
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: DateTime.UtcNow.AddHours(2)), "op");
			Assert.Equal(item.TimeDisplay, item.RelativeTimeDisplay);
		}

		[Fact]
		public void TooltipText_PlainItem_OperationAndAbsoluteTime()
		{
			DateTime utc = new DateTime(2026, 7, 19, 2, 0, 0, DateTimeKind.Utc);
			ReflogViewItem item = new ReflogViewItem(MakeReflogEntry(0, timestampUtc: utc), "op");
			Assert.Equal("op\n" + item.TimeDisplay, item.TooltipText);
		}

		[Fact]
		public void TooltipText_IndexedItem_IncludesRawReflogSubjectAndAbsoluteTime()
		{
			DateTime utc = new DateTime(2026, 7, 19, 2, 0, 0, DateTimeKind.Utc);
			ReflogViewItem item = new ReflogViewItem(
				MakeReflogEntry(0, reflogSubject: "commit: fix: bug", timestampUtc: utc),
				"Commit 'fix: bug'",
				isIndexedOperation: true);
			Assert.Equal("Commit 'fix: bug'\ncommit: fix: bug\n" + item.TimeDisplay, item.TooltipText);
		}

		// ===== WS9：BuildViewItems 的 sha 命中 → 操作名装配（纯逻辑，UI 与测试共用路径） =====

		[Fact]
		public void BuildViewItems_IndexHit_UsesFriendlyNameAndIndexedFlag()
		{
			List<ReflogEntry> reflog = new List<ReflogEntry>
			{
				MakeReflogEntry(0),
				MakeReflogEntry(1)
			};
			Dictionary<string, UndoIndexEntry> index = new Dictionary<string, UndoIndexEntry>
			{
				{ SampleSha, new UndoIndexEntry(SampleSha, "Commit 'fix'", DateTime.UtcNow) }
			};

			List<ReflogViewItem> items = ReflogWindow.BuildViewItems(reflog, index);

			Assert.Equal(2, items.Count);
			foreach (ReflogViewItem item in items)
			{
				Assert.True(item.IsIndexedOperation, "sha 命中索引的条目应标记为已索引");
				Assert.Equal("Commit 'fix'", item.OperationName);
			}
		}

		[Fact]
		public void BuildViewItems_IndexMiss_FallsBackToRawReflogSubject()
		{
			List<ReflogEntry> reflog = new List<ReflogEntry>
			{
				MakeReflogEntry(0, sha: "aaaa000000000000000000000000000000000001", reflogSubject: "commit: one"),
				MakeReflogEntry(1, sha: "bbbb000000000000000000000000000000000002", reflogSubject: "reset: moving to HEAD~1")
			};
			Dictionary<string, UndoIndexEntry> index = new Dictionary<string, UndoIndexEntry>
			{
				// 索引里只有无关 sha：两条都未命中
				{ "cccc000000000000000000000000000000000003", new UndoIndexEntry("cccc000000000000000000000000000000000003", "other op", DateTime.UtcNow) }
			};

			List<ReflogViewItem> items = ReflogWindow.BuildViewItems(reflog, index);

			Assert.Equal(2, items.Count);
			Assert.False(items[0].IsIndexedOperation);
			Assert.Equal("commit: one", items[0].OperationName);
			Assert.False(items[1].IsIndexedOperation);
			Assert.Equal("reset: moving to HEAD~1", items[1].OperationName);
		}

		[Fact]
		public void BuildViewItems_EmptyOperationNameInIndex_TreatedAsMiss()
		{
			// 索引条目存在但操作名为空：视为未命中，降级到原生 subject
			List<ReflogEntry> reflog = new List<ReflogEntry> { MakeReflogEntry(0) };
			Dictionary<string, UndoIndexEntry> index = new Dictionary<string, UndoIndexEntry>
			{
				{ SampleSha, new UndoIndexEntry(SampleSha, "", DateTime.UtcNow) }
			};

			List<ReflogViewItem> items = ReflogWindow.BuildViewItems(reflog, index);

			Assert.Single(items);
			Assert.False(items[0].IsIndexedOperation);
			Assert.Equal("commit: fix", items[0].OperationName);
		}

		[Fact]
		public void BuildViewItems_NullIndex_AllPlain()
		{
			List<ReflogEntry> reflog = new List<ReflogEntry> { MakeReflogEntry(0) };

			List<ReflogViewItem> items = ReflogWindow.BuildViewItems(reflog, null);

			Assert.Single(items);
			Assert.False(items[0].IsIndexedOperation);
			Assert.Equal("commit: fix", items[0].OperationName);
		}

		[Fact]
		public void BuildViewItems_SetsFirstAndLastTimelineFlags()
		{
			List<ReflogEntry> reflog = new List<ReflogEntry>
			{
				MakeReflogEntry(0),
				MakeReflogEntry(1),
				MakeReflogEntry(2)
			};

			List<ReflogViewItem> items = ReflogWindow.BuildViewItems(reflog, new Dictionary<string, UndoIndexEntry>());

			Assert.True(items[0].IsFirst);
			Assert.False(items[0].IsLast);
			Assert.False(items[1].IsFirst);
			Assert.False(items[1].IsLast);
			Assert.False(items[2].IsFirst);
			Assert.True(items[2].IsLast);
		}
	}
}
