namespace ForkPlus.Git
{
	public static class DiffContentExtensions
	{
		public static bool IsConflictResolved(this DiffContent fileContent)
		{
			if (!(fileContent is UnmergedDiffContent { FileType: UnmergedDiffContent.ContentType.Text } unmergedDiffContent))
			{
				return false;
			}
			if (unmergedDiffContent.DiffString.StartsWith("* Unmerged path"))
			{
				return false;
			}
			if (unmergedDiffContent.DiffString.Contains("<<<<<"))
			{
				return false;
			}
			return true;
		}
	}
}
