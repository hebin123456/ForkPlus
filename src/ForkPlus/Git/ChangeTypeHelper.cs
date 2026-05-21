namespace ForkPlus.Git
{
	public static class ChangeTypeHelper
	{
		public static ChangeType Parse(string text)
		{
			if (text.StartsWith("A"))
			{
				return ChangeType.Added;
			}
			if (text.StartsWith("D"))
			{
				return ChangeType.Deleted;
			}
			if (text.StartsWith("R"))
			{
				return ChangeType.Renamed;
			}
			if (text.StartsWith("C"))
			{
				return ChangeType.Copied;
			}
			if (text.StartsWith("M"))
			{
				return ChangeType.Modified;
			}
			if (text.StartsWith("U"))
			{
				return ChangeType.Unmerged;
			}
			if (text.StartsWith("T"))
			{
				return ChangeType.TypeChanged;
			}
			return ChangeType.Unknown;
		}
	}
}
