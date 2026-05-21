namespace ForkPlus.UI.Controls.Editor.Diff
{
	public struct Highlighting
	{
		public Range Range { get; }

		public byte Style { get; }

		public Highlighting(Range range, byte style)
		{
			Range = range;
			Style = style;
		}
	}
}
