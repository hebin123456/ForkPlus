using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

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
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "ImageDiffHighlightPixelsChanged", delegate
			{
				RefreshHighlightImageDiff();
			});
			RefreshHighlightImageDiff();
		}

		public void Refresh(ImageData oldImageData, ImageData newImageData, BitmapSource diffImageSource, bool showTitle)
		{
			if (oldImageData == null || newImageData == null)
			{
				return;
			}
			BitmapSource imageSource = oldImageData.ImageSource;
			if (imageSource == null)
			{
				return;
			}
			BitmapSource imageSource2 = newImageData.ImageSource;
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
			double num = base.ActualHeight - 35.0 - 9.0 - 9.0 - 40.0;
			double num2 = base.ActualWidth - 10.0 - 10.0 - 9.0 - 9.0;
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
			OverlayImage.ClipX = ClipXPlaceholderGrid.ActualWidth;
		}

		private void RefreshLfsLabel(Label lfsLabel, Label notLfsLabel, ImageData imageData)
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
				notLfsLabel.ToolTip = string.Format(PreferencesLocalization.Translate("File is {0} and is not managed by LFS", ForkPlusSettings.Default.UiLanguage), FileHelper.GetReadableFileSize(imageData.FileSize));
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
