// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using System.ComponentModel;
using System.IO;
using Avalonia.Media;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public class LfsFileViewModel : IRoundedSelectionListBoxViewModel, INotifyPropertyChanged
	{
		[Null]
		private string _owner;

		private ListBoxSelectionType _selectionType;

		public int Row { get; set; }

		public string Path { get; }

		public IImage FileTypeIcon { get; }

		[Null]
		public string Owner
		{
			get
			{
				return _owner;
			}
			set
			{
				if (!(value == _owner))
				{
					_owner = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Owner"));
				}
			}
		}

		public ListBoxSelectionType SelectionType
		{
			get
			{
				return _selectionType;
			}
			set
			{
				if (_selectionType != value)
				{
					_selectionType = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectionType"));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public LfsFileViewModel(string path, string owner = null)
		{
			Path = path;
			Owner = owner;
			FileTypeIcon = IconTools.GetImageSourceForExtension(System.IO.Path.GetExtension(path));
		}
	}
}
