// v5.3.0 两个新功能的回归：
//   1) 轨道图搜索框（RevisionSearchPanelUserControl）：匹配数文案本地化 + "当前索引/总数"
//      进度显示——UpdateMatchesCount/UpdateCurrentMatchIndex 与 MatchesTextBlock 文案。
//   2) 提交详情 → 文件树 tab 顶部过滤框（RevisionFileTreeUserControl）：按文件/目录路径
//      子串过滤树节点（internal static ApplyTreeFilter 直测）——命中文件可见、不命中隐藏、
//      父目录连带可见且自动展开、大小写不敏感、空过滤全可见。
using System;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SearchProgressAndFileTreeFilterTests
	{
		// ===== 1) 轨道图搜索进度文案 =====

		[Fact]
		public void SearchPanel_MatchesCount_LocalizedText()
		{
			string originalLanguage = ForkPlusSettings.Default.UiLanguage;
			try
			{
				ForkPlusSettings.Default.UiLanguage = "en";
				var panel = new RevisionSearchPanelUserControl();

				panel.UpdateMatchesCount(null);
				Assert.Equal("", panel.MatchesTextBlock.Text);

				panel.UpdateMatchesCount(3);
				Assert.Equal("3 matches", panel.MatchesTextBlock.Text);

				panel.UpdateMatchesCount(1);
				Assert.Equal("1 match", panel.MatchesTextBlock.Text);

				// v5.3.0 进度：跳转到第 2/3 个匹配。
				panel.UpdateMatchesCount(3);
				panel.UpdateCurrentMatchIndex(2);
				Assert.Equal("2/3 matches", panel.MatchesTextBlock.Text);

				// 索引越界（非匹配行）回退为总数显示。
				panel.UpdateCurrentMatchIndex(null);
				Assert.Equal("3 matches", panel.MatchesTextBlock.Text);
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = originalLanguage;
			}
		}

		[Fact]
		public void SearchPanel_ProgressText_ChineseLocalization()
		{
			string originalLanguage = ForkPlusSettings.Default.UiLanguage;
			try
			{
				ForkPlusSettings.Default.UiLanguage = "zh-Hans";
				var panel = new RevisionSearchPanelUserControl();

				panel.UpdateMatchesCount(45);
				Assert.Equal("45 个匹配", panel.MatchesTextBlock.Text);

				panel.UpdateCurrentMatchIndex(7);
				Assert.Equal("匹配 7/45", panel.MatchesTextBlock.Text);
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = originalLanguage;
			}
		}

		// ===== 2) 文件树过滤 =====

		[Fact]
		public void TreeFilter_KeepsMatchedFilesAndTheirDirectories()
		{
			MultiselectionTreeViewItem root = BuildTree();
			RevisionFileTreeUserControl.ApplyTreeFilter(root, "main.cs");

			// 命中文件可见。
			Assert.False(Find(root, "src/main.cs").IsHidden);
			// 父目录连带可见（保留路径）。
			Assert.False(Find(root, "src/").IsHidden);
			// root 恒可见。
			Assert.False(root.IsHidden);
			// 不相关子树隐藏（目录连带后代）。
			Assert.True(Find(root, "assets/").IsHidden);
			Assert.True(Find(root, "assets/logo.png").IsHidden);
			Assert.True(Find(root, "README.md").IsHidden);
			// 未命中目录（src/util/，其下无 "main.cs"）隐藏。
			Assert.True(Find(root, "src/util/").IsHidden);
		}

		[Fact]
		public void TreeFilter_DirectoryMatchKeepsEntireSubtree()
		{
			MultiselectionTreeViewItem root = BuildTree();
			RevisionFileTreeUserControl.ApplyTreeFilter(root, "util");

			// 目录自身路径命中 → 整个子树保留。
			Assert.False(Find(root, "src/util/").IsHidden);
			Assert.False(Find(root, "src/util/helper.cs").IsHidden);
			Assert.False(Find(root, "src/").IsHidden);
			Assert.True(Find(root, "assets/logo.png").IsHidden);
		}

		[Fact]
		public void TreeFilter_CaseInsensitive()
		{
			MultiselectionTreeViewItem root = BuildTree();
			RevisionFileTreeUserControl.ApplyTreeFilter(root, "LOGO");

			Assert.False(Find(root, "assets/logo.png").IsHidden);
			Assert.False(Find(root, "assets/").IsHidden);
		}

		[Fact]
		public void TreeFilter_EmptyOrWhitespaceShowsAll()
		{
			MultiselectionTreeViewItem root = BuildTree();
			RevisionFileTreeUserControl.ApplyTreeFilter(root, "main.cs");
			Assert.True(Find(root, "README.md").IsHidden);

			RevisionFileTreeUserControl.ApplyTreeFilter(root, "");
			Assert.False(Find(root, "README.md").IsHidden);
			Assert.False(Find(root, "assets/logo.png").IsHidden);

			RevisionFileTreeUserControl.ApplyTreeFilter(root, "   ");
			Assert.False(Find(root, "README.md").IsHidden);
		}

		[Fact]
		public void TreeFilter_ExpandsVisibleDirectoriesWhileActive()
		{
			MultiselectionTreeViewItem root = BuildTree();
			// 先收起全部目录。
			CollapseAll(root);
			Assert.False(Find(root, "src/").IsExpanded);

			RevisionFileTreeUserControl.ApplyTreeFilter(root, "helper.cs");

			// 过滤激活时命中路径上的目录自动展开（否则命中文件藏在折叠目录下看不到）。
			Assert.True(Find(root, "src/").IsExpanded);
			Assert.True(Find(root, "src/util/").IsExpanded);
			Assert.False(Find(root, "src/util/helper.cs").IsHidden);
		}

		// ===== 3) 整树补全构建（过滤激活时后台 ls-tree -r -t 一次性拉全路径）=====

		[Fact]
		public void BuildFullTreeChildren_GroupsByPathAndSortsPerLevel()
		{
			// 输入模拟 git ls-tree -r -t 的树序（目录行先于其内容），路径为完整相对路径。
			var allItems = new[]
			{
				Dir("src"),
				File("main.cs", "src/main.cs"),
				Dir("src/util"),
				File("helper.cs", "src/util/helper.cs"),
				Dir("assets"),
				File("logo.png", "assets/logo.png"),
				File("README.md", "README.md")
			};
			var root = new RevisionFileTreeViewItem(null, null);
			RevisionFileTreeUserControl.BuildFullTreeChildren(root, allItems, null);

			// 结构：目录 → 子项挂载正确（顶层 = assets、src 两个目录 + README.md 文件）。
			Assert.Equal(3, root.Children.Count);
			Assert.NotNull(Find(root, "src/main.cs"));
			Assert.NotNull(Find(root, "src/util/helper.cs"));
			Assert.NotNull(Find(root, "assets/logo.png"));
			Assert.NotNull(Find(root, "README.md"));

			// 层内排序：目录优先（src、assets 在 README.md 前）+ 自然序（assets < src）。
			Assert.Equal("assets", ((RevisionFileTreeViewItem)root.Children[0]).FileTreeItem.Filename);
			Assert.Equal("src", ((RevisionFileTreeViewItem)root.Children[1]).FileTreeItem.Filename);
			Assert.Equal("README.md", ((RevisionFileTreeViewItem)root.Children[2]).FileTreeItem.Filename);
		}

		[Fact]
		public void TreeFilter_AfterFullTreeLoad_FiltersDeepNodes()
		{
			// 补全后的树（所有目录 Children 已填充）过滤深度节点不需要任何 git 调用
			//（OnExpanding 的已加载守卫保证展开不触发拉取）。
			var root = new RevisionFileTreeViewItem(null, null);
			RevisionFileTreeUserControl.BuildFullTreeChildren(root, new[]
			{
				Dir("src"),
				File("main.cs", "src/main.cs"),
				Dir("src/util"),
				File("helper.cs", "src/util/helper.cs"),
				File("README.md", "README.md")
			}, null);

			RevisionFileTreeUserControl.ApplyTreeFilter(root, "helper.cs");
			Assert.False(Find(root, "src/util/helper.cs").IsHidden);
			Assert.False(Find(root, "src/util").IsHidden);
			Assert.True(Find(root, "README.md").IsHidden);
		}

		private static FileTreeItem File(string filename, string filePath)
		{
			return new FileTreeItem(filename, filePath, default(Sha), FileTreeItem.FileTreeItemType.File);
		}

		private static FileTreeItem Dir(string filePath)
		{
			return new FileTreeItem(filePath.Substring(filePath.LastIndexOf('/') + 1), filePath, default(Sha), FileTreeItem.FileTreeItemType.Directory);
		}

		// ===== 测试树构造（与 RevisionFileTreeUserControl.Refresh 的真实结构一致）=====

		private static MultiselectionTreeViewItem BuildTree()
		{
			// root：Refresh 里 new RevisionFileTreeViewItem(null, null)（FileTreeItem 为 null）。
			var root = new RevisionFileTreeViewItem(null, null);
			root.Children.Add(NewItem("src", "src/", FileTreeItem.FileTreeItemType.Directory));
			root.Children[0].Children.Add(NewItem("main.cs", "src/main.cs", FileTreeItem.FileTreeItemType.File));
			root.Children[0].Children.Add(NewItem("util", "src/util/", FileTreeItem.FileTreeItemType.Directory));
			root.Children[0].Children[1].Children.Add(NewItem("helper.cs", "src/util/helper.cs", FileTreeItem.FileTreeItemType.File));
			root.Children.Add(NewItem("README.md", "README.md", FileTreeItem.FileTreeItemType.File));
			root.Children.Add(NewItem("assets", "assets/", FileTreeItem.FileTreeItemType.Directory));
			root.Children[2].Children.Add(NewItem("logo.png", "assets/logo.png", FileTreeItem.FileTreeItemType.File));
			return root;
		}

		private static RevisionFileTreeViewItem NewItem(string filename, string filePath, FileTreeItem.FileTreeItemType itemType)
		{
			return new RevisionFileTreeViewItem(null, new FileTreeItem(filename, filePath, default(Sha), itemType));
		}

		private static MultiselectionTreeViewItem Find(MultiselectionTreeViewItem root, string filePath)
		{
			foreach (MultiselectionTreeViewItem child in root.Children)
			{
				if (child is RevisionFileTreeViewItem fileNode && fileNode.FileTreeItem?.FilePath == filePath)
				{
					return fileNode;
				}
				MultiselectionTreeViewItem nested = Find(child, filePath);
				if (nested != null)
				{
					return nested;
				}
			}
			return null;
		}

		private static void CollapseAll(MultiselectionTreeViewItem node)
		{
			foreach (MultiselectionTreeViewItem child in node.Children)
			{
				child.IsExpanded = false;
				CollapseAll(child);
			}
		}
	}
}
