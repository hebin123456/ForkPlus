namespace ForkPlus.Git.Diff.Presentation
{
	public class VisualLine
	{
		public LineType Type { get; }

		public Range Range { get; }

		public int LineNumber { get; }

		public int NodeIndex { get; }

		public VisualLine(LineType type, Range range, int lineNumber, int nodeIndex)
		{
			Type = type;
			Range = range;
			LineNumber = lineNumber;
			NodeIndex = nodeIndex;
		}
	}
}
