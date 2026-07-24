using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Services;
using ForkPlus.Settings;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Hex
{
	/// <summary>
	/// v3.1.0：基于 AvalonEdit 的十六进制查看器。
	/// 把字节流格式化为固定宽度文本（offset | hex | ascii 三列），
	/// 用 HexColorizer 给三列上色，复用 AvalonEdit 的虚拟化、选中、复制、搜索能力。
	/// </summary>
	public class HexEditor : TextEditor
	{
		private byte[] _bytes;
		private int _bytesPerRow = 16;
		private bool _showAscii = true;
		private bool _showOffset = true;
		private HexColorizer _colorizer;
		private AvaloniaEdit.Search.SearchPanel _searchPanel;
		// v3.1.0：差异字节索引集合（用于 Hex Diff 视图高亮），null 表示不高亮
		private HashSet<int> _highlightedBytes;

		/// <summary>每行字节数（支持 8/16/32）。</summary>
		public int BytesPerRow
		{
			get { return _bytesPerRow; }
			set
			{
				int v = value == 8 || value == 16 || value == 32 ? value : 16;
				if (v != _bytesPerRow)
				{
					_bytesPerRow = v;
					Rebuild();
				}
			}
		}

		public bool ShowAscii
		{
			get { return _showAscii; }
			set
			{
				if (value != _showAscii)
				{
					_showAscii = value;
					Rebuild();
				}
			}
		}

		public bool ShowOffset
		{
			get { return _showOffset; }
			set
			{
				if (value != _showOffset)
				{
					_showOffset = value;
					Rebuild();
				}
			}
		}

		public HexEditor()
		{
			base.IsReadOnly = true;
			base.WordWrap = false;
			base.Options.EnableHyperlinks = false;
			base.Options.EnableEmailHyperlinks = false;
			base.TextArea.SelectionBorder = null;
			base.TextArea.SelectionCornerRadius = 0.0;
			base.FontFamily = new FontFamily("Consolas, Courier New, monospace");
			base.FontSize = 13.0;
			_colorizer = new HexColorizer(this);
			base.TextArea.TextView.LineTransformers.Add(_colorizer);
			// 从设置恢复
			_bytesPerRow = ForkPlusSettings.Default.HexViewBytesPerRow;
			_showAscii = ForkPlusSettings.Default.HexViewShowAscii;
			_showOffset = ForkPlusSettings.Default.HexViewShowOffset;
		}

		/// <summary>初始化内建搜索面板（需在控件加载后调用，否则 TextArea 未就绪）。</summary>
		public void InstallSearchPanel()
		{
			if (_searchPanel == null)
			{
				_searchPanel = AvaloniaEdit.Search.SearchPanel.Install(base.TextArea);
			}
		}

		/// <summary>显示搜索面板。</summary>
		public void ShowSearch()
		{
			_searchPanel?.Open();
			if (_searchPanel != null && !_searchPanel.IsClosed)
			{
				_searchPanel.Reactivate();
			}
		}

		/// <summary>加载字节并渲染。</summary>
		public void LoadBytes(byte[] bytes)
		{
			_bytes = bytes ?? Array.Empty<byte>();
			Rebuild();
		}

		/// <summary>当前已加载的字节（可能为 null）。</summary>
		public byte[] GetBytes()
		{
			return _bytes;
		}

		/// <summary>v3.1.0：标记需要高亮背景的字节索引（用于 Hex Diff）。传 null 清除高亮。</summary>
		public void HighlightBytes(HashSet<int> byteIndices)
		{
			_highlightedBytes = byteIndices;
			_colorizer?.SetHighlightedBytes(byteIndices);
			base.TextArea.TextView.Redraw();
		}

		/// <summary>v3.1.0：当前高亮的字节索引集合（可能为 null）。</summary>
		public HashSet<int> GetHighlightedBytes()
		{
			return _highlightedBytes;
		}

		private void Rebuild()
		{
			if (_bytes == null)
			{
				base.Text = "";
				return;
			}
			string text = HexFormatter.Format(_bytes, _bytesPerRow, _showOffset, _showAscii);
			base.Text = text;
		}

		/// <summary>把选中文本中的 hex 字节解析回原始字节（用于"复制为原始字节"）。</summary>
		public byte[] GetSelectedBytes()
		{
			if (_bytes == null) return Array.Empty<byte>();
			// AvalonEdit Selection 是基于字符偏移的，根据选中起止字符偏移反推字节区间
			int startOffset = base.SelectionStart;
			int endOffset = startOffset + base.SelectionLength;
			ByteRange range = HexFormatter.CharOffsetsToByteRange(startOffset, endOffset, _bytesPerRow, _showOffset, _showAscii);
			int start = Math.Max(0, range.Start);
			int end = Math.Min(_bytes.Length, range.End);
			if (end <= start) return Array.Empty<byte>();
			byte[] result = new byte[end - start];
			Array.Copy(_bytes, start, result, 0, result.Length);
			return result;
		}

		// 阶段 4 里程碑 4.7-a：WPF OnPreviewKeyDown(KeyEventArgs) → Avalonia OnKeyDown(KeyEventArgs)。
		// WPF Keyboard.Modifiers → e.KeyModifiers.HasFlag(KeyModifiers.Control)。
		// WPF Clipboard.SetText → ServiceLocator.Clipboard.SetText（已封装的跨平台剪贴板服务）。
		protected override void OnKeyDown(KeyEventArgs e)
		{
			// Ctrl+C：默认复制 hex 字符串（去除多余空白）
			if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
			{
				string selectedText = base.SelectedText;
				if (!string.IsNullOrEmpty(selectedText))
				{
					try
					{
						ServiceLocator.Clipboard.SetText(selectedText);
						e.Handled = true;
					}
					catch { }
				}
			}
			base.OnKeyDown(e);
		}
	}
}
