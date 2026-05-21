using ForkPlus.Git;
using ForkPlus.UI.Commands;

namespace ForkPlus.UI.UserControls
{
	public static class ChangedFilesExtensions
	{
		public static ShowFileHistoryWindowCommand.Mode Mode(this ChangedFile changedFile)
		{
			if (changedFile.IsDirectory)
			{
				return new ShowFileHistoryWindowCommand.Mode.Directory(changedFile.Path);
			}
			return new ShowFileHistoryWindowCommand.Mode.File(changedFile.Path);
		}
	}
}
