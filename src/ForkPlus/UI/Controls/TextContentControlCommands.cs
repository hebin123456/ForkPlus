using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls.Commands;

namespace ForkPlus.UI.Controls
{
	public class TextContentControlCommands : CommandContainer
	{
		private OpenFileInExternalEditorCommand _openFileInExternalEditor;

		private CopyCommand _copy;

		private HunkHistoryCommand _hunkHistory;

		public OpenFileInExternalEditorCommand OpenFileInExternalEditor => CommandContainer.Lazy(ref _openFileInExternalEditor);

		public CopyCommand Copy => CommandContainer.Lazy(ref _copy);

		public HunkHistoryCommand HunkHistory => CommandContainer.Lazy(ref _hunkHistory);
	}
}
