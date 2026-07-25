// ⚠ 临时桥接类型 ─ 阶段 5 编译过渡用。
// WPF System.Windows.Controls.GridView / GridViewColumn / GridViewColumnHeader 在 Avalonia 中无直接对应。
// WPF ListView.View = new GridView() 提供多列布局；Avalonia 推荐用 DataGrid 或 ItemsControl 自定义布局。
//
// 本组桥接类仅用于让 XAML 中 <GridView> / <GridViewColumn> / <GridViewColumnHeader> 引用通过编译，
// 运行时不渲染列视图（ListView 将退化为简单列表）。阶段 6 需迁移到 DataGrid 或自定义 Grid 布局。
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Styling;

namespace Avalonia.Controls
{
	/// <summary>
	/// WPF System.Windows.Controls.GridView 的 Avalonia 兼容占位。
	/// 表示 ListView 的多列视图模式。本桥接类仅持有列集合，不实际渲染。
	/// </summary>
	public class GridView
	{
		/// <summary>WPF GridView.ColumnHeaderContainerStyle：列头样式。</summary>
		public Style ColumnHeaderContainerStyle { get; set; }

		/// <summary>WPF GridView.Columns：列集合。</summary>
		public ObservableCollection<GridViewColumn> Columns { get; } = new ObservableCollection<GridViewColumn>();

		/// <summary>WPF GridView.AllowsColumnReorder：是否允许拖拽重排序列。</summary>
		public bool AllowsColumnReorder { get; set; }

		/// <summary>WPF GridView.ColumnHeaderStringFormat：列头字符串格式。</summary>
		public string ColumnHeaderStringFormat { get; set; }

		/// <summary>WPF GridView.ColumnHeaderTemplate：列头数据模板。</summary>
		public IDataTemplate ColumnHeaderTemplate { get; set; }

		/// <summary>WPF GridView.ColumnHeaderTemplateSelector：列头模板选择器。</summary>
		public IDataTemplate ColumnHeaderTemplateSelector { get; set; }
	}

	/// <summary>
	/// WPF System.Windows.Controls.GridViewColumn 的 Avalonia 兼容占位。
	/// 表示 GridView 中的一列。
	/// </summary>
	public class GridViewColumn
	{
		/// <summary>WPF GridViewColumn.Width：列宽。</summary>
		public double Width { get; set; } = double.NaN;

		/// <summary>WPF GridViewColumn.ActualWidth：实际渲染宽度。Avalonia 桥接占位：默认与 Width 相同（NaN 时回退 0）。</summary>
		public double ActualWidth
		{
			get => double.IsNaN(Width) ? 0.0 : Width;
			set => Width = value;
		}

		/// <summary>WPF GridViewColumn.Header：列头内容。</summary>
		public object Header { get; set; }

		/// <summary>WPF GridViewColumn.HeaderContainerStyle：列头容器样式。</summary>
		public Style HeaderContainerStyle { get; set; }

		/// <summary>WPF GridViewColumn.HeaderStringFormat：列头字符串格式。</summary>
		public string HeaderStringFormat { get; set; }

		/// <summary>WPF GridViewColumn.HeaderTemplate：列头数据模板。</summary>
		public IDataTemplate HeaderTemplate { get; set; }

		/// <summary>WPF GridViewColumn.HeaderTemplateSelector：列头模板选择器。</summary>
		public IDataTemplate HeaderTemplateSelector { get; set; }

		/// <summary>WPF GridViewColumn.DisplayMemberBinding：单元格文本绑定。</summary>
		public Avalonia.Data.IBinding DisplayMemberBinding { get; set; }

		/// <summary>WPF GridViewColumn.CellTemplate：单元格数据模板。</summary>
		public IDataTemplate CellTemplate { get; set; }

		/// <summary>WPF GridViewColumn.CellTemplateSelector：单元格模板选择器。</summary>
		public IDataTemplate CellTemplateSelector { get; set; }

		/// <summary>WPF GridViewColumn.CellStringFormat：单元格字符串格式。</summary>
		public string CellStringFormat { get; set; }

		/// <summary>WPF GridViewColumn.FieldName：绑定的字段名（部分 WPF 版本）。</summary>
		public string FieldName { get; set; }
	}

	/// <summary>
	/// WPF System.Windows.Controls.GridViewColumnHeader 的 Avalonia 兼容占位。
	/// 表示 GridView 列的表头控件。继承 ContentControl（与 WPF 一致）。
	/// </summary>
	public class GridViewColumnHeader : ContentControl
	{
		/// <summary>WPF GridViewColumnHeader.Column：关联的 GridViewColumn。</summary>
		public GridViewColumn Column { get; set; }

		/// <summary>WPF GridViewColumnHeader.Role：列头角色（普通/填充）。</summary>
		public GridViewColumnHeaderRole Role { get; set; }
	}

	/// <summary>WPF GridViewColumnHeaderRole 枚举的兼容占位。</summary>
	public enum GridViewColumnHeaderRole
	{
		/// <summary>普通列头。</summary>
		Normal = 0,
		/// <summary>填充列头（占满剩余空间）。</summary>
		Padding = 1
	}
}
