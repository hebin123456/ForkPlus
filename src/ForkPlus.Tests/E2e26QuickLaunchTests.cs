// E2E 模块26（2026-09-07）：Quick Launch 快速启动（2 用例）。
// 起因（Linux 用户实测崩溃，2026-09-07）：快速启动里点击"切换工作区"（Switch Workspace，
// 带单参数 Workspace）→ SubmitSelectedItem → CommandTextBox.SetCommandDescriptor →
// PushSection 里 _textBox.Text = null → 经模板 TwoWay 绑定上传把外层 CommandTextBox.Text
// 也置 null → CommandArgumentChanged → RefreshCommandList 的 `.Text.Trim()` 直接
// NullReferenceException（未处理异常弹窗，进程崩）。
// 根因：WPF TextBox.Text 有 CoerceText(null→"") 强制转换，Avalonia 12 没有——同一迁移
// 语义坑的第三例（前两例：CommandTextBox/PlaceholderTextBox 构造期回填，见各自 Migration note）。
// 修复（双层）：① 根因层 PushSection 清空输入改写 string.Empty；② 防御层
// RefreshCommandList 的 filterString 改 (Text ?? string.Empty)。
// 用例1 = 用户路径回归（真实 MainWindow + 鼠标点击列表条目 = 用户截图同款 PointerReleased
// 路径），修复前必 NRE；用例2 = 单元级根因锁定（直构 CommandTextBox + SetCommandDescriptor
// 后 Text 必须非 null）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e26QuickLaunchTests
	{
		private const string ModuleDir = "26-quicklaunch";

		// ============================ 用例1：用户路径回归 ============================

		[Fact]
		public void QuickLaunch_ClickSwitchWorkspace_EntersArgumentState_WithoutNullTextCrash()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) 打开快速启动（生产构造器：owner=MainWindow.Instance，后台 Rescan 仓库列表）=====
						QuickLaunchWindow quickLaunch = new QuickLaunchWindow();
						quickLaunch.Show();
						Dispatcher.UIThread.RunJobs(); // Loaded → RefreshCommandList（默认命令列表装配）

						ListBox listBox = UiClick.Find<ListBox>(quickLaunch, "RepositoriesListBox");
						CommandTextBox commandTextBox = UiClick.Find<CommandTextBox>(quickLaunch, "CommandTextBox");

						// ===== 2) 默认列表应包含 Switch Workspace 条目（DefaultCommandProvider 全量命令）=====
						PaletteCommandItem switchItem = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							switchItem = (listBox.ItemsSource as CommandProviderItem[])
								?.OfType<PaletteCommandItem>()
								.FirstOrDefault(delegate (PaletteCommandItem x)
								{
									return x.Command.Name == "Switch Workspace";
								});
							return switchItem != null;
						}), "默认命令列表应包含 Switch Workspace 条目");

						// ===== 3) 用户路径：鼠标点击列表条目（PointerReleased → SubmitSelectedItem →
					//    SetCommandDescriptor → PushSection + CommandArgumentChanged → RefreshCommandList）。
					//    修复前：PushSection 把 Text 写 null → RefreshCommandList 的 .Text.Trim() NRE，
					//    异常从 RaiseEvent 直接抛出本用例即挂（与 Linux 用户截图堆栈一致）。=====
					// 虚拟化：默认命令列表远超 620×370 窗口的可视行数，条目在视口外不会物化容器——
					// 先 ScrollIntoView 触发物化，再轮询等容器就绪（E2e14 ItemCheckBoxOf 同模式）。
					listBox.ScrollIntoView(switchItem);
					ListBoxItem container = null;
					Assert.True(UiClick.WaitFor(delegate
					{
						container = (ListBoxItem)listBox.ContainerFromItem(switchItem);
						return container != null;
					}), "Switch Workspace 条目应已生成 ListBoxItem 容器（ScrollIntoView 后）");
					// 真实点击的按压阶段由 Avalonia 内建选中逻辑驱动（依赖输入设备 pointer capture，
					// headless RaiseEvent 不经过该管线）；此处程序化设 SelectedItem 复现按压后的选中态
					// （本工程既有模式，见 E2e23 WorkspacesListBox），再经 UiClick.Press 走
					// PointerReleased 生产管线——与用户截图堆栈同款路径。赋值与 Press 之间不插入
					// RunJobs，避免后台 Rescan 回发的 RefreshCommandList 把选中重置回首行。
					listBox.SelectedItem = switchItem;
					UiClick.Press(container, quickLaunch, new Point(5.0, 5.0));
					Dispatcher.UIThread.RunJobs();

						// ===== 4) 断言：进入参数输入态且未崩 =====
						Assert.True(commandTextBox.CommandDescriptor != null,
							"点击后应进入 Switch Workspace 的参数输入态（CommandDescriptor 就位）");
						Assert.True(commandTextBox.Text != null,
							"根因断言：PushSection 清空输入后外层 Text 必须是 string.Empty 而非 null"
							+ "（WPF CoerceText(null→\"\") 语义；null 会在 RefreshCommandList 的 .Text.Trim() 处 NRE）");
						Assert.Equal(string.Empty, commandTextBox.Text);

						// ===== 5) 断言：参数列表已切换到 WorkspaceCommandProvider（Workspaces 分组头 + 工作区条目）=====
						CommandProviderItem[] argumentItems = listBox.ItemsSource as CommandProviderItem[];
						Assert.True(argumentItems != null && argumentItems.Length > 0
							&& argumentItems[0] is HeaderCommandProviderItem,
							"参数态列表首项应为 Workspaces 分组头（WorkspaceCommandProvider）");
						Assert.True(argumentItems.OfType<WorkspaceItem>().Any(),
							"参数态列表应包含工作区条目（默认 Home/Work）");

						ScreenshotHelper.Snap(quickLaunch, "01-quicklaunch-switchworkspace-argument", ModuleDir);
					}
					finally
					{
						// 收尾：快速启动窗必须显式关（Deactivated→CloseWindow 在 headless 下不保证触发，
						// 泄漏进 lifetime.Windows 会殃及后续用例的窗口枚举断言）
						try
						{
							QuickLaunchWindow leftover = WpfApp.Windows.OfType<QuickLaunchWindow>().FirstOrDefault();
							leftover?.Close();
							Dispatcher.UIThread.RunJobs();
						}
						catch
						{
							// 关窗尽力而为，不掩盖测试断言
						}
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 用例2：单元级根因锁定 ============================

		[Fact]
		public void CommandTextBox_SetCommandDescriptor_PushSectionKeepsTextNonNull()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// 直构宿主窗承载 CommandTextBox：Show + RunJobs 后模板应用
				// （PART_LabelsStackPanel / PART_PlaceholderTextBox 就位，PushSection 才有写入目标）
				Window host = new Window
				{
					Width = 600.0,
					Height = 200.0,
					Content = new CommandTextBox()
				};
				host.Show();
				Dispatcher.UIThread.RunJobs();

				CommandTextBox commandTextBox = host.Content as CommandTextBox;
				Assert.True(commandTextBox != null, "宿主窗内应有 CommandTextBox");
				Assert.Equal(string.Empty, commandTextBox.Text); // 构造期回填（既有 Migration note 行为）

				// 带 Workspace 参数的命令（复用 SwitchWorkspaceCommand 的 PublicCommands[0]）
				commandTextBox.SetCommandDescriptor(SwitchWorkspaceCommand.PublicCommands[0]);
				Dispatcher.UIThread.RunJobs();

				// 根因断言：PushSection 清空内层输入时曾写 null，经模板 TwoWay 绑定
				// （PlaceholderTextBox.Text ← TemplatedParent.Text，TextBox.Text 默认 TwoWay）
				// 上传使外层 Text=null。必须保持 string.Empty。
				Assert.True(commandTextBox.Text != null,
					"SetCommandDescriptor 后 Text 必须非 null（PushSection 应写 string.Empty；"
					+ "null 经模板 TwoWay 绑定上传会让 QuickLaunch RefreshCommandList 崩溃）");
				Assert.Equal(string.Empty, commandTextBox.Text);
				Assert.True(commandTextBox.CommandDescriptor != null, "应处于参数输入态");

				host.Close();
				Dispatcher.UIThread.RunJobs();
			});
		}
	}
}
