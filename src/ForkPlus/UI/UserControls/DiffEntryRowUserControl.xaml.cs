using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

		public void SetDiffContent(FrameworkElement content)
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

		protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseLeftButtonUp(e);
			if (_updatingToggleButton)
			{
				return;
			}
			if ((e.OriginalSource as DependencyObject)?.GetParent<Border>() == DiffContentHost)
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
