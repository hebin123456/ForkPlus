using System.ComponentModel;
using System.Windows.Media;

namespace ForkPlus.UI.UserControls
{
	public class UserColorViewModel : INotifyPropertyChanged
	{
		public bool _isSelected;

		public int BrushIndex { get; }

		public SolidColorBrush Brush { get; }

		public bool IsSelected
		{
			get
			{
				return _isSelected;
			}
			set
			{
				if (_isSelected != value)
				{
					_isSelected = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public UserColorViewModel(int brushIndex, SolidColorBrush brush, bool isSelected)
		{
			BrushIndex = brushIndex;
			Brush = brush;
			IsSelected = isSelected;
		}
	}
}
