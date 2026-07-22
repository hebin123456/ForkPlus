using System.Collections.Generic;
using ForkPlus.UI;

// Avalonia spike 版 IRoundedSelectionListBoxViewModelExtensions（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/IRoundedSelectionListBoxViewModelExtensions.cs（43 行）：
//   - WPF: public static class IRoundedSelectionListBoxViewModelExtensions
//   - 嵌套 MultiselectionListViewModelRowComparer : IComparer<IRoundedSelectionListBoxViewModel>
//     （按 Row 字段比较，单例 Instance）
//   - RefreshSelectionType(this IRoundedSelectionListBoxViewModel[]) 扩展：
//     按 Row 排序后，根据相邻 Row 是否连续判定每个项的 SelectionType
//     （Top / Middle / Bottom / Separate）
//   - 依赖：IRoundedSelectionListBoxViewModel / ListBoxSelectionType / ToSortedArray
//
// Avalonia 版差异：
//   1. IRoundedSelectionListBoxViewModel 已存在于本工程（ForkPlus.Avalonia 命名空间，spike 版）
//   2. ListBoxSelectionType 来自 ForkPlus.Core（namespace ForkPlus.UI），需 using ForkPlus.UI
//   3. ToSortedArray 扩展来自 ForkPlus.Core/ArrayExtensions（namespace ForkPlus），
//      从 ForkPlus.Avalonia 经外层命名空间可直接解析
//   4. 无 WPF 依赖，零改动复用排序/判定逻辑
//
// spike 简化：与 WPF 完全一致的 Comparer + RefreshSelectionType 逻辑。
namespace ForkPlus.Avalonia
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
