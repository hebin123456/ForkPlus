// 生产路径复现（2026-10-20，v5.1.5 "插件卸载确认弹窗点确认没有用，窗口直接跑下面去了"）：
// 嵌套模态场景——偏好设置窗口（ForkPlusDialogWindow，shim 模态 PushFrame）→ 插件页
// 「卸载选中」→ MessageBoxWindow.ShowDialog()（shim 模态，嵌套 PushFrame）。
//
// 修复前根因：ForkPlusDialogWindow 构造把 owner 硬登记为 MainWindow.Instance
// （SetOwnerCompat 显式通道），WindowDialogCompat.ShowDialog 的 owner 解析为
// TryGetOwner ?? ActiveWindow——显式登记的 MainWindow 永远赢，嵌套模态下弹窗 owner
// 是 MainWindow 而非父对话框（偏好设置窗口）：①父对话框未被禁用（模态失真）；
// ②弹窗不相对父对话框居中；③确认/取消关闭弹窗时 Avalonia 激活 owner（MainWindow），
// 主窗口冲到 Z 序最前把偏好设置窗口压到下面——用户视角"点确认没用，窗口跑下面去了"。
// 同款问题已在 ReflogWindow（ReflogJumpDialogModalCloseTests）/ConfigureSshKeysWindow/
// IssueTracker/ExternalTools 等处靠显式 SetOwnerCompat(this) 逐点补救，插件页是漏网之鱼。
//
// 修复：构造期登记降级为 SetDefaultOwnerCompat 弱兜底通道；ShowDialog shim owner 解析
// 改四级——显式登记 > 模态栈顶（shim 打开且仍可见的最内层父对话框）> 当前活动窗口 >
// 弱兜底。模态栈分支是关键：headless 无焦点系统（IsActive 恒 false，ActiveWindow 退化成
// "第一个可见窗口"=MainWindow），生产里窗口激活状态偶发缺失时同理——嵌套模态的父窗口
// 由模态嵌套关系决定，不应依赖焦点。本测试按生产时序复现：真 MainWindow → 父对话框经
// shim 模态打开（入栈）→ 嵌套 MessageBoxWindow（构造自动弱登记 MainWindow）→ 断言
// owner 解析落到父对话框（模态栈顶）、TryGetOwner 不再返回 MainWindow（修复前两处皆
// 红灯）→ 点确认后弹窗关闭且 shim 返回 true。
//
// 注意（ReflogJumpDialogModalCloseTests 同款铁律）：
//   1) 不能在模态存活期间用 Run/RunJobs 泵队列（会把 PushFrame 泵进当前调用栈造成
//      测试自身死锁）；Footer chrome 经 Dispatcher.Post 延迟装配，点击用自重投轮询等
//      就绪（全程在模态 frame 内执行）。
//   2) 不走 E2eMainWindowHarness.OpenRepository——本用例只关心窗口 owner 解析与
//      嵌套模态 frame 链，仓库后台数据加载（慢环境全量 git refresh 数秒、沙箱曾
//      60s 超时挂死）与本用例无关，直接 CreateWindow()（停留在仓库管理 tab）。
using System;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class NestedModalDialogOwnerTests
	{
		// ===== 轻量单测：ResolveDialogOwner 三级优先级的确定性语义 =====
		[Fact]
		public void ResolveDialogOwner_ExplicitOwnerBeatsDefaultOwner_InvisibleOwnerNotUsed()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Window plain = null;
			Window mainWindow = null;
			Window hidden = null;
			Window self = null;
			HeadlessAppBootstrap.Run(delegate
			{
				plain = new Window { Width = 100, Height = 80 };
				mainWindow = new Window { Width = 120, Height = 90 };
				hidden = new Window { Width = 100, Height = 80 }; // 不 Show——不可见
				plain.Show();
				mainWindow.Show();
				Dispatcher.UIThread.RunJobs();

				self = new Window();
				// 弱兜底登记 plain、显式登记 mainWindow：显式必须赢（ActiveWindow/弱兜底都抢不过）。
				self.SetDefaultOwnerCompat(plain);
				self.SetOwnerCompat(mainWindow);
				Assert.Same(mainWindow, WindowDialogCompat.ResolveDialogOwner(self));

				// 不可见的显式登记 owner 不得被采用（Avalonia ShowDialog(owner) 要求 owner
				// IsVisible=true，否则 InvalidOperationException——采用即启动期崩溃路径）。
				self.SetOwnerCompat(hidden);
				Window resolved = WindowDialogCompat.ResolveDialogOwner(self);
				Assert.NotSame(hidden, resolved);

				// 降级语义：弱兜底登记不进显式通道（只 SetDefaultOwnerCompat 时 TryGetOwner
				// 查不到，ResolveDialogOwner 仅在无显式登记且无活动窗口时才落到弱兜底）。
				// 用独立窗口避免上面显式登记的状态污染。
				Window weakSelf = new Window();
				weakSelf.SetDefaultOwnerCompat(plain);
				Assert.Null(WindowOwnerCompat.TryGetOwner(weakSelf));
				Assert.Same(plain, WindowOwnerCompat.TryGetDefaultOwner(weakSelf));
				weakSelf.Close();
			});
			// 收尾清理（Run 的 CloseLeftoverTestWindows 也会兜底，双保险）
			HeadlessAppBootstrap.Run(delegate
			{
				self?.Close();
				plain?.Close();
				mainWindow?.Close();
				hidden?.Close();
			});
		}

		// ===== 核心集成复现：嵌套模态下卸载确认弹窗 owner 解析 + 确认关闭链路 =====
		[Fact]
		public void UninstallConfirm_FromNestedModalDialog_OwnerResolvesToParentDialog_SubmitCloses()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var done = new ManualResetEvent(false);
			var result = new bool?[1];
			Exception flowError = null;

			var t = new Thread(delegate()
			{
				try
				{
					Dispatcher.UIThread.Invoke(delegate
					{
						MainWindow mainWindow = E2eMainWindowHarness.CreateWindow();
						ForkPlusDialogWindow parentDialog = null;
						MessageBoxWindow confirmBox = null;
						try
						{
							// 1) 生产保真：父对话框（模拟偏好设置窗口）= ForkPlusDialogWindow。
							//    构造自动 SetDefaultOwnerCompat(MainWindow.Instance)——修复后走弱通道。
							parentDialog = new ForkPlusDialogWindow();
							// 降级断言（修复前：构造 SetOwnerCompat 显式登记 mainWindow，非 null → 红灯）：
							Assert.Null(WindowOwnerCompat.TryGetOwner(parentDialog));
							Assert.Same(mainWindow, WindowOwnerCompat.TryGetDefaultOwner(parentDialog));

							// frame 内要做的事：等父对话框可见 → 生产构造确认框（自动弱登记）→
							// 断言 owner 解析 → 经 shim 模态打开确认框（嵌套 PushFrame frame B，
							// 与生产 UninstallButton_Click 的 new MessageBoxWindow().ShowDialog()
							// 同款同步阻塞调用）→ frame B 内轮询等 Footer 就绪 → 点「卸载」。
							Window parentRef = parentDialog;
							Window mwRef = mainWindow;
							Window[] resolvedRef = new Window[1];
							bool?[] boxResultRef = new bool?[1];
							int attempts = 0;
							Action RunNestedFlow = null;
							Action frameWork = null;
							frameWork = delegate
							{
								// Post 的 job 异常会被 Dispatcher 吞掉且没人再关父对话框 → frame A
								// 永不退出（测试挂死不可诊断）。整体捕获：异常写入 flowError 并
								// 关闭父对话框让 frame 链退出，由外层断言失败回显。
								try
								{
									RunNestedFlow();
								}
								catch (Exception e)
								{
									flowError = e;
									if (parentRef.IsVisible)
									{
										parentRef.Close();
									}
								}
							};
							RunNestedFlow = delegate
							{
								if (!parentRef.IsVisible)
								{
									if (attempts++ < 500)
									{
										Dispatcher.UIThread.Post(frameWork);
										return;
									}
									throw new InvalidOperationException("轮询 500 次父对话框仍不可见");
								}
								// 2) 生产路径构造确认框（PluginsUserControl.UninstallButton_Click 同款，
								//    无显式 owner——构造自动弱登记 MainWindow.Instance）。
								confirmBox = new MessageBoxWindow(
									"确认卸载",
									"将卸载 1 个插件（删除其插件目录），卸载后如需使用请重新安装。\n\n是否继续？",
									"卸载",
									"取消",
									showCancelButton: true,
									showWarningIcon: true);
								// 降级断言（修复前：构造显式登记 mainWindow → 非 null → 红灯复现）：
								Assert.Null(WindowOwnerCompat.TryGetOwner(confirmBox));
								Assert.Same(mwRef, WindowOwnerCompat.TryGetDefaultOwner(confirmBox));
								// owner 解析断言：嵌套模态下必须落到父对话框。headless 无焦点系统
								// （IsActive 恒 false，ActiveWindow 退化成第一个可见窗口=MainWindow），
								// 修复后由模态栈分支保证：父对话框经 shim ShowDialog 打开已入栈，
								// 栈顶=直接父窗口（修复前：TryGetOwner 显式通道恒返回 mainWindow）。
								Window resolved = WindowDialogCompat.ResolveDialogOwner(confirmBox);
								resolvedRef[0] = resolved;
								Assert.Same(parentRef, resolved);

								// frame B 内轮询等 Footer chrome 就绪后点「卸载」。
								int clickAttempts = 0;
								Action clickWhenReady = null;
								clickWhenReady = delegate
								{
									ForkPlusDialogFooter footer = confirmBox.GetVisualDescendants()
										.OfType<ForkPlusDialogFooter>().FirstOrDefault();
									Button submit = confirmBox.GetVisualDescendants()
										.OfType<Button>().FirstOrDefault(b => b.Name == "SubmitButton");
									if (footer == null || submit == null)
									{
										if (clickAttempts++ < 500)
										{
											Dispatcher.UIThread.Post(clickWhenReady);
											return;
										}
										throw new InvalidOperationException("轮询 500 次未等到确认框 Footer/SubmitButton");
									}
									Assert.True(submit.IsEnabled, "确认按钮应可点击");
									submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
								};
								Dispatcher.UIThread.Post(clickWhenReady);

								// 生产调用方式：确认框经阻塞 shim 模态打开（嵌套 PushFrame frame B）。
								// 点「卸载」→ OnSubmit → CloseWithOk → Close(true)（IsWindowModal 经
								// shim 注册表判定）→ frame B 退出 → 返回 true。
								boxResultRef[0] = confirmBox.ShowDialog();
								// 嵌套链完成：关闭父对话框让 frame A 退出（生产里用户随后关设置窗口）。
								parentRef.Close();
							};
							Dispatcher.UIThread.Post(frameWork);

							// 3) 生产调用方式：父对话框经阻塞 shim 模态打开（PushFrame frame A），
							//    嵌套确认框在 frame A 内再开 shim 模态（frame B）。frame 链全部退出
							//    后 shim 返回。父对话框由测试代码普通 Close() 关闭（模拟用户随后
							//    关掉设置窗口，非 CloseWithOk/Cancel）→ Avalonia ShowDialog<bool?>
							//    以 null 结果完成 → shim 返回 null；能返回即证明 A→B 嵌套 frame 链
							//    完整退栈、无 PushFrame 卡死。
							bool? shimResult = parentDialog.ShowDialog();
							Dispatcher.UIThread.RunJobs();

							result[0] = shimResult;
							string diagnostics = "shimResult=" + shimResult
								+ ", boxResult=" + boxResultRef[0]
								+ ", resolved=" + Describe(resolvedRef[0])
								+ ", parent=" + Describe(parentDialog)
								+ ", mw=" + Describe(mainWindow)
								+ ", confirmVisible=" + (confirmBox != null && confirmBox.IsVisible);
							Assert.True(shimResult == null, "父对话框普通 Close 后 shim 应以 null 结果返回：" + diagnostics);
							Assert.True(boxResultRef[0] == true, "点「卸载」后确认框 shim 必须返回 true：" + diagnostics);
							Assert.False(confirmBox.IsVisible, "点「卸载」后确认框必须已关闭：" + diagnostics);
							Assert.False(parentDialog.IsVisible, "父对话框必须已关闭：" + diagnostics);
						}
						finally
						{
							if (parentDialog != null && parentDialog.IsVisible)
							{
								parentDialog.Close();
							}
							// 生产铁律：主窗口不能 Close（Closed → lifetime.Shutdown 杀死 headless
							// App，殃及同片后续用例），收尾只摘除窗口。
							E2eMainWindowHarness.DetachWindow(mainWindow);
						}
					});
				}
				catch (Exception e)
				{
					flowError = e;
				}
				finally
				{
					done.Set();
				}
			});
			t.IsBackground = true;
			t.Start();

			Assert.True(done.WaitOne(60000), "嵌套模态流程挂起（死锁/未完成）" + (flowError != null ? "；flowError=" + flowError : ""));
			Assert.Null(flowError);
		}

		private static string Describe(Window window)
		{
			if (window == null)
			{
				return "<null>";
			}
			return window.GetType().Name + "#" + window.GetHashCode();
		}
	}
}
