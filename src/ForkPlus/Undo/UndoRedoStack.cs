using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Commands;

namespace ForkPlus.Undo
{
	/// <summary>
	/// Undo/Redo 栈管理。每个 RepositoryUserControl 持有一个实例。
	///
	/// 栈语义：
	/// - UndoStack 保存"操作前快照"，peek 顶部即最近一次操作前的状态
	/// - RedoStack 保存"被 undo 的状态"，peek 顶部即最近被 undo 的状态
	/// - 新操作发生时清空 RedoStack
	/// - 操作失败时把刚 push 的快照弹出（不入栈，避免 Undo 一个没发生的操作）
	/// - 栈深度上限 50，超出丢弃最底部
	/// </summary>
	public class UndoRedoStack
	{
		public const int MaxDepth = 50;

		private readonly LinkedList<RepositorySnapshot> _undoStack = new LinkedList<RepositorySnapshot>();
		private readonly LinkedList<RepositorySnapshot> _redoStack = new LinkedList<RepositorySnapshot>();

		public bool CanUndo => _undoStack.Count > 0;
		public bool CanRedo => _redoStack.Count > 0;

		/// <summary>
		/// P3.3：因超过 MaxDepth 被丢弃的 undo 快照数量。
		/// 可在下拉历史底部提示用户"X 个早期操作未在历史中，可通过 reflog 恢复"。
		/// </summary>
		public int LostCount { get; private set; }

		/// <summary>最近一次记录的操作名（用于工具栏 tooltip）。</summary>
		public string LastUndoOperationName => _undoStack.Count > 0 ? _undoStack.First.Value.OperationName : null;

		/// <summary>最近一次 redo 的操作名。</summary>
		public string LastRedoOperationName => _redoStack.Count > 0 ? _redoStack.First.Value.OperationName : null;

		/// <summary>Undo 栈快照列表（从最近到最远，用于下拉历史）。</summary>
		public IReadOnlyList<RepositorySnapshot> UndoHistory => _undoStack.ToList();

		/// <summary>Redo 栈快照列表（从最近到最远）。</summary>
		public IReadOnlyList<RepositorySnapshot> RedoHistory => _redoStack.ToList();

		/// <summary>操作前记录快照。新操作发生时清空 redo 栈。</summary>
		public void RecordBeforeOperation(RepositorySnapshot snapshot)
		{
			if (snapshot == null)
			{
				return;
			}
			_undoStack.AddFirst(snapshot);
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
		/// 弹出 Undo 栈顶快照（即最近一次操作前的状态），并把当前状态推入 Redo 栈。
		/// 返回值：要恢复到的目标快照。
		/// </summary>
		public RepositorySnapshot PopForUndo(RepositorySnapshot currentSnapshot)
		{
			if (_undoStack.Count == 0)
			{
				return null;
			}
			RepositorySnapshot prev = _undoStack.First.Value;
			_undoStack.RemoveFirst();
			if (currentSnapshot != null)
			{
				_redoStack.AddFirst(currentSnapshot);
			}
			return prev;
		}

		/// <summary>
		/// 弹出 Redo 栈顶快照（即最近被 undo 的状态），并把当前状态推入 Undo 栈。
		/// 返回值：要恢复到的目标快照。
		/// </summary>
		public RepositorySnapshot PopForRedo(RepositorySnapshot currentSnapshot)
		{
			if (_redoStack.Count == 0)
			{
				return null;
			}
			RepositorySnapshot next = _redoStack.First.Value;
			_redoStack.RemoveFirst();
			if (currentSnapshot != null)
			{
				_undoStack.AddFirst(currentSnapshot);
			}
			return next;
		}

		/// <summary>跳转到指定历史项（用于下拉历史直接选择某步）。</summary>
		public RepositorySnapshot JumpTo(RepositorySnapshot target, RepositorySnapshot currentSnapshot)
		{
			// 在 undo 栈中找到 target
			LinkedListNode<RepositorySnapshot> node = _undoStack.First;
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
					RepositorySnapshot result = _undoStack.First.Value;
					_undoStack.RemoveFirst();
					if (currentSnapshot != null)
					{
						_redoStack.AddFirst(currentSnapshot);
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
					RepositorySnapshot result = _redoStack.First.Value;
					_redoStack.RemoveFirst();
					if (currentSnapshot != null)
					{
						_undoStack.AddFirst(currentSnapshot);
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
