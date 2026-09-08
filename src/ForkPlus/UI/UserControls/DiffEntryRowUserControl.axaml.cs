using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.UI.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

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

		public void SetDiffContent(global::Avalonia.Controls.Control content)
		{
			if (content == null)
			{
				VisualTreeAttachmentHelper.TrySetChild(DiffContentHost, null, GetType().Name + ".DiffContentHost");
				DiffContentHost.IsVisible = false;
				return;
			}
			if (VisualTreeAttachmentHelper.TrySetChild(DiffContentHost, content, GetType().Name + ".DiffContentHost"))
			{
				DiffContentHost.IsVisible = true;
			}
		}

		public void BringDiffContentIntoView()
		{
			if (DiffContentHost.IsVisible == true)
			{
				DiffContentHost.BringIntoView(); // Migration note：WPF BringIntoView → BringIntoViewCompat 扩展。
			}
			else
			{
				this.BringIntoView(); // Migration note：扩展方法不可裸调用，需显式 this 接收者。
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

		protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			if (_updatingToggleButton)
			{
				return;
			}
			if ((e.Source as global::Avalonia.Visual)?.GetParent<Border>() /* Migration note：Visual 扩展需 Visual 接收者 */ == DiffContentHost)
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
			SeparatorBorder.IsVisible = isExpanded ? true : false;
			if (!isExpanded)
			{
				SetDiffContent(null);
			}
		}
	}
}
