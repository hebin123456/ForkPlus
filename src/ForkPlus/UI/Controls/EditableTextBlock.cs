using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	// Migration note：WPF Control 自带 Padding/FontSize/FontWeight/Foreground 等属性并支持 ControlTheme 的
	// Template Setter；Avalonia 把这些属性下放到 TemplatedControl，故基类由 Control 改为 TemplatedControl
	//（Commonresources.axaml 的 ControlTheme 正是给本控件设置 Template 并 TemplateBinding Padding）。
	public class EditableTextBlock : TemplatedControl
	{
		public static readonly global::Avalonia.StyledProperty<string> ValueProperty =
    global::Avalonia.AvaloniaProperty.Register<EditableTextBlock, string>("Value", null);

		public static readonly global::Avalonia.StyledProperty<bool> IsInEditModeProperty =
    global::Avalonia.AvaloniaProperty.Register<EditableTextBlock, bool>("IsInEditMode", false);

		protected CustomAdorner _adorner;

		public string Value
		{
			get
			{
				return (string)GetValue(ValueProperty);
			}
			set
			{
				SetValue(ValueProperty, value);
			}
		}

		public bool IsInEditMode
		{
			get
			{
				return (bool)GetValue(IsInEditModeProperty);
			}
			set
			{
				SetValue(IsInEditModeProperty, value);
			}
		}

		public void ShowEditor(string text, Action<bool, string> editedCallback, bool centeredHorizontally = false)
		{
			if (_adorner != null)
			{
				HideEditor();
			}
			// Migration note：AdornerLayer 与 Avalonia.Controls.Primitives.AdornerLayer 二义性，显式用 WpfCompat 版本。
			global::ForkPlus.UI.WpfCompat.AdornerLayer adornerLayer = global::ForkPlus.UI.WpfCompat.AdornerLayer.GetAdornerLayer(this);
			if (adornerLayer == null)
			{
				SetCurrentValue(IsInEditModeProperty, false);
				return;
			}
			_adorner = new CustomAdorner(this, centeredHorizontally);
			_adorner.HorizontalAlignment = base.HorizontalAlignment;
			_adorner.VerticalAlignment = base.VerticalAlignment;
			_adorner.Child = CreateAdornerTextBox(text, editedCallback);
			adornerLayer.Add(_adorner);
			IsInEditMode = true;
		}

		public void HideEditor()
		{
			if (_adorner != null)
			{
				_adorner.Child = null;
				global::ForkPlus.UI.WpfCompat.AdornerLayer.GetAdornerLayer(this)?.Remove(_adorner);
				_adorner = null;
				IsInEditMode = false;
			}
		}

		private TextBox CreateAdornerTextBox(string text, Action<bool, string> editedCallback)
		{
			TextBox textBox = new TextBox();
			bool isFinished = false;
			void Finish(bool success)
			{
				if (isFinished)
				{
					return;
				}
				isFinished = true;
				string newText = textBox.Text;
				HideEditor();
				editedCallback(success, newText);
			}
			textBox.HorizontalAlignment = base.HorizontalAlignment;
			textBox.VerticalAlignment = base.VerticalAlignment;
			textBox.MaxWidth = base.MaxWidth;
			textBox.Height = base.Height;
			// Migration note（2026-09-04，"TextBox 里面的字和左/上边框贴得太紧"，同 Textbox.axaml）：
			// base.Padding 未显式设置时为 0；无条件赋值会以局部值 0 覆盖 TextBox 主题默认
			// Padding="2,1"（主题经 Margin="{TemplateBinding Padding}" 下传 TextPresenter），
			// 所有重命名编辑框（仓库管理/侧栏/标签页/子模块）文字重新 0px 贴边框。
			// 仅在调用方显式给了非零 Padding 时才下传。
			if (base.Padding != default(global::Avalonia.Thickness))
			{
				textBox.Padding = base.Padding;
			}
			textBox.Margin = new Thickness(-3.0, 1.0, 0.0, 0.0);
			textBox.FontSize = base.FontSize;
			textBox.Background = global::ForkPlus.UI.Theme.BackgroundBrush;
			textBox.Foreground = global::ForkPlus.UI.Theme.LabelBrush;
			textBox.BorderBrush = global::ForkPlus.UI.Theme.SystemAccentBrush;
			textBox.BorderThickness = new Thickness(1.0);
			textBox.Text = text;
			textBox.SelectAll();
			textBox.LayoutUpdated += delegate
			{
				textBox.Focus();
			};
			textBox.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Escape)
				{
					e.Handled = true;
					Finish(success: false);
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			textBox.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.Enter)
				{
					e.Handled = true;
					Finish(success: true);
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			// Migration note：Avalonia TextBox 无 LostKeyboardFocus（WPF 键盘焦点事件），等价用 LostFocus。
			textBox.LostFocus += delegate
			{
				if (IsInEditMode)
				{
					Finish(success: true);
				}
			};
			return textBox;
		}
	}
}
