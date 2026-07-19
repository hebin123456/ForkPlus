using System;
using System.Collections.Generic;
using ForkPlus.Undo;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.3.0 Undo/Redo 栈管理单元测试。覆盖栈空、MaxDepth、LostCount、JumpTo、CancelLastRecord 等纯逻辑。
	/// 重构说明（v3.3.0）：栈元素从 RepositorySnapshot 改为 UndoEntry（4 字段：HeadSha/CurrentBranchName/OperationName/TimestampUtc）。
	/// 不依赖 GitModule / WPF / Dispatcher。
	/// </summary>
	public class UndoRedoStackTests
	{
		private static UndoEntry MakeEntry(string opName, string headSha = null)
		{
			return new UndoEntry(headSha, "main", opName, DateTime.UtcNow);
		}

		[Fact]
		public void NewStack_CannotUndoOrRedo()
		{
			UndoRedoStack stack = new UndoRedoStack();
			Assert.False(stack.CanUndo);
			Assert.False(stack.CanRedo);
			Assert.Equal(0, stack.LostCount);
			Assert.Null(stack.LastUndoOperationName);
			Assert.Null(stack.LastRedoOperationName);
			Assert.Empty(stack.UndoHistory);
			Assert.Empty(stack.RedoHistory);
		}

		[Fact]
		public void RecordBeforeOperation_NullEntry_SilentlySkipped()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(null);
			Assert.False(stack.CanUndo);
			Assert.Equal(0, stack.LostCount);
		}

		[Fact]
		public void RecordBeforeOperation_EnablesUndo_AndClearsRedo()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(MakeEntry("op1", "sha1"));
			Assert.True(stack.CanUndo);
			Assert.False(stack.CanRedo);
			Assert.Equal("op1", stack.LastUndoOperationName);

			// undo 一次，让 redo 栈有内容
			UndoEntry current = MakeEntry("current", "sha2");
			stack.PopForUndo(current);
			Assert.True(stack.CanRedo);

			// 新操作发生，redo 应被清空
			stack.RecordBeforeOperation(MakeEntry("op2", "sha3"));
			Assert.False(stack.CanRedo);
			Assert.True(stack.CanUndo);
		}

		[Fact]
		public void PopForUndo_EmptyStack_ReturnsNull()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry result = stack.PopForUndo(MakeEntry("current"));
			Assert.Null(result);
		}

		[Fact]
		public void PopForRedo_EmptyStack_ReturnsNull()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry result = stack.PopForRedo(MakeEntry("current"));
			Assert.Null(result);
		}

		[Fact]
		public void PopForUndo_PushesCurrentToRedoStack()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry before = MakeEntry("before-op", "sha1");
			stack.RecordBeforeOperation(before);

			UndoEntry current = MakeEntry("current", "sha2");
			UndoEntry target = stack.PopForUndo(current);

			Assert.Same(before, target);
			Assert.False(stack.CanUndo);
			Assert.True(stack.CanRedo);
			Assert.Same(current, stack.RedoHistory[0]);
		}

		[Fact]
		public void PopForUndo_NullCurrent_OnlyPopsWithoutPushingRedo()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry before = MakeEntry("before-op", "sha1");
			stack.RecordBeforeOperation(before);

			UndoEntry target = stack.PopForUndo(null);

			Assert.Same(before, target);
			Assert.False(stack.CanUndo);
			Assert.False(stack.CanRedo);
		}

		[Fact]
		public void PopForRedo_PushesCurrentToUndoStack()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(MakeEntry("op1", "sha1"));
			UndoEntry current1 = MakeEntry("current1", "sha2");
			stack.PopForUndo(current1);
			Assert.False(stack.CanUndo);

			UndoEntry target = stack.PopForRedo(current1);

			Assert.Same(current1, target);
			Assert.True(stack.CanUndo);
			Assert.False(stack.CanRedo);
		}

		[Fact]
		public void CancelLastRecord_EmptyStack_DoesNotThrow()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.CancelLastRecord();  // should not throw
			Assert.False(stack.CanUndo);
		}

		[Fact]
		public void CancelLastRecord_RemovesTopEntry()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(MakeEntry("op1"));
			stack.RecordBeforeOperation(MakeEntry("op2"));
			stack.CancelLastRecord();

			Assert.True(stack.CanUndo);
			Assert.Equal("op1", stack.LastUndoOperationName);
		}

		[Fact]
		public void MaxDepth_Exceeded_DropsOldestAndIncrementsLostCount()
		{
			UndoRedoStack stack = new UndoRedoStack();
			for (int i = 0; i < UndoRedoStack.MaxDepth + 5; i++)
			{
				stack.RecordBeforeOperation(MakeEntry("op" + i, "sha" + i));
			}
			Assert.Equal(UndoRedoStack.MaxDepth, stack.UndoHistory.Count);
			Assert.Equal(5, stack.LostCount);
			// 最顶上应是最近一次操作
			Assert.Equal("op" + (UndoRedoStack.MaxDepth + 4), stack.LastUndoOperationName);
		}

		[Fact]
		public void MaxDepth_AtLimit_LostCountRemainsZero()
		{
			UndoRedoStack stack = new UndoRedoStack();
			for (int i = 0; i < UndoRedoStack.MaxDepth; i++)
			{
				stack.RecordBeforeOperation(MakeEntry("op" + i));
			}
			Assert.Equal(UndoRedoStack.MaxDepth, stack.UndoHistory.Count);
			Assert.Equal(0, stack.LostCount);
		}

		[Fact]
		public void Clear_RemovesAllAndResetsLostCount()
		{
			UndoRedoStack stack = new UndoRedoStack();
			for (int i = 0; i < UndoRedoStack.MaxDepth + 3; i++)
			{
				stack.RecordBeforeOperation(MakeEntry("op" + i));
			}
			Assert.True(stack.LostCount > 0);

			stack.Clear();

			Assert.False(stack.CanUndo);
			Assert.False(stack.CanRedo);
			Assert.Equal(0, stack.LostCount);
			Assert.Empty(stack.UndoHistory);
			Assert.Empty(stack.RedoHistory);
		}

		[Fact]
		public void JumpTo_TargetInUndoStack_MovesAboveToRedo()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry s1 = MakeEntry("op1");
			UndoEntry s2 = MakeEntry("op2");
			UndoEntry s3 = MakeEntry("op3");
			stack.RecordBeforeOperation(s1);
			stack.RecordBeforeOperation(s2);
			stack.RecordBeforeOperation(s3);
			// undoStack（顶→底）: [s3, s2, s1]

			// 跳到最早的 s1，之上的 s3/s2 应进 redo（按原始栈顺序依次 AddFirst 入 redo 顶），
			// 最后 current 也 AddFirst 到 redo 顶。
			UndoEntry current = MakeEntry("current");
			UndoEntry target = stack.JumpTo(s1, current);

			Assert.Same(s1, target);
			Assert.False(stack.CanUndo);
			Assert.True(stack.CanRedo);
			// redo 栈（顶→底）：current, s2, s3
			// 推导：循环先把 s3 AddFirst 进 redo，再把 s2 AddFirst（s2 在 s3 之上），最后 current AddFirst
			Assert.Same(current, stack.RedoHistory[0]);
			Assert.Same(s2, stack.RedoHistory[1]);
			Assert.Same(s3, stack.RedoHistory[2]);
		}

		[Fact]
		public void JumpTo_TargetInRedoStack_MovesAboveToUndo()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry s1 = MakeEntry("op1");
			UndoEntry s2 = MakeEntry("op2");
			stack.RecordBeforeOperation(s1);
			stack.RecordBeforeOperation(s2);
			// undoStack: [s2, s1]

			// 两次 PopForUndo 清空 undo 栈，redo 栈持有 current entries（不是 s1/s2）
			UndoEntry c1 = MakeEntry("c1");
			UndoEntry c2 = MakeEntry("c2");
			stack.PopForUndo(c1);  // pops s2, pushes c1 to redo
			stack.PopForUndo(c2);  // pops s1, pushes c2 to redo
			// undoStack: [], redoStack: [c2, c1]
			Assert.False(stack.CanUndo);
			Assert.Equal(2, stack.RedoHistory.Count);

			// 跳到 redo 栈底的 c1：c2 应被移到 undo，c1 弹出，current 推入 undo
			UndoEntry current = MakeEntry("current");
			UndoEntry target = stack.JumpTo(c1, current);

			Assert.Same(c1, target);
			Assert.True(stack.CanUndo);  // undoStack: [current, c2]
			Assert.False(stack.CanRedo); // redoStack: []
		}

		[Fact]
		public void JumpTo_TargetNotInStack_ReturnsNull()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(MakeEntry("op1"));

			UndoEntry orphan = MakeEntry("orphan");
			UndoEntry result = stack.JumpTo(orphan, MakeEntry("current"));

			Assert.Null(result);
			Assert.True(stack.CanUndo);  // 栈未动
		}

		[Fact]
		public void JumpTo_NullCurrent_StillWorks()
		{
			UndoRedoStack stack = new UndoRedoStack();
			UndoEntry s1 = MakeEntry("op1");
			UndoEntry s2 = MakeEntry("op2");
			stack.RecordBeforeOperation(s1);
			stack.RecordBeforeOperation(s2);

			UndoEntry target = stack.JumpTo(s1, null);

			Assert.Same(s1, target);
			Assert.False(stack.CanUndo);
			// redo 栈里不应有 null
			Assert.All(stack.RedoHistory, s => Assert.NotNull(s));
			Assert.Equal(1, stack.RedoHistory.Count);  // 只有 s2
		}

		[Fact]
		public void UndoHistory_OrderedFromMostRecentToOldest()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(MakeEntry("op1"));
			stack.RecordBeforeOperation(MakeEntry("op2"));
			stack.RecordBeforeOperation(MakeEntry("op3"));

			IReadOnlyList<UndoEntry> history = stack.UndoHistory;
			Assert.Equal(3, history.Count);
			Assert.Equal("op3", history[0].OperationName);
			Assert.Equal("op2", history[1].OperationName);
			Assert.Equal("op1", history[2].OperationName);
		}

		[Fact]
		public void Record_Undo_Redo_RedoClearedOnNewRecord()
		{
			UndoRedoStack stack = new UndoRedoStack();
			stack.RecordBeforeOperation(MakeEntry("op1", "sha1"));
			stack.RecordBeforeOperation(MakeEntry("op2", "sha2"));

			// Undo 一次
			stack.PopForUndo(MakeEntry("cur1", "sha3"));
			Assert.True(stack.CanRedo);

			// 再 Undo 一次
			stack.PopForUndo(MakeEntry("cur2", "sha4"));
			Assert.True(stack.CanRedo);
			Assert.False(stack.CanUndo);

			// 新操作发生，redo 应清空
			stack.RecordBeforeOperation(MakeEntry("op3", "sha5"));
			Assert.True(stack.CanUndo);
			Assert.False(stack.CanRedo);
		}

		// ===== v3.3.0 新增：UndoEntry 数据结构测试 =====

		[Fact]
		public void UndoEntry_NullOperationName_NormalizedToEmpty()
		{
			UndoEntry entry = new UndoEntry("sha1", "main", null, DateTime.UtcNow);
			Assert.Equal("", entry.OperationName);
		}

		[Fact]
		public void UndoEntry_WithOperationName_PreservesOtherFields()
		{
			DateTime ts = DateTime.UtcNow;
			UndoEntry original = new UndoEntry("sha1", "main", "op1", ts);
			UndoEntry copy = original.WithOperationName("renamed");

			Assert.Equal("sha1", copy.HeadSha);
			Assert.Equal("main", copy.CurrentBranchName);
			Assert.Equal("renamed", copy.OperationName);
			Assert.Equal(ts, copy.TimestampUtc);
		}

		[Fact]
		public void UndoEntry_WithOperationName_NullNormalizesToEmpty()
		{
			UndoEntry original = new UndoEntry("sha1", "main", "op1", DateTime.UtcNow);
			UndoEntry copy = original.WithOperationName(null);

			Assert.Equal("", copy.OperationName);
			Assert.Equal("sha1", copy.HeadSha);
		}
	}
}
