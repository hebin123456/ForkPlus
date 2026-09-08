using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

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
		public global::Avalonia.Media.Imaging.Bitmap DiffImageSource { get; set; }

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
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"ImageDiffHighlightPixelsChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateHighlightImageDiff();
			});
			UpdateHighlightImageDiff();
		}

		public void SetContent(BinaryContent content, [Null] string statusLabel = null, [Null] Brush statusBrush = null, [Null] global::Avalonia.Media.Imaging.Bitmap diffImageSource = null)
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
			global::Avalonia.Controls.ToolTip.SetTip(DescriprionTextBlock,FileHelper.GetReadableFileSizeInBytes(valueOrDefault));
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
				if (ShowLfsImageButton.IsVisible != false)
				{
					ShowLfsImageButton.Collapse();
				}
				if (!CancelLfsButton.IsVisible) // Migration note：WPF Visibility != 0（非 Visible）→ !IsVisible。
				{
					CancelLfsButton.Show();
				}
				if (!LfsProgressBar.IsVisible) // Migration note：WPF Visibility != 0（非 Visible）→ !IsVisible。
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

		public void SetLfsImageData(MemoryStream memoryStream, [Null] global::Avalonia.Media.Imaging.Bitmap diffImageSource = null)
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
			global::Avalonia.Media.Imaging.Bitmap bitmapSource = BinaryDiffUserControl.CreateBitmapSource(memoryStream);
			long length = memoryStream.Length;
			DescriprionTextBlock.Text = GetImageDescription(bitmapSource, length);
			// Migration note：WPF BitmapSource.PixelHeight → Avalonia Bitmap.PixelSize.Height。
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
				global::Avalonia.Controls.ToolTip.SetTip(NotLfsLabel,string.Format(PreferencesLocalization.Translate("File is {0} and is not managed by LFS", ForkPlusSettings.Default.UiLanguage), FileHelper.GetReadableFileSize(fileSize)));
			}
			else
			{
				NotLfsLabel.Collapse();
			}
		}

		private static string GetImageDescription([Null] global::Avalonia.Media.Imaging.Bitmap imageSource, long fileSize)
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
