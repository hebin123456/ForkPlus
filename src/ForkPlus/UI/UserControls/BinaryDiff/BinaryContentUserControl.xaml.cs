// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（Brush/IBrush）
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap（别名替代 BitmapSource）
// - BitmapSource → Bitmap（Avalonia.Media.Imaging.Bitmap，参考 IconTools）
// - WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(..., "ImageDiffHighlightPixelsChanged", ...)
//   → NotificationCenter.Current.ImageDiffHighlightPixelsChanged += ...（直接事件订阅，参考 RevisionListViewUserControl）
// - PixelHeight/PixelWidth → PixelSize.Height/PixelSize.Width（Avalonia Bitmap 用 PixelSize 描述像素尺寸，参考 OverlayImageControl）
// - Visibility != Visibility.Collapsed → IsVisible（Avalonia 以 bool IsVisible 替代 Visibility 枚举，参考 UIElementExtensions）
// - Visibility != 0（0 = Visibility.Visible）→ !IsVisible
// - SetContent 参数 Brush → IBrush（Avalonia 接口类型；TitleTextBlock.Foreground 兼容）
using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public partial class BinaryContentUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public EventHandler<EventArgs> ShowLfsImageButtonClick;

		public EventHandler<EventArgs> CancelLfsButtonClick;

		public EventHandler<EventArgs> SaveAsMenuItemClick;

		private bool _highlightImageDiff;

		private string _statusLabel;

		[Null]
		public Bitmap DiffImageSource { get; set; }

		public bool HighlightImageDiff
		{
			get
			{
				return _highlightImageDiff;
			}
			private set
			{
				_highlightImageDiff = value;
				RefreshDiffImage();
			}
		}

		public BinaryContentUserControl()
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			NotificationCenter.Current.ImageDiffHighlightPixelsChanged += delegate
			{
				UpdateHighlightImageDiff();
			};
			UpdateHighlightImageDiff();
		}

		public void SetContent(BinaryContent content, [Null] string statusLabel = null, [Null] IBrush statusBrush = null, [Null] Bitmap diffImageSource = null)
		{
			_statusLabel = statusLabel;
			DiffImageSource = diffImageSource;
			if (statusBrush != null)
			{
				TitleTextBlock.Foreground = statusBrush;
			}
			TitleTextBlock.Text = string.IsNullOrEmpty(statusLabel) ? "" : PreferencesLocalization.Translate(statusLabel, ForkPlusSettings.Default.UiLanguage);
			long valueOrDefault = (content?.Size).GetValueOrDefault();
			DescriprionTextBlock.Text = FileHelper.GetReadableFileSize(valueOrDefault);
			DescriprionTextBlock.ToolTip = FileHelper.GetReadableFileSizeInBytes(valueOrDefault);
			ImageContainer.Collapse();
			FileContainer.Collapse();
			FileIcon.Collapse();
			DropDownButton.Collapse();
			CancelLfsButton.Collapse();
			ShowLfsImageButton.Collapse();
			FileExtensionTextBlock.Collapse();
			LfsProgressBar.Collapse();
			if (content is ImageContent imageContent)
			{
				RefreshLfsLabel(isLfs: false, valueOrDefault, imageContent.IsTracked);
				MemoryStream memoryStream = imageContent.Data;
				if (Path.GetExtension(imageContent.Path) == ".tga" && memoryStream != null)
				{
					GitCommandResult<MemoryStream> gitCommandResult = BinaryDiffUserControl.DecodeImageData(memoryStream.ToArray());
					if (gitCommandResult.Succeeded)
					{
						memoryStream = gitCommandResult.Result;
					}
					else
					{
						Log.Error(gitCommandResult.Error.FriendlyDescription);
					}
				}
				RefreshImage(memoryStream);
				ImageContainer.Show();
				DropDownButton.Show();
			}
			else if (content is LfsContent lfsContent)
			{
				RefreshLfsLabel(isLfs: true, valueOrDefault, lfsContent.IsTracked);
				FileContainer.Show();
				FileExtensionTextBlock.Show();
				string extension = Path.GetExtension(lfsContent.Path);
				FileExtensionTextBlock.Text = extension;
				DropDownButton.Show();
				if (lfsContent.BinaryFileType == BinaryFileType.LfsImage)
				{
					ShowLfsImageButton.Show();
					FileIcon.Collapse();
				}
				else
				{
					FileExtensionTextBlock.Show();
					FileIcon.Source = IconTools.GetImageSourceForExtension(extension, ShellIconSize.LargeIcon);
					FileIcon.Show();
				}
			}
			else
			{
				FileContainer.Show();
				RefreshLfsLabel(isLfs: false, valueOrDefault, content.IsTracked);
				FileExtensionTextBlock.Show();
				string extension2 = Path.GetExtension(content.Path);
				FileExtensionTextBlock.Text = extension2;
				FileIcon.Source = IconTools.GetImageSourceForExtension(extension2, ShellIconSize.LargeIcon);
				FileIcon.Show();
			}
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			TitleTextBlock.Text = string.IsNullOrEmpty(_statusLabel) ? "" : PreferencesLocalization.Translate(_statusLabel, ForkPlusSettings.Default.UiLanguage);
		}

		public void SetProgress(double? progress)
		{
			if (progress.HasValue)
			{
				double valueOrDefault = progress.GetValueOrDefault();
				if (ShowLfsImageButton.IsVisible)
				{
					ShowLfsImageButton.Collapse();
				}
				if (!CancelLfsButton.IsVisible)
				{
					CancelLfsButton.Show();
				}
				if (!LfsProgressBar.IsVisible)
				{
					LfsProgressBar.Show();
				}
				LfsProgressBar.Value = valueOrDefault;
			}
			else
			{
				CancelLfsButton.Collapse();
				LfsProgressBar.Collapse();
				ShowLfsImageButton.Show();
			}
		}

		public void SetLfsImageData(MemoryStream memoryStream, [Null] Bitmap diffImageSource = null)
		{
			DiffImageSource = diffImageSource;
			RefreshImage(memoryStream);
			ImageContainer.Show();
			FileContainer.Collapse();
			ShowLfsImageButton.Collapse();
			FileExtensionTextBlock.Collapse();
		}

		private void RefreshImage(MemoryStream memoryStream)
		{
			Bitmap bitmapSource = BinaryDiffUserControl.CreateBitmapSource(memoryStream);
			long length = memoryStream.Length;
			DescriprionTextBlock.Text = GetImageDescription(bitmapSource, length);
			double num = ((double?)bitmapSource?.PixelSize.Height) ?? 0.0;
			Image.Source = bitmapSource;
			Image.Height = num;
			DiffImage.Height = num;
			ImageViewBox.MaxHeight = num;
			RefreshDiffImage();
		}

		private void UpdateHighlightImageDiff()
		{
			HighlightImageDiff = ForkPlusSettings.Default.ImageDiffHighlightPixels;
		}

		private void RefreshDiffImage()
		{
			DiffImage.Source = (HighlightImageDiff ? DiffImageSource : null);
		}

		private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
		{
			SaveAsMenuItemClick?.Invoke(sender, EventArgs.Empty);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			CancelLfsButton.Collapse();
			LfsProgressBar.Collapse();
			ShowLfsImageButton.Show();
			CancelLfsButtonClick?.Invoke(sender, EventArgs.Empty);
		}

		private void ShowLfsImageButton_Click(object sender, RoutedEventArgs e)
		{
			ShowLfsImageButtonClick?.Invoke(sender, EventArgs.Empty);
		}

		private void RefreshLfsLabel(bool isLfs, long fileSize, bool isTracked = false)
		{
			if (isLfs)
			{
				LfsLabel.Show();
				NotLfsLabel.Collapse();
				return;
			}
			LfsLabel.Collapse();
			if (isTracked && fileSize > 500000)
			{
				NotLfsLabel.Show();
				NotLfsLabel.ToolTip = string.Format(PreferencesLocalization.Translate("File is {0} and is not managed by LFS", ForkPlusSettings.Default.UiLanguage), FileHelper.GetReadableFileSize(fileSize));
			}
			else
			{
				NotLfsLabel.Collapse();
			}
		}

		private static string GetImageDescription([Null] Bitmap imageSource, long fileSize)
		{
			if (imageSource == null)
			{
				return "";
			}
			string arg = FileSizeFormatter.Format(fileSize);
			return $"W: {imageSource.PixelSize.Width}px | H: {imageSource.PixelSize.Height}px ({arg})";
		}

	}
}
