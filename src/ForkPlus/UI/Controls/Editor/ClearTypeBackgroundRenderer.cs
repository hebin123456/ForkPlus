using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	public class ClearTypeBackgroundRenderer : IBackgroundRenderer
	{
		public KnownLayer Layer => KnownLayer.Background;

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(global::ForkPlus.UI.Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, textView.Bounds.Width, textView.Bounds.Height));
		}
	}
}
