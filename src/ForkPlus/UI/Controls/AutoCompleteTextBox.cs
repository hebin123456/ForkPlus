using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;

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
			// 阶段 4.5：WPF OnIsKeyboardFocusWithinChanged → Avalonia LostFocus 事件。
			// Avalonia 没有 IsKeyboardFocusWithinChanged；用 LostFocus 近似替代。
			LostFocus += delegate
			{
				ClosePopup();
			};
		}

		public void SetAutocompleteProvider(IAutoCompleteProvider autoCompleteProvider)
		{
			_autoCompleteProvider = autoCompleteProvider;
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			if (GetTemplateChild("Popup") is Popup popup)
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

		// 阶段 4.5：WPF OnPreviewKeyDown (tunneling) → Avalonia OnKeyDown (bubbling)。
		// 通过 e.Handled = true 阻止后续处理，达到 Preview 的效果。
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (_popup?.IsOpen == true)
				{
					ClosePopup();
					e.Handled = true;
					return;
				}
			}
			else if (e.Key == Key.Return || e.Key == Key.Tab)
			{
				if (_popup?.IsOpen == true)
				{
					SubmitSelectedSuggestion(e.Key == Key.Tab);
					e.Handled = true;
					return;
				}
			}
			else if (e.Key == Key.Down && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
			{
				if (_listBox != null)
				{
					_listBox.SelectNextRow(_listBox.SelectedIndex, loop: true);
					e.Handled = true;
					return;
				}
			}
			else if (e.Key == Key.Up && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _listBox != null)
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
				// 阶段 4.5：WPF Application.Current.TryFindResource → Theme.FindStyle/FindResource 门面。
				_listBox.Styles.Add(Theme.FindStyle("AutoCompleteListBoxStyle"));
				_listBox.ItemTemplate = Theme.FindResource("AutocompleteListBoxItemTemplate") as IDataTemplate;
				_listBox.MinWidth = 216.0;
				// 阶段 4.5：WPF MouseUp → Avalonia PointerReleased。
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
			// 阶段 4.5：WPF TextBox.GetRectFromCharacterIndex → Avalonia TextBox.GetCursorBounds。
			Rect cursorBounds = base.GetCursorBounds(autoComplete.DropdownPosition);
			int num = 8;
			// 阶段 4.5：WPF Popup.PlacementRectangle 不存在于 Avalonia；
			// 通过 HorizontalOffset/VerticalOffset + PlacementTarget 实现相对定位。
			_popup.HorizontalOffset = cursorBounds.X - (double)num;
			_popup.VerticalOffset = cursorBounds.Y;
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
	}
}
