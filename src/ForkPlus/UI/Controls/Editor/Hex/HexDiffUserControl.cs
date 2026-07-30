using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls.Editor.Hex
{
	/// <summary>
	/// v3.1.0：side-by-side Hex Diff 视图。
	/// 左侧显示 src（旧版本）字节，右侧显示 dst（新版本）字节。
	/// 共享字节宽度 / ASCII / Offset 设置，差异字节用背景色高亮（橙黄）。
	/// 实现 FileContentControl.IFileContentControlSubControl 以便在 SubView 切换时释放 MemoryStream。
	/// </summary>
	public class HexDiffUserControl : Grid, FileContentControl.IFileContentControlSubControl
	{
		private const int MaxBytesForDiffHighlight = 2 * 1024 * 1024; // 2MB：超过此阈值跳过逐字节比较（避免大文件卡顿）

		// 差异字节背景色（橙黄）
		private static readonly Brush DiffByteBackgroundBrush;
		private static readonly Brush DiffByteForegroundBrush;

		static HexDiffUserControl()
		{
			DiffByteBackgroundBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // Gold
			DiffByteBackgroundBrush.Freeze();
			DiffByteForegroundBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)); // Black
			DiffByteForegroundBrush.Freeze();
		}

		private readonly HexEditor _srcEditor;
	private readonly HexEditor _dstEditor;
	private readonly ComboBox _bytesPerRowComboBox;
	private readonly CheckBox _showAsciiCheckBox;
	private readonly CheckBox _showOffsetCheckBox;
	// v3.1.1：左右行对齐（滚动同步）开关，默认勾上
	private readonly CheckBox _syncScrollCheckBox;
	private HexDiffContent _content;

	// v3.1.1：滚动同步防抖状态（参考 SideBySideTextDiffControl 的 100ms 防抖模式，
	// 避免两侧相互触发 ScrollOffsetChanged 形成回环）
	private DateTime _lastScrollTime;
	private HexEditor _lastScrolledEditor;
	private bool _isSyncingScroll;

	// v3.6.5：MD5 行 — 左侧 src（修改前）MD5，右侧 dst（修改后）MD5，与列头两列对齐
	private readonly TextBlock _srcMd5TextBlock;
	private readonly TextBlock _dstMd5TextBlock;

	// v3.7.1：异步加载取消控制。每次 SetContent 取消上一次未完成的异步加载，
	// 避免快速切换文件时旧任务回填到 UI 造成内容错乱。
	private CancellationTokenSource _loadCts;

		public HexDiffUserControl()
		{
			// v3.6.5：四行布局 — Row 0 工具栏，Row 1 MD5 行，Row 2 列头（对齐编辑器），Row 3 编辑器
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

			// 共享工具栏
			DockPanel toolbar = new DockPanel { Margin = new Thickness(4, 2, 4, 2), LastChildFill = false };

			TextBlock bprLabel = new TextBlock
			{
				Text = PreferencesLocalization.Current("Bytes per row") + ":",
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 4, 0)
			};
			DockPanel.SetDock(bprLabel, Dock.Left);
			toolbar.Children.Add(bprLabel);

			_bytesPerRowComboBox = new ComboBox
			{
				Width = 60,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};
			_bytesPerRowComboBox.Items.Add(8);
			_bytesPerRowComboBox.Items.Add(16);
			_bytesPerRowComboBox.Items.Add(32);
			_bytesPerRowComboBox.SelectedItem = ForkPlusSettings.Default.HexViewBytesPerRow;
			_bytesPerRowComboBox.SelectionChanged += BytesPerRowComboBox_SelectionChanged;
			DockPanel.SetDock(_bytesPerRowComboBox, Dock.Left);
			toolbar.Children.Add(_bytesPerRowComboBox);

			_showAsciiCheckBox = new CheckBox
			{
				Content = PreferencesLocalization.Current("Show ASCII"),
				IsChecked = ForkPlusSettings.Default.HexViewShowAscii,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};
			_showAsciiCheckBox.Checked += ShowAsciiCheckBox_Changed;
			_showAsciiCheckBox.Unchecked += ShowAsciiCheckBox_Changed;
			DockPanel.SetDock(_showAsciiCheckBox, Dock.Left);
			toolbar.Children.Add(_showAsciiCheckBox);

			_showOffsetCheckBox = new CheckBox
		{
			Content = PreferencesLocalization.Current("Show offset"),
			IsChecked = ForkPlusSettings.Default.HexViewShowOffset,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 8, 0)
		};
		_showOffsetCheckBox.Checked += ShowOffsetCheckBox_Changed;
		_showOffsetCheckBox.Unchecked += ShowOffsetCheckBox_Changed;
		DockPanel.SetDock(_showOffsetCheckBox, Dock.Left);
		toolbar.Children.Add(_showOffsetCheckBox);

		// v3.1.1：左右行对齐 — 默认勾上。勾上时左右两侧 HexEditor 同步垂直/水平滚动，
		// 一侧拉到第 N 行，另一侧也拉到第 N 行。
		_syncScrollCheckBox = new CheckBox
		{
			Content = PreferencesLocalization.Current("Sync scroll"),
			IsChecked = true,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 8, 0)
		};
		DockPanel.SetDock(_syncScrollCheckBox, Dock.Left);
		toolbar.Children.Add(_syncScrollCheckBox);

			Children.Add(toolbar);
			SetRow(toolbar, 0);

			// v3.6.5：MD5 行 — 与列头同构的两列布局，左侧 src（修改前）MD5，右侧 dst（修改后）MD5。
			// 实际 Text 在 SetContent 时填充。用等宽字体避免 hash 串错位。
			Grid md5Grid = new Grid();
			md5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			md5Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			_srcMd5TextBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(4, 1, 0, 1),
				FontFamily = new FontFamily("Consolas, Courier New, monospace"),
				FontSize = 12,
				TextTrimming = TextTrimming.CharacterEllipsis,
				Text = "MD5: -"
			};
			md5Grid.Children.Add(_srcMd5TextBlock);
			SetColumn(_srcMd5TextBlock, 0);

			_dstMd5TextBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(4, 1, 0, 1),
				FontFamily = new FontFamily("Consolas, Courier New, monospace"),
				FontSize = 12,
				TextTrimming = TextTrimming.CharacterEllipsis,
				Text = "MD5: -"
			};
			md5Grid.Children.Add(_dstMd5TextBlock);
			SetColumn(_dstMd5TextBlock, 1);

			Children.Add(md5Grid);
			SetRow(md5Grid, 1);

			// v3.4.1：列头行 — "修改前" / "修改后"，与下方编辑器左右对齐
			Grid headerGrid = new Grid();
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			TextBlock srcLabel = new TextBlock
			{
				Text = PreferencesLocalization.Current("Before Modification") + ":",
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(4, 2, 0, 2),
				FontWeight = FontWeights.Medium
			};
			headerGrid.Children.Add(srcLabel);
			SetColumn(srcLabel, 0);

			TextBlock dstLabel = new TextBlock
			{
				Text = PreferencesLocalization.Current("After Modification") + ":",
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(4, 2, 0, 2),
				FontWeight = FontWeights.Medium
			};
			headerGrid.Children.Add(dstLabel);
			SetColumn(dstLabel, 1);

			Children.Add(headerGrid);
			SetRow(headerGrid, 2);

			// 两个 HexEditor 并排放在 Grid 里
			Grid editorsGrid = new Grid();
			editorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			editorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			_srcEditor = new HexEditor();
		_srcEditor.Loaded += (s, e) => _srcEditor.InstallSearchPanel();
		// v3.1.1：订阅滚动事件，用于左右行对齐
		_srcEditor.TextArea.TextView.ScrollOffsetChanged += delegate { OnScrollOffsetChanged(_srcEditor); };
		editorsGrid.Children.Add(_srcEditor);
		SetColumn(_srcEditor, 0);

		_dstEditor = new HexEditor();
		_dstEditor.Loaded += (s, e) => _dstEditor.InstallSearchPanel();
		// v3.1.1：订阅滚动事件，用于左右行对齐
		_dstEditor.TextArea.TextView.ScrollOffsetChanged += delegate { OnScrollOffsetChanged(_dstEditor); };
		editorsGrid.Children.Add(_dstEditor);
		SetColumn(_dstEditor, 1);

			Children.Add(editorsGrid);
			SetRow(editorsGrid, 3);
		}

		public void SetContent(HexDiffContent content)
		{
			_content = content;
			// 取消上一次未完成的异步加载
			CancelPendingLoad();
			CancellationTokenSource cts = new CancellationTokenSource();
			_loadCts = cts;
			CancellationToken token = cts.Token;

			// 立即清空并显示 loading 占位，避免用户看到旧内容残留
			_srcEditor.LoadBytes(null);
			_dstEditor.LoadBytes(null);
			_srcMd5TextBlock.Text = "MD5: ...";
			_dstMd5TextBlock.Text = "MD5: ...";

			// UI 线程快照 HexEditor 的格式化参数（后台线程不能访问 DispatcherObject 属性）
			int bytesPerRow = _srcEditor.BytesPerRow;
			bool showOffset = _srcEditor.ShowOffset;
			bool showAscii = _srcEditor.ShowAscii;

			// 提取原始字节（ToArray 是内存拷贝，放后台避免占用 UI 线程）
			Task.Run(() =>
			{
				token.ThrowIfCancellationRequested();
				byte[] srcBytes = content?.SrcData?.ToArray();
				byte[] dstBytes = content?.DstData?.ToArray();

				// 后台：格式化 hex 文本（纯 StringBuilder，无 UI 依赖）
				token.ThrowIfCancellationRequested();
				string srcText = srcBytes == null ? "" : HexFormatter.Format(srcBytes, bytesPerRow, showOffset, showAscii);
				token.ThrowIfCancellationRequested();
				string dstText = dstBytes == null ? "" : HexFormatter.Format(dstBytes, bytesPerRow, showOffset, showAscii);

				// 后台：逐字节比较生成差异索引集合
				token.ThrowIfCancellationRequested();
				HashSet<int> srcDiff = null;
				HashSet<int> dstDiff = null;
				ComputeDiffIndices(srcBytes, dstBytes, out srcDiff, out dstDiff);

				// 后台：MD5 计算
				token.ThrowIfCancellationRequested();
				string srcMd5 = srcBytes == null ? "-" : ComputeMd5Hex(srcBytes);
				token.ThrowIfCancellationRequested();
				string dstMd5 = dstBytes == null ? "-" : ComputeMd5Hex(dstBytes);

				// 切回 UI 线程：只做 base.Text 赋值 + 高亮重绘（DispatcherObject 必须在 UI 线程访问）
				Dispatcher.BeginInvoke(new Action(() =>
				{
					if (token.IsCancellationRequested) return;
					_srcEditor.LoadBytesWithText(srcBytes, srcText);
					_dstEditor.LoadBytesWithText(dstBytes, dstText);
					_srcEditor.HighlightBytes(srcDiff);
					_dstEditor.HighlightBytes(dstDiff);
					_srcMd5TextBlock.Text = "MD5: " + srcMd5;
					_dstMd5TextBlock.Text = "MD5: " + dstMd5;
				}));
			}, token).ContinueWith(t =>
			{
				// 后台异常静默记录（取消导致的异常不算错误）
				if (t.IsFaulted && !(t.Exception?.InnerException is OperationCanceledException))
				{
					Log.Error("Hex diff async load failed", t.Exception);
				}
			}, TaskScheduler.Default);
		}

		/// <summary>v3.7.1：取消正在进行的异步加载。</summary>
		private void CancelPendingLoad()
		{
			if (_loadCts != null)
			{
				try { _loadCts.Cancel(); } catch { }
				_loadCts = null;
			}
		}

		/// <summary>v3.7.1：后台计算 src/dst 的差异字节索引（从 ApplyDiffHighlight 抽出，纯 CPU 无 UI 依赖）。</summary>
		private void ComputeDiffIndices(byte[] srcBytes, byte[] dstBytes, out HashSet<int> srcDiff, out HashSet<int> dstDiff)
		{
			srcDiff = null;
			dstDiff = null;
			if (srcBytes == null || dstBytes == null) return;
			int len = Math.Min(srcBytes.Length, dstBytes.Length);
			if (len > MaxBytesForDiffHighlight) return; // 大文件跳过逐字节比较

			srcDiff = new HashSet<int>();
			dstDiff = new HashSet<int>();
			for (int i = 0; i < len; i++)
			{
				if (srcBytes[i] != dstBytes[i])
				{
					srcDiff.Add(i);
					dstDiff.Add(i);
				}
			}
			// 超出对侧长度的部分也视为差异
			if (srcBytes.Length > dstBytes.Length)
			{
				for (int i = dstBytes.Length; i < srcBytes.Length; i++) srcDiff.Add(i);
			}
			if (dstBytes.Length > srcBytes.Length)
			{
				for (int i = srcBytes.Length; i < dstBytes.Length; i++) dstDiff.Add(i);
			}
		}

		/// <summary>v3.6.5：计算字节数组的 MD5 并返回小写十六进制字符串。</summary>
		private static string ComputeMd5Hex(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return "-";
			using (MD5 md5 = MD5.Create())
			{
				byte[] hash = md5.ComputeHash(bytes);
				StringBuilder sb = new StringBuilder(hash.Length * 2);
				foreach (byte b in hash)
				{
					sb.Append(b.ToString("x2"));
				}
				return sb.ToString();
			}
		}

		public void ControlWillBeRemovedFromFileContentControl()
		{
			// v3.7.1：控件移除时取消未完成的异步加载，避免回填到已失效的 UI
			CancelPendingLoad();
			_content?.DisposeData();
			_content = null;
		}

		private void BytesPerRowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_bytesPerRowComboBox.SelectedItem is int v)
			{
				_srcEditor.BytesPerRow = v;
				_dstEditor.BytesPerRow = v;
				ForkPlusSettings.Default.HexViewBytesPerRow = v;
				ForkPlusSettings.Default.Save();
			}
		}

		private void ShowAsciiCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			bool v = _showAsciiCheckBox.IsChecked.GetValueOrDefault();
			_srcEditor.ShowAscii = v;
			_dstEditor.ShowAscii = v;
			ForkPlusSettings.Default.HexViewShowAscii = v;
			ForkPlusSettings.Default.Save();
		}

		private void ShowOffsetCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		bool v = _showOffsetCheckBox.IsChecked.GetValueOrDefault();
		_srcEditor.ShowOffset = v;
		_dstEditor.ShowOffset = v;
		ForkPlusSettings.Default.HexViewShowOffset = v;
		ForkPlusSettings.Default.Save();
	}

	/// <summary>
	/// v3.1.1：左右行对齐（滚动同步）。一侧滚动时，把另一侧也滚到相同 vertical/horizontal offset。
	/// 采用 100ms 防抖 + _isSyncingScroll 重入守卫，避免两侧相互触发 ScrollOffsetChanged 形成回环。
	/// 参考 SideBySideTextDiffControl.OnScrollOffsetChanged 的实现。
	/// </summary>
	private void OnScrollOffsetChanged(HexEditor editor)
	{
		// 用户取消勾选"左右行对齐"时，完全不同步
		if (_syncScrollCheckBox == null || _syncScrollCheckBox.IsChecked != true)
		{
			return;
		}
		// 正在同步对侧滚动期间触发的回调直接忽略，避免回环
		if (_isSyncingScroll)
		{
			return;
		}
		// 100ms 防抖：连续滚动时只让先发起的那一侧主导，另一侧触发的回调被丢弃
		if (DateTime.Now - _lastScrollTime < TimeSpan.FromMilliseconds(100.0) && editor != _lastScrolledEditor)
		{
			return;
		}
		double verticalOffset = editor.TextArea.TextView.VerticalOffset;
		double horizontalOffset = editor.TextArea.TextView.HorizontalOffset;
		HexEditor other = editor == _srcEditor ? _dstEditor : _srcEditor;
		_isSyncingScroll = true;
		try
		{
			if (editor.IsVerticalOffsetWithinDocumentArea(verticalOffset))
			{
				other.ScrollToVerticalOffset(verticalOffset);
			}
			if (editor.IsHorizontalOffsetWithinDocumentArea(horizontalOffset))
			{
				other.ScrollToHorizontalOffset(horizontalOffset);
			}
		}
		finally
		{
			_isSyncingScroll = false;
		}
		_lastScrollTime = DateTime.Now;
		_lastScrolledEditor = editor;
	}
}
}
