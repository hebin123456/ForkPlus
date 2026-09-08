using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media.Imaging;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public partial class SwipeImageDiffUserControl : UserControl
	{

		public SwipeImageDiffUserControl()
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			base.SizeChanged += delegate
			{
				RefreshOverlayImageSize();
			};
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"ImageDiffHighlightPixelsChanged",delegate
(object sender, global::System.EventArgs e)			{
				RefreshHighlightImageDiff();
			});
			RefreshHighlightImageDiff();
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
					OldTextBlock.Show();
					NewTextBlock.Show();
				}
				else
				{
					OldTextBlock.Collapse();
					NewTextBlock.Collapse();
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
			OverlayImage.HighlightImageDiff = ForkPlusSettings.Default.ImageDiffHighlightPixels;
		}

		private void RefreshClipX()
		{
			OverlayImage.ClipX = ClipXPlaceholderGrid.Bounds.Width;
		}

		private void RefreshLfsLabel(global::Avalonia.Controls.Control lfsLabel, global::Avalonia.Controls.Control notLfsLabel, ImageData imageData) // Migration note：WPF Label → XAML 已改 TextBlock/ContentControl，签名放宽为 Control。
		{
			if (imageData.IsLfs)
			{
				lfsLabel.Show();
				notLfsLabel.Collapse();
				return;
			}
			lfsLabel.Collapse();
			if (imageData.IsTracked && imageData.FileSize > 500000)
			{
				notLfsLabel.Show();
				global::Avalonia.Controls.ToolTip.SetTip(notLfsLabel,string.Format(PreferencesLocalization.Translate("File is {0} and is not managed by LFS", ForkPlusSettings.Default.UiLanguage), FileHelper.GetReadableFileSize(imageData.FileSize)));
			}
			else
			{
				notLfsLabel.Collapse();
			}
		}

		private void ClipXPlaceholderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			RefreshClipX();
		}

	}
}
