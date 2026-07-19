using System;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls.Editor.Hex
{
	/// <summary>
	/// v3.1.0：单文件 Hex 视图容器。
	/// 包装 HexEditor + 工具栏（字节宽度下拉、ASCII/Offset 开关、跳转偏移、搜索、复制为原始字节）。
	/// 实现 FileContentControl.IFileContentControlSubControl 以融入现有 SubView 切换机制。
	/// </summary>
	public class HexContentControl : Grid, FileContentControl.IFileContentControlSubControl
	{
		private readonly HexEditor _editor;
		private readonly ComboBox _bytesPerRowComboBox;
		private readonly CheckBox _showAsciiCheckBox;
		private readonly CheckBox _showOffsetCheckBox;
		private HexContent _content;

		public HexContentControl()
		{
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

			// 工具栏
			DockPanel toolbar = new DockPanel { Margin = new Thickness(4, 2, 4, 2), LastChildFill = false };

			// 字节宽度下拉
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

			// ASCII 开关
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

			// Offset 开关
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

			// 搜索按钮
			Button searchButton = new Button
			{
				Content = "🔍",
				ToolTip = PreferencesLocalization.Current("Search (ASCII or hex bytes like 41 42)"),
				Width = 28,
				Height = 22,
				Margin = new Thickness(0, 0, 4, 0),
				VerticalAlignment = VerticalAlignment.Center
			};
			searchButton.Click += SearchButton_Click;
			DockPanel.SetDock(searchButton, Dock.Left);
			toolbar.Children.Add(searchButton);

			// 复制为原始字节
			Button copyRawButton = new Button
			{
				Content = PreferencesLocalization.Current("Copy as raw bytes"),
				Height = 22,
				Padding = new Thickness(6, 0, 6, 0),
				VerticalAlignment = VerticalAlignment.Center
			};
			copyRawButton.Click += CopyRawButton_Click;
			DockPanel.SetDock(copyRawButton, Dock.Left);
			toolbar.Children.Add(copyRawButton);

			Children.Add(toolbar);
			SetRow(toolbar, 0);

			// HexEditor
			_editor = new HexEditor();
			_editor.Loaded += (s, e) => _editor.InstallSearchPanel();
			SetRow(_editor, 1);
			Children.Add(_editor);
		}

		public void SetContent(HexContent content)
		{
			_content = content;
			if (content?.Data != null)
			{
				_editor.LoadBytes(content.Data.ToArray());
			}
			else
			{
				_editor.LoadBytes(null);
			}
		}

		/// <summary>从 FileContentControl 移除时释放 MemoryStream。</summary>
		public void ControlWillBeRemovedFromFileContentControl()
		{
			_content?.DisposeData();
			_content = null;
		}

		private void BytesPerRowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_bytesPerRowComboBox.SelectedItem is int v)
			{
				_editor.BytesPerRow = v;
				ForkPlusSettings.Default.HexViewBytesPerRow = v;
				ForkPlusSettings.Default.Save();
			}
		}

		private void ShowAsciiCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			bool v = _showAsciiCheckBox.IsChecked.GetValueOrDefault();
			_editor.ShowAscii = v;
			ForkPlusSettings.Default.HexViewShowAscii = v;
			ForkPlusSettings.Default.Save();
		}

		private void ShowOffsetCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			bool v = _showOffsetCheckBox.IsChecked.GetValueOrDefault();
			_editor.ShowOffset = v;
			ForkPlusSettings.Default.HexViewShowOffset = v;
			ForkPlusSettings.Default.Save();
		}

		private void SearchButton_Click(object sender, RoutedEventArgs e)
		{
			_editor.InstallSearchPanel();
			_editor.ShowSearch();
		}

		private void CopyRawButton_Click(object sender, RoutedEventArgs e)
		{
			byte[] bytes = _editor.GetSelectedBytes();
			if (bytes.Length == 0) return;
			try
			{
				Clipboard.SetData(DataFormats.Serializable, bytes);
				// 同时设置文本格式，方便粘贴到文本编辑器
				Clipboard.SetData(DataFormats.Text, BitConverter.ToString(bytes).Replace("-", " "));
			}
			catch { }
		}
	}
}
