using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF AdornerLayer + Adorner → Avalonia 无内置装饰层。
	// 策略：将编辑 TextBox 添加到父 Panel.Children 作为普通控件叠加显示。
	// 原 WPF AdornerLayer.GetAdornerLayer(this) 查找装饰层
	// → 通过 this.GetVisualAncestor<Panel>() 查找父容器。
	// 原 adornerLayer.Add/Remove(_adorner) → panel.Children.Add/Remove(_adorner)。
	// WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubbling)。
	// WPF LostKeyboardFocus → Avalonia LostFocus。
	// TODO(4.5-p): 父 Panel 叠加方案可能影响布局；如需绝对定位，
	// 阶段 6 可改用 Avalonia OverlayLayer 或 Popup 实现。
	public class EditableTextBlock : Control
	{
		// 阶段 4.5：WPF DependencyProperty.Register + FrameworkPropertyMetadata → Avalonia StyledProperty.Register。
		public static readonly StyledProperty<string> ValueProperty =
			AvaloniaProperty.Register<EditableTextBlock, string>(nameof(Value));

		public static readonly StyledProperty<bool> IsInEditModeProperty =
			AvaloniaProperty.Register<EditableTextBlock, bool>(nameof(IsInEditMode));

		protected CustomAdorner _adorner;

		public string Value
		{
			get => GetValue(ValueProperty);
			set => SetValue(ValueProperty, value);
		}

		public bool IsInEditMode
		{
			get => GetValue(IsInEditModeProperty);
			set => SetValue(IsInEditModeProperty, value);
		}

		public void ShowEditor(string text, Action<bool, string> editedCallback, bool centeredHorizontally = false)
		{
			if (_adorner != null)
			{
				HideEditor();
			}
			// 阶段 4.5：WPF AdornerLayer.GetAdornerLayer(this) → 查找父 Panel。
			Panel parentPanel = this.GetVisualAncestor<Panel>();
			if (parentPanel == null)
			{
				return;
			}
			_adorner = new CustomAdorner(this, centeredHorizontally);
			_adorner.HorizontalAlignment = base.HorizontalAlignment;
			_adorner.VerticalAlignment = base.VerticalAlignment;
			_adorner.Content = CreateAdornerTextBox(text, editedCallback);
			parentPanel.Children.Add(_adorner);
			IsInEditMode = true;
		}

		public void HideEditor()
		{
			if (_adorner != null)
			{
				_adorner.Content = null;
				// 阶段 4.5：从父 Panel 移除（原 WPF AdornerLayer.Remove）。
				Panel parentPanel = _adorner.GetVisualAncestor<Panel>();
				parentPanel?.Children.Remove(_adorner);
				_adorner = null;
				IsInEditMode = false;
			}
		}

		private TextBox CreateAdornerTextBox(string text, Action<bool, string> editedCallback)
		{
			TextBox textBox = new TextBox();
			textBox.HorizontalAlignment = base.HorizontalAlignment;
			textBox.VerticalAlignment = base.VerticalAlignment;
			textBox.MaxWidth = base.MaxWidth;
			textBox.Height = base.Height;
			textBox.Padding = base.Padding;
			textBox.Margin = new Thickness(-3.0, 1.0, 0.0, 0.0);
			textBox.FontSize = base.FontSize;
			textBox.Text = text;
			textBox.SelectAll();
			textBox.LayoutUpdated += delegate
			{
				textBox.Focus();
			};
			// 阶段 4.5：WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubbling)。
			textBox.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					e.Handled = true;
					editedCallback(arg1: true, textBox.Text);
				}
				else if (e.Key == Key.Escape)
				{
					e.Handled = true;
					editedCallback(arg1: false, textBox.Text);
				}
			};
			// 阶段 4.5：WPF LostKeyboardFocus → Avalonia LostFocus。
			textBox.LostFocus += delegate
			{
				if (IsInEditMode)
				{
					editedCallback(arg1: true, textBox.Text);
				}
			};
			return textBox;
		}
	}
}
