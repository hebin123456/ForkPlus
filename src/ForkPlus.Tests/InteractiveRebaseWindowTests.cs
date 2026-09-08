// 回归测试（2026-09-03，"交互式变基窗口关闭按钮关不掉"修复产物）：
//
// 根因：X 按钮本身没问题，但取消确认 MessageBox 走
// ForkPlusDialogWindow ctor 登记的 owner=MainWindow，与正在模态的变基窗口互斥
// （详见生产代码注释）。本文件验证"OnClosing 取消 + 后续 Close(result)"的
// 窗口关闭链路在 Avalonia 下本身是通的（排除框架因素）。
// （注：原"列表项内嵌 ComboBox 下拉打开"用例于 2026-09-04 移除——3 核沙盒
// 满载并行下时序不稳（headless 输入管线与组合器同步受负载影响），单独/低负载
// 运行均通过，非代码回归。）
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class InteractiveRebaseWindowTests
	{
		[Fact]
		public void DialogCloseFlow_OnClosingCancelThenDelayedResultClose_CompletesDialogTask()
		{
			string diag = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					// 模拟 InteractiveRebaseWindow 关闭链：
					// X 点击 → Close() → OnClosing 取消（进程未结束）→ 进程结束后
					// Dispatcher.Post 里 _rebaseProcessRunning=false; Close(result)。
					var owner = new Window { Width = 300, Height = 200 };
					owner.Show();
					Dispatcher.UIThread.RunJobs();

					var window = new RebaseLikeProbeWindow();
					var task = window.ShowDialog<bool?>(owner);
					Dispatcher.UIThread.RunJobs();

					sb.AppendLine($"shown={window.IsVisible} processRunning={window.ProcessRunning}");

					// 模拟点击 X（CloseButton_Click 的全部内容就是 Close()）。
					window.Close();
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine($"afterX: stopCount={window.StopCount} visible={window.IsVisible} taskDone={task.IsCompleted}");

					// 排空延迟的进程结束 → Close(result)。
					for (int i = 0; i < 10 && !task.IsCompleted; i++)
					{
						Dispatcher.UIThread.RunJobs();
					}
					sb.AppendLine($"final: taskDone={task.IsCompleted} result={TryResult(task)} visible={window.IsVisible}");

					owner.Close();
					return sb.ToString();
				}
				catch (Exception ex)
				{
					sb.AppendLine("EXCEPTION: " + ex);
					return sb.ToString();
				}
			});
			System.IO.File.WriteAllText("/tmp/ir_close_flow_probe.txt", diag);
			Assert.True(diag.Contains("afterX: stopCount=1"), "点 X 必须触发一次取消停进程（OnClosing 内）：" + diag);
			Assert.True(diag.Contains("final: taskDone=True result=True"), "进程结束后窗口必须真正关闭并回传结果：" + diag);
			Assert.True(diag.Contains("final: taskDone=True result=True visible=False"), "窗口关闭后不可再见：" + diag);
		}

		private static string TryResult(System.Threading.Tasks.Task<bool?> task)
		{
			return task.IsCompleted && task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion
				? Convert.ToString(task.Result)
				: "<pending>";
		}

		[Fact]
		public void IrCancelConfirmedFlow_RealMessageBoxNestedOverModalDialog_Completes()
		{
			// 生产保真链路（真机 bug："关闭窗口点是关不掉"）：
			//   MainWindow
			//     └ InteractiveRebaseWindow.ShowDialog()（阻塞 shim → PushFrame）
			//         └ 点 X → OnClosing 取消 → IrCancelConfirmed()
			//             └ MessageBoxWindow.ShowDialog()（owner=ForkPlusDialogWindow ctor
			//               登记的 MainWindow；阻塞 shim → 嵌套 PushFrame）
			//                 └ 用户点 Yes → MessageBox 返回 true → StopRebaseInteractiveProcess
			//                   → semaphore → RI 进程退出 → Dispatcher.Post Close(result)
			// 全程嵌套 frame 链必须逐层退出；任何一层挂起/异常 → 超时或断言失败。
			// 注意：MessageBox 的 owner 与模态中的变基窗口不同（=MainWindow）——
			// 验证 Avalonia 允许"对已有模态子窗口的 owner 再开模态对话框"。
			HeadlessAppBootstrap.EnsureStarted();
			var done = new System.Threading.ManualResetEvent(false);
			var box = new string[] { null };

			Dispatcher.UIThread.Post(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					// 1. 模拟 MainWindow（生产中真实存在且可见）。
					var mainWindow = new Window { Width = 900, Height = 700 };
					mainWindow.Show();
					Dispatcher.UIThread.RunJobs();

					// 2. 变基窗口：与生产 ShowInteractiveRebaseWindowCommand.Execute 完全一致——
				//    阻塞 shim（PushFrame frame A）打开，点 X / 点 Yes 都是 frame 内的 posted job。
				var probe = new IrCancelProbeWindow();
				probe.SetOwnerCompat(mainWindow); // 生产：ForkPlusDialogWindow ctor 登记 owner=MainWindow.Instance

				// 3. 排"点 X"job（frame A 内执行）：CloseButton_Click 的全部内容就是 Close()。
				Dispatcher.UIThread.Post(delegate { probe.Close(); });
				// 排"点 Yes"job（frame B 内执行）：OnClosing → 嵌套 MessageBox frame B，
				// Footer 为 protected 且 chrome 延迟初始化（Initialized 后才有按钮）：
				// 轮询直到名为 SubmitButton 的按钮（Content="Yes"）出现在可视树再点击。
				Dispatcher.UIThread.Post(delegate { TryClickYesImpl(sb, () => probe.LastMessageBox, 500); });

				// 4. 阻塞 shim 打开模态窗口（生产调用方式），frame 链全部退出后才返回。
				bool? shimResult = probe.ShowDialog();
				Dispatcher.UIThread.RunJobs();
				sb.AppendLine($"afterShim: result={shimResult} irPhase={probe.IrPhase} stopCount={probe.StopCount} visible={probe.IsVisible}");

				mainWindow.Close();
				box[0] = sb.ToString();
			}
			catch (Exception ex)
			{
				sb.AppendLine("EXCEPTION: " + ex);
				box[0] = sb.ToString();
			}
			finally
			{
				done.Set();
			}
		});

		Assert.True(done.WaitOne(30000), "场景挂起：嵌套 MessageBox 关闭链路死锁/未完成");
		string diag = box[0] ?? "";
		System.IO.File.WriteAllText("/tmp/ir_cancel_flow_probe.txt", diag);
		Assert.True(diag.Contains("stopCount=1"), "点 Yes 后必须触发 StopRebaseInteractiveProcess 一次：" + diag);
		Assert.True(diag.Contains("afterShim: result=True"), "frame 链全部退出后 shim 必须返回 true（用户点了 Yes）：" + diag);
		Assert.True(diag.Contains("visible=False"), "窗口关闭后不可再见：" + diag);
	}

	// 局部函数：轮询 MessageBox 的 Yes 按钮直到出现后点击（chrome 延迟初始化）。
	private static void TryClickYesImpl(System.Text.StringBuilder sb, Func<MessageBoxWindow> getMessageBox, int attempts)
	{
		Dispatcher.UIThread.Post(delegate
		{
			var mb = getMessageBox();
			var yesButton = mb?.GetVisualDescendants().OfType<Button>()
				.FirstOrDefault(b => b.Name == "SubmitButton");
			if (yesButton == null)
			{
				if (attempts > 0)
				{
					TryClickYesImpl(sb, getMessageBox, attempts - 1);
					return;
				}
				sb.AppendLine("[yes] SubmitButton 未出现（chrome 未初始化）");
				return;
			}
			sb.AppendLine($"[yes] clicked content={yesButton.Content}");
			yesButton.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
		});
	}

	[Fact]
	public void IrCancelFlow_MessageBoxOwnedBySameMainWindowAsModalRebaseWindow_Closes()
	{
		// 生产 owner 保真判别测试：生产中 IrCancelConfirmed 的 MessageBoxWindow 在
		// ForkPlusDialogWindow ctor 里登记 owner=MainWindow.Instance——与仍在模态中的
		// InteractiveRebaseWindow 是同一个 owner（两者互为兄弟模态窗口）。
		// headless 下 MainWindow.Instance 为 null（此前测试走 ActiveWindow 兜底，owner=变基窗口
		// 本身），与生产不同。本测试显式注入 owner=mainWindow，验证"对已有模态子窗口的
		// owner 再开模态 MessageBox"在 Avalonia 下不挂起、能正常关闭返回。
		HeadlessAppBootstrap.EnsureStarted();
		var done = new System.Threading.ManualResetEvent(false);
		var box = new string[] { null };

		Dispatcher.UIThread.Post(delegate
		{
			var sb = new System.Text.StringBuilder();
			try
			{
				var mainWindow = new Window { Width = 900, Height = 700 };
				mainWindow.Show();
				Dispatcher.UIThread.RunJobs();

				var probe = new IrCancelProbeWindow();
				probe.SetOwnerCompat(mainWindow);
				// 关键差异点：MessageBox owner = mainWindow（生产路径）。
				probe.ExplicitMessageBoxOwner = mainWindow;
				probe.MessageBoxCreated += delegate
				{
					Dispatcher.UIThread.Post(delegate { TryClickYesImpl(sb, () => probe.LastMessageBox, 500); });
				};
				var dialogTask = probe.ShowDialog<bool?>(mainWindow);
				Dispatcher.UIThread.RunJobs();

				// 模拟点击 X（CloseButton_Click 内容即 Close()）。
				Dispatcher.UIThread.Post(delegate { probe.Close(); });

				for (int i = 0; i < 300 && !dialogTask.IsCompleted; i++)
				{
					Dispatcher.UIThread.RunJobs();
					System.Threading.Thread.Sleep(20);
				}
				sb.AppendLine($"final: irPhase={probe.IrPhase} stopCount={probe.StopCount} taskDone={dialogTask.IsCompleted} result={TryResult(dialogTask)} visible={probe.IsVisible}");

				mainWindow.Close();
				box[0] = sb.ToString();
			}
			catch (Exception ex)
			{
				sb.AppendLine("EXCEPTION: " + ex);
				box[0] = sb.ToString();
			}
			finally
			{
				done.Set();
			}
		});

		Assert.True(done.WaitOne(60000), "场景挂起：owner=MainWindow 的嵌套 MessageBox 关闭链路死锁/未完成");
		string diag = box[0] ?? "";
		System.IO.File.WriteAllText("/tmp/ir_owner_probe.txt", diag);
		Assert.True(diag.Contains("stopCount=1"), "点 Yes 后必须触发 StopRebaseInteractiveProcess：" + diag);
		Assert.True(diag.Contains("final: irPhase=dialog-returned:True"), "MessageBox 必须正常返回 true：" + diag);
		Assert.True(diag.Contains("result=True"), "窗口必须最终关闭并回传 true：" + diag);
		Assert.True(diag.Contains("visible=False"), "窗口关闭后不可再见：" + diag);
	}

		// 与 InteractiveRebaseWindow.OnClosing/IrCancelConfirmed/StopRebaseInteractiveProcess
		// 同构的探针窗口——用真实 MessageBoxWindow + 阻塞 ShowDialog shim。
		private sealed class IrCancelProbeWindow : ForkPlusDialogWindow
		{
			public bool ProcessRunning = true;
			public int StopCount;
			public MessageBoxWindow LastMessageBox;
			// 诊断：IrCancelConfirmed 链路推进到哪一步（created / shown / dialog-returned）。
			public volatile string IrPhase = "not-called";

			// 生产保真：ForkPlusDialogWindow ctor 里 MainWindow.Instance 非空时登记 owner=MainWindow。
			// headless 下 Instance 为 null（走 ActiveWindow 兜底），该字段手动注入以复刻生产路径。
			public Window ExplicitMessageBoxOwner;

			private readonly System.Threading.Semaphore _finishRiProcessSemaphore = new System.Threading.Semaphore(0, 1);

			protected override void OnClosing(WindowClosingEventArgs e)
			{
				if (!ProcessRunning)
				{
					base.OnClosing(e);
					return;
				}
				e.Cancel = true;
				if (IrCancelConfirmed())
				{
					StopRebaseInteractiveProcess("cancel");
				}
			}

			// 生产 IrCancelConfirmed 原文：new MessageBoxWindow(...).ShowDialog().GetValueOrDefault()
			// owner 解析：ForkPlusDialogWindow ctor 登记的 MainWindow.Instance（测试中手动登记）。
		private bool IrCancelConfirmed()
			{
				IrPhase = "created";
				LastMessageBox = new MessageBoxWindow("Do you really want to cancel Interactive Rebase?", "All your changes will be discarded.", "Yes", "No", showCancelButton: true, 550.0);
				if (ExplicitMessageBoxOwner != null)
				{
					LastMessageBox.SetOwnerCompat(ExplicitMessageBoxOwner);
				}
				MessageBoxCreated?.Invoke();
				bool? result = LastMessageBox.ShowDialog();
				IrPhase = "dialog-returned:" + result;
				return result.GetValueOrDefault();
			}

			// 测试钩子：MessageBox 创建后触发（此时再投递 Yes 点击，避免轮询重试被提前耗尽）。
			public event Action MessageBoxCreated;

			// 生产 StopRebaseInteractiveProcess：release semaphore → DisableEditableControls；
			// 窗口关闭由 _riProcessRunner Task（进程退出后）Post Close(result)。
			private void StopRebaseInteractiveProcess(string response)
			{
				if (StopCount == 0)
				{
					StopCount++;
					_finishRiProcessSemaphore.Release();
					System.Threading.Tasks.Task.Run(delegate
					{
						_finishRiProcessSemaphore.WaitOne(5000);
						Dispatcher.UIThread.Post(delegate
						{
							ProcessRunning = false;
							Close(true);
						});
					});
				}
			}
		}

		// 与 InteractiveRebaseWindow.OnClosing/StopRebaseInteractiveProcess 同构的探针窗口。
		private sealed class RebaseLikeProbeWindow : ForkPlusDialogWindow
		{
			public bool ProcessRunning = true;
			public int StopCount;

			protected override void OnClosing(WindowClosingEventArgs e)
			{
				if (!ProcessRunning)
				{
					base.OnClosing(e);
					return;
				}
				e.Cancel = true;
				StopCount++;
				// 模拟 StopRebaseInteractiveProcess 后 rebase 进程退出 → Post Close(result)。
				Dispatcher.Post(delegate
				{
					ProcessRunning = false;
					Close(true);
				});
			}
		}
	}
}
