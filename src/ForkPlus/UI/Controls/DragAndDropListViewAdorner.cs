using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DragAndDropListViewAdorner : Adorner
	{
		private readonly double _visualBrushYOffset;

		private readonly Brush[] _visualBrushes;

		private readonly Point _initialPosition;

		private Point NewPosition { get; set; }

		public DragAndDropListViewAdorner(global::Avalonia.Input.InputElement adornedElement, ListBoxItem[] listBoxItems, Point position)
			: base(adornedElement)
		{
			_initialPosition = position;
			// Migration note：WPF VisualBrush(visual) 在 Avalonia 12.1 为 Avalonia.Media.VisualBrush(Visual)，
			// 反编译产物里误写成不存在的 Avalonia.Media.ImmutableBrush。
			Brush[] visualBrushes = listBoxItems.Map((ListBoxItem x) => new global::Avalonia.Media.VisualBrush(x)
			{
				Opacity = 0.4
			});
			_visualBrushes = visualBrushes;
			// Migration note：Avalonia ListBoxItem 无 ActualHeight，等价取 Bounds.Height。
			_visualBrushYOffset = listBoxItems.FirstItem()?.Bounds.Height ?? 0.0;
			base.IsHitTestVisible = false;
		}

		public void UpdatePosition(Point position)
		{
			NewPosition = position;
			InvalidateVisual();
		}

		public override void Render(DrawingContext context)
		{
			Point newPosition = NewPosition;
			// Avalonia Point 无 Offset(原地修改)，用新 Point 等价改写。
			newPosition = new Point(newPosition.X - _initialPosition.X, newPosition.Y - _initialPosition.Y);
			for (int i = 0; i < _visualBrushes.Length; i++)
			{
				Brush brush = _visualBrushes[i];
				if (i > 0)
				{
					newPosition = new Point(newPosition.X, newPosition.Y + _visualBrushYOffset);
				}
				context.DrawRectangle(brush, null, new Rect(newPosition, base.Bounds.Size));
			}
		}
	}
}
