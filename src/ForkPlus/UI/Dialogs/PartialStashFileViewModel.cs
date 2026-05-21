using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public class PartialStashFileViewModel : INotifyPropertyChanged
	{
		public ChangedFile ChangedFile { get; }

		public string FilePath { get; }

		public ImageSource FileTypeIcon { get; }

		public bool Selected { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;

		public PartialStashFileViewModel(ChangedFile changedFile, string filePath, bool selected)
		{
			ChangedFile = changedFile;
			FilePath = filePath;
			Selected = selected;
			FileTypeIcon = IconTools.GetImageSourceForExtension(Path.GetExtension(filePath));
		}
	}
}
