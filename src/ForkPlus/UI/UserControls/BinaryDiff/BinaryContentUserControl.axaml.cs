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

		/// <summary>v4.3.2：该侧动图的解码结果（帧序列）。</summary>
		[Null]
		private AnimatedImage _animatedImage;

		/// <summary>v4.3.2：该侧动图的播放控制器。</summary>
		[Null]
		private AnimatedImagePlayer _animatedPlayer;

		/// <summary>v4.3.2：该侧动图的播放控制条（懒创建，静态图不创建）。</summary>
		[Null]
		private AnimatedImagePlaybackBar _playbackBar;

		[Null]
		public global::Avalonia.Media.Imaging.Bitmap DiffImageSource { get; set; }

		/// <summary>v4.3.2：该侧动图播放器；非动图为 null。</summary>
		[Null]
		public AnimatedImagePlayer AnimatedPlayer => _animatedPlayer;

		/// <summary>v4.3.2：该侧是否为动图。</summary>
		public bool HasAnimatedImage => _animatedPlayer != null;

		/// <summary>v4.3.1：与其它图片视图共享的缩放/平移状态（由 BinaryDiffUserControl 注入）。</summary>
		[Null]
		public ImageZoomState ZoomState
		{
			get
			{
				return ImageControl.ZoomState;
			}
			set
			{
				ImageControl.ZoomState = value;
			}
		}

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
			// v4.3.2：换文件先停掉并释放上一份动图（新内容为动图时 RefreshImage 会重建）。
			DetachAnimated();
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
				RefreshImage(memoryStream, imageContent.Path);
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
			// v4.3.2：动图播放控制条的按钮提示也要随语言切换（代码构建的控件不在逻辑树翻译范围内）。
			_playbackBar?.ApplyLocalization();
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

		/// <param name="path">v4.4.0：仓库内路径，供查看器注册表按扩展名判定；未知时可不传。</param>
		public void SetLfsImageData(MemoryStream memoryStream, [Null] global::Avalonia.Media.Imaging.Bitmap diffImageSource = null, [Null] string path = null)
		{
			DiffImageSource = diffImageSource;
			RefreshImage(memoryStream, path);
			ImageContainer.Show();
			FileContainer.Collapse();
			ShowLfsImageButton.Collapse();
			FileExtensionTextBlock.Collapse();
		}

		private void RefreshImage(MemoryStream memoryStream, [Null] string path)
		{
			// v4.4.0：渲染方式改由查看器注册表判定（动图优先于静态图），不再在本控件里硬编码
			// 动图探测——新增格式只需往 BinaryViewerRegistry 注册查看器。
			// v4.5.0：被插件认领的格式（BinaryViewerKind.Plugin）也在这里解析——解码/渲染统一
			// 收敛到 BinaryContentRenderer，本控件不再直接调用 CreateBitmapSource。
			AnimatedImage animated;
			string statusLabel;
			global::Avalonia.Media.Imaging.Bitmap bitmapSource = BinaryContentRenderer.CreateAnimatedOrStatic(path, memoryStream, out animated, out statusLabel);
			if (animated != null)
			{
				AttachAnimated(animated, memoryStream.Length, statusLabel);
				return;
			}
			DetachAnimated();
			long length = memoryStream.Length;
			DescriprionTextBlock.Text = AppendStatusLabel(GetImageDescription(bitmapSource, length), statusLabel);
			// v4.3.1：原 Viewbox 方案在此设 Image.Height / DiffImage.Height / ImageViewBox.MaxHeight
			// （像素高，用于"不放大超过原始尺寸"）。改由 ZoomPanImageControl 承载后，
			// ImageZoomState.FitScale 已复刻该上限（缩放不超过 1），无需再设尺寸。
			ImageControl.Source = bitmapSource;
			RefreshDiffImage();
		}

		/// <summary>v4.5.0：把插件/解码方给出的一行说明拼到尺寸描述后（无说明则原样返回）。</summary>
		private static string AppendStatusLabel(string description, [Null] string statusLabel)
		{
			if (string.IsNullOrEmpty(statusLabel))
			{
				return description;
			}
			return string.IsNullOrEmpty(description) ? statusLabel : description + " · " + statusLabel;
		}

		/// <summary>v4.3.2：装配动图——挂播放器到图片控件、显示底部播放控制条并自动播放。</summary>
		private void AttachAnimated(AnimatedImage animated, long fileSize, [Null] string statusLabel = null)
		{
			DetachAnimated();
			_animatedImage = animated;
			_animatedPlayer = new AnimatedImagePlayer(animated);
			ImageControl.Player = _animatedPlayer;
			ImageControl.Source = animated.Frames[0];
			DescriprionTextBlock.Text = AppendStatusLabel(GetImageDescription(animated.Frames[0], fileSize), statusLabel);
			if (_playbackBar == null)
			{
				_playbackBar = new AnimatedImagePlaybackBar();
			}
			_playbackBar.Player = _animatedPlayer;
			PlaybackBarContainer.Content = _playbackBar;
			PlaybackBarContainer.Show();
			RefreshDiffImage();
			_animatedPlayer.Play();
		}

		/// <summary>v4.3.2：卸载动图——停播、摘掉播放器、隐藏控制条并释放帧内存。</summary>
		private void DetachAnimated()
		{
			if (_playbackBar != null)
			{
				_playbackBar.Player = null;
			}
			PlaybackBarContainer.Content = null;
			PlaybackBarContainer.Collapse();
			ImageControl.Player = null;
			if (_animatedPlayer != null)
			{
				_animatedPlayer.Dispose();
				_animatedPlayer = null;
			}
			if (_animatedImage != null)
			{
				_animatedImage.Dispose();
				_animatedImage = null;
			}
		}

		/// <summary>v4.3.2：暂停该侧动图播放（切换到滑动/洋葱皮/Hex 时由宿主调用，避免后台空转）。</summary>
		public void PauseAnimation()
		{
			_animatedPlayer?.Pause();
		}

		/// <summary>v4.3.2：恢复该侧动图播放（切回并排视图时由宿主调用）。</summary>
		public void ResumeAnimation()
		{
			_animatedPlayer?.Play();
		}

		private void UpdateHighlightImageDiff()
		{
			HighlightImageDiff = ForkPlusSettings.Default.ImageDiffHighlightPixels;
		}

		private void RefreshDiffImage()
		{
			// v4.3.1：掩码与开关都交给 ZoomPanImageControl（原先是 Image.Source 二选一）。
			ImageControl.DiffSource = DiffImageSource;
			ImageControl.HighlightImageDiff = HighlightImageDiff;
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
