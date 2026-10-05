using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.Plugins.BuiltIn.ImageDiff;

namespace ForkPlus.Plugins.BuiltIn.HexDiff
{
	/// <summary>
	/// v5.0.0：Hex 对比视图（forkplus.hex 插件，二进制文件通配兜底，实现 <see cref="IDiffView"/>）。
	/// 与 4.3.2 非图片二进制的行为一致：默认 side-by-side 文件卡片（共享 <see cref="BinaryContentPanel"/>：
	/// 图标 + 扩展名 + 大小 + LFS 徽章 / LFS smudge / Save As），底部 Side-by-Side / Hex 切换；
	/// Hex 懒创建共享 <see cref="HexDiffUserControl"/>（字节来自宿主预载的
	/// <see cref="DiffViewContext.HexSrc"/>/<see cref="DiffViewContext.HexDst"/>，超过预载阈值的
	/// 超大文件不预载，此时只显示文件卡片、Hex 按钮隐藏）。
	/// 图片专属能力（Swipe/洋葱皮/像素差异高亮/缩放还原）在图片插件的 BinaryDiffView。
	/// </summary>
	public partial class HexDiffView : UserControl, IDiffView
	{
		private bool _showTitle;

		[Null]
		private DiffViewContext _context;

		[Null]
		private IDiffViewHost _host;

		[Null]
		private IDisposable _activeSrcSmudge;

		[Null]
		private IDisposable _activeDstSmudge;

		// Hex 视图 — 原始字节（宿主预载 / LFS smudge）用于懒创建 HexDiffUserControl
		[Null]
		private MemoryStream _hexSrcData;

		[Null]
		private MemoryStream _hexDstData;

		[Null]
		private HexDiffUserControl _hexDiffView;

		// 当前字节是否已 SetContent 到 _hexDiffView（避免侧栏来回切换重复全量重载）
		private bool _hexContentLoaded;

		/// <summary>视图根控件（宿主直接挂载）。</summary>
		global::Avalonia.Controls.Control IDiffView.View => this;

		/// <summary>自带工具条（Side-by-Side/Hex），无需宿主渲染模式切换。</summary>
		IReadOnlyList<DiffViewMode> IDiffView.Modes => Array.Empty<DiffViewMode>();

		/// <summary>无像素级差异高亮能力，不触发（空实现避免 CS0067）。</summary>
		event EventHandler<bool> IDiffView.HighlightPixelsAvailableChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public HexDiffView()
		{
			InitializeComponent();
			// RadioButton 内容（Side-by-Side/Hex）在构造时翻译
			PluginEnvironment.ApplyLocalization(this);
			HexDiffViewContainer.IsVisible = false;
			FallbackUserControl.IsVisible = false;
			// 用户把图片扩展名绑定到 forkplus.hex 时卡片仍能渲染图片：
			// .tga 等宿主侧预处理经宿主能力桥下发到两个面板。
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
			SrcFileContentUserControl.ShowLfsImageButtonClick += delegate
			{
				DiffSideContent srcSide = _context?.Src;
				if (srcSide?.Lfs != null)
				{
					StartSideSmudge(isSrc: true, srcSide);
				}
			};
			SrcFileContentUserControl.CancelLfsButtonClick += delegate
			{
				_activeSrcSmudge?.Dispose();
				_activeSrcSmudge = null;
			};
			DstFileContentUserControl.ShowLfsImageButtonClick += delegate
			{
				DiffSideContent dstSide = _context?.Dst;
				if (dstSide?.Lfs != null)
				{
					StartSideSmudge(isSrc: false, dstSide);
				}
			};
			DstFileContentUserControl.CancelLfsButtonClick += delegate
			{
				_activeDstSmudge?.Dispose();
				_activeDstSmudge = null;
			};
			SrcFileContentUserControl.SaveAsMenuItemClick += delegate
			{
				SaveSideAs(isSrc: true);
			};
			DstFileContentUserControl.SaveAsMenuItemClick += delegate
			{
				SaveSideAs(isSrc: false);
			};
		}

		// ---- IDiffView 生命周期 ----

		/// <summary>下发内容：两侧 DiffSideContent + 宿主能力桥（换文件重置 Hex 视图状态）。</summary>
		public void SetContent(DiffViewContext context, IDiffViewHost host)
		{
			_context = context;
			_host = host;
			_showTitle = context.ShowTitle;
			// 换文件重置 Hex 视图（取消旧后台加载并释放上一份内容）
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

		/// <summary>切换视图模式（用户点底部 RadioButton 的等价入口）。</summary>
		public void SetMode(string modeId)
		{
			if (modeId == "hex")
			{
				if (!HexRadioButton.IsChecked.GetValueOrDefault())
				{
					HexRadioButton.IsChecked = true;
				}
			}
			else if (!SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				SideBySideRadioButton.IsChecked = true;
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
		}

		/// <summary>释放：停后台任务、退订事件、释放内容。之后实例不再复用。</summary>
		public void Release()
		{
			ControlWillBeRemoved();
		}

		/// <summary>宿主切换子视图时清理——取消 Hex 后台加载、停 LFS 任务并释放动图帧。</summary>
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
		}

		// ---- 内容装配 ----

		private void UpdateContent()
		{
			DiffViewContext context = _context;
			if (context == null)
			{
				return;
			}
			if (!SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				SideBySideRadioButton.IsChecked = true;
			}
			FallbackUserControl.IsVisible = false;
			DiffSideContent srcSide = context.Src;
			DiffSideContent dstSide = context.Dst;
			if (srcSide != null && dstSide != null)
			{
				Grid.SetColumnSpan(SrcFileContentUserControl, 1);
				SrcFileContentUserControl.Margin = new Thickness(10.0, 0.0, 5.0, 0.0);
				SrcFileContentUserControl.SetContent(srcSide, context.SrcRole, _showTitle, context.SrcTitleBrush, null);
				Grid.SetColumn(DstFileContentUserControl, 1);
				Grid.SetColumnSpan(DstFileContentUserControl, 1);
				DstFileContentUserControl.Margin = new Thickness(5.0, 0.0, 10.0, 0.0);
				DstFileContentUserControl.SetContent(dstSide, context.DstRole, _showTitle, context.DstTitleBrush, null);
				SrcFileContentUserControl.IsVisible = true;
				DstFileContentUserControl.IsVisible = true;
			}
			else if (srcSide != null)
			{
				Grid.SetColumnSpan(SrcFileContentUserControl, 2);
				SrcFileContentUserControl.Margin = new Thickness(10.0, 0.0, 10.0, 0.0);
				SrcFileContentUserControl.SetContent(srcSide, context.SrcRole, _showTitle, context.SrcTitleBrush, null);
				SrcFileContentUserControl.IsVisible = true;
				DstFileContentUserControl.IsVisible = false;
			}
			else if (dstSide != null)
			{
				Grid.SetColumn(DstFileContentUserControl, 0);
				Grid.SetColumnSpan(DstFileContentUserControl, 2);
				DstFileContentUserControl.Margin = new Thickness(10.0, 0.0, 10.0, 0.0);
				DstFileContentUserControl.SetContent(dstSide, context.DstRole, _showTitle, context.DstTitleBrush, null);
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
			ApplyLfsImageData(isSrc, side, prepared);
		}

		/// <summary>smudge / 缓存命中后回填该侧：渲染卡片内容并刷新 Hex 字节与模式条。</summary>
		private void ApplyLfsImageData(bool isSrc, DiffSideContent side, MemoryStream data)
		{
			BinaryContentPanel panel = isSrc ? SrcFileContentUserControl : DstFileContentUserControl;
			panel.SetLfsImageData(data, null, side.Path);
			if (isSrc)
			{
				_hexSrcData = data;
			}
			else
			{
				_hexDstData = data;
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
				ApplyLfsImageData(isSrc, side, prepared);
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

		// ---- 视图模式切换 ----

		private void ViewModeSelectedItem_Changed(object sender, RoutedEventArgs e)
		{
			if (SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				// 新增/删除文件只有单侧内容，UpdateContent 已把无内容的一侧隐藏；
				// 此处按内容有无恢复，避免把空内容面板重新露出来。
				SrcFileContentUserControl.IsVisible = _context?.Src != null;
				DstFileContentUserControl.IsVisible = _context?.Dst != null;
				HexDiffViewContainer.IsVisible = false;
			}
			else if (HexRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.IsVisible = false;
				DstFileContentUserControl.IsVisible = false;
				ShowHexDiffView();
				HexDiffViewContainer.IsVisible = true;
			}
			UpdateAnimationPlayback();
		}

		/// <summary>并排视图播放动图，Hex 视图暂停，避免后台空转。</summary>
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

		/// <summary>懒创建 HexDiffUserControl 并加载原始字节；同一份数据只 SetContent 一次
		///（侧栏切走再切回不重新解析/不丢"加载更多"进度；换文件由 SetContent 重置标记）。</summary>
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

		/// <summary>工具条可见性：任一侧有内容即显示；Hex 仅在两侧字节可用时显示
		///（小体积二进制由宿主预载；超阈值或加载失败则隐藏，只留文件卡片）。</summary>
		private void RefreshViewModes()
		{
			bool hasSides = _context?.Src != null || _context?.Dst != null;
			if (hasSides)
			{
				ViewModeButtonsContainer.IsVisible = true;
				HexRadioButton.IsVisible = _hexSrcData != null || _hexDstData != null;
			}
			else
			{
				ViewModeButtonsContainer.IsVisible = false;
			}
		}
	}
}
