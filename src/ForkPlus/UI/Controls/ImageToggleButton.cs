using Avalonia.Controls;
using Avalonia.Media;

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

		// 阶段 4.5：WPF ImageSource → Avalonia IImage。
		public IImage Image { get; set; }

		public IImage AlternativeImage { get; set; }

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
