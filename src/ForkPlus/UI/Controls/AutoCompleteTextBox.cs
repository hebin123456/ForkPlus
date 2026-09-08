using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class AutoCompleteTextBox : PlaceholderTextBox
	{
		private const string ElementPopup = "Popup";

		private const int ListBoxMargins = 16;

		private const int ListBoxPaddings = 8;

		[Null]
		private Popup _popup;

		[Null]
		private ListBox _listBox;

		[Null]
		private IAutoCompleteProvider _autoCompleteProvider;

		private readonly DelayedAction<bool> _refreshSuggestions;

		public bool DisableUpdates { get; set; }

		public AutoCompleteTextBox()
		{
			_refreshSuggestions = new DelayedAction<bool>(RefreshSuggestions, 0.03);
		}

		public void SetAutocompleteProvider(IAutoCompleteProvider autoCompleteProvider)
		{
			_autoCompleteProvider = autoCompleteProvider;
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (this.GetTemplateChild("Popup") is Popup popup)
			{
				_popup = popup;
				_popup.PlacementTarget = this;
			}
		}

		protected override void OnTextChanged(TextChangedEventArgs e)
		{
			base.OnTextChanged(e);
			if (!DisableUpdates)
			{
				_refreshSuggestions.InvokeWithDelay(parameter: true);
			}
		}

		protected override void OnIsKeyboardFocusWithinChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnIsKeyboardFocusWithinChanged(e);
			FocusChanged((bool)e.NewValue);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (_popup.IsOpen)
				{
					ClosePopup();
					e.Handled = true;
					return;
				}
			}
			else if (e.Key == Key.Return || e.Key == Key.Tab)
			{
				if (_popup.IsOpen)
				{
					SubmitSelectedSuggestion(e.Key == Key.Tab);
					e.Handled = true;
					return;
				}
			}
			else if (e.Key == Key.Down && !Keyboard.IsKeyDown(Key.LeftShift))
			{
				if (_listBox != null)
				{
					_listBox.SelectNextRow(_listBox.SelectedIndex, loop: true);
					e.Handled = true;
					return;
				}
			}
			else if (e.Key == Key.Up && !Keyboard.IsKeyDown(Key.LeftShift) && _listBox != null)
			{
				_listBox.SelectPreviousRow(_listBox.SelectedIndex, loop: true);
				e.Handled = true;
				return;
			}
			base.OnKeyDown(e);
		}

		private void RefreshSuggestions(bool _)
		{
			AutoCompleteSuggestions autoCompleteSuggestions = _autoCompleteProvider?.GetSuggestions(base.Text, base.CaretIndex);
			if (autoCompleteSuggestions != null && autoCompleteSuggestions.Suggestions.Length != 0)
			{
				OpenPopup(autoCompleteSuggestions);
			}
			else
			{
				ClosePopup();
			}
		}

		private void OpenPopup(AutoCompleteSuggestions autoComplete)
		{
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode() || _popup == null || Application.Current == null)
			{
				return;
			}
			if (_listBox == null)
			{
				_listBox = new ListBox();
{				global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(_listBox, Application.Current.TryFindResource("AutoCompleteListBoxStyle"));
}				_listBox.ItemTemplate = Application.Current.TryFindResource("AutocompleteListBoxItemTemplate") as global::Avalonia.Controls.Templates.IDataTemplate; // Migration note：WPF DataTemplate → Avalonia IDataTemplate
				_listBox.MinWidth = 216.0;
				_listBox.PointerReleased += delegate
				{
					SubmitSelectedSuggestion();
				};
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, _listBox, GetType().Name + ".Popup");
			}
			_listBox.Height = (double)Math.Min(autoComplete.Suggestions.Length, 5) * 21.0 + 8.0 + 16.0;
			_listBox.Items.Clear();
			AutoCompleteSuggestion[] suggestions = autoComplete.Suggestions;
			foreach (AutoCompleteSuggestion newItem in suggestions)
			{
				_listBox.Items.Add(newItem);
			}
			// Migration note：WPF TextBox.GetRectFromCharacterIndex（第 N 个字符的客户区矩形）Avalonia 无对应，
			// 近似取本控件 Bounds 左上角，下拉框出现在文本框下方。
			Rect rectFromCharacterIndex = new Rect(0.0, Bounds.Height, 0.0, 0.0);
			int num = 8;
			// Migration note：WPF Popup.PlacementRectangle → Avalonia Popup.PlacementRect（可空 Rect）。
			_popup.PlacementRect = new Rect(new Point(rectFromCharacterIndex.X - (double)num, rectFromCharacterIndex.Y), rectFromCharacterIndex.Size);
			_popup.PlacementTarget = this;
			_popup.IsOpen = true;
		}

		private void ClosePopup()
		{
			if (_popup != null)
			{
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_listBox = null;
				_popup.IsOpen = false;
			}
		}

		private void SubmitSelectedSuggestion(bool fallbackToFirst = false)
		{
			AutoCompleteSuggestion autoCompleteSuggestion = _listBox.SelectedItem as AutoCompleteSuggestion;
			if (autoCompleteSuggestion == null && fallbackToFirst)
			{
				autoCompleteSuggestion = _listBox.Items.FirstItem<AutoCompleteSuggestion>();
			}
			if (autoCompleteSuggestion != null)
			{
				base.Text = base.Text.Replace(autoCompleteSuggestion.Range, autoCompleteSuggestion.Suggestion);
				base.CaretIndex = autoCompleteSuggestion.Range.Start + autoCompleteSuggestion.Suggestion.Length;
				ClosePopup();
				Focus();
			}
		}

		private void FocusChanged(bool hasFocus)
		{
			if (!hasFocus)
			{
				ClosePopup();
			}
		}

		private bool HasFocus()
		{
			return base.IsFocused;
		}
	}
}
