using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	public partial class SwipeImageDiffUserControl : UserControl
	{

		public SwipeImageDiffUserControl()
		{
			InitializeComponent();
			PluginEnvironment.ApplyLocalization(this);
			base.SizeChanged += delegate
			{
				RefreshOverlayImageSize();
			};
			PluginEnvironment.ImageDiffHighlightPixelsChanged += delegate(bool newValue)
			{
				RefreshHighlightImageDiff();
			};
			RefreshHighlightImageDiff();
		}

		/// <summary>v4.3.1：与并排视图共享的缩放/平移状态（由 BinaryDiffUserControl 注入同一实例）。</summary>
		[Null]
		public ImageZoomState ZoomState
		{
			get
			{
				return OverlayImage.ZoomState;
			}
			set
			{
				OverlayImage.ZoomState = value;
			}
		}

		public void Refresh(ImageData oldImageData, ImageData newImageData, global::Avalonia.Media.Imaging.Bitmap diffImageSource, bool showTitle)
		{
			if (oldImageData == null || newImageData == null)
			{
				return;
			}
			global::Avalonia.Media.Imaging.Bitmap imageSource = oldImageData.ImageSource;
			if (imageSource == null)
			{
				return;
			}
			global::Avalonia.Media.Imaging.Bitmap imageSource2 = newImageData.ImageSource;
			if (imageSource2 != null)
			{
				OverlayImage.SetContent(imageSource, imageSource2, diffImageSource);
				RefreshOverlayImageSize();
				RefreshLfsLabel(NewLfsLabel, NewNotLfsLabel, newImageData);
				RefreshLfsLabel(OldLfsLabel, OldNotLfsLabel, oldImageData);
				if (showTitle)
				{
					OldTextBlock.IsVisible = true;
					NewTextBlock.IsVisible = true;
				}
				else
				{
					OldTextBlock.IsVisible = false;
					NewTextBlock.IsVisible = false;
				}
			}
		}

		private void RefreshOverlayImageSize()
		{
			double num = base.Bounds.Height - 35.0 - 9.0 - 9.0 - 40.0;
			double num2 = base.Bounds.Width - 10.0 - 10.0 - 9.0 - 9.0;
			if (num > 0.0 && num2 > 0.0)
			{
				OverlayImage.ParentBounds = new Size(num2, num);
				RefreshClipX();
			}
		}

		private void RefreshHighlightImageDiff()
		{
			OverlayImage.HighlightImageDiff = PluginEnvironment.HighlightImageDiff;
		}

		private void RefreshClipX()
		{
			OverlayImage.ClipX = ClipXPlaceholderGrid.Bounds.Width;
		}

		private void RefreshLfsLabel(global::Avalonia.Controls.Control lfsLabel, global::Avalonia.Controls.Control notLfsLabel, ImageData imageData) // Migration note：WPF Label → XAML 已改 TextBlock/ContentControl，签名放宽为 Control。
		{
			if (imageData.IsLfs)
			{
				lfsLabel.IsVisible = true;
				notLfsLabel.IsVisible = false;
				return;
			}
			lfsLabel.IsVisible = false;
			if (imageData.IsTracked && imageData.FileSize > 500000)
			{
				notLfsLabel.IsVisible = true;
				global::Avalonia.Controls.ToolTip.SetTip(notLfsLabel,string.Format(PluginEnvironment.Translate("File is {0} and is not managed by LFS"), PluginSizeFormat.ReadableFileSize(imageData.FileSize)));
			}
			else
			{
				notLfsLabel.IsVisible = false;
			}
		}

		private void ClipXPlaceholderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			RefreshClipX();
		}

	}
}
