using System;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class FileListEventArgs : EventArgs
	{
		public ChangedFile SelectedFile { get; private set; }

		public FileListEventArgs(ChangedFile selectedItem)
		{
			SelectedFile = selectedItem;
		}
	}
}
