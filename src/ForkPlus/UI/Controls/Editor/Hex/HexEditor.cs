using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ForkPlus.Settings;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Avalonia;
using Avalonia.Input;

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
		private global::AvaloniaEdit.Search.SearchPanel _searchPanel;
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
			// Bug 修复（2026-09-04，"二进制对比（Hex Diff）显示一片空白"）：
			// Avalonia 的 ControlTheme 不像 WPF DefaultStyleKey 沿基类链匹配派生类——
			// HexEditor 的 StyleKey 是自身类型，AvaloniaEdit 官方 {x:Type TextEditor} 主题
			// 匹配不到它 → 无模板 → 无 ScrollViewer → TextArea 不挂视觉树：Text 已赋值
			// 但 TextView.Bounds=0x0、VisualLines 永不重建，渲染一片空白（工具栏/MD5 正常）。
			// 与 CodeEditor 同模式：从应用资源取 {x:Type HexEditor} 专属 ControlTheme 挂到 Theme。
			object hexEditorTheme = Avalonia.Application.Current?.TryFindResource(typeof(HexEditor));
			if (hexEditorTheme != null)
			{
				global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(this, hexEditorTheme);
			}
			base.IsReadOnly = true;
			base.WordWrap = false;
			base.Options.EnableHyperlinks = false;
			base.Options.EnableEmailHyperlinks = false;
			// Bug 修复（2026-09-04）：AvaloniaEdit 12.x 默认 AllowScrollBelowDocument=true
			//（WPF AvalonEdit 默认 false），十六进制视图同样能滚到内容底下一大块空白。
			base.Options.AllowScrollBelowDocument = false;
			base.TextArea.SelectionBorder = null;
			base.TextArea.SelectionCornerRadius = 0.0;
			base.FontFamily = new global::Avalonia.Media.FontFamily("Consolas, Courier New, monospace");
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
				_searchPanel = global::AvaloniaEdit.Search.SearchPanel.Install(this); // Migration note：Install 接收 TextEditor（this），不是 TextArea。
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

		/// <summary>v3.7.1：加载字节，并使用调用方在后台线程预先格式化好的文本，跳过 Format。
		/// 用于 Hex Diff 异步化：Format 在后台线程算完，UI 线程只做 base.Text 赋值。</summary>
		public void LoadBytesWithText(byte[] bytes, string preformattedText)
		{
			_bytes = bytes ?? Array.Empty<byte>();
			base.Text = preformattedText ?? "";
		}

		/// <summary>v3.7.1：增量追加 — 把新增字节拼到已有 _bytes 末尾，并把已格式化好的新段文本追加到文档末尾。
		/// 用于"加载更多"：避免每次追加都整串 base.Text= 重建行树，改用 TextDocument.Insert 在末尾增量插入。</summary>
		public void AppendBytesWithText(byte[] additionalBytes, string additionalFormattedText, int totalByteLength)
		{
			if (additionalBytes == null || additionalBytes.Length == 0) return;
			byte[] combined = new byte[totalByteLength];
			if (_bytes != null && _bytes.Length > 0)
			{
				Array.Copy(_bytes, 0, combined, 0, Math.Min(_bytes.Length, totalByteLength));
			}
			Array.Copy(additionalBytes, 0, combined, _bytes == null ? 0 : _bytes.Length, additionalBytes.Length);
			_bytes = combined;
			// 末尾追加文本（第一段已有内容时先补换行）
			if (base.Document.TextLength > 0)
			{
				base.Document.Insert(base.Document.TextLength, "\n" + (additionalFormattedText ?? ""));
			}
			else
			{
				base.Document.Text = additionalFormattedText ?? "";
			}
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

		protected override void OnKeyDown(global::Avalonia.Input.KeyEventArgs e)
		{
			// Ctrl+C：默认复制 hex 字符串（去除多余空白）
			if (e.Key == global::Avalonia.Input.Key.C && (global::ForkPlus.UI.WpfCompat.Keyboard.Modifiers & global::ForkPlus.UI.WpfCompat.ModifierKeys.Control) == global::ForkPlus.UI.WpfCompat.ModifierKeys.Control)
			{
				string selectedText = base.SelectedText;
				if (!string.IsNullOrEmpty(selectedText))
				{
					try
					{
						global::ForkPlus.UI.WpfCompat.Clipboard.SetText(selectedText); // Migration note：WPF Clipboard.SetText 静态 → WpfCompat 兼容层。
						e.Handled = true;
					}
					catch { }
				}
			}
			base.OnKeyDown(e);
		}
	}
}
