using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;

namespace ForkPlus.UI.UserControls
{
	public static class RepositoryUserControlExtensions
	{
		private static SubmodulesToUpdate Empty = new SubmodulesToUpdate(new Tuple<Submodule, bool>[0]);

		public static SubmodulesToUpdate SubmodulesToUpdate(this RepositoryUserControl repositoryUserControl)
		{
			Submodule[] array = repositoryUserControl.RepositoryData?.Submodules.Items;
			if (array != null)
			{
				ChangedFile[] changedFiles = repositoryUserControl.RepositoryStatus?.ChangedFiles;
				if (changedFiles != null)
				{
					if (array.Length == 0 || !ForkPlusSettings.Default.UpdateSubmodulesOnCheckout)
					{
						return Empty;
					}
					return new SubmodulesToUpdate(array.Map((Submodule s) => new Tuple<Submodule, bool>(s, changedFiles.ContainsItem((ChangedFile x) => x.Path == s.Path))));
				}
			}
			return Empty;
		}
	}
}
