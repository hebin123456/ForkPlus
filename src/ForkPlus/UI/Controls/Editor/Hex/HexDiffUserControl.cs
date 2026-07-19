using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
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
		private HexDiffContent _content;

		public HexDiffUserControl()
		{
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

			// Src / Dst 标签
			TextBlock srcLabel = new TextBlock
			{
				Text = PreferencesLocalization.Current("Source") + ":",
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 4, 0)
			};
			DockPanel.SetDock(srcLabel, Dock.Left);
			toolbar.Children.Add(srcLabel);

			TextBlock dstLabel = new TextBlock
			{
				Text = PreferencesLocalization.Current("Destination") + ":",
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(20, 0, 4, 0)
			};
			DockPanel.SetDock(dstLabel, Dock.Left);
			toolbar.Children.Add(dstLabel);

			Children.Add(toolbar);
			SetRow(toolbar, 0);

			// 两个 HexEditor 并排放在 Grid 里
			Grid editorsGrid = new Grid();
			editorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			editorsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			_srcEditor = new HexEditor();
			_srcEditor.Loaded += (s, e) => _srcEditor.InstallSearchPanel();
			editorsGrid.Children.Add(_srcEditor);
			SetColumn(_srcEditor, 0);

			_dstEditor = new HexEditor();
			_dstEditor.Loaded += (s, e) => _dstEditor.InstallSearchPanel();
			editorsGrid.Children.Add(_dstEditor);
			SetColumn(_dstEditor, 1);

			Children.Add(editorsGrid);
			SetRow(editorsGrid, 1);
		}

		public void SetContent(HexDiffContent content)
		{
			_content = content;
			byte[] srcBytes = content?.SrcData?.ToArray();
			byte[] dstBytes = content?.DstData?.ToArray();
			_srcEditor.LoadBytes(srcBytes);
			_dstEditor.LoadBytes(dstBytes);
			ApplyDiffHighlight(srcBytes, dstBytes);
		}

		/// <summary>逐字节比较两侧，在差异字节位置叠加背景色。
		/// 实现：直接修改 HexEditor 内的 AvalonEdit 文本 — 这里改用一种轻量方式：
		/// 通过给两侧 HexEditor 安装一个 DiffHighlightTransformer 来高亮差异字节。</summary>
		private void ApplyDiffHighlight(byte[] srcBytes, byte[] dstBytes)
		{
			if (srcBytes == null || dstBytes == null) return;
			int len = Math.Min(srcBytes.Length, dstBytes.Length);
			if (len > MaxBytesForDiffHighlight) return; // 大文件跳过

			// 收集 src 侧差异字节索引
			System.Collections.Generic.HashSet<int> srcDiff = new System.Collections.Generic.HashSet<int>();
			System.Collections.Generic.HashSet<int> dstDiff = new System.Collections.Generic.HashSet<int>();
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

			_srcEditor.HighlightBytes(srcDiff);
			_dstEditor.HighlightBytes(dstDiff);
		}

		public void ControlWillBeRemovedFromFileContentControl()
		{
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
	}
}
