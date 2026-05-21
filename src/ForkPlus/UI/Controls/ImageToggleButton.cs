using System.Windows.Controls;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	internal class ImageToggleButton : Button
	{
		private bool _state;

		private readonly Image _imageControl = new Image();

		public bool State
		{
			get
			{
				return _state;
			}
			set
			{
				_state = value;
				RefreshImages();
			}
		}

		public ImageSource Image { get; set; }

		public ImageSource AlternativeImage { get; set; }

		public ImageToggleButton()
		{
			base.Content = _imageControl;
		}

		private void RefreshImages()
		{
			if (State)
			{
				_imageControl.Source = Image;
			}
			else
			{
				_imageControl.Source = AlternativeImage;
			}
		}
	}
}
