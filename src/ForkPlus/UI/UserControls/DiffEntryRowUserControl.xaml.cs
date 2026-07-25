// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（Control/Border/ContextMenu）
// - using System.Windows.Controls.Primitives → 移除（ToggleButton 仅在 XAML 中使用，.cs 未直接引用类型）
// - using System.Windows.Input → using Avalonia.Input（PointerReleasedEventArgs）
// - using System.Windows.Media → using Avalonia.Media（Geometry）
// - FrameworkElement → Avalonia.Controls.Control（参数类型，参考 FileContentControl）
// - OnPreviewMouseLeftButtonUp(PointerPressedEventArgs) → OnPointerReleased(PointerReleasedEventArgs)（参考 DragAndDropListViewItem）
// - e.OriginalSource → e.Source（参考 ClosableTabItem/MultiselectionTreeView）
// - DependencyObject → AvaloniaObject（参考 DependencyObjectExtensions）
// - Visibility.Collapsed/Visible → Avalonia.Layout.Visibility（需 using Avalonia.Layout）
// - Geometry.Parse（API 兼容，Avalonia.Media.Geometry.Parse）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public partial class DiffEntryRowUserControl : UserControl
	{
		private static readonly Geometry CollapsedArrowGeometry = Geometry.Parse("M0,0L3.5,3.5 0,7");

		private static readonly Geometry ExpandedArrowGeometry = Geometry.Parse("M0,0L3.5,3.5 7,0");

		private bool _updatingToggleButton;

		public DiffEntry Entry { get; }

		public bool IsExpanded
		{
			get
			{
				return Entry.IsExpanded;
			}
			set
			{
				if (Entry.IsExpanded != value)
				{
					Entry.IsExpanded = value;
				}
				else
				{
					UpdateExpansionVisualState(value);
				}
			}
		}

		public event EventHandler SelectionChanged;

		public DiffEntryRowUserControl(DiffEntry entry)
		{
			Entry = entry ?? throw new ArgumentNullException(nameof(entry));
			InitializeComponent();
			base.DataContext = Entry;
			base.ContextMenu = new ContextMenu();
			Entry.PropertyChanged += Entry_PropertyChanged;
			UpdateExpansionVisualState(Entry.IsExpanded);
		}

		public void ClearDiffContent()
		{
			Entry.PropertyChanged -= Entry_PropertyChanged;
			SetDiffContent(null);
		}

		// 阶段 4.5：WPF FrameworkElement → Avalonia.Controls.Control（参考 FileContentControl）。
		public void SetDiffContent(Control content)
		{
			if (content == null)
			{
				VisualTreeAttachmentHelper.TrySetChild(DiffContentHost, null, GetType().Name + ".DiffContentHost");
				DiffContentHost.Visibility = Visibility.Collapsed;
				return;
			}
			if (VisualTreeAttachmentHelper.TrySetChild(DiffContentHost, content, GetType().Name + ".DiffContentHost"))
			{
				DiffContentHost.Visibility = Visibility.Visible;
			}
		}

		public void BringDiffContentIntoView()
		{
			if (DiffContentHost.Visibility == Visibility.Visible)
			{
				DiffContentHost.BringIntoView();
			}
			else
			{
				BringIntoView();
			}
		}

		private void HeaderToggleButton_CheckedChanged(object sender, RoutedEventArgs e)
		{
			if (_updatingToggleButton)
			{
				return;
			}
			Entry.IsExpanded = HeaderToggleButton.IsChecked.GetValueOrDefault();
			e.Handled = true;
		}

		// 阶段 4.5：WPF OnPreviewMouseLeftButtonUp(PointerPressedEventArgs) → Avalonia OnPointerReleased(PointerReleasedEventArgs)（参考 DragAndDropListViewItem）。
		// WPF e.OriginalSource → Avalonia e.Source（参考 ClosableTabItem）。WPF DependencyObject → AvaloniaObject（参考 DependencyObjectExtensions）。
		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			if (_updatingToggleButton)
			{
				return;
			}
			if ((e.Source as AvaloniaObject)?.GetParent<Border>() == DiffContentHost)
			{
				return;
			}
			Entry.IsExpanded = !Entry.IsExpanded;
			e.Handled = true;
		}

		private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsExpanded")
			{
				UpdateExpansionVisualState(Entry.IsExpanded);
				SelectionChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private void UpdateExpansionVisualState(bool isExpanded)
		{
			_updatingToggleButton = true;
			HeaderToggleButton.IsChecked = isExpanded;
			_updatingToggleButton = false;
			ArrowPath.Data = isExpanded ? ExpandedArrowGeometry : CollapsedArrowGeometry;
			SeparatorBorder.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
			if (!isExpanded)
			{
				SetDiffContent(null);
			}
		}
	}
}
