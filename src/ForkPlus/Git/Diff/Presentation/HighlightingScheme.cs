namespace ForkPlus.Git.Diff.Presentation
{
	public class HighlightingScheme
	{
		public Range[] ServiceRegions { get; private set; }

		public Range[] RemoveRegions { get; private set; }

		public Range[] AddRegions { get; private set; }

		public Range[] AlignmentRegions { get; private set; }

		public Range[] ExtraRemoveRegions { get; private set; }

		public Range[] ExtraAddRegions { get; private set; }

		public HighlightingScheme(Range[] serviceRegions, Range[] removeRegions, Range[] addRegions, Range[] alignmentRegions, Range[] extraRemoveRegions, Range[] extraAddRegions)
		{
			ServiceRegions = serviceRegions;
			RemoveRegions = removeRegions;
			AddRegions = addRegions;
			AlignmentRegions = alignmentRegions;
			ExtraRemoveRegions = extraRemoveRegions;
			ExtraAddRegions = extraAddRegions;
		}
	}
}
