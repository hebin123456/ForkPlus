using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace ForkPlus.UI.Controls
{
	public class EditableTextBlock : Control
	{
		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(EditableTextBlock), new FrameworkPropertyMetadata(null));

		public static readonly DependencyProperty IsInEditModeProperty = DependencyProperty.Register("IsInEditMode", typeof(bool), typeof(EditableTextBlock), new FrameworkPropertyMetadata(false));

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
			AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
			if (adornerLayer == null)
			{
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
				AdornerLayer.GetAdornerLayer(this)?.Remove(_adorner);
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
			textBox.PreviewKeyDown += delegate(object s, KeyEventArgs e)
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
			textBox.LostKeyboardFocus += delegate
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
