using System.Collections.Generic;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Git.Commands
{
	public class GetCustomCommandsGitCommand
	{
		public CustomCommand[] Execute(GitModule gitModule)
		{
			CustomCommand[] array = CustomCommandManager.Load(CustomCommandManager.SharedLocalPath(gitModule), shared: true);
			CustomCommand[] array2 = CustomCommandManager.Load(CustomCommandManager.LocalPath(gitModule));
			List<CustomCommand> list = new List<CustomCommand>(array.Length + array2.Length);
			list.AddRange(array);
			list.AddRange(array2);
			return list.ToArray();
		}
	}
}
