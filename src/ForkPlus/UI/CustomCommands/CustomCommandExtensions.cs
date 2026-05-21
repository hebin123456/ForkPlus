using System;
using System.Collections;
using System.Windows.Controls;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.CustomCommands
{
	public static class CustomCommandExtensions
	{
		public static bool IsVersionSupported(this CustomCommand command)
		{
			return command.Version <= 2;
		}

		public static void AddCustomCommandItem(this CustomCommand command, RepositoryUserControl repositoryUserControl, CustomCommandEnvironment env, string[] path, int pathIndex, IList menuItems, int startIndex = 0)
		{
			string text = env.ReplaceVariablesWithValues(path[pathIndex]);
			if (pathIndex < path.Length - 1)
			{
				MenuItem menuItem = FindOrCreateFolderItem(menuItems, text, startIndex);
				command.AddCustomCommandItem(repositoryUserControl, env, path, pathIndex + 1, menuItem.Items);
				return;
			}
			MenuItem value = RepositoryUserControl.Commands.RunCustomCommand.CreateMenuItem(text, delegate
			{
				RepositoryUserControl.Commands.RunCustomCommand.Execute(repositoryUserControl, command, env);
			}, command.IsVersionSupported());
			menuItems.Add(value);
		}

		private static MenuItem FindOrCreateFolderItem(IList menuItems, string name, int startIndex)
		{
			for (int i = startIndex; i < menuItems.Count; i++)
			{
				if (menuItems[i] is MenuItem menuItem && menuItem.Header.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) && menuItem.Items.Count > 0)
				{
					return menuItem;
				}
			}
			MenuItem menuItem2 = RepositoryUserControl.Commands.RunCustomCommand.CreateMenuItem(name);
			menuItems.Add(menuItem2);
			return menuItem2;
		}
	}
}
