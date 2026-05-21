namespace ForkPlus.Git
{
	public struct BehindAheadCount
	{
		public int Left { get; }

		public int Right { get; }

		public BehindAheadCount(int left, int right)
		{
			Left = left;
			Right = right;
		}
	}
}
