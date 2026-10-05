using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.Plugins.BuiltIn.HexDiff;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v5.0.0：图片 / 二进制对比视图（自主工程 BinaryDiffUserControl 迁移，实现 <see cref="IDiffView"/>）。
	/// 视图模式：并排（含动图播放）/ Swipe / 洋葱皮 / Hex；LFS smudge、.tga 解码、保存、错误弹窗
	/// 一律经 <see cref="IDiffViewHost"/> 回宿主完成，插件不感知 Git 领域类型。
	/// </summary>
	public partial class BinaryDiffView : UserControl, IDiffView
	{
		private bool _showTitle;

		[Null]
		private DiffViewContext _context;

		[Null]
		private IDiffViewHost _host;

		[Null]
		private ImageData _srcImageData;

		[Null]
		private ImageData _dstImageData;

		[Null]
		private IDisposable _activeSrcSmudge;

		[Null]
		private IDisposable _activeDstSmudge;

		[Null]
		private global::Avalonia.Media.Imaging.Bitmap _diffImageSource;

		/// <summary>v4.3.1：图片对比各视图（并排/Swipe/洋葱皮）共享的缩放/平移状态——
		/// 同一实例即同一缩放与中心点，切换视图、左右两栏都保持同步。</summary>
		private readonly ImageZoomState _imageZoomState = new ImageZoomState();

		// v3.4.1：Hex 视图 — 原始字节（图片自带 / LFS smudge / 宿主预载）用于创建 HexDiffUserControl
		[Null]
		private MemoryStream _hexSrcData;

		[Null]
		private MemoryStream _hexDstData;

		[Null]
		private HexDiffUserControl _hexDiffView;

		// v3.7.2：当前字节是否已 SetContent 到 _hexDiffView（避免侧栏来回切换重复全量重载）
		private bool _hexContentLoaded;

		[Null]
		public global::Avalonia.Media.Imaging.Bitmap DiffImageSource
		{
			get
			{
				return _diffImageSource;
			}
			private set
			{
				_diffImageSource = value;
				this.HighlightPixelsAvailableChanged?.Invoke(this, _diffImageSource != null);
			}
		}

		/// <summary>「高亮差异像素」能力变化（映射原 DiffImageSourceChanged；差异掩码可用性）。</summary>
		public event EventHandler<bool> HighlightPixelsAvailableChanged;

		/// <summary>视图根控件（宿主直接挂载）。</summary>
		global::Avalonia.Controls.Control IDiffView.View => this;

		/// <summary>内置工具条（Side-by-Side/Swipe/Onion Skin/Hex），无需宿主渲染模式切换。</summary>
		IReadOnlyList<DiffViewMode> IDiffView.Modes => Array.Empty<DiffViewMode>();

		public BinaryDiffView()
		{
			InitializeComponent();
			// v3.4.1：让 RadioButton 内容（Side-by-Side/Swipe/Onion Skin/Hex）在构造时翻译
			PluginEnvironment.ApplyLocalization(this);
			// v4.3.1：四个图片视图共用同一个缩放状态实例——滚轮缩放/拖动平移在左右两栏、
			// 并排/Swipe/洋葱皮之间天然同步，且按归一化中心点对齐（尺寸不一致也能对上同一位置）。
			_imageZoomState.Changed += delegate
			{
				UpdateResetZoomButton();
			};
			SrcFileContentUserControl.ZoomState = _imageZoomState;
			DstFileContentUserControl.ZoomState = _imageZoomState;
			SwipeImageDiffView.ZoomState = _imageZoomState;
			OnionSkinImageDiffView.ZoomState = _imageZoomState;
			UpdateResetZoomButton();
			// v3.4.1：Hex 视图容器初始隐藏
			HexDiffViewContainer.IsVisible = false;
			FallbackUserControl.IsVisible = false;
			// v5.0.0：.tga 等宿主侧图片预处理经宿主能力桥下发到两个面板
			SrcFileContentUserControl.PrepareStream = PrepareImageStream;
			DstFileContentUserControl.PrepareStream = PrepareImageStream;
			WirePanelEvents();
		}

		/// <summary>宿主能力桥就绪后的图片预处理（.tga 解码失败由宿主记日志并回退原图）。</summary>
		[Null]
		private MemoryStream PrepareImageStream([Null] string path, MemoryStream raw)
		{
			if (_host == null || path == null || raw == null)
			{
				return raw;
			}
			MemoryStream prepared = _host.PrepareImageStream(path, raw);
			return prepared ?? raw;
		}

		private void WirePanelEvents()
		{
			BinaryContentPanel srcPanel = SrcFileContentUserControl;
			srcPanel.ShowLfsImageButtonClick = (EventHandler<EventArgs>)Delegate.Combine(srcPanel.ShowLfsImageButtonClick, (EventHandler<EventArgs>)delegate
			{
				DiffSideContent srcSide = _context?.Src;
				if (srcSide?.Lfs != null)
				{
					StartSideSmudge(isSrc: true, srcSide);
				}
			});
			BinaryContentPanel srcPanel2 = SrcFileContentUserControl;
			srcPanel2.CancelLfsButtonClick = (EventHandler<EventArgs>)Delegate.Combine(srcPanel2.CancelLfsButtonClick, (EventHandler<EventArgs>)delegate
			{
				_activeSrcSmudge?.Dispose();
				_activeSrcSmudge = null;
			});
			BinaryContentPanel dstPanel = DstFileContentUserControl;
			dstPanel.ShowLfsImageButtonClick = (EventHandler<EventArgs>)Delegate.Combine(dstPanel.ShowLfsImageButtonClick, (EventHandler<EventArgs>)delegate
			{
				DiffSideContent dstSide = _context?.Dst;
				if (dstSide?.Lfs != null)
				{
					StartSideSmudge(isSrc: false, dstSide);
				}
			});
			BinaryContentPanel dstPanel2 = DstFileContentUserControl;
			dstPanel2.CancelLfsButtonClick = (EventHandler<EventArgs>)Delegate.Combine(dstPanel2.CancelLfsButtonClick, (EventHandler<EventArgs>)delegate
			{
				_activeDstSmudge?.Dispose();
				_activeDstSmudge = null;
			});
			BinaryContentPanel srcPanel3 = SrcFileContentUserControl;
			srcPanel3.SaveAsMenuItemClick = (EventHandler<EventArgs>)Delegate.Combine(srcPanel3.SaveAsMenuItemClick, (EventHandler<EventArgs>)delegate
			{
				SaveSideAs(isSrc: true);
			});
			BinaryContentPanel dstPanel3 = DstFileContentUserControl;
			dstPanel3.SaveAsMenuItemClick = (EventHandler<EventArgs>)Delegate.Combine(dstPanel3.SaveAsMenuItemClick, (EventHandler<EventArgs>)delegate
			{
				SaveSideAs(isSrc: false);
			});
		}

		// ---- IDiffView 生命周期 ----

		/// <summary>下发内容：两侧 DiffSideContent + 宿主能力桥（原 UpdateDiff/UpdateContent）。</summary>
		public void SetContent(DiffViewContext context, IDiffViewHost host)
		{
			_context = context;
			_host = host;
			_showTitle = context.ShowTitle;
			// v3.4.1：换文件重置 Hex 视图状态（取消旧后台加载并释放上一份内容）
			if (_hexDiffView != null)
			{
				_hexDiffView.ControlWillBeRemovedFromFileDiffControl();
				HexDiffViewContainer.Content = null;
				_hexDiffView = null;
			}
			_hexContentLoaded = false;
			_hexSrcData = context.HexSrc;
			_hexDstData = context.HexDst;
			UpdateContent();
		}

		/// <summary>切换视图模式（宿主工具条/用户绑定；对应用户点底部 RadioButton）。</summary>
		public void SetMode(string modeId)
		{
			switch (modeId)
			{
				case "swipe":
					if (!SwipeRadioButton.IsChecked.GetValueOrDefault())
					{
						SwipeRadioButton.IsChecked = true;
					}
					break;
				case "onion":
					if (!OnionSkinRadioButton.IsChecked.GetValueOrDefault())
					{
						OnionSkinRadioButton.IsChecked = true;
					}
					break;
				case "hex":
					if (!HexRadioButton.IsChecked.GetValueOrDefault())
					{
						HexRadioButton.IsChecked = true;
					}
					break;
				default:
					if (!SideBySideRadioButton.IsChecked.GetValueOrDefault())
					{
						SideBySideRadioButton.IsChecked = true;
					}
					break;
			}
		}

		/// <summary>切入显示：并排视图恢复动图播放。</summary>
		public void Activate()
		{
			UpdateAnimationPlayback();
		}

		/// <summary>切走/失活：暂停动图播放（省电防闪）。</summary>
		public void Deactivate()
		{
			SrcFileContentUserControl.PauseAnimation();
			DstFileContentUserControl.PauseAnimation();
		}

		/// <summary>应用当前语言（宿主在语言切换时广播）。</summary>
		public void ApplyLocalization()
		{
			PluginEnvironment.ApplyLocalization(this);
			SrcFileContentUserControl.ApplyLocalization();
			DstFileContentUserControl.ApplyLocalization();
			// 语言切换后"还原大小 (N%)"的文案要用新语言重出。
			UpdateResetZoomButton();
		}

		/// <summary>释放：停后台任务、退订事件、释放位图。之后实例不再复用。</summary>
		public void Release()
		{
			ControlWillBeRemoved();
		}

		/// <summary>v3.7.2：宿主切换子视图时清理——取消 Hex 后台加载、停 LFS 任务并释放动图帧。</summary>
		private void ControlWillBeRemoved()
		{
			if (_hexDiffView != null)
			{
				_hexDiffView.ControlWillBeRemovedFromFileDiffControl();
				_hexDiffView = null;
				HexDiffViewContainer.Content = null;
			}
			_hexContentLoaded = false;
			_hexSrcData = null;
			_hexDstData = null;
			_activeSrcSmudge?.Dispose();
			_activeSrcSmudge = null;
			_activeDstSmudge?.Dispose();
			_activeDstSmudge = null;
			SrcFileContentUserControl.ReleaseContent();
			DstFileContentUserControl.ReleaseContent();
			_srcImageData?.ImageSource?.Dispose();
			_dstImageData?.ImageSource?.Dispose();
			DiffImageSource?.Dispose();
			DiffImageSource = null;
			_srcImageData = null;
			_dstImageData = null;
		}

		// ---- 内容装配（原 UpdateContent） ----

		private void UpdateContent()
		{
			DiffViewContext context = _context;
			if (context == null)
			{
				return;
			}
			// v4.3.1：换文件后回到贴合视图的初始缩放，避免沿用上一个文件的缩放/中心点。
			_imageZoomState.Reset();
			_srcImageData = null;
			_dstImageData = null;
			DiffImageSource = null;
			if (!SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				SideBySideRadioButton.IsChecked = true;
			}
			FallbackUserControl.IsVisible = false;
			DiffSideContent srcSide = context.Src;
			DiffSideContent dstSide = context.Dst;
			// 双侧均有字节（非 LFS）：直接解码为图片（原 ImageContent 分支）
			if (srcSide != null && srcSide.Lfs == null && srcSide.Data != null)
			{
				_srcImageData = ImageData.Create(srcSide.Data, isLfs: false, srcSide.IsTracked);
			}
			if (dstSide != null && dstSide.Lfs == null && dstSide.Data != null)
			{
				_dstImageData = ImageData.Create(dstSide.Data, isLfs: false, dstSide.IsTracked);
			}
			bool bothSides = srcSide != null && dstSide != null;
			if (bothSides)
			{
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				Grid.SetColumnSpan(SrcFileContentUserControl, 1);
				SrcFileContentUserControl.Margin = new Thickness(10.0, 0.0, 5.0, 0.0);
				// 修复（2026-09-14，高亮像素双侧显示）：差异掩码左右两侧各自叠加同一份——
				// 左图在自己身上看到"将要变化的位置"，右图在自己身上看到"已变化的位置"。
				SrcFileContentUserControl.SetContent(srcSide, context.SrcRole, _showTitle, context.SrcTitleBrush, DiffImageSource);
				Grid.SetColumn(DstFileContentUserControl, 1);
				Grid.SetColumnSpan(DstFileContentUserControl, 1);
				DstFileContentUserControl.Margin = new Thickness(5.0, 0.0, 10.0, 0.0);
				DstFileContentUserControl.SetContent(dstSide, context.DstRole, _showTitle, context.DstTitleBrush, DiffImageSource);
				SrcFileContentUserControl.IsVisible = true;
				DstFileContentUserControl.IsVisible = true;
			}
			else if (srcSide != null)
			{
				Grid.SetColumnSpan(SrcFileContentUserControl, 2);
				SrcFileContentUserControl.Margin = new Thickness(10.0, 0.0, 10.0, 0.0);
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				// 修复（2026-09-14）：单边展示（对侧无图/二进制）也把差异掩码传给可见侧。
				SrcFileContentUserControl.SetContent(srcSide, context.SrcRole, _showTitle, context.SrcTitleBrush, DiffImageSource);
				SrcFileContentUserControl.IsVisible = true;
				DstFileContentUserControl.IsVisible = false;
			}
			else if (dstSide != null)
			{
				Grid.SetColumn(DstFileContentUserControl, 0);
				Grid.SetColumnSpan(DstFileContentUserControl, 2);
				DstFileContentUserControl.Margin = new Thickness(10.0, 0.0, 10.0, 0.0);
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				DstFileContentUserControl.SetContent(dstSide, context.DstRole, _showTitle, context.DstTitleBrush, DiffImageSource);
				DstFileContentUserControl.IsVisible = true;
				SrcFileContentUserControl.IsVisible = false;
			}
			else
			{
				FallbackUserControl.IsVisible = true;
			}
			// 本地 LFS 缓存命中：直接出图（原 GitLfsGetCachedFileGitCommand 分支）
			TryLoadCachedLfsImage(isSrc: true, srcSide);
			TryLoadCachedLfsImage(isSrc: false, dstSide);
			RefreshViewModes();
			// v4.3.2：内容装配完再按当前视图决定动图是否播放（并排即播、其它视图暂停）。
			UpdateAnimationPlayback();
		}

		/// <summary>LFS 图片侧的本地缓存取回（.git/lfs/objects 命中时免 smudge 直接出图）。</summary>
		private void TryLoadCachedLfsImage(bool isSrc, [Null] DiffSideContent side)
		{
			if (_host == null || side?.Lfs == null || !side.IsLfsImage)
			{
				return;
			}
			MemoryStream result = _host.GetCachedLfsData(side.Lfs);
			if (result == null)
			{
				return;
			}
			MemoryStream prepared = PrepareImageStream(side.Path, result) ?? result;
			ImageData imageData = ImageData.Create(prepared, isLfs: true, side.IsTracked);
			ApplyLfsImageData(isSrc, side, prepared, imageData);
		}

		/// <summary>smudge / 缓存命中后回填该侧：解码数据、差异掩码、Hex 字节并刷新模式条。</summary>
		private void ApplyLfsImageData(bool isSrc, DiffSideContent side, MemoryStream data, [Null] ImageData imageData)
		{
			if (isSrc)
			{
				_srcImageData = imageData;
				_hexSrcData = data; // v3.4.1：存原始字节供 Hex 视图
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				// 修复（2026-09-14，高亮像素双侧显示）：LFS 路径同样给左侧补传掩码。
				DstFileContentUserControl.DiffImageSource = DiffImageSource;
				SrcFileContentUserControl.SetLfsImageData(data, DiffImageSource, side.Path);
			}
			else
			{
				_dstImageData = imageData;
				_hexDstData = data; // v3.4.1：存原始字节供 Hex 视图
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				DstFileContentUserControl.SetLfsImageData(data, DiffImageSource, side.Path);
				SrcFileContentUserControl.DiffImageSource = DiffImageSource;
			}
			RefreshViewModes();
		}

		/// <summary>「显示 LFS 图片」按钮：经宿主启动 smudge 任务（进度/完成回调均在 UI 线程）。</summary>
		private void StartSideSmudge(bool isSrc, DiffSideContent side)
		{
			BinaryContentPanel panel = isSrc ? SrcFileContentUserControl : DstFileContentUserControl;
			panel.SetProgress(0.0);
			IDisposable active = isSrc ? _activeSrcSmudge : _activeDstSmudge;
			active?.Dispose();
			IDisposable job = _host.RunLfsSmudge(side.Lfs, delegate (double? progress)
			{
				panel.SetProgress(progress);
			}, delegate (LfsSmudgeResult response)
			{
				if (isSrc)
				{
					_activeSrcSmudge = null;
				}
				else
				{
					_activeDstSmudge = null;
				}
				panel.SetProgress(null);
				if (!response.Succeeded)
				{
					_host.ShowError(response.Error ?? "Failed to smudge LFS file");
					return;
				}
				MemoryStream data = response.Data;
				if (data == null)
				{
					return;
				}
				MemoryStream prepared = PrepareImageStream(side.Path, data) ?? data;
				ImageData imageData = ImageData.Create(prepared, isLfs: true, side.IsTracked);
				ApplyLfsImageData(isSrc, side, prepared, imageData);
			});
			if (isSrc)
			{
				_activeSrcSmudge = job;
			}
			else
			{
				_activeDstSmudge = job;
			}
		}

		/// <summary>「Save As...」：LFS 侧先 smudge 再保存；普通侧直接保存（对话框 + 写文件在宿主）。</summary>
		private void SaveSideAs(bool isSrc)
		{
			if (_host == null)
			{
				return;
			}
			DiffSideContent side = isSrc ? _context?.Src : _context?.Dst;
			if (side == null)
			{
				return;
			}
			string suggestedFileName = Path.GetFileName(side.Path);
			if (side.Lfs != null)
			{
				BinaryContentPanel panel = isSrc ? SrcFileContentUserControl : DstFileContentUserControl;
				panel.SetProgress(0.0);
				IDisposable active = isSrc ? _activeSrcSmudge : _activeDstSmudge;
				active?.Dispose();
				IDisposable job = _host.RunLfsSmudge(side.Lfs, delegate (double? progress)
				{
					panel.SetProgress(progress);
				}, delegate (LfsSmudgeResult response)
				{
					if (isSrc)
					{
						_activeSrcSmudge = null;
					}
					else
					{
						_activeDstSmudge = null;
					}
					panel.SetProgress(null);
					if (!response.Succeeded)
					{
						_host.ShowError(response.Error ?? "Failed to smudge LFS file");
						return;
					}
					_host.SaveFileAs(suggestedFileName, response.Data);
				});
				if (isSrc)
				{
					_activeSrcSmudge = job;
				}
				else
				{
					_activeDstSmudge = job;
				}
			}
			else
			{
				_host.SaveFileAs(suggestedFileName, side.Data);
			}
		}

		// ---- 视图模式切换（原 ImageDiffSelectedItem_Changed 等） ----

		private void ImageDiffSelectedItem_Changed(object sender, RoutedEventArgs e)
		{
			if (SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				// 修复（2026-09-18）：新增/删除文件只有单侧内容，UpdateContent 已把无内容的
				// 一侧隐藏；此处按内容有无恢复，避免把空内容面板重新露出来。
				SetPanelVisible(SrcFileContentUserControl, _context?.Src != null);
				SetPanelVisible(DstFileContentUserControl, _context?.Dst != null);
				SwipeImageDiffView.IsVisible = false;
				OnionSkinImageDiffView.IsVisible = false;
				HexDiffViewContainer.IsVisible = false;
			}
			else if (SwipeRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.IsVisible = false;
				DstFileContentUserControl.IsVisible = false;
				OnionSkinImageDiffView.IsVisible = false;
				HexDiffViewContainer.IsVisible = false;
				SwipeImageDiffView.IsVisible = true;
				SwipeImageDiffView.Refresh(_srcImageData, _dstImageData, DiffImageSource, _showTitle);
			}
			else if (OnionSkinRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.IsVisible = false;
				DstFileContentUserControl.IsVisible = false;
				SwipeImageDiffView.IsVisible = false;
				HexDiffViewContainer.IsVisible = false;
				OnionSkinImageDiffView.IsVisible = true;
				OnionSkinImageDiffView.Refresh(_srcImageData, _dstImageData, DiffImageSource, _showTitle);
			}
			else if (HexRadioButton.IsChecked.GetValueOrDefault())
			{
				// v3.4.1：Hex 视图 — 显示原始字节的 side-by-side 十六进制比较
				SrcFileContentUserControl.IsVisible = false;
				DstFileContentUserControl.IsVisible = false;
				SwipeImageDiffView.IsVisible = false;
				OnionSkinImageDiffView.IsVisible = false;
				ShowHexDiffView();
				HexDiffViewContainer.IsVisible = true;
			}
			// v4.3.1：视图模式变化会影响"还原大小"是否该显示（Hex 不可缩放 → 不显示）。
			UpdateResetZoomButton();
			// v4.3.2：动图控制条只在并排视图出现；离开并排时暂停播放，切回时恢复。
			UpdateAnimationPlayback();
		}

		private static void SetPanelVisible(BinaryContentPanel panel, bool visible)
		{
			panel.IsVisible = visible;
		}

		/// <summary>v4.3.2：并排视图播放动图，其余视图（Swipe/洋葱皮/Hex）暂停，避免后台空转。</summary>
		private void UpdateAnimationPlayback()
		{
			if (SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.ResumeAnimation();
				DstFileContentUserControl.ResumeAnimation();
			}
			else
			{
				SrcFileContentUserControl.PauseAnimation();
				DstFileContentUserControl.PauseAnimation();
			}
		}

		/// <summary>v3.4.1：懒创建 HexDiffUserControl 并加载原始字节。
		/// v3.7.2：同一份数据只 SetContent 一次（_hexContentLoaded 标记）——侧栏切走再切回
		/// 时不重新解析/不丢"加载更多"进度；换文件由 SetContent 重置标记。</summary>
		private void ShowHexDiffView()
		{
			if (_hexDiffView == null)
			{
				_hexDiffView = new HexDiffUserControl();
				HexDiffViewContainer.Content = _hexDiffView;
			}
			if (!_hexContentLoaded && (_hexSrcData != null || _hexDstData != null))
			{
				_hexDiffView.SetContent(_hexSrcData, _hexDstData, _context?.FilePath);
				_hexContentLoaded = true;
			}
		}

		private void RefreshViewModes()
		{
			// v3.7.2（"OTF 变更没有 hex 对比"）：非图片二进制（如 OTF/字体/音视频）同样显示
			// 工具栏——Side-by-Side + Hex（卡片视图带 LFS 徽章，且可切 hex 对比）。
			// Swipe/Onion Skin 仅对双侧图片有意义。
			bool bothImages = _srcImageData != null && _dstImageData != null;
			bool hasBinarySides = _context?.Src != null || _context?.Dst != null;
			if (bothImages || hasBinarySides)
			{
				ViewModeButtonsContainer.IsVisible = true;
				SwipeRadioButton.IsVisible = bothImages;
				OnionSkinRadioButton.IsVisible = bothImages;
				// Hex 仅在两侧字节可用时显示（图片自带字节；大体积二进制由宿主预载；
				// 超阈值或加载失败则隐藏）
				HexRadioButton.IsVisible = _hexSrcData != null || _hexDstData != null;
			}
			else
			{
				ViewModeButtonsContainer.IsVisible = false;
			}
		}

		/// <summary>v4.3.1：图片被缩放（非 100%）时在对比视图中间偏下悬浮"还原大小 (N%)"，
		/// 点击还原为贴合视图的初始状态（各视图共享同一缩放状态，一处还原全部还原）。</summary>
		private void UpdateResetZoomButton()
		{
			bool zoomed = _imageZoomState.IsZoomed && !HexRadioButton.IsChecked.GetValueOrDefault();
			ResetZoomButton.IsVisible = zoomed;
			if (zoomed)
			{
				ResetZoomButton.Content = PluginEnvironment.Format("Reset Size ({0}%)", _imageZoomState.ZoomPercent);
				ToolTip.SetTip(ResetZoomButton, PluginEnvironment.Translate("Reset Size"));
			}
		}

		private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
		{
			_imageZoomState.Reset();
		}

		// ---- 像素差异掩码（原 GetDiffImage 一族，逐字节比较 + 品红差异位） ----

		[Null]
		private global::Avalonia.Media.Imaging.Bitmap GetDiffImage([Null] ImageData lhsImageData, [Null] ImageData rhsImageData)
		{
			// v4.3.2：任一侧是动图时不做像素差异高亮——GIF/WebP/APNG 按帧播放，
			// 拿首帧算出的差异掩码既无意义又会盖住动画，直接返回 null 关闭高亮。
			if ((lhsImageData?.IsAnimated ?? false) || (rhsImageData?.IsAnimated ?? false))
			{
				return null;
			}
			global::Avalonia.Media.Imaging.Bitmap bitmapSource = lhsImageData?.ImageSource;
			if (bitmapSource != null)
			{
				global::Avalonia.Media.Imaging.Bitmap bitmapSource2 = rhsImageData?.ImageSource;
				if (bitmapSource2 != null && bitmapSource.PixelSize.Width == bitmapSource2.PixelSize.Width && bitmapSource.PixelSize.Height == bitmapSource2.PixelSize.Height)
				{
					// Avalonia 解码位图本身即为 Bgra8888/Rgba8888（无调色板/调色板转换），
					// 按位图实际 Format 的 BitsPerPixel 计算每像素字节数，逐通道比较。
					int num = (bitmapSource.Format ?? global::Avalonia.Platform.PixelFormat.Bgra8888).BitsPerPixel / 8;
					int num2 = bitmapSource.PixelSize.Width * num;
					byte[] array = CopyPixelsToArray(bitmapSource, num2);
					byte[] array2 = CopyPixelsToArray(bitmapSource2, num2);
					byte[] array3 = new byte[bitmapSource2.PixelSize.Height * num2];
					int pixelWidth = bitmapSource.PixelSize.Width;
					int pixelHeight = bitmapSource.PixelSize.Height;
					for (int i = 0; i < pixelHeight; i++)
					{
						for (int j = 0; j < pixelWidth; j++)
						{
							int num3 = i * pixelWidth * num + j * num;
							int num4 = i * pixelWidth * num + j * num;
							byte lhs = array[num3];
							byte lhs2 = array[num3 + 1];
							byte lhs3 = array[num3 + 2];
							byte lhs4 = array[num3 + 3];
							byte rhs = array2[num4];
							byte rhs2 = array2[num4 + 1];
							byte rhs3 = array2[num4 + 2];
							byte rhs4 = array2[num4 + 3];
							if (!SamePixel(lhs3, rhs3) || !SamePixel(lhs2, rhs2) || !SamePixel(lhs, rhs) || !SamePixel(lhs4, rhs4))
							{
								array3[num4] = byte.MaxValue;
								array3[num4 + 1] = 0;
								array3[num4 + 2] = byte.MaxValue;
								array3[num4 + 3] = byte.MaxValue;
							}
						}
					}
					return CreateBitmapFromArray(array3, bitmapSource.PixelSize.Width, bitmapSource.PixelSize.Height, num2);
				}
			}
			return null;
		}

		/// <summary>把位图像素拷贝到托管数组（GCHandle 钉住数组取指针后 CopyPixels）。</summary>
		private static byte[] CopyPixelsToArray(global::Avalonia.Media.Imaging.Bitmap source, int stride)
		{
			byte[] array = new byte[source.PixelSize.Height * stride];
			global::Avalonia.PixelRect sourceRect = new global::Avalonia.PixelRect(0, 0, source.PixelSize.Width, source.PixelSize.Height);
			System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(array, System.Runtime.InteropServices.GCHandleType.Pinned);
			try
			{
				source.CopyPixels(sourceRect, handle.AddrOfPinnedObject(), array.Length, stride);
			}
			finally
			{
				handle.Free();
			}
			return array;
		}

		/// <summary>从像素数组构建位图（差异像素为 BGRA(255,0,255,255) 品红，DPI 统一按 96）。</summary>
		private static global::Avalonia.Media.Imaging.Bitmap CreateBitmapFromArray(byte[] pixels, int width, int height, int stride)
		{
			System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
			try
			{
				return new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Premul, handle.AddrOfPinnedObject(), new global::Avalonia.PixelSize(width, height), new global::Avalonia.Vector(96.0, 96.0), stride);
			}
			finally
			{
				handle.Free();
			}
		}

		private static bool SamePixel(byte lhs, byte rhs)
		{
			return Math.Abs(lhs - rhs) < 5;
		}
	}
}
