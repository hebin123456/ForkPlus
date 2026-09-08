// 回归测试（2026-09-07，"自定义 git-mm 实例的下拉框在弹出选择文件的资源管理器后没法消失，
// 只有资源管理器关了才消失，自定义 git 实例的下拉框也是，类似的框都修复一下；而且
// linux/mac 版本的 git-mm 都是 git-mm 这个名字，但是选择弹出的那个资源管理器找不到这个文件"）：
//   1) 下拉收起：三个实例下拉（git / git-mm / git-ai）的 SelectionChanged 处理器在弹文件
//      对话框前必须显式 IsDropDownOpen=false。根因：处理器内同步阻塞弹对话框（StorageProvider
//      经 PushFrame 嵌套消息循环），ComboBox 自己"点击项后收起下拉"的处理排在本次事件之后——
//      阻塞期间下拉恒开；Linux/macOS 的 portal 对话框是独立进程，不夺走本窗口焦点，
//      失焦自动收起也不触发。程序化选中不走 Avalonia 的点击收起路径，只有修复的
//      显式 IsDropDownOpen=false 会让下拉闭合（headless 存储提供器 Noop → 对话框立即取消）。
//   2) 可执行文件过滤器：Linux/macOS 可执行文件无扩展名（git-mm/git-ai/git 本体），
//      *.exe 类 glob 在 portal/NSOpenPanel 上把它们全部滤掉——非 Windows 平台必须无过滤器。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GitInstanceDropdownTests
	{
		[Fact]
		public void AddCustomSelection_ClosesDropdownBeforeFileDialog()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new PreferencesWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					var git = window.GitUserControl;
					AssertDropdownClosesOnAddCustom(git.GitMmInstanceComboBox);
					AssertDropdownClosesOnAddCustom(git.GitAiInstanceComboBox);
					// git 实例下拉：CI 环境（forkgitinstance env var 兜底注入）下禁用（生产行为），
					// 禁用时跳过；启用时同样断言（三处处理器同一修复模式）。
					if (git.GitInstanceComboBox.IsEnabled)
					{
						AssertDropdownClosesOnAddCustom(git.GitInstanceComboBox);
					}
				}
				finally
				{
					window.Close();
				}
			});
		}

		private static void AssertDropdownClosesOnAddCustom(ComboBox comboBox)
		{
			var items = comboBox.ItemsSource.Cast<GitUserControl.GitInstanceItem>().ToArray();
			var addCustom = items.FirstOrDefault(x => x.GitInstanceType == GitUserControl.GitInstanceType.AddCustom);
			Assert.NotNull(addCustom);

			comboBox.IsDropDownOpen = true;
			Dispatcher.UIThread.RunJobs();
			Assert.True(comboBox.IsDropDownOpen, "前置：下拉应处于打开状态");

			// 程序化选中"添加自定义实例..."（生产：用户在下拉里点击该项）→
			// SelectionChanged → headless 存储提供器 Noop → 对话框立即取消 → SelectedItem 复原
			comboBox.SelectedItem = addCustom;
			Dispatcher.UIThread.RunJobs();

			Assert.False(comboBox.IsDropDownOpen,
				"选择'添加自定义实例'后下拉必须收起（修复前保持打开直到文件对话框关闭）");
		}

		[Fact]
		public void ExecutableFilter_NullOnUnix_ExeOnWindows()
		{
			// 非可执行过滤不受影响；可执行类过滤器在 Unix 退化为 null（无过滤器）
			if (OperatingSystem.IsWindows())
			{
				Assert.Equal("*.exe", OpenDialog.ExecutableFilterOrNullOnUnix("*.exe"));
				Assert.Equal("*.exe; *.cmd", OpenDialog.ExecutableFilterOrNullOnUnix("*.exe; *.cmd"));
			}
			else
			{
				Assert.Null(OpenDialog.ExecutableFilterOrNullOnUnix("*.exe"));
				Assert.Null(OpenDialog.ExecutableFilterOrNullOnUnix("*.bat; *.exe; *.cmd"));
			}
		}

		[Fact]
		public void BuildFileTypes_SplitsSemiColonPatterns_AndNullSpecMeansNoFilter()
		{
			// 多模式串 "*.a; *.b" 必须拆成独立 Pattern（整串单个 glob 恒不匹配）
			var multi = global::ForkPlus.UI.StorageProviderDialogs.BuildFileTypes(
				new[] { ("Applications", "*.exe; *.cmd") });
			Assert.NotNull(multi);
			Assert.Single(multi);
			Assert.Equal(new[] { "*.exe", "*.cmd" }, multi[0].Patterns);

			// 空模式 → 无过滤器（Unix 选择无扩展名可执行文件）
			Assert.Null(global::ForkPlus.UI.StorageProviderDialogs.BuildFileTypes(
				new[] { ("Applications", (string)null) }));
			Assert.Null(global::ForkPlus.UI.StorageProviderDialogs.BuildFileTypes(null));
		}
	}
}
