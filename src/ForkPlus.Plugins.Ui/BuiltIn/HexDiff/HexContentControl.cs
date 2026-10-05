using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.Plugins.BuiltIn.HexDiff
{
	/// <summary>
	/// v3.1.0：单文件 Hex 视图容器。
	/// 包装 HexEditor + 工具栏（字节宽度下拉、ASCII/Offset 开关、跳转偏移、搜索、复制为原始字节）。
	/// 实现 FileContentControl.IFileContentControlSubControl 以融入现有 SubView 切换机制。
	/// </summary>
	public class HexContentControl : Grid
	{
		private readonly HexEditor _editor;
		private readonly ComboBox _bytesPerRowComboBox;
		private readonly CheckBox _showAsciiCheckBox;
		private readonly CheckBox _showOffsetCheckBox;
		private global::System.IO.MemoryStream _data;

		public HexContentControl()
		{
			RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

			// 工具栏
			DockPanel toolbar = new DockPanel { Margin = new Thickness(4, 2, 4, 2), LastChildFill = false };

			// 字节宽度下拉
			TextBlock bprLabel = new TextBlock
			{
				Text = PluginEnvironment.Translate("Bytes per row") + ":",
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
			_bytesPerRowComboBox.SelectedItem = PluginEnvironment.HexBytesPerRow;
			_bytesPerRowComboBox.SelectionChanged += BytesPerRowComboBox_SelectionChanged;
			DockPanel.SetDock(_bytesPerRowComboBox, Dock.Left);
			toolbar.Children.Add(_bytesPerRowComboBox);

			// ASCII 开关
			_showAsciiCheckBox = new CheckBox
			{
				Content = PluginEnvironment.Translate("Show ASCII"),
				IsChecked = PluginEnvironment.HexShowAscii,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};
			_showAsciiCheckBox.IsCheckedChanged+=ShowAsciiCheckBox_Changed;
			DockPanel.SetDock(_showAsciiCheckBox, Dock.Left);
			toolbar.Children.Add(_showAsciiCheckBox);

			// Offset 开关
			_showOffsetCheckBox = new CheckBox
			{
				Content = PluginEnvironment.Translate("Show offset"),
				IsChecked = PluginEnvironment.HexShowOffset,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};
			_showOffsetCheckBox.IsCheckedChanged+=ShowOffsetCheckBox_Changed;
			DockPanel.SetDock(_showOffsetCheckBox, Dock.Left);
			toolbar.Children.Add(_showOffsetCheckBox);

			// 搜索按钮
			Button searchButton = global::ForkPlus.Plugins.PluginCompat.WithTip(new Button
			{
				Content = "🔍",				Width = 28,				Height = 22,				Margin = new Thickness(0, 0, 4, 0),				VerticalAlignment = VerticalAlignment.Center
			},PluginEnvironment.Translate("Search (ASCII or hex bytes like 41 42)"));
			searchButton.Click += SearchButton_Click;
			DockPanel.SetDock(searchButton, Dock.Left);
			toolbar.Children.Add(searchButton);

			// 复制为原始字节
			Button copyRawButton = new Button
			{
				Content = PluginEnvironment.Translate("Copy as raw bytes"),
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

		public void SetContent([global::ForkPlus.Plugins.Null] string path, global::System.IO.MemoryStream data)
		{
			_data = data;
			if (data != null)
			{
				_editor.LoadBytes(data.ToArray());
			}
			else
			{
				_editor.LoadBytes(null);
			}
		}

		/// <summary>从 FileContentControl 移除时释放 MemoryStream。</summary>
		public void ControlWillBeRemovedFromFileContentControl()
		{
			if (_data != null)
			{
				try { _data.Dispose(); } catch { }
				_data = null;
			}
		}

		private void BytesPerRowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_bytesPerRowComboBox.SelectedItem is int v)
			{
				_editor.BytesPerRow = v;
				PluginEnvironment.HexBytesPerRow = v;
				PluginEnvironment.SaveHexSettings();
			}
		}

		private void ShowAsciiCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			bool v = _showAsciiCheckBox.IsChecked.GetValueOrDefault();
			_editor.ShowAscii = v;
			PluginEnvironment.HexShowAscii = v;
			PluginEnvironment.SaveHexSettings();
		}

		private void ShowOffsetCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			bool v = _showOffsetCheckBox.IsChecked.GetValueOrDefault();
			_editor.ShowOffset = v;
			PluginEnvironment.HexShowOffset = v;
			PluginEnvironment.SaveHexSettings();
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
				PluginEnvironment.SetClipboardData(PluginEnvironment.ClipboardFormats.Serializable, bytes);
				// 同时设置文本格式，方便粘贴到文本编辑器
				PluginEnvironment.SetClipboardData(PluginEnvironment.ClipboardFormats.Text, BitConverter.ToString(bytes).Replace("-", " "));
			}
			catch { }
		}
	}
}
