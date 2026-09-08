using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class PlaceholderTextBox : TextBox
	{
		public static readonly global::Avalonia.StyledProperty<string> PlaceholderProperty =
    global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, string>("Placeholder");

		public static readonly global::Avalonia.StyledProperty<global::Avalonia.Media.IImage> IconProperty =
    global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, global::Avalonia.Media.IImage>("Icon");

		public string Placeholder
		{
			get
			{
				return (string)GetValue(PlaceholderProperty);
			}
			set
			{
				SetValue(PlaceholderProperty, value);
			}
		}

		public global::Avalonia.Media.IImage Icon
		{
			get
			{
				return (global::Avalonia.Media.IImage)GetValue(IconProperty);
			}
			set
			{
				SetValue(IconProperty, value);
			}
		}

		public PlaceholderTextBox()
		{
			// WPF TextBox.Text defaults to an empty string; keep that contract for migrated callers.
			if (base.Text == null)
			{
				base.Text = string.Empty;
			}
			// Migration note（2026-09-07 v3.14 修复，"很多 TextBox 左侧大留空"）：WPF 原模板有
			// <Trigger Property="Icon"><x:Null/> → IconElement.Visibility=Collapsed>——无图标时
			// Auto 列折叠为 0 宽。迁移版该 Trigger 因"无选择器等价物"被丢弃：空 Image（14x14 +
			// Margin 4）恒占位 → 128 处无 Icon 的输入框（AI 偏好"推理服务 URL"等）左侧凭空多出
			// 18px 留白。Avalonia 选择器不支持"属性=null"匹配——改为伪类方案：Icon 变化时维护
			// :noicon 伪类，主题里 ^:noicon /template/ Image#IconElement → IsVisible=False
			//（不可见元素不参与布局度量，Auto 列折叠为 0，与 WPF Collapsed 同效）。
			PseudoClasses.Set(":noicon", Icon is null);
			IconProperty.Changed.AddClassHandler<PlaceholderTextBox>(OnIconChanged);
			base.Loaded += delegate
			{
				base.ContextMenu = GetContextMenu();
			};
			// Preserve the WPF-style override points used by derived text boxes.
			TextChanged += delegate (object s, global::Avalonia.Controls.TextChangedEventArgs e)
			{
				OnTextChanged(e);
			};
			PropertyChanged += delegate (object s, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
			{
				if (e.Property == global::Avalonia.Input.InputElement.IsKeyboardFocusWithinProperty)
				{
					OnIsKeyboardFocusWithinChanged(e);
				}
			};
		}

		/// <summary>Icon 属性变化时同步 :noicon 伪类（主题据此折叠 IconElement，见构造函数注释）。</summary>
		private static void OnIconChanged(PlaceholderTextBox owner, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			owner.PseudoClasses.Set(":noicon", e.NewValue is null);
		}

		protected virtual void OnTextChanged(global::Avalonia.Controls.TextChangedEventArgs e)
		{
		}

		protected virtual void OnIsKeyboardFocusWithinChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
		}

		/// <summary>WPF TextBoxBase.SelectionLength（Avalonia 无，由 SelectionStart/SelectionEnd 推导）。</summary>
		public int SelectionLength => global::System.Math.Abs(SelectionEnd - SelectionStart);

		/// <summary>WPF TextBoxBase.IsSelectionActive（Avalonia 无，近似映射 IsFocused）。</summary>
		public bool IsSelectionActive => IsFocused;

		/// <summary>WPF UIElement.IsKeyboardFocused（Avalonia 无，近似映射 IsFocused）。</summary>
		public bool IsKeyboardFocused => IsFocused;

		protected virtual ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.AddDefaultTextBoxMenuItems(this);
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, this);
			return contextMenu;
		}
	}
}
