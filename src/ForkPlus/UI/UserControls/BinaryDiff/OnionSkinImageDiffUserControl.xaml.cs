// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap（别名替代 BitmapSource）
// - 新增 using Avalonia.Controls.Primitives（RoutedPropertyChangedEventArgs<double>，Slider.ValueChanged）
// - BitmapSource → Bitmap（Avalonia.Media.Imaging.Bitmap，参考 IconTools）
// - WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(..., "ImageDiffHighlightPixelsChanged", ...)
//   → NotificationCenter.Current.ImageDiffHighlightPixelsChanged += ...（直接事件订阅，参考 RevisionListViewUserControl）
// - ActualHeight/ActualWidth → Bounds.Height/Bounds.Width（参考 DragAndDropListViewItem）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public partial class OnionSkinImageDiffUserControl : UserControl
	{

		public OnionSkinImageDiffUserControl()
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			base.SizeChanged += delegate
			{
				RefreshOverlayImageSize();
			};
			NotificationCenter.Current.ImageDiffHighlightPixelsChanged += delegate
			{
				RefreshHighlightImageDiff();
			};
			RefreshHighlightImageDiff();
		}

		public void Refresh(ImageData oldImageData, ImageData newImageData, Bitmap diffImageSource, bool showTitle)
		{
			if (oldImageData == null || newImageData == null)
			{
				return;
			}
			Bitmap imageSource = oldImageData.ImageSource;
			if (imageSource == null)
			{
				return;
			}
			Bitmap imageSource2 = newImageData.ImageSource;
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
				RefreshOverlayImageOpacity();
			}
		}

		private void RefreshHighlightImageDiff()
		{
			OverlayImage.HighlightImageDiff = ForkPlusSettings.Default.ImageDiffHighlightPixels;
		}

		private void RefreshOverlayImageOpacity()
		{
			OverlayImage.NewOpacity = Slider.Value;
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

		private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			RefreshOverlayImageOpacity();
		}

	}
}
