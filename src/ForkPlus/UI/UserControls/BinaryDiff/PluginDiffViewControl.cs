using System;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using ForkPlus.Git;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Plugins;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	/// <summary>
	/// v5.0.0：对比视图的宿主适配器（取代原主工程 BinaryDiffUserControl 的对外角色）。
	/// 职责：① 把宿主领域模型（BinaryDiffContent / UnknownBinaryDiffContent / LfsDiffContent +
	/// HexDiffContent 预载字节）压平为插件侧 <see cref="DiffViewContext"/>；② 按文件路径经
	/// <see cref="DiffViewPluginRegistry"/> 路由到对比视图插件并挂载其 View；③ 经
	/// <see cref="DiffViewHostAdapter"/> 向插件供给 LFS/保存/错误弹窗等宿主能力；
	/// ④ 转发宿主生命周期（本地化 / 子视图卸载 / 高亮像素开关可用性）。
	/// UI 逻辑全部在插件内（ForkPlus.Plugins），本类不含渲染代码。
	/// </summary>
	public class PluginDiffViewControl : Grid, ILocalizableControl, DiffControlContainer.IFileDiffControlSubControl
	{
		[Null]
		private IDiffView _view;

		[Null]
		private DiffViewHostAdapter _host;

		[Null]
		private string _currentPluginId;

		/// <summary>「高亮差异像素」可用性变化（原 DiffImageSourceChanged；FileControlHeader 联动开关）。</summary>
		public event EventHandler<bool> DiffImageSourceChanged;

		/// <summary>
		/// 与原 BinaryDiffUserControl.UpdateDiff 同签名：宿主（FileDiffControl / CommitFileDiffControl /
		/// FileMergeControl）下发 DiffContent + 可选的 Hex 预载字节。
		/// </summary>
		public void UpdateDiff(RepositoryUserControl repositoryUserControl, DiffContent diffContent, bool showTitle = true, HexDiffContent hexContent = null)
		{
			if (diffContent == null)
			{
				return;
			}
			ChangedFile changedFile = diffContent.ChangedFile;
			DiffViewContext context = BuildContext(changedFile, diffContent, showTitle, hexContent);
			// 路由：优先目标侧（新路径）——重命名场景下用户看到的是新名字的文件类型。
			string routingPath = context.Dst?.Path ?? context.Src?.Path ?? changedFile.Path;
			IDiffViewPlugin plugin = DiffViewPluginRegistry.Resolve(routingPath, context.Src?.Size, context.Dst?.Size);
			if (plugin == null)
			{
				// 无任何插件认领（注册表被清空的异常场景）：回退到无内容占位。
				ReleaseView();
				ShowNoPluginFallback();
				return;
			}
			if (_view == null || _currentPluginId != plugin.Id)
			{
				ReleaseView();
				_host = new DiffViewHostAdapter(repositoryUserControl);
				_view = plugin.CreateView();
				_currentPluginId = plugin.Id;
				_view.HighlightPixelsAvailableChanged += View_HighlightPixelsAvailableChanged;
				global::Avalonia.Controls.Control viewControl = ((IDiffView)_view).View;
				base.Children.Add(viewControl);
			}
			else if (_host != null && repositoryUserControl != null)
			{
				// 同一插件实例被复用（侧栏切换文件）：刷新宿主桥的仓库上下文。
				_host.UpdateRepository(repositoryUserControl);
			}
			_view.SetContent(context, _host);
		}

		private void View_HighlightPixelsAvailableChanged(object sender, bool available)
		{
			this.DiffImageSourceChanged?.Invoke(this, available);
		}

		/// <summary>把宿主 DiffContent 领域模型压平为插件侧 DiffViewContext。</summary>
		private static DiffViewContext BuildContext(ChangedFile changedFile, DiffContent diffContent, bool showTitle, [Null] HexDiffContent hexContent)
		{
			MemoryStream hexSrc = null;
			MemoryStream hexDst = null;
			DiffSideContent src = null;
			DiffSideContent dst = null;
			string srcPath = changedFile.OldPath ?? changedFile.Path;
			string dstPath = changedFile.Path;
			if (diffContent is BinaryDiffContent binaryDiffContent)
			{
				hexSrc = binaryDiffContent.SrcData;
				hexDst = binaryDiffContent.DstData;
				if (binaryDiffContent.SrcData != null)
				{
					src = CreateSide(srcPath, changedFile.Tracked, binaryDiffContent.SrcData);
				}
				if (binaryDiffContent.DstData != null)
				{
					dst = CreateSide(dstPath, changedFile.Tracked, binaryDiffContent.DstData);
				}
			}
			else if (diffContent is UnknownBinaryDiffContent unknownBinaryDiffContent)
			{
				// v3.7.2：宿主对 ≤50MB 非 LFS 二进制预载的字节（供 Hex 模式增量渲染）
				if (hexContent != null)
				{
					hexSrc = hexContent.SrcData;
					hexDst = hexContent.DstData;
				}
				if (unknownBinaryDiffContent.SrcSize.HasValue)
				{
					src = new DiffSideContent(srcPath, changedFile.Tracked, unknownBinaryDiffContent.SrcSize, loadData: null);
				}
				if (unknownBinaryDiffContent.DstSize.HasValue)
				{
					dst = new DiffSideContent(dstPath, changedFile.Tracked, unknownBinaryDiffContent.DstSize, loadData: null);
				}
			}
			else if (diffContent is LfsDiffContent lfsDiffContent)
			{
				if (lfsDiffContent.Src != null)
				{
					src = CreateLfsSide(srcPath, changedFile.Tracked, lfsDiffContent.Src, lfsDiffContent.BinaryFileType == BinaryFileType.LfsImage);
				}
				if (lfsDiffContent.Dst != null)
				{
					dst = CreateLfsSide(dstPath, changedFile.Tracked, lfsDiffContent.Dst, lfsDiffContent.BinaryFileType == BinaryFileType.LfsImage);
				}
			}
			// 角色与标题颜色：双侧 old/new；仅左侧 removed；仅右侧 created（与原实现一致）。
			DiffSideRole srcRole = (dst != null) ? DiffSideRole.Old : DiffSideRole.Removed;
			DiffSideRole dstRole = (src != null) ? DiffSideRole.New : DiffSideRole.Created;
			return new DiffViewContext(dstPath, showTitle, src, dst, srcRole, dstRole,
				global::ForkPlus.UI.Theme.Diff.RemovedForegroundBrush, global::ForkPlus.UI.Theme.Diff.AddedForegroundBrush, hexSrc, hexDst);
		}

		/// <summary>有字节的一侧：字节可能是 LFS 指针文本（长度 120–1024 之间先探测），否则按图片字节处理。</summary>
		[Null]
		private static DiffSideContent CreateSide(string path, bool isTracked, MemoryStream data)
		{
			if (CanBeLfs(data))
			{
				LfsPointer lfsPointer = LfsPointer.Parse(Encoding.UTF8.GetString(data.ToArray()));
				if (lfsPointer != null)
				{
					return CreateLfsSide(path, isTracked, lfsPointer, isLfsImage: true);
				}
			}
			return new DiffSideContent(path, isTracked, data.Length, () => data);
		}

		[Null]
		private static DiffSideContent CreateLfsSide(string path, bool isTracked, LfsPointer pointer, bool isLfsImage)
		{
			return new DiffSideContent(path, isTracked, pointer.Size, loadData: null, new LfsRef(pointer.Sha256String, pointer.Size), isLfsImage);
		}

		/// <summary>原 CanBeLfs：LFS 指针文件长度恒在 120–1024 字节之间。</summary>
		private static bool CanBeLfs(MemoryStream memoryStream)
		{
			if (memoryStream.Length <= 120 || memoryStream.Length >= 1024)
			{
				return false;
			}
			return true;
		}

		private void ShowNoPluginFallback()
		{
			FallbackUserControl fallback = new FallbackUserControl
			{
				FallbackMessage = PreferencesLocalization.Current("File has no changes")
			};
			base.Children.Add(fallback);
		}

		public void ApplyLocalization()
		{
			_view?.ApplyLocalization();
		}

		/// <summary>宿主（DiffControlContainer）切换子视图时清理：释放插件视图并取消其后台任务。</summary>
		public void ControlWillBeRemovedFromFileDiffControl()
		{
			ReleaseView();
		}

		private void ReleaseView()
		{
			if (_view != null)
			{
				_view.HighlightPixelsAvailableChanged -= View_HighlightPixelsAvailableChanged;
				_view.Release();
				_view = null;
			}
			_currentPluginId = null;
			_host?.Dispose();
			_host = null;
			base.Children.Clear();
		}
	}
}
