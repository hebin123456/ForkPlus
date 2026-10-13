using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

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

		private readonly List<IAutoCompleteProvider> _autoCompleteProviders = new List<IAutoCompleteProvider>();

		private readonly DelayedAction<bool> _refreshSuggestions;

		public bool DisableUpdates { get; set; }

		public AutoCompleteTextBox()
		{
			_refreshSuggestions = new DelayedAction<bool>(RefreshSuggestions, 0.03);
		}

		/// <summary>设置补全提供器（替换已有的全部提供器）。</summary>
		public void SetAutocompleteProvider(IAutoCompleteProvider autoCompleteProvider)
		{
			_autoCompleteProviders.Clear();
			AddAutocompleteProvider(autoCompleteProvider);
		}

		/// <summary>追加一个补全提供器：按注册顺序依次求值，取第一个非空结果。</summary>
		public void AddAutocompleteProvider([Null] IAutoCompleteProvider autoCompleteProvider)
		{
			if (autoCompleteProvider != null)
			{
				_autoCompleteProviders.Add(autoCompleteProvider);
			}
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
			AutoCompleteSuggestions autoCompleteSuggestions = null;
			foreach (IAutoCompleteProvider autoCompleteProvider in _autoCompleteProviders)
			{
				autoCompleteSuggestions = autoCompleteProvider.GetSuggestions(base.Text, base.CaretIndex);
				if (autoCompleteSuggestions != null && autoCompleteSuggestions.Suggestions.Length != 0)
				{
					break;
				}
				autoCompleteSuggestions = null;
			}
			if (autoCompleteSuggestions != null)
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
				// v5.1.3 修复：鼠标点击建议项无效（只有键盘 ↑↓ + 回车/Tab 生效）。
				// 真实点击序中按下会让键盘焦点离开文本框 → OnIsKeyboardFocusWithinChanged
				// 触发 FocusChanged(false) → ClosePopup() 在 PointerReleased 之前就拆掉浮层
				// （_listBox 置空、Popup.Child 摘除），释放事件再也回不到列表，点击落空。
				// 改为在 PointerPressed 阶段处理：真实输入下隧道路由段早于 ListBoxItem 抢焦点，
				// 直接把命中的建议应用掉并置 Handled，浮层由 SubmitSuggestion 自行关闭。
				// 同时注册 Tunnel|Bubble 两段：headless 的合成点击（直接 RaiseEvent）不经
				// 输入管线的隧道路由，只有冒泡段能命中，两段一起注册保证两种路径都生效，
				// 重复命中由 ListBox_PointerPressed 开头的幂等守卫拦掉。
				_listBox.AddHandler(InputElement.PointerPressedEvent, ListBox_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
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
			AutoCompleteSuggestion autoCompleteSuggestion = ((_listBox != null) ? (_listBox.SelectedItem as AutoCompleteSuggestion) : null);
			if (autoCompleteSuggestion == null && fallbackToFirst && _listBox != null)
			{
				autoCompleteSuggestion = _listBox.Items.FirstItem<AutoCompleteSuggestion>();
			}
			SubmitSuggestion(autoCompleteSuggestion);
		}

		/// <summary>鼠标左键按下：解析被点中的建议项并应用。</summary>
		private void ListBox_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			// 幂等守卫：本处理器同时注册在 Tunnel 与 Bubble 两段路由（真实输入下两段都会
			// 命中同一个 ListBox）。第一段应用建议后浮层已关闭（_popup.IsOpen=false、
			// _listBox=null），第二段据此直接返回，避免用旧 Range 对已替换文本重复套用。
			if (_popup == null || !_popup.IsOpen || _listBox == null)
			{
				return;
			}
			if (!e.GetCurrentPoint(_listBox).Properties.IsLeftButtonPressed)
			{
				return;
			}
			AutoCompleteSuggestion autoCompleteSuggestion = null;
			if (e.Source is Visual source)
			{
				autoCompleteSuggestion = source.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext as AutoCompleteSuggestion;
			}
			if (autoCompleteSuggestion == null)
			{
				autoCompleteSuggestion = _listBox.SelectedItem as AutoCompleteSuggestion;
			}
			if (autoCompleteSuggestion == null)
			{
				return;
			}
			e.Handled = true;
			SubmitSuggestion(autoCompleteSuggestion);
		}

		private void SubmitSuggestion([Null] AutoCompleteSuggestion autoCompleteSuggestion)
		{
			if (autoCompleteSuggestion == null)
			{
				return;
			}
			base.Text = base.Text.Replace(autoCompleteSuggestion.Range, autoCompleteSuggestion.Suggestion);
			base.CaretIndex = autoCompleteSuggestion.Range.Start + autoCompleteSuggestion.Suggestion.Length;
			ClosePopup();
			Focus();
		}

		private void FocusChanged(bool hasFocus)
		{
			// 点击建议项时焦点会先落到浮层列表（ListBoxItem）上，此时不能关闭浮层，
			// 否则随后的 PointerPressed 处理拿不到 _listBox；只有焦点真正离开浮层才关闭。
			if (!hasFocus && (_listBox == null || !_listBox.IsKeyboardFocusWithin))
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
