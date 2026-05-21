namespace ForkPlus.Git
{
	public static class StatusTypeHelper
	{
		public static StatusType Parse(char statusTypeChar)
		{
			switch (statusTypeChar)
			{
			case 'A':
				return StatusType.Added;
			case 'B':
				return StatusType.Broken;
			case 'C':
				return StatusType.Copied;
			case 'D':
				return StatusType.Deleted;
			case '!':
				return StatusType.Ignored;
			case 'M':
				return StatusType.Modified;
			case 'R':
				return StatusType.Renamed;
			case 'T':
				return StatusType.TypeChanged;
			case 'X':
				return StatusType.Unknown;
			case 'U':
				return StatusType.Unmerged;
			case '?':
				return StatusType.Untracked;
			case ' ':
				return StatusType.None;
			default:
				Log.Warn($"Unknown changed file status: {statusTypeChar}");
				return StatusType.None;
			}
		}
	}
}
