namespace ForkPlus.UI
{
	public struct GraphLine
	{
		public byte Id { get; }

		public byte TopColumn { get; }

		public byte Column { get; }

		public byte BottomColumn { get; }

		public GraphLine(byte id, byte top, byte column, byte bottom)
		{
			Id = id;
			TopColumn = top;
			Column = column;
			BottomColumn = bottom;
		}
	}
}
