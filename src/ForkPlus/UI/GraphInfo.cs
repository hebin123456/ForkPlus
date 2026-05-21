namespace ForkPlus.UI
{
	public class GraphInfo
	{
		public GraphLine[] Lines { get; }

		public byte CurrentCommitColumn { get; }

		public byte CurrentCommitLineId { get; }

		public GraphInfo(GraphLine[] lines, byte currentPointIndex, byte laneIndex)
		{
			Lines = lines;
			CurrentCommitColumn = currentPointIndex;
			CurrentCommitLineId = laneIndex;
		}
	}
}
