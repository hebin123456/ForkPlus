// ForkPlus.Tests 全部 headless UI 测试的统一启动器（"归一"，2026-09-02）：
// MenuWindowSmokeTests / UiSmokeHeadlessTests / DetachedPopupBehaviorTests /
// ResourceCompatTests 共享同一个进程级 headless Application——继承真实 ForkPlus.App，
// 拿到全套 App.axaml 资源（FluentTheme / OxyPlot / AvaloniaEdit 主题 / 合并字典），
// 只 override 掉启动副作用（IPC 单例、主窗口、git 版本弹窗）。
//
// 为什么不再用各测试类自带的 EnsureStarted + SpinUntil(5s/30s)：
//   1) Dispatcher.UIThread 是进程级单例，首个触碰它的线程成为 owner。xUnit 默认跨
//      Collection 并行，任意并行测试（直接或经生产代码间接）先碰 Dispatcher.UIThread，
//      headless 启动线程初始化 Compositor 时即抛 "different thread owns it"，未处理
//      异常直接崩掉 test host——偶发 "Test Run Aborted" 的根因（迁移期实证）。
//   2) SpinUntil 带超时：冷启 JIT 慢时"超时后继续"，worker 线程抢走 Dispatcher 归属，
//      启动线程随即崩溃；且 5s/30s 两档不一致，行为随机器负载漂移。
//   3) 4 份启动代码配了 2 种 App（真实 App / 裸 FluentTheme App），先启动者胜出——
//      后续类复用哪个 App 取决于执行顺序，需要真实资源的测试结果顺序相关。
//
// 启动时序（两层保障）：
//   [ModuleInitializer] 程序集加载期（任何测试代码执行前）spawn 启动线程——注意只
//   spawn 不等待：模块初始化器在加载器锁下运行，app 线程初始化要加载 Avalonia/
//   ForkPlus 程序集、触碰本模块类型，若在此阻塞等待会死锁（已实证：testhost 永久挂起）。
//   启动线程第一件事"抢占" Dispatcher.UIThread 归属（先于耗时 JIT/Setup，窗口微秒级，
//   任何测试线程都赶不上），随后正常 Setup——Compositor 初始化的 VerifyAccess 恒通过。
//   测试线程经 EnsureStarted 无超时等待就绪（发生在模块初始化完成之后，无锁风险）。
// 启动失败不在 ModuleInit 抛（避免连累非 headless 测试加载程序集），记入 startupError，
// 由首个 headless 测试经 EnsureStarted 显式抛出。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;

namespace ForkPlus.Tests
{
	internal static class HeadlessAppBootstrap
	{
		// 继承真实 App：构造时 InitializeComponent() 加载 App.axaml 全部资源/样式/DataTemplates；
		// 只 override 掉启动逻辑（IPC 单例、主窗口、git 版本弹窗等），保真且不依赖桌面。
		private sealed class HeadlessRealApp : global::ForkPlus.App
		{
			public override void OnFrameworkInitializationCompleted()
			{
			}
		}

		private static readonly ManualResetEvent Ready = new ManualResetEvent(false);
		private static int startRequested;
		private static volatile Exception startupError;

		// ===== 后台错误弹窗看门狗（模块16 回归轮全量挂死根因修复，2026-09-05）=====
		// 现象：dotnet test 空转挂死 15+ 分钟，CPU 0%，dotnet-stack 实证 UI 线程卡在
		// 两层嵌套 WindowDialogCompat.ShowDialog → PushFrame；dotnet-dump 读出两个
		// GitCommandError+BtError 文本：对已被 TestRepoFactory.Cleanup 删除的临时仓库
		// （/tmp/fpe2e_*/work/.git）打开 bt 仓库失败——前序用例的后台刷新任务在 tab
		// 关闭后仍在跑，目录删除 → 刷新失败 → RefreshRepositoryCommand 向 Dispatcher
		// 排队 ErrorWindow.ShowDialog()（模态）→ headless 无人点 Close → 永久死锁。
		// 看门狗：DispatcherTimer（模态 PushFrame 泵消息期间也会触发）自动关闭任何
		// 可见 ErrorWindow 并记录其文本——把"无限挂死"变成"带根因文本的测试失败"。
		internal static readonly List<string> CapturedErrorDialogs = new List<string>();

		private static DispatcherTimer _errorDialogWatchdog;

		// 模块 25（2026-09-06）：ErrorWindow 自身 E2E 用例的看门狗暂停开关——
		// 看门狗每 200ms 自动关闭任何可见 ErrorWindow（含 RunJobs 触发），而模块 25
		// 用例需要窗口存活断言/截图/点击修复按钮。暂停期间用例必须自己 Close 窗口；
		// 恢复后 Run&lt;T&gt; 收尾的 CloseVisibleErrorDialogs 仍会兜底关残留（防挂死不回退）。
		private static bool _errorDialogWatchdogSuspended;

		/// <summary>暂停/恢复错误弹窗看门狗（ErrorWindow 自身用例专用；finally 必须恢复）。</summary>
		internal static void SetErrorDialogWatchdogSuspended(bool suspended)
		{
			_errorDialogWatchdogSuspended = suspended;
		}

		/// <summary>扫描并关闭当前所有可见 ErrorWindow，记录其文本（看门狗 tick 与
		/// Run&lt;T&gt; 收尾同步调用——后者消除 200ms 定时器滞后带来的漏检窗口）。</summary>
		private static void CloseVisibleErrorDialogs()
		{
			try
			{
				if (_errorDialogWatchdogSuspended)
				{
					return;
				}
				if (Application.Current?.ApplicationLifetime is not ClassicDesktopStyleApplicationLifetime lifetime)
				{
					return;
				}
				Window[] windows = lifetime.Windows.ToArray();
				foreach (Window window in windows)
				{
					if (window is global::ForkPlus.UI.Dialogs.ErrorWindow errorWindow && errorWindow.IsVisible)
					{
						string text;
						try
						{
							text = errorWindow.MessageTextBox?.Text;
						}
						catch
						{
							text = null;
						}
						lock (CapturedErrorDialogs)
						{
							CapturedErrorDialogs.Add(string.IsNullOrEmpty(text)
								? "<ErrorWindow 无文本>"
								: text);
						}
						errorWindow.Close(); // ShowDialog 的 PushFrame 随窗口关闭退出
					}
				}
			}
			catch
			{
				// 看门狗自身异常绝不能影响测试线程
			}
		}

		private static void StartErrorDialogWatchdog()
		{
			// 在 UI 线程（启动线程）上创建；Default 优先级保证测试里显式 RunJobs() 也会触发 tick
			_errorDialogWatchdog = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Default, delegate
			{
				CloseVisibleErrorDialogs();
			});
			_errorDialogWatchdog.Start();
		}

		/// <summary>测试间窗口泄漏兜底（2026-09-07，模块28 全 E2e 回归发现的既有基建缺口）：
		/// 部分用例非模态 Show 的窗口（TagDetailsWindow / GitLfsStatusWindow / PatchWindow...）
		/// 结束时未 Close，遗留到进程级 WpfApp.Windows——后续用例的 AnyDialogWatchdog 负向
		/// 断言（"按键不得弹窗"）会把残留窗口当成本用例的意外弹窗（QuickPush 捕获到
		/// TagDetailsWindow 等 3 例失败）。Run&lt;T&gt; 收尾统一关闭所有可见窗口：
		///   ① ErrorWindow 先由 CloseVisibleErrorDialogs 处理（含文本捕获语义），这里跳过；
		///   ② MainWindow 类型跳过——Closed→lifetime.Shutdown 铁律，生命周期归测试挂具
		///     （CloseRepositoryTab/DetachWindow）管理，Hide 过的 IsVisible=false 不受影响；
		/// 模块25 ErrorWindow 自身用例的暂停开关只影响错误看门狗，本兜底照常执行
		///（用例断言在 func 内已结束，收尾关闭不回退任何既有防线）。</summary>
		private static void CloseLeftoverTestWindows()
		{
			try
			{
				if (Application.Current?.ApplicationLifetime is not ClassicDesktopStyleApplicationLifetime lifetime)
				{
					return;
				}
				foreach (Window window in lifetime.Windows.ToArray())
				{
					if (!window.IsVisible || window is global::ForkPlus.UI.MainWindow || window is global::ForkPlus.UI.Dialogs.ErrorWindow)
					{
						continue;
					}
					try
					{
						window.Close();
					}
					catch
					{
						// 单个窗口关闭失败不阻碍其余清理
					}
				}
			}
			catch
			{
				// 兜底自身异常绝不能影响测试线程
			}
		}

		[System.Runtime.CompilerServices.ModuleInitializer]
		internal static void ModuleInit()
		{
			EnsureGitInstanceForTests();
			StartAsync();
		}

		/// <summary>
		/// 端到端测试的 git 实例兜底（2026-09-04，"Cannot find git instance"）：
		/// App.GitPath 解析链为 环境变量(forkgitinstance) → settings.GitInstancePath → 内置实例
		/// （LocalApplicationData/ForkPlus/gitInstance/2.50.1/bin/git）。沙箱/CI 没有安装内置
		/// git 时，原先所有走 GitRequest 的 e2e 全部失败（"Cannot find git instance"）。
		/// 这里在 App 静态构造（bootstrap 线程 Configure&lt;HeadlessRealApp&gt; 触发）之前设好
		/// 环境变量指向系统 git；已有显式环境变量或内置实例存在时不动，保证开发机行为不变。
		/// </summary>
		private static void EnsureGitInstanceForTests()
		{
			try
			{
				string envVariable = global::ForkPlus.Consts.ForkPlus.GitInstanceEnvVariable;
				if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVariable)))
				{
					return;
				}
				string exeName = OperatingSystem.IsWindows() ? "git.exe" : "git";
				// 与 App.GetForkGitInstancePath 相同的路径拼法，但直接计算——避免提前触发 App 静态构造
				string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				string bundledGit = Path.Combine(localApplicationData, "ForkPlus", "gitInstance", "2.50.1", "bin", exeName);
				if (File.Exists(bundledGit))
				{
					return;
				}
				// 内置 git 缺失：从 PATH 找系统 git 作 e2e 兜底
				string pathVariable = Environment.GetEnvironmentVariable("PATH");
				if (string.IsNullOrEmpty(pathVariable))
				{
					return;
				}
				foreach (string directory in pathVariable.Split(Path.PathSeparator))
				{
					try
					{
						string candidate = Path.Combine(directory.Trim(), exeName);
						if (File.Exists(candidate))
						{
							Environment.SetEnvironmentVariable(envVariable, candidate);
							return;
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		// 所有 headless 测试类的唯一入口：确保 App 已就绪。等待无超时上限——"超时后继续"
		// 会让 worker 抢走 Dispatcher 归属（见类注释 2），宁可挂住暴露真实启动问题。
		// 此时模块初始化早已完成，阻塞等待没有加载锁风险。
		internal static void EnsureStarted()
		{
			StartAsync();
			Ready.WaitOne();
			if (startupError != null)
			{
				throw new InvalidOperationException("headless Application 启动失败：" + startupError.Message, startupError);
			}
		}

		// 在 UI 线程执行 func 并排空 job 队列（原 4 个测试类各自的 Run<T> 收拢于此）。
		// 错误弹窗策略（看门狗配套）：动作期间若出现 ErrorWindow（看门狗已自动关闭并
		// 记录文本），测试以根因文本失败——生产代码弹错误窗说明该用例窗口内有真实
		// 错误，静默吞掉会把 bug 藏进绿色用例（用户 Bug 修复策略约定）。
		// 例外：ExpectErrorDialogs() 声明的用例（如模块17 Lean sync "You must sync"
		// 校验路径——错误弹窗本身就是被测行为），收尾不抛，测试用 TakeCapturedErrorDialogs
		// 取文本断言。
		private static bool _expectErrorDialogs;

		/// <summary>声明"本用例预期会出现错误弹窗"：看门狗照旧关闭+记录，Run 收尾不因
		/// 捕获到弹窗而失败。调用后用 TakeCapturedErrorDialogs() 取捕获文本断言。</summary>
		internal static void ExpectErrorDialogs()
		{
			_expectErrorDialogs = true;
			lock (CapturedErrorDialogs)
			{
				CapturedErrorDialogs.Clear();
			}
		}

		/// <summary>取走（并清空）看门狗捕获的错误弹窗文本快照。</summary>
		internal static string[] TakeCapturedErrorDialogs()
		{
			lock (CapturedErrorDialogs)
			{
				string[] snapshot = CapturedErrorDialogs.ToArray();
				CapturedErrorDialogs.Clear();
				return snapshot;
			}
		}

		/// <summary>窥视（不清空）当前已捕获的错误弹窗文本——供用例内轮询等待预期弹窗
		/// 被看门狗处理（后台 Task 弹窗有 200ms 看门狗滞后）。</summary>
		internal static string[] PeekCapturedErrorDialogs()
		{
			lock (CapturedErrorDialogs)
			{
				return CapturedErrorDialogs.ToArray();
			}
		}

		internal static T Run<T>(Func<T> func)
		{
			EnsureStarted();
			return Dispatcher.UIThread.InvokeAsync(delegate
			{
				bool expectErrors = _expectErrorDialogs;
				_expectErrorDialogs = false; // 声明只对本次 Run 生效
				string[] capturedDuringRun = null;
				try
				{
					lock (CapturedErrorDialogs)
					{
						if (!expectErrors && CapturedErrorDialogs.Count > 0)
						{
							// 用例间遗留（上一个用例收尾后的后台任务弹窗）：记控制台不归责当前用例
							Console.WriteLine("[HeadlessAppBootstrap] 测试间遗留 ErrorWindow（看门狗已关闭）: "
								+ string.Join(" | ", CapturedErrorDialogs));
						}
						if (expectErrors)
						{
							CapturedErrorDialogs.Clear(); // 预期场景：遗留弹窗不干扰本用例断言
						}
						else
						{
							CapturedErrorDialogs.Clear();
						}
					}
					T result = func();
					Dispatcher.UIThread.RunJobs();
					CloseVisibleErrorDialogs(); // 同步扫描一次，消除定时器 200ms 滞后的漏检
					lock (CapturedErrorDialogs)
					{
						capturedDuringRun = CapturedErrorDialogs.ToArray();
						CapturedErrorDialogs.Clear();
					}
					if (capturedDuringRun.Length > 0 && !expectErrors)
					{
						throw new InvalidOperationException("用例执行期间出现 git 错误弹窗（看门狗已自动关闭，根因文本如下）："
							+ Environment.NewLine + string.Join(Environment.NewLine, capturedDuringRun));
					}
					return result;
				}
				finally
			{
				// 窗口泄漏兜底（func 抛异常路径同样清理）：非模态 Show 未 Close 的测试窗口
				// 关闭，防遗留到后续用例的负向断言——见 CloseLeftoverTestWindows 注释
				CloseLeftoverTestWindows();
				if (expectErrors)
				{
					// 快照回填：func 期间捕获的弹窗文本交还 TakeCapturedErrorDialogs（调用方断言）
					// ——上面"捕获后清空"在预期场景会把文本丢掉，这里从局部变量恢复
					lock (CapturedErrorDialogs)
					{
						if (CapturedErrorDialogs.Count == 0 && capturedDuringRun != null)
						{
							CapturedErrorDialogs.AddRange(capturedDuringRun);
						}
					}
				}
			}
			}).GetAwaiter().GetResult();
		}

		internal static void Run(Action action)
		{
			Run<object>(delegate
			{
				action();
				return null;
			});
		}

		private static bool _registerHeadlessDragSourceDone;

		/// <summary>向 AvaloniaLocator 注册 headless 进程内拖拽源（反射，绕过 PrivateApi 编译封锁）。
		/// 等价于 Avalonia 11 公开写法 AvaloniaLocator.CurrentMutable
		/// .Bind&lt;IPlatformDragSource&gt;().ToConstant(HeadlessDragSourceProxy.CreatePlatformDragSource())。
		/// 只在首次启动注册一次（幂等防御：已注册时跳过——代理本身无状态，重复注册无害但没必要）。</summary>
		private static void RegisterHeadlessDragSource()
		{
			if (_registerHeadlessDragSourceDone)
			{
				return;
			}
			// 经公开类型 DragDrop 定位 Avalonia.Base 真实程序集（运行时加载 lib 实现，
			// 非编译期 ref 程序集——PrivateApi 哨兵签名替换只存在于 ref，真实成员可反射）。
			global::System.Type locatorType = typeof(global::Avalonia.Input.DragDrop).Assembly
				.GetType("Avalonia.AvaloniaLocator");
			global::System.Type dragSourceType = typeof(global::Avalonia.Input.Platform.IPlatformDragSource);
			const global::System.Reflection.BindingFlags StaticFlags =
				global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static;
			const global::System.Reflection.BindingFlags InstanceFlags =
				global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance;
			object mutableLocator = locatorType.GetProperty("CurrentMutable", StaticFlags).GetValue(null);
			object helper = locatorType.GetMethod("Bind", InstanceFlags)
				.MakeGenericMethod(dragSourceType)
				.Invoke(mutableLocator, null);
			helper.GetType().GetMethod("ToConstant", InstanceFlags)
				.MakeGenericMethod(dragSourceType)
				.Invoke(helper, new object[] { HeadlessDragSourceProxy.CreatePlatformDragSource() });
			_registerHeadlessDragSourceDone = true;
		}

		private static void StartAsync()
		{
			if (Interlocked.Exchange(ref startRequested, 1) == 0)
			{
				var t = new Thread(delegate()
				{
					try
					{
						// ⚠️ 第一件事抢占 Dispatcher.UIThread 归属（进程级单例、首触线程拥有）：
						// 必须发生在耗时的 JIT/App 构造之前，赶在任何并行测试线程触碰之前——
						// 否则下方 Setup 里 Compositor 初始化的 VerifyAccess 直接崩掉 test host。
						GC.KeepAlive(Dispatcher.UIThread);
						// SetupWithClassicDesktopLifetime（而非 SetupWithoutStarting）：挂上
						// ClassicDesktopStyleApplicationLifetime——WpfApp.Windows / ShowDialog
						// 兼容层从它取窗口列表；lifetime 赋值必须发生在 Setup 之前，不能用
						// SetupWithoutStarting + 后补（之后赋值 Application 会抛异常）。
						// UseSkia + UseHeadlessDrawing=false（2026-09-03，"TextBox 选区像素级验证"引入）：
						// 默认 headless 绘图模式没有真渲染后端——RenderTargetBitmap.Render 产出空位图、
						// CaptureRenderedFrame 恒 null，任何像素级回归断言都无从谈起。切换到真 Skia
						// 软件渲染（Tests 项目经 Avalonia.Desktop 传递引用 Skia）后 RTB 可像素读回
						// （Bitmap.CopyPixels(ILockedFramebuffer)）。全套件 3958 用例实证切换后仍全绿。
						// With(FontSetup.CreateFontManagerOptions())（2026-09-09，v4.0.5 字体内置）：
						// 与生产 Program.BuildAvaloniaApp 共用同一份全局字体回退配置（内置
						// Noto Sans CJK SC 子集）。生产已启用该回退，测试环境保持一致才能
						// 验证字体行为；内嵌字体与本机系统 Noto CJK 同源同字形，覆盖区间内
						// 的文本度量不变，既有像素级回归不受影响。
						AppBuilder.Configure<HeadlessRealApp>()
							.UseSkia()
							.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
							.With(global::ForkPlus.FontSetup.CreateFontManagerOptions())
							.SetupWithClassicDesktopLifetime(Array.Empty<string>(), delegate { });
						// 默认 ShutdownMode.OnLastWindowClose：单个测试关闭唯一窗口会把
						// Dispatcher 整个 shut down，后续测试的 InvokeAsync 全部
						// TaskCanceledException。测试由 xunit 进程托管生命周期，改显式关闭。
						if (Application.Current.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime desktopLifetime)
						{
							desktopLifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
						}
						// E2E（2026-09-05，"上下文搜索无结果"）：生产 App 在 RunStartup() 里做
						// ServiceLocator.Initialize，而 headless 覆写了 OnFrameworkInitializationCompleted
						// 跳过启动副作用——ServiceLocator.Dispatcher 恒 null，DelayedAction 等经
						// ServiceLocator.Dispatcher?.Post 的回调被静默丢弃（修订列表搜索、各种防抖全部失效）。
						// 这里补齐与生产一致的轻量服务（纯包装类，构造无副作用）。
						if (!global::ForkPlus.Services.ServiceLocator.IsInitialized)
						{
							global::ForkPlus.Services.ServiceLocator.Initialize(
								dispatcher: new global::ForkPlus.Services.Wpf.WpfDispatcher(Dispatcher.UIThread),
								designMode: new global::ForkPlus.Services.Wpf.WpfDesignModeService(),
								appContext: new global::ForkPlus.Services.Wpf.WpfAppContext(),
								clipboard: new global::ForkPlus.Services.Wpf.WpfClipboardService(),
								timer: new global::ForkPlus.Services.Wpf.WpfTimerService(),
								toast: new global::ForkPlus.Services.Wpf.WpfToastNotificationService(),
								windowManager: new global::ForkPlus.Services.Wpf.WpfWindowManagerService());
						}
						// E2E 拖拽（E2e29，2026-09-10）：headless 平台无 IPlatformDragSource——
					// DragDrop.DoDragDropAsync 查不到服务直接 no-op，DragOver/Drop 永不触发。
					// 注册进程内拖拽源（把后续指针事件转成拖放路由事件，语义对齐 DragDropDevice/
					// Win32 OLE，详见 HeadlessInProcessDragSource 注释），tab 拖拽用例得以走完整管线。
					// PrivateApi 双重封锁（2026-09-10 实证，Avalonia 12.1.1 源码）：不但
					// IPlatformDragSource 标了 [PrivateApi]，整个 AvaloniaLocator 类也标了
					// [PrivateApi]——编译器把 CurrentMutable 当不存在（CS0117），Bind/ToConstant
					// 同样不可访问。反射注册：运行时加载的是 lib 实现程序集（真实签名未裁），
					// 经 DragDrop 所在程序集定位 AvaloniaLocator 类型后逐级反射调用
					// CurrentMutable.Bind<IPlatformDragSource>().ToConstant(代理)。
					// 服务查询路径（DragDrop.cs 源码）：AvaloniaLocator.Current.GetService<T>——
					// 静态构造 Current = CurrentMutable 同一实例，往 CurrentMutable 注册即生效。
					RegisterHeadlessDragSource();
						// E2E 环境隔离（2026-09-09，CI 偶发红 "QuickFetch 不应弹窗: ErrorWindow →
						// cannot lock ref"）：每个测试窗口都带一个 1 分钟首 tick 的
						// AutomaticBackgroundFetchManager（FetchRemotesAutomatically 默认 true），
						// 全量套件并行创建多窗口且运行十几分钟——早期窗口的 timer tick 会对
						// 其他用例刚打开的仓库做后台 fetch，与用例自身的 QuickFetch 并发竞争
						// refs/remotes/* 的 git 乐观锁，后完成方 CAS 失败弹 ErrorWindow 打断断言。
						// 测试环境一律禁用后台自动 fetch（生产 QuickFetchCommand 另有同名 job
						// 防重入兜底真实用户场景）。
						global::ForkPlus.Settings.ForkPlusSettings.Default.FetchRemotesAutomatically = false;
						// v4.0.5 字体预热（2026-09-09）：内嵌 CJK 回退字体（~25MB OTF × 2 字重）的
						// 加载解析、字形整形、文本测量管线首次开销一次性发生在启动期——否则落在
						// 首个 E2E 用例的首次渲染里，慢环境（本沙箱）下视图装配竞态临界后移，
						// E2e05-10 的 WaitFor 轮询裸 NRE 翻车率上升（对照实验：基线 0 vs 带字体 4-10）。
						// Measure 即触发 TextLayout → FontManager 回退链解析 → 内嵌字体加载整形；
						// 文本须含 CJK 字符（拉丁文本不触发回退），两字重各预热一次。
						var latinCJK = "ForkPlus 字体预热 提交 검색 かな gjpqy";
						new global::Avalonia.Controls.TextBlock { Text = latinCJK, FontSize = 13 }.Measure(global::Avalonia.Size.Infinity);
						new global::Avalonia.Controls.TextBlock { Text = latinCJK, FontSize = 13, FontWeight = global::Avalonia.Media.FontWeight.Bold }.Measure(global::Avalonia.Size.Infinity);
					}
					catch (Exception e)
					{
						startupError = e;
						Ready.Set();
						return;
					}
					Ready.Set();
				// 错误弹窗看门狗必须在 MainLoop 之前启动（见类头注释）：此后 UI 线程持续
				// 泵消息，任何时点（含用例间）出现的 ErrorWindow 都会被自动关闭并记录
				StartErrorDialogWatchdog();
				Dispatcher.UIThread.MainLoop(new CancellationToken());
				});
				t.IsBackground = true;
				t.Start();
			}
		}
	}
}
