// 真机 bug 复现（2026-09-03，"轨道图可收拢节点悬浮详情视图空白"）：
// WPF 原版 RevisionGraphTooltipUserControl 用 ListView+GridView 三列渲染折叠范围内的
// 提交（复合列 graph+upstream+refs+subject / 头像 19 / 日期 125）；Avalonia 迁移时
// GridView 整体被注释删除，ListView 没有 ItemTemplate → 数据加载后渲染空白（用户
// 报告"加载出来的是一个空白视图"）。
// 修复：按主提交列表 SingleRowRevisionTemplate 的方式重建为 Grid 列布局 DataTemplate
//（TooltipRevisionRowTemplate）并绑回 ItemTemplate。本测试两层回归：
//   1) XAML 结构断言——模板存在、绑定未丢、关键元素齐全（防"模板被删"回归）；
//   2) 行为验证——按生产模板逐元素构建 ListBox 行，喂 POCO 数据，验证容器生成、
//      绑定解析（日期文本/头像背景/Subject）——证明"有模板且数据流得进去"。
using System;
using System.IO;
using Shapes = Avalonia.Controls.Shapes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionGraphTooltipTests
	{
		// ===== 1) XAML 结构防回归 =====

		[Fact]
		public void TooltipXaml_HasRowTemplateBound_WithOriginalColumns()
		{
			string root = FindRepositoryRoot();
			string xaml = File.ReadAllText(Path.Combine(root,
				"src", "ForkPlus", "UI", "UserControls", "RevisionGraphTooltipUserControl.axaml"));

			// 修复核心：模板存在且绑回 RevisionListView（注释块内不会出现此绑定语句，
			// 断言天然只匹配生效的标记）。
			Assert.Contains("x:Key=\"TooltipRevisionRowTemplate\"", xaml);
			Assert.Contains("ItemTemplate=\"{StaticResource TooltipRevisionRowTemplate}\"", xaml);

			// 原版三列的关键元素：mini 轨道图（禁用嵌套 tooltip）、upstream 圆点、refs 徽章、
			// subject 高亮、头像、日期。缺失任何一个即回归到"部分空白"。
			Assert.Contains("controls:GraphCellView", xaml);
			Assert.Contains("ShowGraphToolTip=\"False\"", xaml);
			Assert.Contains("UpstreamStatusToBrushConverter", xaml);
			Assert.Contains("ReferencesDataTemplates", xaml);
			Assert.Contains("Path=\"Subject\"", xaml);
			Assert.Contains("controls:AvatarImage", xaml);
			Assert.Contains("Path=\"UserBackgroundBrush\"", xaml);
			Assert.Contains("Path=\"AuthorDateLongString\"", xaml);
		}

		// ===== 2) 行为验证：生产模板结构 + POCO 数据 → 行渲染非空 =====

		[Fact]
		public void RowTemplate_ProducesNonBlankRows_WithResolvedBindings()
		{
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 600, Height = 300 };
				var data = new[]
				{
					new TooltipRowData(),
					new TooltipRowData()
				};

				// —— 与生产 TooltipRevisionRowTemplate 逐元素对应的行模板 ——
				FuncDataTemplate<object> template = new FuncDataTemplate<object>(delegate(object item, INameScope nameScope)
				{
					var grid = new Grid { Height = 23, ClipToBounds = true };
					grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
					grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
					grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
					grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
					grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
					grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

					var graphCell = new GraphCellView { CellHeight = 23, Margin = new Thickness(4, 0, 0, 0), ShowGraphToolTip = false };
					Grid.SetColumn(graphCell, 0);
					grid.Children.Add(graphCell);

					var upstreamDot = new Shapes.Ellipse { Margin = new Thickness(4, 0, 4, 0), Width = 5, Height = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
					upstreamDot.Bind(Shapes.Ellipse.FillProperty, new Avalonia.Data.Binding("UpstreamStatus") { Converter = new RevisionUpstreamStatusToBrushConverter() });
					upstreamDot.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding("UpstreamStatus") { Converter = new RevisionUpstreamStatusToVisibilityConverter() });
					Grid.SetColumn(upstreamDot, 1);
					grid.Children.Add(upstreamDot);

					var refsControl = new ItemsControl { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true, MaxWidth = 360 };
					refsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("References"));
					Grid.SetColumn(refsControl, 2);
					grid.Children.Add(refsControl);

					var subjectText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis };
					subjectText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Subject"));
					Grid.SetColumn(subjectText, 3);
					grid.Children.Add(subjectText);

					var avatarBorder = new Border { Margin = new Thickness(0, 2, 0, 2), Width = 18, BorderThickness = new Thickness(1, 1, 0, 1), CornerRadius = new CornerRadius(3, 0, 0, 3) };
					avatarBorder.Bind(Border.BackgroundProperty, new Avalonia.Data.Binding("UserBackgroundBrush"));
					var avatar = new AvatarImage { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Center };
					avatar.Bind(AvatarImage.UserIdentityProperty, new Avalonia.Data.Binding("Author"));
					avatarBorder.Child = avatar;
					Grid.SetColumn(avatarBorder, 4);
					grid.Children.Add(avatarBorder);

					var dateText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
					dateText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("AuthorDateLongString"));
					Grid.SetColumn(dateText, 5);
					grid.Children.Add(dateText);

					return grid;
				});

				// —— 与生产 RevisionListView 相同的容器配置（Theme/ItemContainerTheme）——
				var listView = new NoUIAutomationListView();
				if (ResolveResource("ListViewWithGridViewStyle") is ControlTheme theme)
				{
					listView.Theme = theme;
				}
				if (ResolveResource("NoSelectionRevisionListViewItemStyle") is ControlTheme containerTheme)
				{
					listView.ItemContainerTheme = containerTheme;
				}
				listView.ItemTemplate = template;
				listView.ItemsSource = data;

				window.Content = listView;
				window.Show();
				window.UpdateLayout();
				Dispatcher.UIThread.RunJobs();

				// 行容器生成（模板生效，不再是空白列表）。
				bool twoRows = listView.ItemCount == 2
					&& listView.ContainerFromIndex(0) != null
					&& listView.ContainerFromIndex(1) != null;

				// 行内 mini 轨道图存在（GraphCellView 对非 DecoratedRevision 数据安全回退）。
				bool hasGraphCell = FindVisual<GraphCellView>(listView) != null;

				// 绑定解析：Subject 文本渲染（证明数据流入行模板）。
				bool subjectRendered = false;
				foreach (TextBlock textBlock in FindAllVisuals<TextBlock>(listView))
				{
					if (textBlock.Text == TooltipRowData.SubjectText)
					{
						subjectRendered = true;
						break;
					}
				}

				// 绑定解析：日期列文本（原第 3 列）。
				bool dateRendered = false;
				foreach (TextBlock textBlock in FindAllVisuals<TextBlock>(listView))
				{
					if (textBlock.Text == TooltipRowData.DateText)
					{
						dateRendered = true;
						break;
					}
				}

				// 绑定解析：头像背景（原第 2 列 UserBackgroundBrush）。
				bool avatarBackgroundResolved = false;
				foreach (Border border in FindAllVisuals<Border>(listView))
				{
					if (border.Width == 18 && border.Background == TooltipRowData.Brush)
					{
						avatarBackgroundResolved = true;
						break;
					}
				}

				window.Close();
				return twoRows && hasGraphCell && subjectRendered && dateRendered && avatarBackgroundResolved;
			});
			Assert.True(pass);
		}

		// ===== 测试数据（绑定路径与 DecoratedRevision 同名，POCO 即可让 Binding 解析）=====

		private sealed class TooltipRowData
		{
			public const string SubjectText = "Fix collapsed-node hover tooltip blank view";
			public const string DateText = "3 Sep 2026 10:00";
			public static readonly IBrush Brush = new SolidColorBrush(Colors.Orange);

			public string Subject => SubjectText;

			public string SubjectSearchString => null;

			public ActiveBranchCommitStatus UpstreamStatus => ActiveBranchCommitStatus.None;

			public ReferenceViewModel[] References => new ReferenceViewModel[0];

			public IBrush UserBackgroundBrush => Brush;

			public UserIdentity Author => null;

			public string AuthorDateLongString => DateText;
		}

		// ===== 助手 =====

		private static object ResolveResource(string key)
		{
			// headless App 继承真实 App.axaml（App.Current.Resources 含全部合并字典主题）。
			if (App.Current != null && App.Current.TryFindResource(key, ThemeVariant.Light, out object value))
			{
				return value;
			}
			return null;
		}

		private static T FindVisual<T>(Visual root) where T : Visual
		{
			foreach (Visual descendant in root.GetVisualDescendants())
			{
				if (descendant is T typed)
				{
					return typed;
				}
			}
			return null;
		}

		private static System.Collections.Generic.IEnumerable<T> FindAllVisuals<T>(Visual root) where T : Visual
		{
			foreach (Visual descendant in root.GetVisualDescendants())
			{
				if (descendant is T typed)
				{
					yield return typed;
				}
			}
		}

		private static string FindRepositoryRoot()
		{
			// 与 ClassCoverageManifestTests 相同的仓库根定位（找 .git，两仓布局都成立）。
			string directory = AppContext.BaseDirectory;
			while (!string.IsNullOrWhiteSpace(directory))
			{
				if (Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git")))
				{
					return directory;
				}
				directory = Path.GetDirectoryName(directory);
			}
			throw new DirectoryNotFoundException("Could not find repository root.");
		}
	}
}
