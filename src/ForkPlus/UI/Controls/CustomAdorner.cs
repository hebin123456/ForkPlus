using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ForkPlus.UI;

namespace ForkPlus.UI.Controls
{
	public class CustomAdorner : Adorner
	{
		private bool _centeredHorizontallyInParent;

		private FrameworkElement _child;

		public FrameworkElement Child
		{
			get
			{
				return _child;
			}
			set
			{
				if (_child != value)
				{
					if (_child != null)
					{
						RemoveVisualChild(_child);
						RemoveLogicalChild(_child);
					}
					if (value != null && !VisualTreeAttachmentHelper.PrepareForNewParent(value, GetType().Name + ".Child"))
					{
						value = null;
					}
					_child = value;
					if (_child != null)
					{
						AddLogicalChild(_child);
						AddVisualChild(_child);
					}
					InvalidateMeasure();
				}
			}
		}

		protected override int VisualChildrenCount => (Child != null) ? 1 : 0;

		public CustomAdorner(UIElement adornernedElement, bool centeredHorizontally = false)
			: base(adornernedElement)
		{
			_centeredHorizontallyInParent = centeredHorizontally;
		}

		protected override Visual GetVisualChild(int index)
		{
			return Child;
		}

		protected override Size MeasureOverride(Size constraint)
		{
			if (Child == null)
			{
				return default(Size);
			}
			Child.Measure(constraint);
			Size result = Child.DesiredSize;
			if (result.Width < 40.0)
			{
				result = new Size(40.0, result.Height);
			}
			return result;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (Child == null)
			{
				return default(Size);
			}
			double x = 0.0;
			double y = 0.0;
			if (base.HorizontalAlignment == HorizontalAlignment.Center || _centeredHorizontallyInParent)
			{
				x = (0.0 - finalSize.Width) / 2.0;
			}
			Child.Arrange(new Rect(x, y, finalSize.Width, finalSize.Height));
			return finalSize;
		}
	}
}
