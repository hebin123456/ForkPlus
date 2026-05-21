using System;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public interface ICommitDiffSelectionLayer
	{
		[Null]
		CommitDiffSelectedRange ActiveChunk { get; }

		event EventHandler Stage;

		event EventHandler UnStage;

		event EventHandler Discard;
	}
}
