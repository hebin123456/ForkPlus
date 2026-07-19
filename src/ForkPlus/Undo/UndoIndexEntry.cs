using System;

namespace ForkPlus.Undo
{
	/// <summary>
	/// v3.3.0：.git/forkplus-undo-index.json 的单条记录。
	/// 用作 reflog 之上的元数据层：为 reflog 条目附加 UI 友好的操作名。
	///
	/// reflog 自身只记录 git 原生 message（如 "commit: fix: bug" / "reset: moving to HEAD~1"），
	/// 对用户不够友好。本索引通过 HeadSha 与 reflog 条目做 join，渲染历史列表时优先用 OperationName。
	///
	/// 持久化策略：
	/// - 存储位置：.git/forkplus-undo-index.json（与 reflog 同生命周期，clone 后是空的）
	/// - 与 reflog 不同步时降级显示 reflog 原生 message，不报错
	/// - 文件损坏/缺失时静默删除并重建，不阻断 Undo/Redo
	/// </summary>
	public sealed class UndoIndexEntry
	{
		/// <summary>HEAD commit sha（40 字符），用作与 reflog 条目的 join key。</summary>
		public string HeadSha { get; set; }

		/// <summary>UI 友好的操作名，例如 "Commit 'fix: bug'" / "Checkout 'feature/x'"。</summary>
		public string OperationName { get; set; }

		/// <summary>记录时间（UTC ISO 8601）。</summary>
		public DateTime TimestampUtc { get; set; }

		/// <summary>操作类型（Commit/Checkout/Reset/Merge/Rebase/CherryPick/Revert/Stash/Tag 等），
		/// 预留给后续 v3.4+ 的 UI 图标。</summary>
		public string OperationType { get; set; }

		public UndoIndexEntry()
		{
			// Newtonsoft.Json 反序列化需要无参构造
		}

		public UndoIndexEntry(string headSha, string operationName, DateTime timestampUtc, string operationType = null)
		{
			HeadSha = headSha;
			OperationName = operationName ?? "";
			TimestampUtc = timestampUtc;
			OperationType = operationType;
		}
	}
}
