using System;
using System.Collections.Generic;
using ForkPlus.UI.Commands;

namespace ForkPlus.UI.QuickLaunch
{
	public class WorkspaceCommandProvider : ICommandProvider
	{
		private readonly WorkspaceItem[] _allItems;

		public ArgumentType Type => ArgumentType.Workspace;

		public CommandProviderItem[] Items { get; private set; }

		public WorkspaceCommandProvider(Workspace[] workspaces)
		{
			_allItems = CreateViewModels(workspaces);
			Items = GetFilteredItems("");
		}

		public void Refresh(string filter)
		{
			Items = GetFilteredItems(filter);
		}

		private CommandProviderItem[] GetFilteredItems(string filterString)
		{
			IReadOnlyList<WorkspaceItem> readOnlyList = _allItems.FuzzyFilter(filterString, (WorkspaceItem x) => x.Title);
			CommandProviderItem[] array = new CommandProviderItem[readOnlyList.Count + 1];
			array[0] = new HeaderCommandProviderItem("Workspaces");
			for (int i = 1; i < array.Length; i++)
			{
				readOnlyList[i - 1].FuzzySearchString = filterString;
				array[i] = readOnlyList[i - 1];
			}
			return array;
		}

		private WorkspaceItem[] CreateViewModels(Workspace[] workspaces)
		{
			WorkspaceItem[] array = workspaces.Map((Workspace x) => new WorkspaceItem(x));
			Array.Sort(array, (WorkspaceItem x, WorkspaceItem y) => NaturalStringComparer.Instance.Compare(x.Title, y.Title));
			return array;
		}
	}
}
