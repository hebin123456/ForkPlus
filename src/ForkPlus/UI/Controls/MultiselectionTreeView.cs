using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.UI.Controls.Flattener;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class MultiselectionTreeView : global::Avalonia.Controls.ListBox
	{
		private class DropTarget
		{
			public TreeViewControlItem Item;

			public double Y;

			public MultiselectionTreeViewItem Node;

			public int Index;

			public DragDropEffects Effect;
		}

		private class UpdateLock : IDisposable
		{
			private MultiselectionTreeView _instance;

			private readonly IDisposable _flattenerDeferral;

			public UpdateLock(MultiselectionTreeView instance)
			{
				_instance = instance;
				_instance._updatesLocked = true;
				// 性能优化（大仓库侧栏批量插入）：同时抑制 flattener 的逐节点通知，
				// 批量结束（Dispose）时只发一次 Reset 刷新列表。
				_flattenerDeferral = _instance._flattener?.DeferNotifications();
			}

			public void Dispose()
			{
				_instance._updatesLocked = false;
				_flattenerDeferral?.Dispose();
			}
		}

		// Migration note：WPF 非 DependencyProperty 的 CLR 封装对应 Avalonia StyledProperty；
		// Avalonia 12 无 AvaloniaProperty.Register(string, Type, Type) 非泛型重载（CS0411），
		// 改用 Register<MultiselectionTreeView, MultiselectionTreeViewItem>("RootItem")。
		public static readonly global::Avalonia.StyledProperty<MultiselectionTreeViewItem> RootItemProperty;

		[Null]
		private ExpandedTreeViewElement[] _itemsToExpand;

		private string _filterString;

		private FlattenerNode.Flattener _flattener;

		private bool doNotScrollOnExpanding;

		private bool _updatesLocked;

		private TreeViewControlItem _previewNodeView;

		// 最近一次同步过 IsSelected 的选中节点快照（对象引用）。
		// 见 OnSelectionChanged 的 Migration note：Avalonia 12 的事件参数是按索引惰性解析的
		// 视图，源收缩后不可枚举，选中差集只能按引用维护。
		private readonly HashSet<MultiselectionTreeViewItem> _knownSelectedNodes = new HashSet<MultiselectionTreeViewItem>();

		public MultiselectionTreeViewItem RootItem
		{
			get
			{
				return (MultiselectionTreeViewItem)GetValue(RootItemProperty);
			}
			set
			{
				SetValue(RootItemProperty, value);
			}
		}

		public new IEnumerable ItemsSource
		{
			get
			{
				return base.ItemsSource;
			}
			set
			{
				throw new NotSupportedException("Use RootItem property instead");
			}
		}

		public bool RememberExpandedItems { get; set; }

		public bool AllowDragDrop { get; set; }

		public string FilterString
		{
			get
			{
				return _filterString;
			}
			set
			{
				if (_filterString != value)
				{
					bool num = RememberExpandedItems && string.IsNullOrEmpty(_filterString) && !string.IsNullOrEmpty(value);
					bool flag = RememberExpandedItems && !string.IsNullOrEmpty(_filterString) && string.IsNullOrEmpty(value);
					_filterString = value;
					if (num)
					{
						_itemsToExpand = this.GetExpandedItems();
					}
					Refilter();
					if (flag && _itemsToExpand != null)
					{
						RootItem.CollapseAllChildren();
						this.SetExpandedItems(_itemsToExpand);
						_itemsToExpand = null;
					}
					else
					{
						RootItem.ExpandAllChildren();
					}
				}
			}
		}

		public MultiselectionTreeViewItem LastClickedItem { get; internal set; }

		static MultiselectionTreeView()
		{
			// Migration note：WPF 用 AvaloniaProperty.Register(name, propertyType, ownerType) 非泛型重载；
			// Avalonia 12 只有 Register<TOwner, TValue> 泛型重载（CS0411），按泛型形式注册。
			RootItemProperty = global::Avalonia.AvaloniaProperty.Register<MultiselectionTreeView, MultiselectionTreeViewItem>("RootItem");
			// Migration note：WPF VirtualizingStackPanel.VirtualizationModeProperty.OverrideMetadata(..., VirtualizationMode.Recycling)
			// 让本控件容器回收复用；Avalonia 12 的 VirtualizingStackPanel 没有 VirtualizationMode 概念
			// （CS0117/CS0103/CS0305），其 ItemsPresenter 容器生成器默认即回收复用容器，故此行为降级为无操作。
		}

		public MultiselectionTreeView()
		{
			// Migration note：WPF ListBox 有 protected virtual OnSelectionChanged 虚方法可 override；
			// Avalonia 12 的 ListBox 没有该方法（CS0117），改为订阅 SelectionChanged 事件，
			// 保持"选中变化同步节点 IsSelected"的原语义（见 OnSelectionChanged）。
			SelectionChanged += MultiselectionTreeView_SelectionChanged;
		}

		private void MultiselectionTreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			OnSelectionChanged(e);
		}

		public void Refilter()
		{
			MultiselectionTreeViewItem rootItem = RootItem;
			if (rootItem == null)
			{
				return;
			}
			foreach (MultiselectionTreeViewItem child in rootItem.Children)
			{
				rootItem.ApplyFilterToChild(child, _filterString);
			}
		}

		public void Expand(MultiselectionTreeViewItem node, bool expandChildren)
		{
			node.IsExpanded = true;
			if (!expandChildren)
			{
				return;
			}
			foreach (MultiselectionTreeViewItem child in node.Children)
			{
				Expand(child, expandChildren: true);
			}
		}

		public void SelectAndFocus(MultiselectionTreeViewItem node)
		{
			base.SelectedItems.Add(node);
			if (base.IsFocused)
			{
				FocusNode(node);
			}
		}

		// Migration note：WPF ItemsControl.GetContainerForItemOverride()（返回 ItemContainer）在
		// Avalonia 12 无此虚方法；对应机制是 CreateContainerForItemOverride(item, index, recycleKey)。
		// 原非 override 的 GetContainerForItemOverride 永远不会被框架调用 → 实际容器是 ListBox
		// 默认 ListBoxItem（非 TreeViewControlItem），PrepareContainerForItemOverride 里
		// (element as TreeViewControlItem) 为 null → NRE（主窗口仓库列表渲染崩溃实证）。
		// 注：WPF IsItemItsOwnContainerOverride 在 Avalonia 12 无对应虚方法（数据项均为
		// MultiselectionTreeViewItem，本身不是容器，无需该判断）。
		protected override global::Avalonia.Controls.Control CreateContainerForItemOverride(object item, int index, object recycleKey)
		{
			return new TreeViewControlItem();
		}

		protected override void PrepareContainerForItemOverride(global::Avalonia.Controls.Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as TreeViewControlItem)?.ParentTreeView = this;
		}

		protected void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			// Migration note（Linux 启动必崩修复，2026-09-07）：WPF 的 SelectionChangedEventArgs.
			// RemovedItems/AddedItems 是对象引用快照，可安全枚举；Avalonia 12 中它们是
			// SelectedItems<T> 惰性视图——按 SelectionModel 记录的【索引】对当前 ItemsSourceView
			// 现场解析。若源集合在选中索引记录之后收缩（本控件 _flattener_CollectionChanged 的
			// Remove 回调内 UpdateFocusedNode → SelectedItems.Clear() 正是此情形：提交的取消选中
			// 集合携带收缩前的过期索引，枚举时经 ItemsSourceView.GetAt → Flattener.get_Item 越界抛
			// ArgumentOutOfRangeException），Linux 用户实测启动即崩（仓库管理器单仓库时选中项即
			// 可见序列末项，后台重扫描 → Refresh → Children.Clear() 后索引必然越界，进程终止）。
			// 修复：不枚举事件参数的惰性视图，改按引用集合 SelectedItems（真实对象引用，无索引
			// 解析——UpdateFocusedNode 里 .Cast<>() 先行执行未崩即实证）与上次已知选中快照求差集，
			// 同步节点 IsSelected（保持原语义：新选中置 true / 不再选中置 false）。
			HashSet<MultiselectionTreeViewItem> currentSelection = new HashSet<MultiselectionTreeViewItem>(base.SelectedItems.OfType<MultiselectionTreeViewItem>());
			foreach (MultiselectionTreeViewItem previouslySelectedNode in _knownSelectedNodes)
			{
				if (!currentSelection.Contains(previouslySelectedNode))
				{
					previouslySelectedNode.IsSelected = false;
				}
			}
			foreach (MultiselectionTreeViewItem selectedNode in currentSelection)
			{
				selectedNode.IsSelected = true;
			}
			_knownSelectedNodes.Clear();
			_knownSelectedNodes.UnionWith(currentSelection);
			// Migration note：WPF 在 override 末尾调 base.OnSelectionChanged(e) 触发 ListBox 的 SelectionChanged 事件；
			// Avalonia 12 中本方法改为由 SelectionChanged 事件回调（见构造函数订阅），事件已由基类触发，无需再转发。
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			TreeViewControlItem treeViewControlItem = e.Source as TreeViewControlItem;
			switch (e.Key)
			{
			case Key.Left:
				if (treeViewControlItem != null && ItemsControl.ItemsControlFromItemContainer(treeViewControlItem) == this)
				{
					if (treeViewControlItem.Node.IsExpanded)
					{
						treeViewControlItem.Node.IsExpanded = false;
					}
					else if (treeViewControlItem.Node.ParentItem != null)
					{
						FocusNode(treeViewControlItem.Node.ParentItem);
					}
					e.Handled = true;
				}
				break;
			case Key.Right:
				if (treeViewControlItem != null && ItemsControl.ItemsControlFromItemContainer(treeViewControlItem) == this)
				{
					if (!treeViewControlItem.Node.IsExpanded && treeViewControlItem.Node.ShowExpander)
					{
						treeViewControlItem.Node.IsExpanded = true;
					}
					else if (treeViewControlItem.Node.Children.Count > 0)
					{
						treeViewControlItem.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
					}
					e.Handled = true;
				}
				break;
			}
			if (!e.Handled)
			{
				base.OnKeyDown(e);
			}
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			Point position = e.GetPosition(this);
			// Migration note（2026-09-03，"暂存区文件双击不能穿梭"根因）：
			// WPF 原版是 GetObjectAtPoint<TreeViewControlItem>(position) as MultiselectionTreeViewItem——
			// GetObjectAtPoint 返回 item（即节点，Flattener 直出的 MultiselectionTreeViewItem），
			// 不是 TreeViewControlItem 容器；迁移时误写 as TreeViewControlItem，cast 恒为 null，
			// OnDoubleTapped 里该赋值会把容器 OnPointerPressed 已设好的节点清空，
			// 订阅者读到的 LastClickedItem 恒 null → ItemDoubleClick 从不触发。
			LastClickedItem = this.GetObjectAtPoint<TreeViewControlItem>(position) as MultiselectionTreeViewItem;
			base.OnPointerPressed(e);
		}

		protected override void OnDoubleTapped(global::Avalonia.Input.TappedEventArgs e)
		{
			Point position = e.GetPosition(this);
			// 同 OnPointerPressed：GetObjectAtPoint 返回节点 item，cast 到容器类型恒为 null，
			// 会在双击链路上把 LastClickedItem 清空。WPF 原版 cast 到 MultiselectionTreeViewItem。
			LastClickedItem = this.GetObjectAtPoint<TreeViewControlItem>(position) as MultiselectionTreeViewItem;
			base.OnDoubleTapped(e);
			// v3.12 修复（文件列表双击穿梭失效）：WPF 的 base.OnMouseDoubleClick 内部 RaiseEvent
			// 同步触发订阅者，末尾清空 LastClickedItem 时订阅者已执行完；Avalonia 的
			// OnDoubleTapped 是 class handler，先于 instance 订阅者（FileListUserControl 的
			// DoubleTapped += ...）执行——原样迁移的同步清空让所有订阅者读到的
			// LastClickedItem 恒为 null，双击 Stage/Unstage 穿梭从不触发。
			// 清空推迟到事件路由与本轮同步处理完全结束后（Dispatcher.Post）。
			Dispatcher.UIThread.Post(delegate
			{
				LastClickedItem = null;
			}, global::Avalonia.Threading.DispatcherPriority.Input);
		}

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == RootItemProperty)
			{
				Reload();
			}
		}

		private void Reload()
		{
			if (_flattener != null)
			{
				_flattener.Unmount();
			}
			if (RootItem != null)
			{
				RootItem.IsExpanded = true;
				_flattener = new FlattenerNode.Flattener(RootItem);
				_flattener.CollectionChanged += _flattener_CollectionChanged;
				base.ItemsSource = _flattener;
			}
		}

		public void FocusNode(MultiselectionTreeViewItem node)
		{
			if (node == null)
			{
				throw new ArgumentNullException("node");
			}
			ScrollIntoView(node);
			// Migration note：WPF 判断 ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated 决定
			// 立即/延迟聚焦，延迟路径用 Dispatcher.Post(DispatcherPriority.Loaded, DispatcherOperationCallback, node)；
			// Avalonia 12 的 ItemContainerGenerator 没有 Status/GeneratorStatus（CS1061/CS0103），
			// 也没有 DispatcherOperationCallback 委托（CS0246）。替代判定：ContainerFromItem 取到容器
			// 即视为"容器已生成"，取不到则经 Dispatcher.Post(..., DispatcherPriority.Loaded) 等容器/布局
			// 就绪后再聚焦一次，保持 WPF"未生成则推迟到 Loaded"的语义。
			if (base.ContainerFromItem(node) is global::Avalonia.Controls.Control container)
			{
				container.Focus();
			}
			else
			{
				base.Dispatcher.Post(delegate
				{
					OnFocusItem(node);
				}, DispatcherPriority.Loaded);
			}
		}

		public void HandleExpanding(MultiselectionTreeViewItem node)
		{
			if (doNotScrollOnExpanding)
			{
				return;
			}
			MultiselectionTreeViewItem multiselectionTreeViewItem = node;
			while (true)
			{
				MultiselectionTreeViewItem multiselectionTreeViewItem2 = multiselectionTreeViewItem.Children.LastOrDefault((MultiselectionTreeViewItem c) => c.IsVisible);
				if (multiselectionTreeViewItem2 == null)
				{
					break;
				}
				multiselectionTreeViewItem = multiselectionTreeViewItem2;
			}
			if (multiselectionTreeViewItem != node)
			{
				ScrollIntoView((object)multiselectionTreeViewItem);
				// Migration note：WPF Dispatcher.Post(priority, action) 参数序在 Avalonia 是
				// Post(action, priority)（CS1503），此处按 Avalonia 顺序调整。
				base.Dispatcher.Post(delegate
				{
					ScrollIntoView((object)node);
				}, DispatcherPriority.Loaded);
			}
		}

		public void ScrollIntoView(MultiselectionTreeViewItem node)
		{
			if (node == null)
			{
				throw new ArgumentNullException("node");
			}
			doNotScrollOnExpanding = true;
			foreach (MultiselectionTreeViewItem item in node.Ancestors())
			{
				item.IsExpanded = true;
			}
			doNotScrollOnExpanding = false;
			ScrollIntoView((object)node);
		}

		public IDisposable LockUpdates()
		{
			return new UpdateLock(this);
		}

		private object OnFocusItem(object item)
		{
			if (base.ContainerFromItem(item) is global::Avalonia.Controls.Control frameworkElement)
			{
				frameworkElement.Focus();
			}
			return null;
		}

		private void _flattener_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != NotifyCollectionChangedAction.Remove || base.Items.Count <= 0)
			{
				return;
			}
			List<MultiselectionTreeViewItem> list = null;
			foreach (MultiselectionTreeViewItem oldItem in e.OldItems)
			{
				if (oldItem.IsSelected)
				{
					if (list == null)
					{
						list = new List<MultiselectionTreeViewItem>();
					}
					list.Add(oldItem);
				}
			}
			if (!_updatesLocked && list != null)
			{
				List<MultiselectionTreeViewItem> newSelection = base.SelectedItems.Cast<MultiselectionTreeViewItem>().Except(list).ToList();
				UpdateFocusedNode(newSelection, Math.Max(0, e.OldStartingIndex - 1));
			}
		}

		private void UpdateFocusedNode(List<MultiselectionTreeViewItem> newSelection, int topSelectedIndex)
	{
		if (!_updatesLocked)
		{
			// Migration note：WPF MultiSelector.SetSelectedItems(IEnumerable) 原子替换选中集合，
			// Avalonia 12 的 ListBox 没有该 API（CS0103）；等效实现：清空 SelectedItems 后逐个添加
			// （会触发 SelectionChanged → OnSelectionChanged 同步节点 IsSelected，与 WPF 行为一致）。
			IList selectedItems = base.SelectedItems;
			if (selectedItems != null)
			{
				selectedItems.Clear();
				foreach (MultiselectionTreeViewItem item in newSelection ?? Enumerable.Empty<MultiselectionTreeViewItem>())
				{
					selectedItems.Add(item);
				}
			}
			if (base.SelectedItem == null)
			{
				// 边界保护（2026-09-07）：Flattener 刚收缩，topSelectedIndex 按 OldStartingIndex 计算
				// 可能已越界（Avalonia SelectionModel 对超界索引同样抛 ArgumentOutOfRangeException）；
				// WPF SelectedIndex 越界赋值仅静默忽略，按此语义钳制。
				if (topSelectedIndex >= 0 && topSelectedIndex < base.Items.Count)
				{
					base.SelectedIndex = topSelectedIndex;
				}
			}
		}
	}

		public IEnumerable<MultiselectionTreeViewItem> GetTopLevelSelection()
		{
			IEnumerable<MultiselectionTreeViewItem> enumerable = base.SelectedItems.OfType<MultiselectionTreeViewItem>();
			HashSet<MultiselectionTreeViewItem> selectionHash = new HashSet<MultiselectionTreeViewItem>(enumerable);
			return enumerable.Where((MultiselectionTreeViewItem item) => item.Ancestors().All((MultiselectionTreeViewItem a) => !selectionHash.Contains(a)));
		}

		protected void OnDragEnter(DragEventArgs e)
		{
			OnDragOver(e);
		}

		protected void OnDragOver(DragEventArgs e)
		{
			e.DragEffects= DragDropEffects.None;
			if (RootItem != null)
			{
				e.Handled = true;
				e.DragEffects= RootItem.GetDropEffect(e, RootItem.Children.Count);
			}
		}

		protected void OnDrop(DragEventArgs e)
		{
			e.DragEffects= DragDropEffects.None;
			if (RootItem != null)
			{
				e.Handled = true;
				e.DragEffects= RootItem.GetDropEffect(e, RootItem.Children.Count);
				if (e.DragEffects!= 0)
				{
					RootItem.InternalDrop(e, RootItem.Children.Count);
				}
			}
		}

		internal void HandleDragEnter(TreeViewControlItem item, DragEventArgs e)
		{
			HandleDragOver(item, e);
		}

		internal void HandleDragOver(TreeViewControlItem item, DragEventArgs e)
		{
			HidePreview();
			e.DragEffects= DragDropEffects.None;
			DropTarget dropTarget = GetDropTarget(item, e);
			if (dropTarget != null)
			{
				e.Handled = true;
				e.DragEffects= dropTarget.Effect;
				ShowPreview(dropTarget.Item);
			}
		}

		internal void HandleDrop(TreeViewControlItem item, DragEventArgs e)
		{
			try
			{
				HidePreview();
				DropTarget dropTarget = GetDropTarget(item, e);
				if (dropTarget != null)
				{
					e.Handled = true;
					e.DragEffects= dropTarget.Effect;
					dropTarget.Node.InternalDrop(e, dropTarget.Index);
				}
			}
			catch (Exception ex)
			{
				Log.Debug(ex.ToString());
				throw;
			}
		}

		internal void HandleDragLeave(TreeViewControlItem item, DragEventArgs e)
		{
			HidePreview();
			e.Handled = true;
		}

		private DropTarget GetDropTarget(TreeViewControlItem item, DragEventArgs e)
		{
			List<DropTarget> list = BuildDropTargets(item, e);
			double y = e.GetPosition(item).Y;
			foreach (DropTarget item2 in list)
			{
				if (item2.Y >= y)
				{
					return item2;
				}
			}
			return null;
		}

		private List<DropTarget> BuildDropTargets(TreeViewControlItem item, DragEventArgs e)
		{
			List<DropTarget> list = new List<DropTarget>();
			_ = item.Node;
			TryAddDropTarget(list, item, e);
			double actualHeight = item.Bounds.Height;
			double num = 0.2 * actualHeight;
			double y = actualHeight / 2.0;
			double y2 = actualHeight - num;
			if (list.Count == 2)
			{
				list[0].Y = y;
			}
			else if (list.Count == 3)
			{
				list[0].Y = num;
				list[1].Y = y2;
			}
			if (list.Count > 0)
			{
				list[list.Count - 1].Y = actualHeight;
			}
			return list;
		}

		private void TryAddDropTarget(List<DropTarget> targets, TreeViewControlItem item, DragEventArgs e)
		{
			GetNodeAndIndex(item, out var node, out var index);
			if (node != null)
			{
				DragDropEffects dropEffect = node.GetDropEffect(e, index);
				if (dropEffect != 0)
				{
					DropTarget item2 = new DropTarget
					{
						Item = item,
						Node = node,
						Index = index,
						Effect = dropEffect
					};
					targets.Add(item2);
				}
			}
		}

		private void GetNodeAndIndex(TreeViewControlItem item, out MultiselectionTreeViewItem node, out int index)
		{
			node = null;
			index = 0;
			node = item.Node;
			index = node.Children.Count;
		}

		private void ShowPreview(TreeViewControlItem item)
		{
			_previewNodeView = item;
			_previewNodeView.Background = Application.Current.TryFindResource("TreeViewItem.SelectedInactive.Background") as Brush;
		}

		private void HidePreview()
		{
			if (_previewNodeView != null)
			{
				// Migration note：WPF Control.BackgroundProperty 在 Avalonia 不存在（Background 定义在 TemplatedControl）；
				// TreeViewControlItem : ListBoxItem → ContentControl → TemplatedControl，
				// 改用 TemplatedControl.BackgroundProperty 清除拖放预览背景。
				_previewNodeView.ClearValue(global::Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
				_previewNodeView = null;
			}
		}
	}
}
