// E2E 模块28（2026-09-06）：快捷键系统性全量覆盖测试。
// 背景：E2e01-27 各模块从功能视角顺带覆盖了部分快捷键，但没有系统性验证
// "按键 → CommandRouter 翻译 → 命令执行" 全链路。迁移期（WPF InputBindings →
// Avalonia CommandRouter）最易发生的回归是：命令定义了 KeyGesture 但绑定
// 注册丢失（InitializeKeyBindings 漏一行）、修饰键语义偏差（精确匹配）、
// 作用域错误（window 级手势被子控件误消费 / 控件级手势越权全局触发）。
//
// 覆盖口径（golden 清单由反射 dump 生成：MAIN 39 手势 / REPO 18 / COMMIT 8 / REPOMANAGER 3）：
//   MAIN（MainWindowCommands，39 手势）—— window 级绑定，全窗口生效
//     （其中 QuickFetch Ctrl+Shift+Alt+F 与 OpenRepositoryInFileExplorer Ctrl+Alt+O
//      迁移期走 MainWindow.OnKeyUp/OnKeyDown 手工处理，不走 CommandRouter）
//   REPO（RepositoryUserControlCommands，18）—— RevisionList/Sidebar/详情区/文件树/StageFile 作用域
//   COMMIT（CommitUserControlCommands，8）  —— CommitUserControl/StageFileUserControl 作用域
//   REPOMANAGER（RepositoryManagerUserControlCommands，3）—— 仓库管理 tab 树作用域
//
// 两层验证：
//   ① 清单完整性（01）：反射枚举 4 容器全部 IUICommand 的 Shortcut/SecondaryShortcut，
//      逐一断言手势已注册为 CommandBinding（读 CommandRouter._bindings 反射表）——
//      捕获"定义了快捷键但绑定丢失"的迁移 bug；FileHistoryWindow 独立 TopLevel 单独验证。
//   ② 行为验证（02-23）：真实 RaiseEvent(KeyDown) 走生产路由（Tunnel 消费 →
//      CommandRouter 冒泡翻译），按可观察副作用断言：ViewMode/tab/设置/git 终态/
//      剪贴板/窗口出现。模态弹窗用看门狗 DispatcherTimer 自动关闭（PushFrame
//      泵消息期间 timer 正常触发，与 HeadlessAppBootstrap 错误看门狗同机制）。
//
// 截图 → docs/evidence/e2e/28-keyboardshortcuts/。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e28KeyboardShortcutsTests
	{
		private const string ModuleDir = "28-keyboardshortcuts";

		// ============================ 基建：按键模拟 ============================

		/// <summary>复位 Keyboard shim 的全局修饰键状态（进程级单例，用例间残留会污染
		/// KeyboardHelper.IsCtrlDown 等消费者——如提交主题框 Enter 跳焦点的 !IsCtrlDown 判断）。
		/// KeyUp 事件带 KeyModifiers=None：tracker 先置 _lastModifiers=None，再由
		/// RemoveModifierKey 按"修饰键不在 _lastModifiers 中"清出 _downKeys。</summary>
		private static void ResetKeyboardModifiers(InputElement target)
		{
			foreach (Key mod in new[] { Key.LeftCtrl, Key.LeftShift, Key.LeftAlt, Key.LWin })
			{
				target.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyUpEvent,
					Key = mod,
					KeyModifiers = KeyModifiers.None
				});
			}
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>模拟真实按键：RaiseEvent(KeyDown) 走完整生产路由——Tunnel 阶段
		/// TopLevel 的 Keyboard shim tracker 更新全局修饰键状态、控件级 Tunnel 消费器
		/// （修订列表 Ctrl+A、搜索框 Enter）有机会消费；随后 CommandRouter（TopLevel
		/// Bubble）按"精确修饰键匹配 + 事件源在宿主子树内"翻译手势执行命令。
		/// 收尾 KeyUp 复位全局键盘状态（防跨用例污染，见 ResetKeyboardModifiers）。</summary>
		private static void PressKey(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
		{
			ResetKeyboardModifiers(target);
			target.RaiseEvent(new KeyEventArgs
			{
				RoutedEvent = InputElement.KeyDownEvent,
				Key = key,
				KeyModifiers = modifiers
			});
			Dispatcher.UIThread.RunJobs();
			ResetKeyboardModifiers(target);
		}

		/// <summary>在焦点控件上按键（比直接 window 上 Raise 更保真：事件源是真实焦点元素，
		/// 控件级手势的作用域判定走生产路径）。无真实焦点时回退到 window 自身。</summary>
		private static void PressKeyOnFocused(Window window, Key key, KeyModifiers modifiers = KeyModifiers.None)
		{
			InputElement focused = window.FocusManager?.GetFocusedElement() as InputElement;
			if (focused == null)
			{
				// headless 下焦点可能未建立：把焦点给 window 内容根，保证事件源在子树内
				focused = window;
			}
			PressKey(focused, key, modifiers);
		}

		/// <summary>模拟 KeyUp 阶段的手工快捷键（迁移期路径：QuickFetch Ctrl+Shift+Alt+F 不走
		/// CommandRouter，由 MainWindow.OnKeyUp 读 KeyboardHelper 修饰键状态处理）。
		/// 路由顺序与生产一致：Tunnel 阶段 Keyboard shim tracker 先把事件 KeyModifiers 记入
		/// 全局状态（_lastModifiers=Ctrl|Shift|Alt），冒泡到 MainWindow.OnKeyUp 时
		/// IsCtrlDown/IsShiftDown/IsAltDown 均已为真；收尾复位防跨用例污染。</summary>
		private static void PressKeyUp(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
		{
			ResetKeyboardModifiers(target);
			target.RaiseEvent(new KeyEventArgs
			{
				RoutedEvent = InputElement.KeyUpEvent,
				Key = key,
				KeyModifiers = modifiers
			});
			Dispatcher.UIThread.RunJobs();
			ResetKeyboardModifiers(target);
		}

		// ============================ 基建：模态弹窗看门狗 ============================

		/// <summary>模态弹窗自动关闭看门狗：DispatcherTimer 周期扫描 WpfApp.Windows，
		/// 目标类型窗口出现即记录引用并 Close——ShowDialog 的 PushFrame 泵消息期间
		/// timer 正常触发（HeadlessAppBootstrap 错误看门狗同机制），按键调用返回后
		/// 用 SeenWindow 断言"窗口确实弹出过"。
		/// 超时兜底：时限内目标未出现但存在"白名单之外"的可见窗口时全部关闭——
		/// 命令弹了非预期类型窗口时 PushFrame 无人退出，会把测试永久挂死。</summary>
		private sealed class ModalDialogWatchdog : IDisposable
		{
			private readonly DispatcherTimer _timer;
			private readonly Stopwatch _watch;
			private readonly Window[] _keepAlive;
			private readonly Type _targetType;

			public Window SeenWindow;

			public static ModalDialogWatchdog WaitForAndClose<TWindow>(params Window[] keepAlive) where TWindow : Window
			{
				return new ModalDialogWatchdog(typeof(TWindow), keepAlive);
			}

			private ModalDialogWatchdog(Type targetType, Window[] keepAlive)
			{
				_targetType = targetType;
				_keepAlive = keepAlive ?? new Window[0];
				_watch = Stopwatch.StartNew();
				_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Default, Tick);
				_timer.Start();
			}

			private void Tick(object sender, EventArgs e)
			{
				try
				{
					Window[] windows = WpfApp.Windows.ToArray();
					foreach (Window window in windows)
					{
						if (!window.IsVisible || _keepAlive.Contains(window))
						{
							continue;
						}
						if (_targetType.IsInstanceOfType(window))
						{
							SeenWindow = window;
							window.Close(); // ShowDialog 的 PushFrame 随窗口关闭退出
							Stop();
							return;
						}
					}
					// 超时兜底（12s）：目标类型未出现但存在其他非白名单可见窗口——
					// 命令弹了非预期窗口，强制关闭防挂死（随后 SeenWindow==null 断言失败暴露）
					if (_watch.ElapsedMilliseconds > 12000)
					{
						foreach (Window window in windows)
						{
							if (window.IsVisible && !_keepAlive.Contains(window))
							{
								try
								{
									window.Close();
								}
								catch
								{
								}
							}
						}
						Stop();
					}
				}
				catch
			{
				// 看门狗自身异常绝不影响测试线程
			}
		}

		private void Stop()
		{
			_timer.Stop();
			_watch.Stop();
		}

		/// <summary>等待目标窗口被看到（非模态 Show 场景防时序竞争）：DispatcherTimer
		/// tick 有毫秒级滞后——模态 ShowDialog 按键调用阻塞在 PushFrame 泵消息期间
		/// tick 正常触发，而非模态 Show 的按键链路（全模块运行 JIT 热身后 &lt;50ms）
		/// 可能在 timer 到期 tick 前就走到断言（模块28 Ctrl+B 全量绿单独红的根因）。
		/// 断言前调用本方法：泵消息 + 让真实时间推进过 tick 周期，SeenWindow 就绪即
		/// 返回；超时返回 false 由调用方断言报错。</summary>
		public bool WaitForSeen(int timeoutMs = 2000)
		{
			Stopwatch watch = Stopwatch.StartNew();
			while (SeenWindow == null && watch.ElapsedMilliseconds < timeoutMs)
			{
				Dispatcher.UIThread.RunJobs();
				if (SeenWindow != null)
				{
					return true;
				}
				System.Threading.Thread.Sleep(20);
			}
			return SeenWindow != null;
		}

		public void Dispose()
		{
			Stop();
		}
	}

		/// <summary>看门狗变体：不限定类型，关闭按键后出现的任何非白名单可见窗口
		/// （用于"按键必须无弹窗"的负向断言——DidCloseAnything==false 即全程无弹窗）。</summary>
		private sealed class AnyDialogWatchdog : IDisposable
	{
		private readonly DispatcherTimer _timer;

		private readonly Window[] _keepAlive;

		public Window ClosedWindow;

		/// <summary>关闭窗口时抓取的诊断文本（ErrorWindow 的 MessageTextBox 内容等），
		/// 负向断言失败时打印，避免只看到窗口类型而看不到根因。</summary>
		public string ClosedWindowText;

		public static AnyDialogWatchdog Arm(params Window[] keepAlive)
		{
			return new AnyDialogWatchdog(keepAlive);
		}

		private AnyDialogWatchdog(Window[] keepAlive)
		{
			_keepAlive = keepAlive ?? new Window[0];
			_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Default, Tick);
			_timer.Start();
		}

		private void Tick(object sender, EventArgs e)
		{
			try
			{
				foreach (Window window in WpfApp.Windows.ToArray())
				{
					if (window.IsVisible && !_keepAlive.Contains(window))
					{
						ClosedWindow = window;
						ClosedWindowText = DescribeWindow(window);
						window.Close();
						_timer.Stop();
						return;
					}
				}
			}
			catch
			{
			}
		}

		/// <summary>关闭前提取窗口可诊断文本：ErrorWindow 取 MessageTextBox.Text（git 错误正文），
		/// 其它窗口退化为类型名。FindControl 必须在 Close 前调用（视觉树拆除后取不到）。</summary>
		private static string DescribeWindow(Window window)
		{
			try
			{
				if (window is global::ForkPlus.UI.Dialogs.ErrorWindow)
				{
					global::Avalonia.Controls.TextBox textBox = window.FindControl<global::Avalonia.Controls.TextBox>("MessageTextBox");
					string text = textBox?.Text;
					if (!string.IsNullOrWhiteSpace(text))
					{
						string trimmed = text.Trim();
						return trimmed.Length > 400 ? trimmed.Substring(0, 400) + "..." : trimmed;
					}
					return "<ErrorWindow 无文本>";
				}
				return window.GetType().Name;
			}
			catch
			{
				return window.GetType().Name;
			}
		}

		public void Dispose()
		{
			_timer.Stop();
		}
	}

	/// <summary>确认框自动提交看门狗：MessageBoxWindow 出现时不 Close 而是点指定文本的
	/// 按钮——ConfirmAndStashBeforeRestore 等按 ShowDialog 返回值分叉的调用，无结果
	/// Close 会走"取消"分支（操作中止）。DispatcherTimer 在 ShowDialog 的 PushFrame
	/// 泵内正常触发（与 ModalDialogWatchdog 同机制）。Close 兜底同前：非目标窗口
	/// 出现且超时则强关防挂死。
	/// 注：本模块暂未使用（UndoRedo 用例改走"外部 reset --hard 清脏 → 免弹窗"的
	/// 确定性路径）；留给后续需要"确认框点提交"语义的用例复用。</summary>
	private sealed class MessageBoxAutoSubmitWatchdog : IDisposable
	{
		private readonly DispatcherTimer _timer;
		private readonly Stopwatch _watch;
		private readonly Window[] _keepAlive;
		private readonly string _buttonKey;

		public bool Clicked;
		public string Error;

		public static MessageBoxAutoSubmitWatchdog Arm(string buttonKey, params Window[] keepAlive)
		{
			return new MessageBoxAutoSubmitWatchdog(buttonKey, keepAlive);
		}

		private MessageBoxAutoSubmitWatchdog(string buttonKey, Window[] keepAlive)
		{
			_buttonKey = buttonKey;
			_keepAlive = keepAlive ?? new Window[0];
			_watch = Stopwatch.StartNew();
			_timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Default, Tick);
			_timer.Start();
		}

		private void Tick(object sender, EventArgs e)
		{
			try
			{
				MessageBoxWindow msgBox = WpfApp.Windows.OfType<MessageBoxWindow>()
					.FirstOrDefault(w => w.IsVisible && !_keepAlive.Contains(w));
				if (msgBox != null)
				{
					string text = E2eMainWindowHarness.Tr(_buttonKey);
					Button button = UiClick.FindAll<Button>(msgBox)
						.FirstOrDefault(delegate (Button b) { return UiClick.ContentText(b) == text; });
					if (button == null)
					{
						Error = "确认框中找不到按钮 " + text;
					}
					else
					{
						button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
						Clicked = true;
					}
					Stop();
					return;
				}
				// 超时兜底（12s）：无确认框但出现了其他非白名单可见窗口——强关防挂死
				if (_watch.ElapsedMilliseconds > 12000)
				{
					foreach (Window window in WpfApp.Windows.ToArray())
					{
						if (window.IsVisible && !_keepAlive.Contains(window))
						{
							try
							{
								window.Close();
							}
							catch
							{
							}
						}
					}
					Stop();
				}
			}
			catch (Exception ex)
			{
				Error = ex.ToString();
				Stop();
			}
		}

		private void Stop()
		{
			_timer.Stop();
			_watch.Stop();
		}

		public void Dispose()
		{
			Stop();
		}
	}

	// ============================ 基建：git / 剪贴板杂项 ============================

		/// <summary>外部 git 进程执行（断言终态用；forkgitinstance 环境变量已在测试基建
		/// 里指向可用 git，这里直接走 PATH——rev-parse/commit/push 等基础操作任何版本均可）。</summary>
		private static string Git(string workDir, string args)
		{
			var psi = new ProcessStartInfo("git", args)
			{
				WorkingDirectory = workDir,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			using var process = Process.Start(psi);
			string output = process.StandardOutput.ReadToEnd().Trim();
			process.WaitForExit();
			return output;
		}

		private static string HeadSha(string workDir)
	{
		return Git(workDir, "rev-parse HEAD");
	}

	/// <summary>工作区是否脏（与生产 Redo/Undo 的 IsWorkingTreeDirty 同口径：实时 git status）。</summary>
	private static bool IsDirty(string workDir)
	{
		return Git(workDir, "status --porcelain").Length > 0;
	}

		private static string ClipboardText()
		{
			return global::ForkPlus.Services.ServiceLocator.Clipboard.GetText();
		}

		private static void SetClipboard(string text)
		{
			global::ForkPlus.Services.ServiceLocator.Clipboard.SetText(text);
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>取当前可见窗口中指定类型的实例（排除测试主窗口）。</summary>
		private static TWindow FindOpenWindow<TWindow>(Window exclude) where TWindow : Window
		{
			return WpfApp.Windows.OfType<TWindow>().FirstOrDefault(w => w != exclude && w.IsVisible);
		}

		// ============================ 01) 清单完整性：全手势绑定注册验证 ============================

		/// <summary>反射读取挂到指定 TopLevel 的全部 CommandRouter 绑定，返回
		/// (宿主类型, 手势) 对列表——验证"命令定义了快捷键但绑定丢失"的迁移 bug。</summary>
		private static List<(Type HostType, KeyGesture Gesture)> CollectRegisteredGestures(TopLevel topLevel)
		{
			var result = new List<(Type, KeyGesture)>();
			FieldInfo bindingsField = typeof(CommandRouter).GetField("_bindings",
				BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(bindingsField);
			object table = bindingsField.GetValue(null);
		// ConditionalWeakTable<TopLevel, List<Entry>>.TryGetValue(TKey, out TValue)：
		// 反射查找必须用实例化的 TKey（TopLevel），不能传 typeof(object)
		Type tableType = table.GetType();
		MethodInfo tryGetValue = tableType.GetMethod("TryGetValue",
			new[] { tableType.GetGenericArguments()[0], tableType.GetGenericArguments()[1].MakeByRefType() });
		Assert.NotNull(tryGetValue);
			object[] args = { topLevel, null };
			bool found = (bool)tryGetValue.Invoke(table, args);
			if (!found)
			{
				return result;
			}
			var entries = (System.Collections.IEnumerable)args[1];
			Type entryType = table.GetType().GetGenericArguments()[1].GetGenericArguments()[0];
			FieldInfo bindingField = entryType.GetField("Binding");
			FieldInfo hostField = entryType.GetField("Host");
			foreach (object entry in entries)
			{
				var binding = (CommandBinding)bindingField.GetValue(entry);
				var host = hostField.GetValue(entry);
				if (binding?.Command?.InputGestures == null)
				{
					continue;
				}
				foreach (KeyGesture gesture in binding.Command.InputGestures)
				{
					result.Add((host?.GetType(), gesture));
				}
			}
			return result;
		}

		/// <summary>枚举命令容器里全部带手势的命令（与 ZShortcutInventoryDumpTests 同口径）。</summary>
		private static List<(string CommandName, KeyGesture Primary, KeyGesture Secondary)> CollectCommandGestures(object container)
		{
			var result = new List<(string, KeyGesture, KeyGesture)>();
			foreach (PropertyInfo property in container.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => typeof(IUICommand).IsAssignableFrom(p.PropertyType)).OrderBy(p => p.Name))
			{
				var command = (IUICommand)property.GetValue(container);
				if (command?.Shortcut != null || command?.SecondaryShortcut != null)
				{
					result.Add((property.Name, command.Shortcut, command.SecondaryShortcut));
				}
			}
			return result;
		}

		[Fact]
		public void Inventory_AllDeclaredGesturesAreRegisteredAsBindings()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 触发 commit 视图切换（保证 CommitUserControl/StageFileUserControl 已装配），
						// 再回 revision 视图（RevisionChanges/FileTree 等静态声明的控件随
						// RepositoryUserControl 构造已注册，这里双视图都过一遍）
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						repoControl.ActivateRevisionView();
						Dispatcher.UIThread.RunJobs();

						var registered = CollectRegisteredGestures(window);
						Assert.True(registered.Count > 0, "主窗口应已注册 CommandRouter 绑定");
						var registeredSet = registered
							.Select(t => (t.HostType?.FullName ?? "<null>", t.Gesture.Key, t.Gesture.KeyModifiers))
							.ToHashSet();

						// ===== ① MAIN 容器：除 CopyRevisionSha/CopyRevisionInfo 外全部是 window 级绑定 =====
						var main = CollectCommandGestures(new global::ForkPlus.UI.MainWindowCommands());
						int windowLevelCount = 0;
					foreach (var (name, primary, secondary) in main)
					{
						foreach (KeyGesture gesture in new[] { primary, secondary }.Where(g => g != null))
						{
							if (name == "CopyRevisionSha" || name == "CopyRevisionInfo")
							{
								// 主窗口不注册（只绑定在修订列表/文件历史等作用域，见 ②），跳过 window 级断言
								continue;
							}
							if (name == "QuickFetch" || name == "OpenRepositoryInFileExplorer")
							{
								// 迁移期特殊路径：不走 CommandRouter——QuickFetch 在 MainWindow.OnKeyUp、
								// OpenRepositoryInFileExplorer 在 MainWindow.OnKeyDown 手工处理
								//（读 KeyboardHelper 全局修饰键状态），行为验证见 ManualHandler 用例
								continue;
							}
							Assert.Contains((typeof(MainWindow).FullName, gesture.Key, gesture.KeyModifiers), registeredSet);
							windowLevelCount++;
						}
					}
					Assert.True(windowLevelCount >= 30,
						"主窗口级手势应 >= 30 个（39 golden 扣除 Ctrl+C 两项及 2 个手工处理项），实际 " + windowLevelCount);

						// ===== ② REPO/COMMIT/REPOMANAGER 容器：至少注册到某个宿主 =====
						var scoped = new List<(string, KeyGesture, KeyGesture)>();
						scoped.AddRange(CollectCommandGestures(new RepositoryUserControlCommands()));
						scoped.AddRange(CollectCommandGestures(new CommitUserControlCommands()));
						scoped.AddRange(CollectCommandGestures(new global::ForkPlus.UI.UserControls.RepositoryManagerUserControlCommands()));
						int scopedCount = 0;
						var missing = new List<string>();
						foreach (var (name, primary, secondary) in scoped)
						{
							foreach (KeyGesture gesture in new[] { primary, secondary }.Where(g => g != null))
							{
								bool registeredAnywhere = registeredSet.Any(t => t.Item2 == gesture.Key && t.Item3 == gesture.KeyModifiers);
								if (!registeredAnywhere)
								{
									missing.Add(name + " (" + gesture + ")");
								}
								else
								{
									scopedCount++;
								}
							}
						}
						Assert.True(missing.Count == 0,
							"作用域命令的手势未注册到任何宿主（迁移绑定丢失）: " + string.Join(", ", missing));
						Assert.True(scopedCount >= 20, "作用域手势应 >= 20 个，实际 " + scopedCount);

						// ===== ③ FileHistoryWindow（独立 TopLevel）内部的 4 组绑定 =====
						var historyWindow = new FileHistoryWindow(repoControl,
							new ShowFileHistoryWindowCommand.Mode.File("readme.md"), null, null);
						historyWindow.Show();
						Dispatcher.UIThread.RunJobs();
						try
						{
							var historyGestures = CollectRegisteredGestures(historyWindow)
								.Select(t => (t.Gesture.Key, t.Gesture.KeyModifiers)).ToHashSet();
							// OpenFileInDefaultEditor / CopyRevisionSha / CopyRevisionInfo / RunExternalDiffTool
							Assert.Contains((Key.O, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt), historyGestures);
							Assert.Contains((Key.C, KeyModifiers.Control), historyGestures);
							Assert.Contains((Key.C, KeyModifiers.Control | KeyModifiers.Shift), historyGestures);
							Assert.Contains((Key.D, KeyModifiers.Control), historyGestures);
						}
						finally
						{
							historyWindow.Close();
							Dispatcher.UIThread.RunJobs();
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 02) 视图/侧栏切换：Ctrl+1/2/0、Ctrl+Shift+1/2 ============================

		[Fact]
		public void ViewSwitching_Ctrl1_Ctrl2_CtrlShift1_CtrlShift2_Ctrl0()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 打开仓库后默认修订视图（RepositoryViewMode.RevisionViewMode=0）
						Assert.Equal(RepositoryViewMode.RevisionViewMode, repoControl.ViewMode);

						// ===== 1) Ctrl+1 → Commit 视图 =====
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control);
						Assert.Equal(RepositoryViewMode.CommitViewMode, repoControl.ViewMode);

						// ===== 2) Ctrl+2 → Revision 视图 =====
						PressKeyOnFocused(window, Key.D2, KeyModifiers.Control);
						Assert.Equal(RepositoryViewMode.RevisionViewMode, repoControl.ViewMode);

						// ===== 3) Ctrl+Shift+2 → 激活侧栏搜索 tab =====
						repoControl.Sidebar.BranchesTabItem.IsSelected = true; // 先离开搜索 tab
						Dispatcher.UIThread.RunJobs();
						Assert.False(repoControl.Sidebar.SearchTabItem.IsSelected);
						PressKeyOnFocused(window, Key.D2, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(repoControl.Sidebar.SearchTabItem.IsSelected, "Ctrl+Shift+2 应激活侧栏搜索 tab");

						// ===== 4) Ctrl+Shift+1 → 激活侧栏分支 tab =====
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(repoControl.Sidebar.BranchesTabItem.IsSelected, "Ctrl+Shift+1 应激活侧栏分支 tab");
						Assert.False(repoControl.Sidebar.SearchTabItem.IsSelected);

						// ===== 5) Ctrl+0 → Show HEAD（切回修订视图并选中 head 修订）=====
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control); // 先到 commit 视图
						Assert.Equal(RepositoryViewMode.CommitViewMode, repoControl.ViewMode);
						PressKeyOnFocused(window, Key.D0, KeyModifiers.Control);
						Assert.Equal(RepositoryViewMode.RevisionViewMode, repoControl.ViewMode);
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate { return revList.SelectedRevision != null; }),
							"Ctrl+0 后应选中 head 修订");
						string headSha = HeadSha(repo);
						Assert.Equal(headSha, revList.SelectedRevision.Sha.ToString());
						ScreenshotHelper.Snap(window, "01-view-switching", ModuleDir);

						// ===== 6) 修饰键精确匹配：Ctrl+Shift+1 不应误触发 Ctrl+1 的行为 =====
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);
						Assert.True(repoControl.ViewMode == RepositoryViewMode.RevisionViewMode,
							"带额外 Alt 的 Ctrl+Shift+1 不应匹配任何命令（修饰键精确匹配语义）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 03) Tab 管理：Ctrl+T/W/F4/Tab ============================

		[Fact]
		public void TabManagement_CtrlT_CtrlW_CtrlF4_CtrlTab_CtrlShiftTab()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// x:Name="TabControl" 生成的 internal 字段（InternalsVisibleTo 可达）
						var tabControl = window.TabControl;
						// 打开仓库后：OpenRepository 会移除 RestoreSession 建的仓库管理 tab → 只剩 1 个仓库 tab
						Assert.Equal(1, tabControl.Items.Count);
						Assert.True(window.TabManager.ActiveRepositoryUserControl != null, "初始应选中仓库 tab");

						// ===== 1) Ctrl+T → 选中/新建仓库管理 tab =====
						PressKeyOnFocused(window, Key.T, KeyModifiers.Control);
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(2, tabControl.Items.Count);
						Assert.True(window.TabManager.ActiveRepositoryUserControl == null, "Ctrl+T 后应选中仓库管理 tab");
						var selected = tabControl.SelectedTab;
						Assert.NotNull(selected);
						Assert.Equal(global::ForkPlus.UI.Controls.TabItemMode.RepositoryManager, selected.Mode);
						ScreenshotHelper.Snap(window, "02-ctrl-t-repository-manager-tab", ModuleDir);

						// ===== 2) Ctrl+Tab → 切到下一个 tab（回到仓库 tab）=====
						PressKeyOnFocused(window, Key.Tab, KeyModifiers.Control);
						Dispatcher.UIThread.RunJobs();
						Assert.True(window.TabManager.ActiveRepositoryUserControl != null, "Ctrl+Tab 应切回仓库 tab");

						// ===== 3) Ctrl+Shift+Tab → 切到上一个 tab（回到仓库管理 tab）=====
						PressKeyOnFocused(window, Key.Tab, KeyModifiers.Control | KeyModifiers.Shift);
						Dispatcher.UIThread.RunJobs();
						Assert.True(window.TabManager.ActiveRepositoryUserControl == null, "Ctrl+Shift+Tab 应切回仓库管理 tab");

						// ===== 4) Ctrl+W → 关闭当前 tab（仓库管理 tab），只剩仓库 tab =====
						PressKeyOnFocused(window, Key.W, KeyModifiers.Control);
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return tabControl.Items.Count == 1; }),
							"Ctrl+W 后应只剩 1 个 tab");
						Assert.True(window.TabManager.ActiveRepositoryUserControl != null);

						// ===== 5) Ctrl+F4（secondary）→ 关闭最后一个仓库 tab =====
						// RemoveTab 对最后一个 tab 自动 NewTab（生产语义：永远至少留一个仓库管理 tab）
						PressKeyOnFocused(window, Key.F4, KeyModifiers.Control);
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return tabControl.Items.Count == 1
								&& tabControl.SelectedTab?.Mode == global::ForkPlus.UI.Controls.TabItemMode.RepositoryManager;
						}), "Ctrl+F4 关闭最后一个 tab 后应自动重建仓库管理 tab");
						ScreenshotHelper.Snap(window, "02b-ctrl-f4-last-tab-auto-manager", ModuleDir);
					}
					finally
					{
						// tab 已全部关闭：CloseRepositoryTab 内部 CloseTab 对已不存在的 tab 是幂等的
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 04) 提交视图：Enter / Ctrl+Shift+S / Ctrl+Shift+Alt+S ============================

		[Fact]
		public void CommitView_StageShortcuts_Enter_CtrlShiftS_CtrlShiftAltS()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control); // Ctrl+1 → commit 视图
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(); }),
							"工作区未暂存文件未装配");

						// ===== 1) Enter（焦点在未暂存列表）→ stage 选中文件 =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						PressKey(stage.UnstagedFilesFileListUserControl, Key.Return);
						Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Any(f => f.Path == "a.txt"); }),
							"Enter 应暂存选中的 a.txt");
						Assert.False(stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"), "a.txt 暂存后应从未暂存列表消失");

						// ===== 2) Enter（焦点在已暂存列表）→ unstage =====
						stage.StagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						PressKey(stage.StagedFilesFileListUserControl, Key.Return);
						Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"); }),
							"Enter 在已暂存列表应反暂存 a.txt");

						// ===== 3) Ctrl+Shift+S（secondary）→ stage =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						PressKey(stage.UnstagedFilesFileListUserControl, Key.S, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Any(f => f.Path == "a.txt"); }),
							"Ctrl+Shift+S 应暂存选中的 a.txt");
						stage.StagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						PressKey(stage.StagedFilesFileListUserControl, Key.S, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"); }),
							"Ctrl+Shift+S 在已暂存列表应反暂存");

						// ===== 4) Ctrl+Shift+Alt+S → 全部 stage（未暂存列表选中态）=====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						PressKey(stage.UnstagedFilesFileListUserControl, Key.S,
							KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.All(f => f.IsDirectory)
								&& stage.AllStagedFiles.Any(f => f.Path == "a.txt");
						}), "Ctrl+Shift+Alt+S 应暂存全部未暂存文件");
						ScreenshotHelper.Snap(window, "03-ctrl-shift-alt-s-all-staged", ModuleDir);

						// ===== 5) Ctrl+Shift+Alt+S（已暂存列表选中态）→ 全部 unstage =====
						stage.StagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						PressKey(stage.StagedFilesFileListUserControl, Key.S,
							KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);
						Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Length == 0; }),
							"Ctrl+Shift+Alt+S 在已暂存列表应反暂存全部");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 05) 提交执行：Ctrl+Enter / Ctrl+Shift+Enter ============================

		[Fact]
		public void Commit_CtrlEnter_CtrlShiftEnter()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						string before = HeadSha(repo);
					PressKeyOnFocused(window, Key.D1, KeyModifiers.Control);
					CommitUserControl commit = repoControl.Content.CommitUserControl;
					StageFileUserControl stage = commit.StageFileUserControl;
					Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"); }),
						"CreateBasic 的未暂存修改（a.txt）未装配");

					// ===== 0) 先暂存 a.txt（IsCommitAllowed 要求 staged 非空）=====
					stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
					Dispatcher.UIThread.RunJobs();
					PressKey(stage.UnstagedFilesFileListUserControl, Key.Return); // Enter stage
					Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Any(f => f.Path == "a.txt"); }),
						"a.txt 应已暂存");

					// ===== 1) 焦点在提交主题框：纯 Enter 只跳焦点不提交（CommitSubjectTextBox_PreviewKeyDown）=====
					commit.CommitSubjectTextBox.Focus();
					Dispatcher.UIThread.RunJobs();
					commit.CommitSubjectTextBox.Text = "commit via shortcut";
					PressKey(commit.CommitSubjectTextBox, Key.Return);
					object focusedAfterEnter = window.FocusManager?.GetFocusedElement();
					Assert.True(ReferenceEquals(commit.CommitDescriptionTextBox, focusedAfterEnter),
						"提交主题框按 Enter 应跳到描述框（不触发提交）");
					Assert.True(before == HeadSha(repo), "纯 Enter 不应产生提交");

					// ===== 2) Ctrl+Enter（secondary，焦点在描述框）→ 直接提交 =====
					// 描述框 AcceptsReturn：Avalonia TextBox 类处理无条件消费 Enter，迁移修复
					// （CommitDescriptionTextBox.OnKeyDown 对带 Ctrl 的 Return 放行冒泡）见该文件注释
					PressKey(commit.CommitDescriptionTextBox, Key.Return, KeyModifiers.Control);
					E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
					string afterFirst = HeadSha(repo);
					Assert.True(before != afterFirst, "Ctrl+Enter 应产生新提交");
					Assert.Equal("commit via shortcut", Git(repo, "log -1 --format=%s"));
					// 提交成功后：staged 清空 + 消息清空（生产行为）
					// 注：不能直接断言——提交完成回调（Dispatcher.Post）里的 Refresh(SubDomain.All)
					// 在 WaitForRepositoryJobs 末次泵时才把 status 刷新 job 入队，同步读
					// AllStagedFiles 读到的是旧值；必须用 WaitFor 泵轮（与模块04 stage 断言同款）。
					Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Length == 0; }),
						"提交后 staged 列表应清空（status 刷新完成）");
					Assert.Equal("", commit.FullCommitMessage);

						// ===== 3) Ctrl+Shift+Enter（primary）→ commit-and-push（生产语义）=====
					// CommitAndPush getter（CommitUserControl.axaml.cs）：auto-push 关闭时
					// IsShiftDown=true → commitAndPush=true → 提交成功后的 Dispatcher.Post
					// 回调里 QuickPush.Execute 弹 PushWindow（模态）。看门狗必须包住
					// 按键 + WaitForRepositoryJobs 全程——PushWindow 在 job 完成后的
					// Post 回调里弹出，可能落在 WaitForRepositoryJobs 的泵期间。
					File.AppendAllText(Path.Combine(repo, "b.txt"), "more\n");
					repoControl.InvalidateAndRefresh(global::ForkPlus.Git.SubDomain.Status);
					Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "b.txt"); }),
						"b.txt 的新修改应出现在未暂存列表");
					stage.UnstagedFilesFileListUserControl.SelectFile("b.txt");
					Dispatcher.UIThread.RunJobs();
					PressKey(stage.UnstagedFilesFileListUserControl, Key.Return); // Enter stage
					Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Any(f => f.Path == "b.txt"); }),
						"b.txt 应已暂存");
					commit.CommitSubjectTextBox.Text = "second commit via primary shortcut";
					// primary 手势也压在描述框上（保证事件源在 CommitUserControl 子树内）
					commit.CommitDescriptionTextBox.Focus();
					Dispatcher.UIThread.RunJobs();
					PushWindow pushWindow = null;
					using (var watchdog = ModalDialogWatchdog.WaitForAndClose<PushWindow>(window))
					{
						PressKey(commit.CommitDescriptionTextBox, Key.Return, KeyModifiers.Control | KeyModifiers.Shift);
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						pushWindow = watchdog.SeenWindow as PushWindow;
					}
					string afterSecond = HeadSha(repo);
					Assert.True(afterFirst != afterSecond, "Ctrl+Shift+Enter 应产生第二个提交");
					Assert.Equal("second commit via primary shortcut", Git(repo, "log -1 --format=%s"));
					Assert.True(pushWindow != null,
						"auto-push 关闭时 Ctrl+Shift+Enter 语义是 commit-and-push：提交后应弹出 PushWindow");
					ScreenshotHelper.Snap(window, "04-commit-via-shortcuts", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 06) Discard：Delete / Ctrl+Shift+D（确认弹窗） ============================

		[Fact]
		public void CommitView_Discard_Delete_CtrlShiftD_ShowsConfirmation()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control);
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"); }),
							"a.txt 未装配");

						string originalContent = File.ReadAllText(Path.Combine(repo, "a.txt"));

						// ===== 1) Delete → Discard 确认弹窗（模态，看门狗自动取消关闭）=====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						MessageBoxWindow discardDialog = null;
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<MessageBoxWindow>(window))
						{
							PressKey(stage.UnstagedFilesFileListUserControl, Key.Delete);
							discardDialog = watchdog.SeenWindow as MessageBoxWindow;
						}
						Assert.True(discardDialog != null, "Delete 应弹出 discard 确认框");
						// 看门狗直接 Close 等价于取消：文件内容不变（未确认 discard）
						Assert.True(originalContent == File.ReadAllText(Path.Combine(repo, "a.txt")),
							"确认框取消（窗口直接关闭）不应丢弃修改");

						// ===== 2) Ctrl+Shift+D（secondary）→ 同一确认弹窗 =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						MessageBoxWindow discardDialog2 = null;
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<MessageBoxWindow>(window))
						{
							PressKey(stage.UnstagedFilesFileListUserControl, Key.D, KeyModifiers.Control | KeyModifiers.Shift);
							discardDialog2 = watchdog.SeenWindow as MessageBoxWindow;
						}
						Assert.True(discardDialog2 != null, "Ctrl+Shift+D 应弹出 discard 确认框");
						Assert.True(originalContent == File.ReadAllText(Path.Combine(repo, "a.txt")),
							"Ctrl+Shift+D 确认框取消后文件内容同样不应变化");
						ScreenshotHelper.Snap(window, "06-discard-confirm-cancelled", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 07) 撤销/重做：Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z ============================

		[Fact]
		public void UndoRedo_CtrlZ_CtrlY_CtrlShiftZ()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 0) 快捷键提交一个变更（AddUndoable 入栈），提交后工作区 clean =====
					string before = HeadSha(repo);
					PressKeyOnFocused(window, Key.D1, KeyModifiers.Control);
					CommitUserControl commit = repoControl.Content.CommitUserControl;
					StageFileUserControl stage = commit.StageFileUserControl;
					Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"); }));
					stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
					Dispatcher.UIThread.RunJobs();
					PressKey(stage.UnstagedFilesFileListUserControl, Key.Return); // stage
					Assert.True(UiClick.WaitFor(delegate { return stage.AllStagedFiles.Any(f => f.Path == "a.txt"); }),
						"a.txt 应已暂存");
					commit.CommitSubjectTextBox.Text = "to be undone";
					// Ctrl+Enter 压在描述框上：Commit 是 CommitUserControl 作用域命令，
					// 事件源必须在宿主子树内（PressKeyOnFocused 焦点缺失时回退 window 会失配）
					commit.CommitDescriptionTextBox.Focus();
					Dispatcher.UIThread.RunJobs();
					PressKey(commit.CommitDescriptionTextBox, Key.Return, KeyModifiers.Control); // Ctrl+Enter 提交
					E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
					string committed = HeadSha(repo);
					Assert.True(before != committed, "Ctrl+Enter 应产生待撤销的提交");

					// ===== 1) Ctrl+Z → undo（git reset --hard 回退 HEAD）=====
					// Undo/Redo 是 window 级绑定；压在 repoControl 上（避开 TextBox——
					// 描述框仍持有焦点，Avalonia TextBox 的 Ctrl+Z 类处理会先消费掉）
					PressKey(repoControl, Key.Z, KeyModifiers.Control);
					E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
					Assert.True(UiClick.WaitFor(delegate { return HeadSha(repo) == before; }),
						"Ctrl+Z 应把 HEAD 回退到提交前");

					// ===== 2) Ctrl+Y（secondary）→ redo（HEAD 前进回提交）=====
					// undo 的快照恢复带回提交前的未暂存改动（CreateBasic 的 a.txt）→ Redo 走
					// ConfirmAndStashBeforeRestore 弹 stash 确认框（模态泵，无 UI 接管会挂死；
					// dotnet-stack 实证挂点）。改用确定性路径：外部 reset --hard 清掉脏改动，
					// isDirty（实时 git status）= false → 无弹窗直接 redo
					Git(repo, "reset --hard");
					Assert.True(UiClick.WaitFor(delegate { return !IsDirty(repo); }),
						"reset --hard 后工作区应 clean（redo 不弹 stash 确认框的前提）");
					PressKey(repoControl, Key.Y, KeyModifiers.Control);
					E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
					Assert.True(UiClick.WaitFor(delegate { return HeadSha(repo) == committed; }),
						"Ctrl+Y 应重做到提交后的 HEAD");

					// ===== 3) 再 undo 一次 + Ctrl+Shift+Z（primary）→ redo =====
					PressKey(repoControl, Key.Z, KeyModifiers.Control);
					E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
					Assert.True(UiClick.WaitFor(delegate { return HeadSha(repo) == before; }));
					Git(repo, "reset --hard"); // 同上：清掉 undo 恢复的脏改动，redo 免弹窗
					Assert.True(UiClick.WaitFor(delegate { return !IsDirty(repo); }));
					PressKey(repoControl, Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
					E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
					Assert.True(UiClick.WaitFor(delegate { return HeadSha(repo) == committed; }),
						"Ctrl+Shift+Z 应等价 redo");
					ScreenshotHelper.Snap(window, "05-undo-redo-via-shortcuts", ModuleDir);
				}
				finally
				{
					E2eMainWindowHarness.CloseRepositoryTab(window, repo);
				}
			});
		}
		finally
		{
			TestRepoFactory.Cleanup(repo);
		}
	}

		// ============================ 08) 剪贴板：修订列表 Ctrl+C / Ctrl+Shift+C ============================

		[Fact]
		public void Clipboard_CtrlC_CtrlShiftC_OnRevisionList()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 选中 head 修订（Ctrl+0 = ShowHead，见用例 02）
						PressKeyOnFocused(window, Key.D0, KeyModifiers.Control);
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate { return revList.SelectedRevision != null; }),
							"head 修订应被选中");
						string headSha = HeadSha(repo);
						SetClipboard("SENTINEL");

						// ===== 1) Ctrl+C → CopyRevisionSha（宿主=RevisionListViewUserControl 作用域绑定）=====
						PressKey(revList, Key.C, KeyModifiers.Control);
						Assert.Equal(headSha, ClipboardText());

						// ===== 2) Ctrl+Shift+C → CopyRevisionInfo（"缩写sha - message"）=====
						PressKey(revList, Key.C, KeyModifiers.Control | KeyModifiers.Shift);
						string info = ClipboardText();
						Assert.True(info.Contains(headSha.Substring(0, 6)),
							"CopyRevisionInfo 应含缩写 sha，实际: " + info);
						Assert.True(info.Contains(" - "),
							"CopyRevisionInfo 应为 'sha - message' 格式，实际: " + info);
						ScreenshotHelper.Snap(window, "07-clipboard-revision", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 09) 主窗口级弹窗类：Ctrl+N/G/,/P/B ============================

		[Fact]
		public void DialogWindows_CtrlN_CtrlG_CtrlOemComma_CtrlP_CtrlB()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// Ctrl+N → CloneWindow（模态）
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<CloneWindow>(window))
						{
							PressKeyOnFocused(window, Key.N, KeyModifiers.Control);
							Assert.True(watchdog.SeenWindow is CloneWindow, "Ctrl+N 应打开 CloneWindow");
						}

						// Ctrl+G → InitGitMmRepositoryWindow（模态）
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<InitGitMmRepositoryWindow>(window))
						{
							PressKeyOnFocused(window, Key.G, KeyModifiers.Control);
							Assert.True(watchdog.SeenWindow is InitGitMmRepositoryWindow,
								"Ctrl+G 应打开 InitGitMmRepositoryWindow");
						}

						// Ctrl+, → PreferencesWindow（模态）
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<PreferencesWindow>(window))
						{
							PressKeyOnFocused(window, Key.OemComma, KeyModifiers.Control);
							Assert.True(watchdog.SeenWindow is PreferencesWindow, "Ctrl+, 应打开 PreferencesWindow");
						}

						// Ctrl+P → QuickLaunchWindow（非模态 Show：看门狗同样可关，但需等关闭完成
						// 再继续，否则残留窗口会被下一个看门狗误认）
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<QuickLaunchWindow>(window))
						{
							PressKeyOnFocused(window, Key.P, KeyModifiers.Control);
							watchdog.WaitForSeen(); // 非模态：断言前等 timer tick（防时序竞争）
							Assert.True(watchdog.SeenWindow is QuickLaunchWindow, "Ctrl+P 应打开 QuickLaunchWindow");
						}
						Assert.True(UiClick.WaitFor(delegate
						{
							return WpfApp.Windows.All(w => !w.IsVisible || w == window);
						}), "非模态 QuickLaunchWindow 应被看门狗关闭");

						// Ctrl+B → QuickLaunchWindow(checkout 模式)（非模态）
						using (var watchdog = ModalDialogWatchdog.WaitForAndClose<QuickLaunchWindow>(window))
						{
							PressKeyOnFocused(window, Key.B, KeyModifiers.Control);
							watchdog.WaitForSeen(); // 非模态：断言前等 timer tick（防时序竞争）
							Assert.True(watchdog.SeenWindow is QuickLaunchWindow,
								"Ctrl+B 应打开 QuickLaunchCheckout（QuickLaunchWindow checkout 模式）");
						}
						Assert.True(UiClick.WaitFor(delegate
						{
							return WpfApp.Windows.All(w => !w.IsVisible || w == window);
						}), "非模态 QuickLaunchCheckout 应被看门狗关闭");
						ScreenshotHelper.Snap(window, "08-window-dialogs", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 10) 仓库级弹窗类：Ctrl+Shift+B/T/F/L/P/H ============================

		[Fact]
		public void RepoDialogs_CtrlShiftB_T_F_L_P_H()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						void AssertOpens<TDialog>(Key key, string what) where TDialog : Window
						{
							using (var watchdog = ModalDialogWatchdog.WaitForAndClose<TDialog>(window))
							{
								PressKeyOnFocused(window, key, KeyModifiers.Control | KeyModifiers.Shift);
								Assert.True(watchdog.SeenWindow is TDialog, what);
							}
						}

						AssertOpens<CreateBranchWindow>(Key.B, "Ctrl+Shift+B 应打开 CreateBranchWindow");
						AssertOpens<CreateTagWindow>(Key.T, "Ctrl+Shift+T 应打开 CreateTagWindow");
						AssertOpens<FetchWindow>(Key.F, "Ctrl+Shift+F 应打开 FetchWindow");
						AssertOpens<PullWindow>(Key.L, "Ctrl+Shift+L 应打开 PullWindow");
						AssertOpens<PushWindow>(Key.P, "Ctrl+Shift+P 应打开 PushWindow");
						// CreateBasic 有未暂存修改 a.txt：SaveStash 可直接开
						AssertOpens<SaveStashWindow>(Key.H, "Ctrl+Shift+H 应打开 SaveStashWindow");
						ScreenshotHelper.Snap(window, "09-repo-dialogs", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 11) 快捷操作：QuickFetch（OnKeyUp 手工）/ QuickPull ============================

		[Fact]
		public void QuickOps_QuickFetch_CtrlShiftAltF_QuickPull_CtrlShiftAltL()
		{
			string work = TestRepoFactory.CreateRemoteBehind();
			string bare = Path.Combine(Directory.GetParent(work)!.FullName, "remote.git");
			bool savedFetchAllRemotes = global::ForkPlus.Settings.ForkPlusSettings.Default.Fetch_FetchAllRemotes;
			bool savedFetchAllTags = global::ForkPlus.Settings.ForkPlusSettings.Default.FetchAllTags;
			try
			{
				string localMain = HeadSha(work);
				string remoteMain = Git(bare, "rev-parse main"); // other 推的远端新提交
				Assert.NotEqual(localMain, remoteMain);
				// QuickFetch 走 FetchGitCommand(remote)：固定单远端 + 不带 tags，保证行为确定
				global::ForkPlus.Settings.ForkPlusSettings.Default.Fetch_FetchAllRemotes = false;
				global::ForkPlus.Settings.ForkPlusSettings.Default.FetchAllTags = false;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						// ===== 1) Ctrl+Shift+Alt+F → QuickFetch：迁移期手工路径（MainWindow.OnKeyUp，
						//      不走 CommandRouter）。KeyUp 路由：Tunnel 阶段 Keyboard shim 先记录
						//      修饰键，冒泡到 MainWindow.OnKeyUp 时 IsCtrlDown/IsAltDown/IsShiftDown 均真 =====
						using (var guard = AnyDialogWatchdog.Arm(window))
						{
							PressKeyUp(window, Key.F, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift);
							E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
							Assert.True(guard.ClosedWindow == null,
								"QuickFetch 不应弹窗: " + (guard.ClosedWindow != null ? guard.ClosedWindow.GetType().Name + " → " + guard.ClosedWindowText : "?"));
						}
						Assert.True(UiClick.WaitFor(delegate { return Git(work, "rev-parse origin/main") == remoteMain; }),
							"QuickFetch 后 origin/main 应前进到远端 main");
						Assert.Equal(localMain, HeadSha(work)); // fetch 只更新远端跟踪 ref，本地 main 不动

						// ===== 2) Ctrl+Shift+Alt+L → QuickPull（CommandRouter 绑定）：上游存在 →
						//      直接 fast-forward pull（无弹窗），本地 main 前进到远端 main =====
						using (var guard = AnyDialogWatchdog.Arm(window))
						{
							PressKeyOnFocused(window, Key.L, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift);
							E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
							Assert.True(guard.ClosedWindow == null,
								"QuickPull 不应弹窗: " + (guard.ClosedWindow != null ? guard.ClosedWindow.GetType().Name + " → " + guard.ClosedWindowText : "?"));
						}
						Assert.True(UiClick.WaitFor(delegate { return HeadSha(work) == remoteMain; }),
							"QuickPull 应把本地 main 快进到远端 main");
						ScreenshotHelper.Snap(window, "10-quick-fetch-pull", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				global::ForkPlus.Settings.ForkPlusSettings.Default.Fetch_FetchAllRemotes = savedFetchAllRemotes;
				global::ForkPlus.Settings.ForkPlusSettings.Default.FetchAllTags = savedFetchAllTags;
				TestRepoFactory.Cleanup(work);
			}
		}

		// ============================ 12) QuickPush：有上游时直接推送（无弹窗） ============================

		[Fact]
		public void QuickPush_CtrlShiftAltP_PushesDirectlyWhenUpstreamExists()
		{
			string work = TestRepoFactory.CreateBareRemote(); // main 领先 origin/main 一个提交
			string bare = Path.Combine(Directory.GetParent(work)!.FullName, "remote.git");
			try
			{
				string localMain = HeadSha(work);
				Assert.NotEqual(localMain, Git(bare, "rev-parse main")); // 远端确实落后
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						// QuickPush 语义：active 分支有上游 → 直接入队 push（无 PushWindow）
						using (var guard = AnyDialogWatchdog.Arm(window))
						{
							PressKeyOnFocused(window, Key.P, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift);
							E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
							Assert.True(guard.ClosedWindow == null,
								"有上游时 QuickPush 应静默推送不弹窗: " + (guard.ClosedWindow != null ? guard.ClosedWindow.GetType().Name + " → " + guard.ClosedWindowText : "?"));
						}
						Assert.True(UiClick.WaitFor(delegate { return Git(bare, "rev-parse main") == localMain; }),
							"QuickPush 应把领先的提交推到远端（bare main 前进到本地 main）");
						ScreenshotHelper.Snap(window, "11-quick-push", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(work);
			}
		}

		// ============================ 13) 布局缩放：Ctrl+OemPlus / Ctrl+OemMinus / Ctrl+Subtract ============================

		[Fact]
		public void LayoutScale_CtrlOemPlus_CtrlOemMinus_CtrlSubtract()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						int original = global::ForkPlus.Settings.ForkPlusSettings.Default.LayoutScaling;
						try
						{
							int expected = original;
							// ===== 1) Ctrl+OemPlus → +10（上限 200）=====
							PressKeyOnFocused(window, Key.OemPlus, KeyModifiers.Control);
							expected = Math.Min(expected + 10, 200);
							Assert.Equal(expected, global::ForkPlus.Settings.ForkPlusSettings.Default.LayoutScaling);

							// ===== 2) Ctrl+OemMinus（primary）→ -10（下限 100）=====
							PressKeyOnFocused(window, Key.OemMinus, KeyModifiers.Control);
							expected = Math.Max(expected - 10, 100);
							Assert.Equal(expected, global::ForkPlus.Settings.ForkPlusSettings.Default.LayoutScaling);

							// ===== 3) Ctrl+Subtract（secondary）→ 等价 -10 =====
							PressKeyOnFocused(window, Key.Subtract, KeyModifiers.Control);
							expected = Math.Max(expected - 10, 100);
							Assert.Equal(expected, global::ForkPlus.Settings.ForkPlusSettings.Default.LayoutScaling);
							ScreenshotHelper.Snap(window, "12-layout-scale", ModuleDir);
						}
						finally
						{
							// 恢复全局设置（进程级单例，防污染其他用例）
							global::ForkPlus.Settings.ForkPlusSettings.Default.LayoutScaling = original;
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 14) 开关类：Ctrl+Shift+A / Ctrl+Shift+. / F5 ============================

		[Fact]
		public void Toggles_CtrlShiftA_CtrlShiftOemPeriod_F5()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) Ctrl+Shift+A → ToggleReferenceFilter：FilterReferences 空 ↔ [active 分支] =====
						Assert.Equal(0, repoControl.GitModule.Settings.FilterReferences.Length);
						PressKeyOnFocused(window, Key.A, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.GitModule.Settings.FilterReferences.Length != 0;
						}), "Ctrl+Shift+A 应把 FilterReferences 设为 active 分支");
						Assert.Contains("refs/heads/main", repoControl.GitModule.Settings.FilterReferences);
						PressKeyOnFocused(window, Key.A, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.GitModule.Settings.FilterReferences.Length == 0;
						}), "再按 Ctrl+Shift+A 应清空 FilterReferences");

						// ===== 2) Ctrl+Shift+. → ToggleShowReflogInRevisionList（修订视图：翻转属性）=====
						bool reflogBefore = repoControl.ShowReflogInRevisionList;
						PressKeyOnFocused(window, Key.OemPeriod, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.ShowReflogInRevisionList == !reflogBefore;
						}), "Ctrl+Shift+. 应翻转 ShowReflogInRevisionList");
						PressKeyOnFocused(window, Key.OemPeriod, KeyModifiers.Control | KeyModifiers.Shift);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.ShowReflogInRevisionList == reflogBefore;
						}), "再按 Ctrl+Shift+. 应翻回");

						// ===== 3) F5 → RefreshRepositoryData：全量刷新不弹窗、修订列表仍可用 =====
						using (var guard = AnyDialogWatchdog.Arm(window))
						{
							PressKeyOnFocused(window, Key.F5);
							E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
							Assert.True(guard.ClosedWindow == null,
								"F5 刷新不应弹窗: " + (guard.ClosedWindow != null ? guard.ClosedWindow.GetType().Name + " → " + guard.ClosedWindowText : "?"));
						}
						PressKeyOnFocused(window, Key.D0, KeyModifiers.Control); // Ctrl+0 复选 head
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate { return revList.SelectedRevision != null; }),
							"F5 刷新后修订列表应仍可用");
						ScreenshotHelper.Snap(window, "13-toggles-refresh", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 15) 外部工具手工路径：Ctrl+Alt+O / Ctrl+Alt+T ============================

	[Fact]
	public void ManualHandlers_CtrlAltO_SilentDegradation_CtrlAltT_ShellToolConfigError()
	{
		string repo = TestRepoFactory.CreateBasic();
		try
		{
			HeadlessAppBootstrap.Run(delegate
			{
				RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
				try
				{
					// ===== 1) Ctrl+Alt+O（FileExplorer，MainWindow.OnKeyDown 手工路径）：静默降级 =====
					// 口径（2026-09-07 与 WPF 原版逐行对齐后定夺）：沙盒无 xdg-open，
					// Process.Start(UseShellExecute) 抛 Win32Exception → catch(Log.Warn) 静默，
					// 不弹窗、不崩。前置修复：Keyboard shim 左右修饰键语义（此前左 Alt 组合被
					// IsKeyDown(RightAlt) 按位误判 → Ctrl+Alt+O 静默早退，连 Process.Start 都走不到）。
					using (var guard = AnyDialogWatchdog.Arm(window))
					{
						PressKeyOnFocused(window, Key.O, KeyModifiers.Control | KeyModifiers.Alt);
						Dispatcher.UIThread.RunJobs();
						Assert.True(guard.ClosedWindow == null,
							"无文件管理器环境 Ctrl+Alt+O 应静默降级不弹窗: "
							+ (guard.ClosedWindow != null ? guard.ClosedWindow.GetType().Name + " → " + guard.ClosedWindowText : "?"));
					}

					// ===== 2) Ctrl+Alt+T（ShellTool，CommandRouter 绑定）：弹配置错误窗 =====
					// 口径：ShellTool 路径不存在时 Log.Error + ErrorWindow 告知用户——WPF 原版
					// 逐行一致的生产正确行为（提示用户去配置），不是回归。断言走看门狗捕获
					//（ErrorWindow.ShowDialog 的模态泵由 200ms 看门狗自动关闭，本段不得挂
					// AnyDialogWatchdog——50ms 扫描会抢先关窗导致看门狗捕获不到文本）。
					HeadlessAppBootstrap.ExpectErrorDialogs();
					PressKeyOnFocused(window, Key.T, KeyModifiers.Control | KeyModifiers.Alt);
					string shellPath = global::ForkPlus.Settings.ForkPlusSettings.Default.ShellTool.ApplicationPath;
					Assert.True(UiClick.WaitFor(delegate
					{
						return HeadlessAppBootstrap.PeekCapturedErrorDialogs().Length > 0;
					}), "Ctrl+Alt+T 在 ShellTool 未配置时应弹 ErrorWindow（看门狗捕获，15s 超时）");
					string[] captured = HeadlessAppBootstrap.TakeCapturedErrorDialogs();
				// 口径对齐（2026-09-07 ExternalToolManager 跨平台修复）：Unix 上 ShellTool.
				// ApplicationPath 可能为 null（沙盒无终端模拟器），错误提示回退为候选终端列表
				// （镜像生产 OpenRepositoryInShellToolCommand.Execute 的 notFoundDisplay 逻辑），
				// 用户能看出该装哪个终端。
				string expectedDisplay = shellPath ?? string.Join("/", global::ForkPlus.UI.ShellTool.UnixTerminalEmulatorCandidates);
				Assert.Contains(E2eMainWindowHarness.TrFormat(
					"Cannot find shellToolPath at '{0}'", expectedDisplay), captured);
					ScreenshotHelper.Snap(window, "15-manual-handlers-shelltool-error", ModuleDir);

					// ===== 3) 应用仍可响应：视图切换照常工作 =====
					PressKeyOnFocused(window, Key.D1, KeyModifiers.Control);
					Assert.Equal(RepositoryViewMode.CommitViewMode, repoControl.ViewMode);
				}
				finally
				{
					E2eMainWindowHarness.CloseRepositoryTab(window, repo);
				}
			});
		}
		finally
		{
			TestRepoFactory.Cleanup(repo);
		}
	}

		// ============================ 16) 作用域负向：宿主子树外按键不得越权触发 ============================

		[Fact]
		public void ScopeNegatives_GesturesOutsideHostSubtree_DoNothing()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						string headBefore = HeadSha(repo);
						PressKeyOnFocused(window, Key.D1, KeyModifiers.Control); // commit 视图
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate { return stage.AllUnstagedFiles.Any(f => f.Path == "a.txt"); }));

						// 焦点移到侧栏（RepositoryUserControl 子树内、CommitUserControl/
						// StageFileUserControl/RevisionListViewUserControl 子树外）
						repoControl.Sidebar.SidebarTreeView.Focus();
						Dispatcher.UIThread.RunJobs();

						using (var guard = AnyDialogWatchdog.Arm(window))
						{
							// ===== 1) Ctrl+Enter（Commit，CommitUserControl 作用域）在侧栏 → 不提交 =====
							PressKeyOnFocused(window, Key.Return, KeyModifiers.Control);
							E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
							Assert.Equal(headBefore, HeadSha(repo));
							Assert.True(HeadSha(repo) == headBefore, "作用域外 Ctrl+Enter 不应提交");

							// ===== 2) Ctrl+Shift+Alt+S（ToggleAllFilesStage，StageFile 作用域）→ 不 stage =====
							PressKeyOnFocused(window, Key.S,
								KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);
							E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
							Assert.True(stage.AllStagedFiles.Length == 0, "作用域外 Ctrl+Shift+Alt+S 不应暂存");

							// ===== 3) Ctrl+C（CopyRevisionSha，修订列表作用域，head 已选）→ 剪贴板不变 =====
							SetClipboard("SENTINEL");
							PressKeyOnFocused(window, Key.C, KeyModifiers.Control);
							Assert.Equal("SENTINEL", ClipboardText());

							Assert.True(guard.ClosedWindow == null,
								"负向按键不应弹任何窗口: " + (guard.ClosedWindow != null ? guard.ClosedWindow.GetType().Name + " → " + guard.ClosedWindowText : "?"));
						}
						ScreenshotHelper.Snap(window, "14-scope-negatives", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
		}
	}
}
}
