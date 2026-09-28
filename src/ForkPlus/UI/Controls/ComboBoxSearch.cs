using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 分支下拉框搜索过滤附加行为（2026-09-28，"Push/Pull/Track 等分支下拉框分支太多找不到"）：
	/// 在 XAML 上给 ComboBox 设 controls:ComboBoxSearch.IsEnabled="True" 即启用——
	/// ControlTheme（Theme/Styles/Combobox.axaml）据 .searchable 类把弹层顶部的
	/// PART_SearchBox 显示出来；本行为负责：
	///   ① 下拉打开时缓存当前 ItemsSource 快照、清空搜索词并把焦点给搜索框；
	///   ② 输入时按项文本（Name/ShortName/Title/FullReference 属性或字符串本体，
	///      OrdinalIgnoreCase 包含匹配）把 ItemsSource 临时替换为过滤子集——
	///      文本为空的项（分隔符）在过滤时一律隐藏；
	///   ③ 关闭下拉/清空搜索词时原样恢复 ItemsSource 与选中项，对外部（对话框
	///      code-behind）换 ItemsSource 的情况同步刷新缓存，保证 Push 窗口
	///      "Custom..." 交互、Pull 窗口异步刷新等既有流程不被破坏。
	/// 键盘：↑/↓ 在当前可见项里移动选中，Enter 选中首项/当前项并关闭，Escape
	/// 先清空搜索词、再关闭下拉。
	/// </summary>
	// 注意：不能是 static class——RegisterAttached<ComboBoxSearch, ...> 要拿本类做类型参数
	//（C# 静态类不可作类型参数，CS0718），同 SpinnerBehavior 的写法：非静态类 + 全静态成员。
	public class ComboBoxSearch
	{
		// Migration note：静态类不能作泛型类型参数（CS0718），用 (name, Type ownerType) 重载
		//（Avalonia 附加属性静态 owner 的标准写法，同 AvaloniaProperty.RegisterAttached<THost, TValue>）。
		public static readonly AttachedProperty<bool> IsEnabledProperty =
			AvaloniaProperty.RegisterAttached<ComboBox, bool>("IsEnabled", typeof(ComboBoxSearch));

		private const string SearchBoxPartName = "PART_SearchBox";
		private const string SearchableClass = "searchable";

		// 项文本提取器按类型缓存（分支下拉框项型固定：LocalBranch/RemoteBranch/
		// RemoteBranchItem/DropdownItem/...），避免每项每字符反射。
		private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TextPropertiesCache = new();

		private static readonly ConditionalWeakTable<ComboBox, SearchState> States = new ConditionalWeakTable<ComboBox, SearchState>();

		static ComboBoxSearch()
		{
			// 泛型 AddClassHandler 须显式给 TValue（单泛型重载是非泛型 AvaloniaPropertyChangedEventArgs 版本）。
			IsEnabledProperty.Changed.AddClassHandler<ComboBox, bool>(OnIsEnabledChanged);
		}

		public static bool GetIsEnabled(ComboBox obj)
		{
			return obj.GetValue(IsEnabledProperty);
		}

		public static void SetIsEnabled(ComboBox obj, bool value)
		{
			obj.SetValue(IsEnabledProperty, value);
		}

		private static void OnIsEnabledChanged(ComboBox comboBox, AvaloniaPropertyChangedEventArgs<bool> e)
		{
			if (e.NewValue.Value)
			{
				comboBox.Classes.Add(SearchableClass);
				comboBox.TemplateApplied += ComboBox_TemplateApplied;
				comboBox.PropertyChanged += ComboBox_PropertyChanged;
				comboBox.SelectionChanged += ComboBox_SelectionChanged;
				States.GetOrCreateValue(comboBox);
			}
			else
			{
				comboBox.Classes.Remove(SearchableClass);
				comboBox.TemplateApplied -= ComboBox_TemplateApplied;
				comboBox.PropertyChanged -= ComboBox_PropertyChanged;
				comboBox.SelectionChanged -= ComboBox_SelectionChanged;
				if (States.TryGetValue(comboBox, out SearchState state))
				{
					RestoreOriginal(comboBox, state);
					DetachSearchBox(state);
				}
			}
		}

		private static void ComboBox_TemplateApplied(object sender, TemplateAppliedEventArgs e)
		{
			ComboBox comboBox = (ComboBox)sender;
			if (!States.TryGetValue(comboBox, out SearchState state))
			{
				return;
			}
			// 模板重应用（切主题等）会重建搜索框，先解绑旧的再接新的。
			DetachSearchBox(state);
			if (e.NameScope.Find(SearchBoxPartName) is PlaceholderTextBox searchBox)
			{
				state.SearchBox = searchBox;
				searchBox.TextChanged += SearchBox_TextChanged;
				searchBox.KeyDown += SearchBox_KeyDown;
				searchBox.Placeholder = PreferencesLocalization.Current("Search");
			}
		}

		private static void DetachSearchBox(SearchState state)
		{
			if (state.SearchBox != null)
			{
				state.SearchBox.TextChanged -= SearchBox_TextChanged;
				state.SearchBox.KeyDown -= SearchBox_KeyDown;
				state.SearchBox = null;
			}
		}

		private static void ComboBox_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
		{
			ComboBox comboBox = (ComboBox)sender;
			if (!States.TryGetValue(comboBox, out SearchState state))
			{
				return;
			}
			if (e.Property == ComboBox.IsDropDownOpenProperty)
			{
				if (comboBox.IsDropDownOpen)
				{
					BeginSession(comboBox, state);
				}
				else
				{
					EndSession(comboBox, state);
				}
			}
			else if (e.Property == ItemsControl.ItemsSourceProperty && !state.InternalChange)
			{
				// 外部（对话框 code-behind）换掉了 ItemsSource：刷新快照；若弹层正开着
				// 且有搜索词，则基于新列表重新过滤。快照必须一并作废，否则继续滤旧列表。
				state.OriginalSource = comboBox.ItemsSource;
				state.OriginalItems = null;
				if (state.Filtering)
				{
					state.Filtering = false;
					if (comboBox.IsDropDownOpen && state.SearchBox != null && state.SearchBox.Text.Length > 0)
					{
						ApplyFilter(comboBox, state, state.SearchBox.Text);
					}
				}
			}
		}

		private static void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox comboBox = (ComboBox)sender;
			if (States.TryGetValue(comboBox, out SearchState state) && state.Filtering && e.AddedItems != null && e.AddedItems.Count > 0)
			{
				// 过滤态下用户在子集里选中了项 → 记住，关闭时还原到完整列表仍保持该选中。
				state.RememberedSelection = e.AddedItems[0];
			}
		}

		private static void BeginSession(ComboBox comboBox, SearchState state)
		{
			state.OriginalSource = comboBox.ItemsSource;
			state.OriginalItems = null;
			state.Filtering = false;
			state.RememberedSelection = null;
			if (state.SearchBox != null)
			{
				if (state.SearchBox.Text.Length > 0)
				{
					state.ResettingText = true;
					try
					{
						state.SearchBox.Text = "";
					}
					finally
					{
						state.ResettingText = false;
					}
				}
				// 弹层内容挂到 PopupRoot 需要一个布局周期，投递后再聚焦（同菜单搜索框
				// AttachSearchBoxFocusGuard 的投递模式，ComboBox 无菜单的焦点抢占问题）。
				Dispatcher.UIThread.Post(delegate
				{
					if (comboBox.IsDropDownOpen && state.SearchBox != null)
					{
						state.SearchBox.Focus();
						state.SearchBox.SelectAll();
					}
				}, DispatcherPriority.Input);
			}
		}

		private static void EndSession(ComboBox comboBox, SearchState state)
		{
			if (state.Filtering)
			{
				RestoreOriginal(comboBox, state);
			}
			if (state.SearchBox != null && state.SearchBox.Text.Length > 0)
			{
				state.ResettingText = true;
				try
				{
					state.SearchBox.Text = "";
				}
				finally
				{
					state.ResettingText = false;
				}
			}
		}

		private static void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			PlaceholderTextBox searchBox = (PlaceholderTextBox)sender;
			ComboBox comboBox = FindOwnerComboBox(searchBox);
			if (comboBox == null || !States.TryGetValue(comboBox, out SearchState state) || state.ResettingText)
			{
				return;
			}
			ApplyFilter(comboBox, state, searchBox.Text);
		}

		private static void SearchBox_KeyDown(object sender, KeyEventArgs e)
		{
			PlaceholderTextBox searchBox = (PlaceholderTextBox)sender;
			ComboBox comboBox = FindOwnerComboBox(searchBox);
			if (comboBox == null || !States.TryGetValue(comboBox, out SearchState state))
			{
				return;
			}
			if (e.Key == Key.Escape)
			{
				if (searchBox.Text.Length > 0)
				{
					searchBox.Text = "";
				}
				else
				{
					comboBox.IsDropDownOpen = false;
				}
				e.Handled = true;
			}
			else if (e.Key == Key.Enter || e.Key == Key.Return)
			{
				object[] enterItems = comboBox.Items.Cast<object>().ToArray();
				if (comboBox.SelectedItem == null && enterItems.Length != 0)
				{
					comboBox.SelectedItem = enterItems[0];
				}
				comboBox.IsDropDownOpen = false;
				e.Handled = true;
			}
			else if (e.Key == Key.Down || e.Key == Key.Up)
			{
				object[] array = comboBox.Items.Cast<object>().ToArray();
				if (array.Length != 0)
				{
					int num = Array.IndexOf(array, comboBox.SelectedItem);
					int num2 = ((e.Key == Key.Down) ? Math.Min(num + 1, array.Length - 1) : Math.Max(num - 1, 0));
					comboBox.SelectedItem = array[num2];
					// 改 SelectedItem 会触发 ComboBox.TryFocusSelectedItem 把焦点塞给选中项容器，
					// 投递一个更低优先级的焦点守护把焦点还给搜索框（2026-09-28 焦点丢失修复）。
					RefocusSearchBox(comboBox, state);
				}
				e.Handled = true;
			}
		}

		/// <summary>弹层内容挂在 PopupRoot（独立窗口）下，视觉祖先链不通——沿逻辑祖先链
		///（模板部件 → Popup → ComboBox）找宿主 ComboBox，视觉链仅作兜底。</summary>
		private static ComboBox FindOwnerComboBox(Visual visual)
		{
			return visual.GetLogicalAncestors().OfType<ComboBox>().FirstOrDefault() ?? visual.GetVisualAncestors().OfType<ComboBox>().FirstOrDefault();
		}

		private static void ApplyFilter(ComboBox comboBox, SearchState state, string text)
		{
			if (!comboBox.IsDropDownOpen)
			{
				return;
			}
			if (state.OriginalSource == null)
			{
				// 仅支持 ItemsSource 填充的下拉框（分支下拉框全部如此）；直接 Items 的不管。
				return;
			}
			if (string.IsNullOrEmpty(text))
			{
				if (state.Filtering)
				{
					RestoreOriginal(comboBox, state);
					// 还原时回填 SelectedItem 同样会触发 TryFocusSelectedItem 抢焦点。
					RefocusSearchBox(comboBox, state);
				}
				return;
			}
			state.OriginalItems ??= state.OriginalSource.Cast<object>().ToList();
			if (!state.Filtering)
			{
				state.RememberedSelection = comboBox.SelectedItem;
			}
			// 换源前先显式取消选中（2026-09-28 残影修复）：选中容器在物化时会把
			// TabOnceActiveElement 指到它身上且弹层内 GotFocus 不会路由回 ComboBox、
			// 搜索框聚焦清不掉；VSP 回收"TabOnce 容器"时有保持挂载不摘除的路径
			//（VirtualizingStackPanel.RecycleElement），旧容器残影叠在搜索结果上。
			// 先取消选中让 AnchorIndex 归 -1 → SelectingItemsControl 顺带把
			// TabOnceActiveElement 清空，旧容器走正常回收路径被干净摘除。
			if (comboBox.SelectedItem != null)
			{
				comboBox.SelectedItem = null;
			}
			List<object> list = new List<object>();
			foreach (object item in state.OriginalItems)
			{
				if (Matches(item, text))
				{
					list.Add(item);
				}
			}
			state.InternalChange = true;
			try
			{
				comboBox.ItemsSource = list;
			}
			finally
			{
				state.InternalChange = false;
			}
			state.Filtering = true;
			// 换 ItemsSource 后选中项容器重建/回填选中项都可能伴随焦点抢占，
			// 投递焦点守护兜底（Background 晚于布局/渲染，能盖过容器物化期的抢占）。
			RefocusSearchBox(comboBox, state);
		}

		/// <summary>焦点守护（2026-09-28 焦点丢失修复）：过滤/键导航改了 SelectedItem 或
		/// ItemsSource 后，ComboBox 会调 TryFocusSelectedItem 把焦点塞给选中项容器，导致
		/// 搜索框无法连续输入。这里用 Background 优先级（晚于 Render/布局）投递一次还焦：
		/// 仅当下拉仍开着且焦点不在搜索框内时才 Focus（不 SelectAll，保住光标位置）；
		/// 若用户已用鼠标点进列表项等主动移走焦点，则不干预。</summary>
		private static void RefocusSearchBox(ComboBox comboBox, SearchState state)
		{
			Dispatcher.UIThread.Post(delegate
			{
				if (comboBox.IsDropDownOpen && state.SearchBox != null && !state.SearchBox.IsKeyboardFocusWithin)
				{
					state.SearchBox.Focus();
				}
			}, DispatcherPriority.Background);
		}

		private static void RestoreOriginal(ComboBox comboBox, SearchState state)
		{
			if (!state.Filtering)
			{
				return;
			}
			state.InternalChange = true;
			try
			{
				comboBox.ItemsSource = state.OriginalSource;
			}
			finally
			{
				state.InternalChange = false;
			}
			if (state.RememberedSelection != null && comboBox.SelectedItem != state.RememberedSelection)
			{
				comboBox.SelectedItem = state.RememberedSelection;
			}
			state.Filtering = false;
			state.RememberedSelection = null;
			state.OriginalItems = null;
		}

		private static bool Matches(object item, string text)
		{
			if (item == null)
			{
				return false;
			}
			foreach (string candidateText in GetCandidateTexts(item))
			{
				if (candidateText != null && candidateText.Length != 0 && candidateText.IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1)
				{
					return true;
				}
			}
			return false;
		}

		private static IEnumerable<string> GetCandidateTexts(object item)
		{
			if (item is string text)
			{
				yield return text;
				yield break;
			}
			Type type = item.GetType();
			PropertyInfo[] array = TextPropertiesCache.GetOrAdd(type, CreateTextPropertyExtractors);
			for (int i = 0; i < array.Length; i++)
			{
				yield return (string)array[i].GetValue(item);
			}
		}

		private static PropertyInfo[] CreateTextPropertyExtractors(Type type)
		{
			string[] array = new string[4] { "ShortName", "Name", "Title", "FullReference" };
			List<PropertyInfo> list = new List<PropertyInfo>(array.Length);
			string[] array2 = array;
			foreach (string name in array2)
			{
				PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
				if (property != null && property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
				{
					list.Add(property);
				}
			}
			return list.ToArray();
		}

		/// <summary>单个下拉框的过滤会话状态（弹层打开期间有效）。</summary>
		private sealed class SearchState
		{
			public PlaceholderTextBox SearchBox;
			public IEnumerable OriginalSource;
			public List<object> OriginalItems;
			public object RememberedSelection;
			public bool Filtering;
			public bool InternalChange;
			public bool ResettingText;
		}
	}
}
