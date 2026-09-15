// 回归测试（2026-09-14，"FileDiff SideBySide 左右不严格对齐"修复产物）：
// 根因：VisualPatch.CreateVisualSubChunk 的 "\ No newline at end of file" pragma 行
// 只加在所属一侧（Deleted 标记→左侧，Added 标记→右侧），而 Alignment 空行补偿只按
// Deleted/Added 行数差计算、未计入 pragma 行 → 单侧缺末行换行的 diff 左右行数差 1，
// 该侧之后所有内容错位一行（用户报告场景）。
// 修复：change 块两侧总行数按 max(Deleted+pragmaD, Added+pragmaA) 对齐，短侧块尾补
// Alignment 空行；另修 PostContext pragma 误查 Deleted 位（应为 Context 位，与
// PatchExtensions/PatchParser 一致），末行 context 无换行时不再静默丢弃标记。
// 本测试守卫：SideBySide 两侧 StringValue 行数在所有场景下必须相等；带标记场景
// 两侧行数与内容配对正确；Split 视图不得混入 Alignment 空行。
using System.Linq;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using Xunit;
using Range = ForkPlus.Range;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SideBySideVisualPatchAlignmentTests
	{
		private static Diff MakeDiff(string[] lines, params SubChunk[] subChunks)
		{
			var chunk = new Chunk(1, lines.Length, 1, lines.Length, null, subChunks);
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
		}

		private static int CountLines(string stringValue)
		{
			// 每个视觉行（含 Alignment 空行/pragma 行）都以 \n 结尾
			return stringValue.Count((char c) => c == '\n');
		}

		private static (VisualPatch old, VisualPatch @new) CreateSideBySidePatches(Diff diff)
		{
			VisualPatch oldPatch;
			VisualPatch newPatch;
			VisualPatch.CreateSideBySideVisualPatch(diff, entireFile: false, DiffLocation.Revision, out oldPatch, out newPatch);
			return (oldPatch, newPatch);
		}

		private static void AssertSideBySideAligned(Diff diff, string scenario)
		{
			var (oldPatch, newPatch) = CreateSideBySidePatches(diff);
			int left = CountLines(oldPatch.StringValue);
			int right = CountLines(newPatch.StringValue);
			Assert.True(left == right, $"{scenario}: SideBySide 左右行数应相等，实际 left={left}, right={right}");
		}

		// 用 LineType.Pragma 计数而非文案断言（文案随 UiLanguage 本地化，如中文
		// "文件末尾没有换行符"，断言英文会因测试机语言设置而脆断）
		private static int CountPragmaLines(VisualPatch patch)
		{
			return patch.VisualDiff.VisualChunks.Sum((VisualChunk c) => c.VisualLines.Count((VisualLine l) => l.Type == LineType.Pragma));
		}

		[Fact]
		public void SideBySide_BaselineWithoutPragma_StaysAligned()
		{
			// 1删2增（Added 多）与 2删1增（Deleted 多）：原有 max(0, A-D)/max(0, D-A) 逻辑必须保持
			var lines1 = new[]
			{
				"ctx1\n", "ctx2\n",
				"del1\n",
				"add1\n", "add2\n",
				"ctx3\n", "ctx4\n"
			};
			var sc1 = new SubChunk(new Range(0, 2), new Range(2, 3), new Range(3, 5), new Range(5, 7), NoNewLineAtEndOfFile.None);
			AssertSideBySideAligned(MakeDiff(lines1, sc1), "基线 1删2增");

			var lines2 = new[]
			{
				"ctx1\n", "ctx2\n",
				"del1\n", "del2\n",
				"add1\n",
				"ctx3\n"
			};
			var sc2 = new SubChunk(new Range(0, 2), new Range(2, 4), new Range(4, 5), new Range(5, 6), NoNewLineAtEndOfFile.None);
			AssertSideBySideAligned(MakeDiff(lines2, sc2), "基线 2删1增");
		}

		[Fact]
		public void SideBySide_OnlyOldFileLacksNewline_LeftAndRightAligned()
		{
			// git 真实场景：old="a\nx"（x 无换行）→ new="a\nb\nc\n"
			// diff: a / -x + "\ No newline" / +b +c（Deleted 标记，1删2增）
			var lines = new[] { "a\n", "x\n", "b\n", "c\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 4), new Range(4, 4), NoNewLineAtEndOfFile.Deleted);
			var diff = MakeDiff(lines, sc);
			AssertSideBySideAligned(diff, "旧文件缺末行换行（Deleted）");
			// 修复前左侧多 1 行（pragma 无对侧补偿）；同时验证 pragma 只出现在左侧
			var (oldPatch, newPatch) = CreateSideBySidePatches(diff);
			Assert.Equal(1, CountPragmaLines(oldPatch));
			Assert.Equal(0, CountPragmaLines(newPatch));
		}

		[Fact]
		public void SideBySide_OnlyNewFileLacksNewline_LeftAndRightAligned()
		{
			// 镜像场景：new 侧缺末行换行（Added 标记）。修复前右侧多 1 行
			var lines = new[] { "a\n", "x\n", "b\n", "c\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 4), new Range(4, 4), NoNewLineAtEndOfFile.Added);
			var diff = MakeDiff(lines, sc);
			AssertSideBySideAligned(diff, "新文件缺末行换行（Added）");
			var (oldPatch, newPatch) = CreateSideBySidePatches(diff);
			Assert.Equal(0, CountPragmaLines(oldPatch));
			Assert.Equal(1, CountPragmaLines(newPatch));
		}

		[Fact]
		public void SideBySide_OnlyNewFileLacksNewline_WithMoreDeleted_Aligned()
		{
			// 2删1增 + Added 标记：右侧 = 1 add + pragma + 1 对齐空行 = 3 行；左侧 = 2 del + 1 对齐空行 = 3 行
			var lines = new[] { "a\n", "x\n", "y\n", "b\n", "ctx\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 3), new Range(3, 4), new Range(4, 5), NoNewLineAtEndOfFile.Added);
			AssertSideBySideAligned(MakeDiff(lines, sc), "2删1增+Added 标记");
		}

		[Fact]
		public void SideBySide_BothFilesLackNewline_LeftAndRightAligned()
		{
			// 两侧都缺末行换行：两侧各得一条 pragma，天然平衡，守卫修复不引入回归
			var lines = new[] { "a\n", "x\n", "b\n", "c\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 4), new Range(4, 4), NoNewLineAtEndOfFile.Deleted | NoNewLineAtEndOfFile.Added);
			AssertSideBySideAligned(MakeDiff(lines, sc), "两侧都缺末行换行");
		}

		[Fact]
		public void SideBySide_ContextAtEofWithoutNewline_PragmaVisibleOnBothSidesAndAligned()
		{
			// 末行为未变 context 且无换行（Context 标记）：
			// 1) 两侧行数相等；2) 标记不再被静默丢弃（两侧各一条，与 Split/PatchExtensions 一致）
			var lines = new[] { "a\n", "x\n", "y\n", "b\n", "c\n", "z\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 3), new Range(3, 5), new Range(5, 6), NoNewLineAtEndOfFile.Context);
			var diff = MakeDiff(lines, sc);
			AssertSideBySideAligned(diff, "末行 context 无换行");
			var (oldPatch, newPatch) = CreateSideBySidePatches(diff);
			// 标记不再被静默丢弃：两侧各一条（修复前误查 Deleted 位恒不命中 → 0 条）
			Assert.Equal(1, CountPragmaLines(oldPatch));
			Assert.Equal(1, CountPragmaLines(newPatch));
		}

		[Fact]
		public void SideBySide_MultipleSubChunksWithMixedFlags_AllAligned()
		{
			// 多 subchunk 混合：中段 1删2增无标记 + 末段 1删1增 + Deleted 标记。
			// 行数组按 [pre0][del0][add0][post0=pre1][del1][add1] 布局。
			var lines = new[]
			{
				"p\n",             // 0  subchunk0 pre
				"d0\n",            // 1  subchunk0 deleted
				"a0\n", "a1\n",    // 2-3 subchunk0 added
				"q\n",             // 4  subchunk0 post = subchunk1 pre
				"d1\n",            // 5  subchunk1 deleted（无换行标记）
				"a2\n"             // 6  subchunk1 added
			};
			var sc0 = new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 4), new Range(4, 5), NoNewLineAtEndOfFile.None);
			var sc1 = new SubChunk(new Range(4, 5), new Range(5, 6), new Range(6, 7), new Range(7, 7), NoNewLineAtEndOfFile.Deleted);
			AssertSideBySideAligned(MakeDiff(lines, sc0, sc1), "多 subchunk 混合标记");
		}

		[Fact]
		public void SideBySide_ChangeRegionRows_ArePairedBlockWise()
		{
			// 行级配对验证（1删1增 + Deleted 标记）：change 块第 1 行必须是 del↔add 同行，
			// 第 2 行左侧 pragma ↔ 右侧 Alignment 空行
			var lines = new[] { "a\n", "x\n", "b\n", "c\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 3), new Range(3, 4), NoNewLineAtEndOfFile.Deleted);
			var diff = MakeDiff(lines, sc);
			var (oldPatch, newPatch) = CreateSideBySidePatches(diff);
			var oldTypes = oldPatch.VisualDiff.VisualChunks[0].VisualLines.Select((VisualLine l) => l.Type).ToArray();
			var newTypes = newPatch.VisualDiff.VisualChunks[0].VisualLines.Select((VisualLine l) => l.Type).ToArray();
			// 注意：@@ 头行直接写 StringBuilder，不生成 VisualLine，故首行即 precontext。
			// 左侧：Context, Deleted, Pragma, Context；右侧：Context, Added, Alignment(镜像 pragma), Context
			Assert.Equal(new[] { LineType.Context, LineType.Deleted, LineType.Pragma, LineType.Context }, oldTypes);
			Assert.Equal(new[] { LineType.Context, LineType.Added, LineType.Alignment, LineType.Context }, newTypes);
		}

		[Fact]
		public void Split_WithNoNewlinePragma_HasNoAlignmentLines()
		{
			// Split 视图（CreateVisualPatch）内联显示 del+add 块，不应混入 Alignment 空行
			var lines = new[] { "a\n", "x\n", "b\n", "c\n" };
			var sc = new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 4), new Range(4, 4), NoNewLineAtEndOfFile.Deleted);
			var patch = VisualPatch.CreateVisualPatch(MakeDiff(lines, sc), entireFile: false, DiffLocation.Revision);
			Assert.NotNull(patch);
			Assert.DoesNotContain(patch.VisualDiff.VisualChunks[0].VisualLines, (VisualLine l) => l.Type == LineType.Alignment);
			// Split 内联（@@ 头行不在 VisualLines 中）：Context, Deleted, Pragma, Added, Added
			var types = patch.VisualDiff.VisualChunks[0].VisualLines.Select((VisualLine l) => l.Type).ToArray();
			Assert.Equal(new[] { LineType.Context, LineType.Deleted, LineType.Pragma, LineType.Added, LineType.Added }, types);
		}
	}
}
