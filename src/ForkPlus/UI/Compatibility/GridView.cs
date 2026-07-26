// WPF System.Windows.Controls.GridView / GridViewColumn / GridViewColumnHeader 在 Avalonia 中无直接对应。
// WPF ListView.View = new GridView() 提供多列布局；Avalonia 推荐用 DataGrid 或 ItemsControl 自定义布局。
//
// 本组桥接类用于让 XAML 中 <GridView> / <GridViewColumn> / <GridViewColumnHeader> 引用通过编译，
// 运行时通过 GridViewRenderer.BuildItemTemplate 从 GridView.Columns 构建合并的 ItemTemplate
// （见 WpfBridgeTypes.cs 中 ListView.ViewProperty.Changed class handler）。
// - 单列 GridView：直接用该列的 CellTemplate 作为 ItemTemplate（RevisionListView 场景）
// - 多列 GridView：用水平 Grid 排列各列 CellTemplate（InteractiveRebaseWindow 场景）
// 列头（GridViewColumnHeader）暂不渲染：RevisionListView 中列头被隐藏，
// InteractiveRebaseWindow 等场景无交互需求。后续如需列头可扩展为 Grid + ContentPresenter。
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace Avalonia.Controls
{
	/// <summary>
	/// WPF System.Windows.Controls.GridView 的 Avalonia 兼容占位。
	/// 表示 ListView 的多列视图模式。本桥接类仅持有列集合，不实际渲染。
	/// </summary>
	public class GridView : AvaloniaObject
	{
		// 阶段 5：ColumnHeaderContainerStyle 改为 StyledProperty 以支持 {DynamicResource} 绑定。
		public static readonly StyledProperty<Style> ColumnHeaderContainerStyleProperty =
			AvaloniaProperty.Register<GridView, Style>(nameof(ColumnHeaderContainerStyle));

		/// <summary>WPF GridView.ColumnHeaderContainerStyle：列头样式。</summary>
		public Style ColumnHeaderContainerStyle
		{
			get => GetValue(ColumnHeaderContainerStyleProperty);
			set => SetValue(ColumnHeaderContainerStyleProperty, value);
		}

		/// <summary>WPF GridView.Columns：列集合。</summary>
		[Content]
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
	public class GridViewColumn : AvaloniaObject
	{
		/// <summary>WPF GridViewColumn.Width：列宽。</summary>
		public double Width { get; set; } = double.NaN;

		/// <summary>WPF GridViewColumn.ActualWidth：实际渲染宽度。Avalonia 桥接占位：默认与 Width 相同（NaN 时回退 0）。</summary>
		public double ActualWidth
		{
			get => double.IsNaN(Width) ? 0.0 : Width;
			set => Width = value;
		}

		/// <summary>WPF GridViewColumn.Header：列头内容（ContentProperty，XAML 中作为隐式内容）。</summary>
		[Content]
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
