using System;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public interface ICommitTextDiffControl : ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		bool IsStaged { get; set; }

		bool IsNewOrUntracked { get; set; }

		// 修复（2026-09-16）：AI 代码检视页面屏蔽 暂存/丢弃 浮窗的开关，向下透传到 CommitCodeEditor。
		bool ShowStageDiscardButtons { get; set; }

		event EventHandler<CommitCodeEditor> ToggleStage;

		event EventHandler<CommitCodeEditor> Stage;

		event EventHandler<CommitCodeEditor> Unstage;

		event EventHandler<CommitCodeEditor> Discard;
	}
}
