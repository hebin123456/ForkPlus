using System.Collections.Generic;
using System.Linq;

namespace ForkPlus.Undo
{
	/// <summary>
	/// v3.3.0：Undo/Redo 栈管理。每个 RepositoryUserControl 持有一个实例。
	///
	/// 重构说明（v3.3.0）：
	/// - 栈元素从 RepositorySnapshot（11 字段，抓 7 次 git 进程）改为 UndoEntry（4 字段，只读 HEAD + branch）
	/// - HEAD sha 是恢复真相源，恢复用 git reset --hard &lt;sha&gt;（不再 5 步重建分支/tag/stash）
	/// - OperationName 通过 UndoIndexStore 持久化到 .git/forkplus-undo-index.json（跨会话保留）
	/// - 持久化 + CLI 兼容由 reflog 兜底（栈空时仍可从 reflog 视图恢复）
	///
	/// 栈语义（保留 v3.0.0 行为）：
	/// - UndoStack 保存"操作前 entry"，peek 顶部即最近一次操作前的状态
	/// - RedoStack 保存"被 undo 的状态"，peek 顶部即最近被 undo 的状态
	/// - 新操作发生时清空 RedoStack
	/// - 操作失败时把刚 push 的 entry 弹出（不入栈，避免 Undo 一个没发生的操作）
	/// - 栈深度上限 50，超出丢弃最底部
	/// </summary>
	public class UndoRedoStack
	{
		public const int MaxDepth = 50;

		private readonly LinkedList<UndoEntry> _undoStack = new LinkedList<UndoEntry>();
		private readonly LinkedList<UndoEntry> _redoStack = new LinkedList<UndoEntry>();

		public bool CanUndo => _undoStack.Count > 0;
		public bool CanRedo => _redoStack.Count > 0;

		/// <summary>
		/// 因超过 MaxDepth 被丢弃的 undo entry 数量。
		/// 下拉历史底部提示"X 个早期操作未在历史中，可通过 reflog 恢复"。
		/// </summary>
		public int LostCount { get; private set; }

		/// <summary>最近一次记录的操作名（用于工具栏 tooltip）。</summary>
		public string LastUndoOperationName => _undoStack.Count > 0 ? _undoStack.First.Value.OperationName : null;

		/// <summary>最近一次 redo 的操作名。</summary>
		public string LastRedoOperationName => _redoStack.Count > 0 ? _redoStack.First.Value.OperationName : null;

		/// <summary>Undo 栈 entry 列表（从最近到最远，用于下拉历史）。</summary>
		public IReadOnlyList<UndoEntry> UndoHistory => _undoStack.ToList();

		/// <summary>Redo 栈 entry 列表（从最近到最远）。</summary>
		public IReadOnlyList<UndoEntry> RedoHistory => _redoStack.ToList();

		/// <summary>操作前记录 entry。新操作发生时清空 redo 栈。</summary>
		public void RecordBeforeOperation(UndoEntry entry)
		{
			if (entry == null)
			{
				return;
			}
			_undoStack.AddFirst(entry);
			if (_undoStack.Count > MaxDepth)
			{
				_undoStack.RemoveLast();
				LostCount++;
			}
			_redoStack.Clear();
		}

		/// <summary>操作失败时撤销最近一次记录（不入栈）。</summary>
		public void CancelLastRecord()
		{
			if (_undoStack.Count > 0)
			{
				_undoStack.RemoveFirst();
			}
		}

		/// <summary>
		/// 弹出 Undo 栈顶 entry（即最近一次操作前的状态），并把当前状态推入 Redo 栈。
		/// 返回值：要恢复到的目标 entry。
		/// </summary>
		public UndoEntry PopForUndo(UndoEntry currentEntry)
		{
			if (_undoStack.Count == 0)
			{
				return null;
			}
			UndoEntry prev = _undoStack.First.Value;
			_undoStack.RemoveFirst();
			if (currentEntry != null)
			{
				_redoStack.AddFirst(currentEntry);
			}
			return prev;
		}

		/// <summary>
		/// 弹出 Redo 栈顶 entry（即最近被 undo 的状态），并把当前状态推入 Undo 栈。
		/// 返回值：要恢复到的目标 entry。
		/// </summary>
		public UndoEntry PopForRedo(UndoEntry currentEntry)
		{
			if (_redoStack.Count == 0)
			{
				return null;
			}
			UndoEntry next = _redoStack.First.Value;
			_redoStack.RemoveFirst();
			if (currentEntry != null)
			{
				_undoStack.AddFirst(currentEntry);
			}
			return next;
		}

		/// <summary>跳转到指定历史项（用于下拉历史直接选择某步）。</summary>
		public UndoEntry JumpTo(UndoEntry target, UndoEntry currentEntry)
		{
			// 在 undo 栈中找到 target
			LinkedListNode<UndoEntry> node = _undoStack.First;
			while (node != null)
			{
				if (ReferenceEquals(node.Value, target))
				{
					// 把 target 之上所有项移到 redo
					while (_undoStack.First != node)
					{
						_redoStack.AddFirst(_undoStack.First.Value);
						_undoStack.RemoveFirst();
					}
					UndoEntry result = _undoStack.First.Value;
					_undoStack.RemoveFirst();
					if (currentEntry != null)
					{
						_redoStack.AddFirst(currentEntry);
					}
					return result;
				}
				node = node.Next;
			}
			// 在 redo 栈中找
			node = _redoStack.First;
			while (node != null)
			{
				if (ReferenceEquals(node.Value, target))
				{
					while (_redoStack.First != node)
					{
						_undoStack.AddFirst(_redoStack.First.Value);
						_redoStack.RemoveFirst();
					}
					UndoEntry result = _redoStack.First.Value;
					_redoStack.RemoveFirst();
					if (currentEntry != null)
					{
						_undoStack.AddFirst(currentEntry);
					}
					return result;
				}
				node = node.Next;
			}
			return null;
		}

		public void Clear()
		{
			_undoStack.Clear();
			_redoStack.Clear();
			LostCount = 0;
		}
	}
}
