using System.Collections.Generic;

namespace ForkPlus.UI
{
	public static class IRoundedSelectionListBoxViewModelExtensions
	{
		public class MultiselectionListViewModelRowComparer : IComparer<IRoundedSelectionListBoxViewModel>
		{
			public static readonly MultiselectionListViewModelRowComparer Instance = new MultiselectionListViewModelRowComparer();

			public int Compare(IRoundedSelectionListBoxViewModel x, IRoundedSelectionListBoxViewModel y)
			{
				return x.Row.CompareTo(y.Row);
			}
		}

		public static void RefreshSelectionType(this IRoundedSelectionListBoxViewModel[] selectedItems)
		{
			selectedItems = selectedItems.ToSortedArray(MultiselectionListViewModelRowComparer.Instance);
			for (int i = 0; i < selectedItems.Length; i++)
			{
				IRoundedSelectionListBoxViewModel roundedSelectionListBoxViewModel = selectedItems[i];
				bool flag = i > 0 && selectedItems[i - 1].Row + 1 == roundedSelectionListBoxViewModel.Row;
				bool flag2 = i < selectedItems.Length - 1 && selectedItems[i + 1].Row - 1 == roundedSelectionListBoxViewModel.Row;
				if (flag && flag2)
				{
					roundedSelectionListBoxViewModel.SelectionType = ListBoxSelectionType.Middle;
				}
				else if (flag)
				{
					roundedSelectionListBoxViewModel.SelectionType = ListBoxSelectionType.Bottom;
				}
				else if (flag2)
				{
					roundedSelectionListBoxViewModel.SelectionType = ListBoxSelectionType.Top;
				}
				else
				{
					roundedSelectionListBoxViewModel.SelectionType = ListBoxSelectionType.Separate;
				}
			}
		}
	}
}
