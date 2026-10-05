using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v5.0.0：二进制对比单侧面板，自主工程 BinaryContentUserControl 迁移（插件化）。
	/// v5.0.0：从 Image 插件移入共享组件工程（Ui）——图片插件（BinaryDiffView）与
	/// Hex 插件（HexDiffView）的文件卡片/并排视图共用本面板。
	/// 领域模型从宿主的 ImageContent/LfsContent/BinaryContent 压平为 <see cref="DiffSideContent"/>：
	/// Lfs 引用非空 → LFS 卡片（图片 LFS 显示「显示 LFS 图片」按钮）；字节可用 → 图片渲染
	///（经 BinaryViewerRegistry 判定动图/静态图）；其余 → 通用文件卡片（图标 + 扩展名 + 大小）。
	/// </summary>
	public partial class BinaryContentPanel : UserControl
	{
		public EventHandler<EventArgs> ShowLfsImageButtonClick;

		public EventHandler<EventArgs> CancelLfsButtonClick;

		public EventHandler<EventArgs> SaveAsMenuItemClick;

		private bool _highlightImageDiff;

		private string _statusLabel;

		/// <summary>该侧内容（v5.0.0：宿主下发的插件侧投影；由 SetContent 赋值）。</summary>
		[Null]
		private DiffSideContent _side;

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

		/// <summary>
		/// 图片字节就绪前的宿主侧预处理（当前为 .tga 经 biturbo 原生解码）。
		/// 由视图（BinaryDiffView/HexDiffView）注入（包装 IDiffViewHost.PrepareImageStream）；
		/// 未注入时原样返回。
		/// </summary>
		[Null]
		public Func<string, MemoryStream, MemoryStream> PrepareStream { get; set; }

		/// <summary>v4.3.1：与其它图片视图共享的缩放/平移状态（由 BinaryDiffView 注入）。</summary>
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

		public BinaryContentPanel()
		{
			InitializeComponent();
			PluginEnvironment.ApplyLocalization(this);
			PluginEnvironment.ImageDiffHighlightPixelsChanged += delegate (bool newValue)
			{
				UpdateHighlightImageDiff();
			};
			UpdateHighlightImageDiff();
		}

		/// <summary>该侧内容（供 Save As 等回调读取路径/字节）。</summary>
		[Null]
		public DiffSideContent Side => _side;

		/// <summary>
		/// 下发单侧内容。role + showTitle 决定标题文案（old/new/created/removed），
		/// titleBrush 为宿主按主题注入的标题颜色。
		/// </summary>
		public void SetContent([Null] DiffSideContent side, DiffSideRole role, bool showTitle, [Null] IBrush titleBrush, [Null] global::Avalonia.Media.Imaging.Bitmap diffImageSource)
		{
			// v4.3.2：换文件先停掉并释放上一份动图（新内容为动图时 RefreshImage 会重建）。
			DetachAnimated();
			_side = side;
			_statusLabel = showTitle ? RoleStatusLabel(role) : null;
			DiffImageSource = diffImageSource;
			if (titleBrush != null)
			{
				TitleTextBlock.Foreground = titleBrush;
			}
			TitleTextBlock.Text = string.IsNullOrEmpty(_statusLabel) ? "" : PluginEnvironment.Translate(_statusLabel);
			long valueOrDefault = side?.Size ?? side?.Data?.Length ?? 0L;
			DescriprionTextBlock.Text = PluginSizeFormat.ReadableFileSize(valueOrDefault);
			global::Avalonia.Controls.ToolTip.SetTip(DescriprionTextBlock, PluginSizeFormat.ReadableFileSizeInBytes(valueOrDefault));
			ImageContainer.IsVisible = false;
			FileContainer.IsVisible = false;
			FileIcon.IsVisible = false;
			SaveAsDropDownButton.IsVisible = false;
			CancelLfsButton.IsVisible = false;
			ShowLfsImageButton.IsVisible = false;
			FileExtensionTextBlock.IsVisible = false;
			LfsProgressBar.IsVisible = false;
			if (side == null)
			{
				return;
			}
			if (side.Lfs != null)
			{
				// LFS 指针侧：卡片 + LFS 徽章（宿主 smudge 前无真身字节）。
				RefreshLfsLabel(isLfs: true, valueOrDefault, side.IsTracked);
				FileContainer.IsVisible = true;
				FileExtensionTextBlock.IsVisible = true;
				string extension = Path.GetExtension(side.Path);
				FileExtensionTextBlock.Text = extension;
				SaveAsDropDownButton.IsVisible = true;
				if (side.IsLfsImage)
				{
					ShowLfsImageButton.IsVisible = true;
					FileIcon.IsVisible = false;
				}
				else
				{
					FileExtensionTextBlock.IsVisible = true;
					FileIcon.Source = PluginEnvironment.GetFileIcon(extension);
					FileIcon.IsVisible = true;
				}
				return;
			}
			MemoryStream data = side.Data;
			if (data != null)
			{
				// 字节可用：走图片渲染路径（.tga 先经宿主 biturbo 解码）。
				RefreshLfsLabel(isLfs: false, valueOrDefault, side.IsTracked);
				MemoryStream memoryStream = data;
				if (PrepareStream != null)
				{
					MemoryStream prepared = PrepareStream(side.Path, memoryStream);
					if (prepared != null)
					{
						memoryStream = prepared;
					}
				}
				RefreshImage(memoryStream, side.Path);
				ImageContainer.IsVisible = true;
				SaveAsDropDownButton.IsVisible = true;
				return;
			}
			// 无字节（大文件未预载）：通用文件卡片。
			FileContainer.IsVisible = true;
			RefreshLfsLabel(isLfs: false, valueOrDefault, side.IsTracked);
			FileExtensionTextBlock.IsVisible = true;
			string extension2 = Path.GetExtension(side.Path);
			FileExtensionTextBlock.Text = extension2;
			FileIcon.Source = PluginEnvironment.GetFileIcon(extension2);
			FileIcon.IsVisible = true;
		}

		private static string RoleStatusLabel(DiffSideRole role)
		{
			switch (role)
			{
				case DiffSideRole.Old:
					return "old";
				case DiffSideRole.New:
					return "new";
				case DiffSideRole.Created:
					return "created";
				case DiffSideRole.Removed:
					return "removed";
				default:
					return null;
			}
		}

		public void ApplyLocalization()
		{
			PluginEnvironment.ApplyLocalization(this);
			TitleTextBlock.Text = string.IsNullOrEmpty(_statusLabel) ? "" : PluginEnvironment.Translate(_statusLabel);
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
					ShowLfsImageButton.IsVisible = false;
				}
				if (!CancelLfsButton.IsVisible)
				{
					CancelLfsButton.IsVisible = true;
				}
				if (!LfsProgressBar.IsVisible)
				{
					LfsProgressBar.IsVisible = true;
				}
				LfsProgressBar.Value = valueOrDefault;
			}
			else
			{
				CancelLfsButton.IsVisible = false;
				LfsProgressBar.IsVisible = false;
				ShowLfsImageButton.IsVisible = true;
			}
		}

		/// <param name="path">v5.0.0：仓库内路径，供查看器注册表按扩展名判定；未知时可不传。</param>
		public void SetLfsImageData(MemoryStream memoryStream, [Null] global::Avalonia.Media.Imaging.Bitmap diffImageSource = null, [Null] string path = null)
		{
			DiffImageSource = diffImageSource;
			RefreshImage(memoryStream, path);
			ImageContainer.IsVisible = true;
			FileContainer.IsVisible = false;
			ShowLfsImageButton.IsVisible = false;
			FileExtensionTextBlock.IsVisible = false;
		}

		private void RefreshImage(MemoryStream memoryStream, [Null] string path)
		{
			// v5.0.0：渲染方式改由查看器注册表判定（动图优先于静态图），不再在本控件里硬编码
			// 动图探测——新增格式只需往 BinaryViewerRegistry 注册查看器。
			IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest(path, memoryStream));
			if (viewer.Kind == BinaryViewerKind.AnimatedImage)
			{
				// 帧数/总像素超阈值或解码失败的动图 TryDecode 返回 null，退回静态首帧显示。
				AnimatedImage animated = AnimatedImage.TryDecode(memoryStream);
				if (animated != null)
				{
					AttachAnimated(animated, memoryStream.Length);
					return;
				}
			}
			DetachAnimated();
			global::Avalonia.Media.Imaging.Bitmap bitmapSource = ImageData.CreateBitmapSource(memoryStream);
			long length = memoryStream.Length;
			DescriprionTextBlock.Text = GetImageDescription(bitmapSource, length);
			ImageControl.Source = bitmapSource;
			RefreshDiffImage();
		}

		/// <summary>v4.3.2：装配动图——挂播放器到图片控件、显示底部播放控制条并自动播放。</summary>
		private void AttachAnimated(AnimatedImage animated, long fileSize)
		{
			DetachAnimated();
			_animatedImage = animated;
			_animatedPlayer = new AnimatedImagePlayer(animated);
			ImageControl.Player = _animatedPlayer;
			ImageControl.Source = animated.Frames[0];
			DescriprionTextBlock.Text = GetImageDescription(animated.Frames[0], fileSize);
			if (_playbackBar == null)
			{
				_playbackBar = new AnimatedImagePlaybackBar();
			}
			_playbackBar.Player = _animatedPlayer;
			PlaybackBarContainer.Content = _playbackBar;
			PlaybackBarContainer.IsVisible = true;
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
			PlaybackBarContainer.IsVisible = false;
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

		/// <summary>v4.3.2：暂停该侧动图播放（切换到 Hex 等视图时由视图调用，避免后台空转）。</summary>
		public void PauseAnimation()
		{
			_animatedPlayer?.Pause();
		}

		/// <summary>v5.0.0：视图 Release 时释放该侧内容（停动图、清位图），防止帧内存滞留。</summary>
		public void ReleaseContent()
		{
			DetachAnimated();
			ImageControl.Source = null;
			ImageControl.DiffSource = null;
			DiffImageSource = null;
			_side = null;
		}

		/// <summary>v4.3.2：恢复该侧动图播放（切回并排视图时由视图调用）。</summary>
		public void ResumeAnimation()
		{
			_animatedPlayer?.Play();
		}

		private void UpdateHighlightImageDiff()
		{
			HighlightImageDiff = PluginEnvironment.HighlightImageDiff;
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

		private void SaveAsDropDownButton_Click(object sender, RoutedEventArgs e)
		{
			// 原宿主 DropDownButton 的本地等价：点击按钮打开「Save As...」上下文菜单。
			ContextMenu contextMenu = SaveAsDropDownButton.ContextMenu;
			if (contextMenu != null && !contextMenu.IsOpen)
			{
				contextMenu.Open();
			}
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			CancelLfsButton.IsVisible = false;
			LfsProgressBar.IsVisible = false;
			ShowLfsImageButton.IsVisible = true;
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
				LfsLabel.IsVisible = true;
				NotLfsLabel.IsVisible = false;
				return;
			}
			LfsLabel.IsVisible = false;
			if (isTracked && fileSize > 500000)
			{
				NotLfsLabel.IsVisible = true;
				global::Avalonia.Controls.ToolTip.SetTip(NotLfsLabel, string.Format(PluginEnvironment.Translate("File is {0} and is not managed by LFS"), PluginSizeFormat.ReadableFileSize(fileSize)));
			}
			else
			{
				NotLfsLabel.IsVisible = false;
			}
		}

		private static string GetImageDescription([Null] global::Avalonia.Media.Imaging.Bitmap imageSource, long fileSize)
		{
			if (imageSource == null)
			{
				return "";
			}
			string arg = PluginSizeFormat.Format(fileSize);
			return $"W: {imageSource.PixelSize.Width}px | H: {imageSource.PixelSize.Height}px ({arg})";
		}

	}
}
