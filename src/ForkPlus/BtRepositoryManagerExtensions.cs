using System;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;

namespace ForkPlus
{
	internal static class BtRepositoryManagerExtensions
	{
		public static GitCommandResult<RepositoryManager> Into(this ref BtRepositoryManager btRepositoryManager)
		{
			string[] stringArray = btRepositoryManager.source_dirs.GetStringArray(btRepositoryManager.source_dirs_len);
			byte scan_depth = btRepositoryManager.scan_depth;
			string[] stringArray2 = btRepositoryManager.ignore.GetStringArray(btRepositoryManager.ignore_len);
			RepositoryManager.Repository[] structArray = btRepositoryManager.repositories.GetStructArray(btRepositoryManager.repositories_len, delegate(int index, BtRepositoryManagerRepository btRepository)
			{
				string utf8String = btRepository.path.GetUtf8String();
				string alias = ((!(btRepository.alias != IntPtr.Zero)) ? null : btRepository.alias.GetUtf8String());
				int? opened = ((btRepository.opened == 0) ? null : new int?((int)btRepository.opened));
				RepositoryColor color = (RepositoryColor)btRepository.color;
				return new RepositoryManager.Repository(utf8String, alias, opened, color);
			});
			return GitCommandResult<RepositoryManager>.Success(new RepositoryManager(stringArray, scan_depth, stringArray2, structArray));
		}
	}
}
