using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ForkPlus.UI;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class CustomAdorner : Adorner
	{
		private bool _centeredHorizontallyInParent;

		private global::Avalonia.Controls.Control _child;

		// Bug 修复（2026-09-07，"重命名仓库……只是一个很窄的小框"）：本装饰器承载重命名
		// 编辑 TextBox，宽度必须按名字内容自量——进入编辑态后被装饰的 ETB 已塌缩为 0 宽，
		// 若照默认把 Width 设成它的 Bounds 会得到 0 宽编辑框。见 Adorner.TracksAdornedElementSize。
		public override bool TracksAdornedElementSize => false;

		public global::Avalonia.Controls.Control Child
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

		public CustomAdorner(global::Avalonia.Input.InputElement adornernedElement, bool centeredHorizontally = false)
			: base(adornernedElement)
		{
			IsHitTestVisible = true;
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
