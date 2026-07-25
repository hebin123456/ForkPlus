using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class PlaceholderTextBox : TextBox
	{
		public static readonly StyledProperty<string> PlaceholderProperty =
			AvaloniaProperty.Register<PlaceholderTextBox, string>(nameof(Placeholder));

		// 阶段 4.5：WPF IImage → Avalonia IImage。
		public static readonly StyledProperty<IImage> IconProperty =
			AvaloniaProperty.Register<PlaceholderTextBox, IImage>(nameof(Icon));

		public string Placeholder
		{
			get => GetValue(PlaceholderProperty);
			set => SetValue(PlaceholderProperty, value);
		}

		public IImage Icon
		{
			get => GetValue(IconProperty);
			set => SetValue(IconProperty, value);
		}

		// 阶段 5：WPF TextBox.SelectionLength 兼容。Avalonia TextBox 无 SelectionLength 属性，
		// 用 SelectionEnd - SelectionStart 计算。
		public int SelectionLength
		{
			get => SelectionEnd - SelectionStart;
			set
			{
				int start = SelectionStart;
				SelectionEnd = start + value;
			}
		}

		public PlaceholderTextBox()
		{
			base.Loaded += delegate
			{
				base.ContextMenu = GetContextMenu();
			};
		}

		protected virtual ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.AddDefaultTextBoxMenuItems(this);
			return contextMenu;
		}

		/// <summary>
		/// WPF TextBox.GetRectFromCharacterIndex 兼容：返回指定字符索引处的光标边界矩形。
		/// Avalonia TextBox 无此 API；此处用 FormattedText 近似度量字符宽度，返回相对控件左上角的 Rect。
		/// 主要用于 AutoCompleteTextBox 定位下拉 Popup。
		/// </summary>
		public Rect GetCursorBounds(int characterIndex)
		{
			// 阶段 5：Avalonia TextBox 无 GetRectFromCharacterIndex。用 FormattedText 度量字符位置。
			// 仅度量前 characterIndex 个字符的宽度，垂直方向用 FontSize 近似行高。
			if (characterIndex < 0)
			{
				characterIndex = 0;
			}
			string text = base.Text ?? string.Empty;
			if (characterIndex > text.Length)
			{
				characterIndex = text.Length;
			}
			string prefix = text.Substring(0, characterIndex);
			double x = 0.0;
			double y = 0.0;
			double height = base.FontSize + 4.0;
			if (!string.IsNullOrEmpty(prefix))
			{
				try
				{
					var formatted = new FormattedText(
						prefix,
						System.Globalization.CultureInfo.InvariantCulture,
						base.FlowDirection,
						new Typeface(base.FontFamily, base.FontStyle, base.FontWeight),
						base.FontSize,
						Avalonia.Media.Brushes.Black);
					x = formatted.Width;
				}
				catch
				{
					// 度量失败时退化为字符数 × 估计字符宽
					x = characterIndex * (base.FontSize * 0.5);
				}
			}
			// 加上 Padding.Left 与 BorderThickness.Left 的偏移
			x += base.Padding.Left + base.BorderThickness.Left;
			y += base.Padding.Top + base.BorderThickness.Top;
			return new Rect(x, y, 2.0, height);
		}
	}
}
