// 阶段 6：GridView 列视图渲染器。
// WPF ListView.View = GridView 提供多列布局；Avalonia ListBox 无 GridView 概念。
// 本类在 ListView 设置 View 属性时，从 GridView.Columns 构建合并的 ItemTemplate：
// - 单列 GridView：直接用该列的 CellTemplate 作为 ItemTemplate（RevisionListView 场景）。
// - 多列 GridView：用水平 Grid 排列各列 CellTemplate，按列 Width 设置 ColumnDefinition（InteractiveRebaseWindow 场景）。
//
// 列头（GridViewColumnHeader）暂不渲染（WPF 列头在 RevisionListView 中被隐藏，
// InteractiveRebaseWindow 等场景无交互需求）。后续如需列头可扩展为 Grid + ContentPresenter。
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Avalonia.Controls
{
	/// <summary>
	/// 从 GridView 构建合并的 ItemTemplate，让 ListBox/ListView 能渲染多列内容。
	/// </summary>
	internal static class GridViewRenderer
	{
		/// <summary>
		/// 根据 GridView 的 Columns 构建适用于 ListBox.ItemTemplate 的 IDataTemplate。
		/// 单列时直接返回该列 CellTemplate；多列时返回包装 DataTemplate（水平 Grid 排列）。
		/// </summary>
		public static IDataTemplate BuildItemTemplate(GridView gridView)
		{
			if (gridView == null || gridView.Columns.Count == 0)
			{
				return null;
			}

			// 单列：直接用该列 CellTemplate（最常见场景，如 RevisionListView）
			if (gridView.Columns.Count == 1)
			{
				return gridView.Columns[0].CellTemplate;
			}

			// 多列：用 FuncDataTemplate 构建 Grid 容器，每列实例化其 CellTemplate
			var columns = gridView.Columns;
			return new FuncDataTemplate<object>((data, _) =>
			{
				var grid = new Grid();
				grid.VerticalAlignment = VerticalAlignment.Stretch;
				grid.HorizontalAlignment = HorizontalAlignment.Stretch;

				for (int i = 0; i < columns.Count; i++)
				{
					var col = columns[i];
					var colDef = new ColumnDefinition();
					double width = col.Width;
					if (double.IsNaN(width) || width <= 0)
					{
						// 未指定宽度（含 NaN）的列占剩余空间
						colDef.Width = GridLength.Star;
					}
					else
					{
						colDef.Width = new GridLength(width, GridUnitType.Pixel);
					}
					grid.ColumnDefinitions.Add(colDef);

					IDataTemplate cellTemplate = col.CellTemplate;
					if (cellTemplate != null)
					{
						Control cell = cellTemplate.Build(data) as Control;
						if (cell != null)
						{
							cell.SetValue(Grid.ColumnProperty, i);
							grid.Children.Add(cell);
						}
					}
				}

				return grid;
			}, true);
		}
	}
}
