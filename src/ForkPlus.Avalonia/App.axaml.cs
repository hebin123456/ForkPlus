using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ForkPlus.Accounts;
using ForkPlus.Avalonia.Dialogs;
using ForkPlus.Avalonia.Services;
using ForkPlus.Avalonia.Views;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForkPlus.Avalonia
{
    // Phase 1/4：Avalonia Application 实现。
    // 与 WPF 主工程 src/ForkPlus/App.xaml.cs 的 Application 实现并存互不冲突——
    // 两者在不同的 assembly 中（namespace 不同），由各自 exe 启动加载。
    //
    // WPF 源映射（src/ForkPlus/App.xaml.cs，1205 行）：
    //   - RegisterGlobalExceptionLogging()（line 346）：DispatcherUnhandledException +
    //     AppDomain.CurrentDomain.UnhandledException + FirstChanceException +
    //     TaskScheduler.UnobservedTaskException
    //   - OnStartup（line 598）：ServiceLocator.Initialize + theme init + WelcomeWindow flow
    //   - InitializeForkInstance()（line 874）：WelcomeWindow.ShowDialog() if Guid empty
    //   - HandleCommandLineArguments()（line 1021）：NamedPipe IPC for single-instance
    //   - OnExit（line 939）：ForkPlusSettings.Default.Save + IPC server dispose
    //
    // Avalonia 11 vs WPF 差异：
    //   1. Application 基类无 OnStartup/OnExit 虚方法 → 用 OnFrameworkInitializationCompleted
    //      + IControlledApplicationLifetime.Exit 事件
    //   2. Application.Current.Shutdown() → (ApplicationLifetime as
    //      IClassicDesktopStyleApplicationLifetime)?.Shutdown()
    //   3. DispatcherUnhandledException 不存在（Avalonia 无此事件）→ 仅用
    //      AppDomain.CurrentDomain.UnhandledException + TaskScheduler.UnobservedTaskException
    //   4. Application.Current → Application.Current!（null-forgiving）
    //   5. PreferencesLocalization → ServiceLocator.Localization（Core 抽象）
    //
    // spike 简化策略（task spec）：
    //   - Single instance check → 省略（WPF 用 NamedPipe IPC，跨平台实现复杂且 spike 阶段不必要）
    //   - Tray icon → 省略（WPF 用 System.Windows.Forms.NotifyIcon，跨平台无等价 API）
    //   - Welcome flow → 保留（通过 WelcomeWindow 注入 callback 实现）
    //   - Exception handling → AppDomain.CurrentDomain.UnhandledException +
    //     TaskScheduler.UnobservedTaskException（省略 DispatcherUnhandledException 和
    //     FirstChanceException，前者 Avalonia 无此事件，后者仅用于诊断 WPF VisualParenting 异常）
    //   - Theme initialization → 已通过 IThemeService.ApplyTheme 实现
    //   - IPC server (AskPass/Default) → 省略（依赖 NamedPipe + AccountManager，spike 阶段不接入）
    //
    // 职责：
    //   1. 加载 XAML 资源（App.axaml，含 FluentTheme）
    //   2. 在 OnFrameworkInitializationCompleted 中构建 DI 容器 + 初始化 ServiceLocator
    //   3. 注册全局异常日志
    //   4. 处理命令行参数（spike：仅日志记录，不实现 single-instance IPC）
    //   5. Welcome 流程：Guid 为空时显示 WelcomeWindow；取消则 Shutdown
    //   6. 应用默认主题（Light）+ 显示 MainWindow
    //   7. 退出时保存 ForkPlusSettings + 释放 Host
    //
    // Avalonia 11：AvaloniaNameSourceGenerator 会为继承自 Window/UserControl/Control 的
    // axaml 生成 InitializeComponent() 方法，但 Application 根元素不在生成范围内。
    // 故 App.axaml.cs 直接调用 AvaloniaXamlLoader.Load(this) 加载 XAML（与 generator
    // 生成的 InitializeComponent 内部实现一致，见 generated/*.g.cs）。
    public partial class App : Application
    {
        private IHost _host;

        // 对照 WPF App.xaml.cs line 81/83/142：Git 路径解析静态属性
        // spike 版：简化实现，直接从环境变量/ForkPlusSettings 读取
        public static readonly string? EnvironmentGitInstancePath = TryGetEnvironmentGitInstancePath();
        public static readonly string? ForkGitInstancePath = null; // spike: Fork 内置 git 省略
        public static string GitPath => EnvironmentGitInstancePath ?? ForkPlusSettings.Default.GitInstancePath ?? ForkGitInstancePath ?? "git";
        public static string ShellPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(GitPath) ?? "", "sh.exe");
        public static string BashPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(GitPath) ?? "", "bash.exe");

        private static string? TryGetEnvironmentGitInstancePath()
        {
            // spike: 从 PATH 查找 git（WPF 版用 GetEnvironmentVariable + Where-Where）
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;
            foreach (var dir in pathEnv.Split(System.IO.Path.PathSeparator))
            {
                var candidate = System.IO.Path.Combine(dir, "git");
                if (System.IO.File.Exists(candidate)) return candidate;
                candidate = System.IO.Path.Combine(dir, "git.exe");
                if (System.IO.File.Exists(candidate)) return candidate;
            }
            return null;
        }

        public override void Initialize()
        {
            // 直接调用 AvaloniaXamlLoader.Load 加载 App.axaml
            // （generator 不为 Application 根元素生成 InitializeComponent）
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // 构建 DI 容器：注册 Core 抽象接口的实现（Phase 1 仅占位，Phase 3-6 逐步填充）
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    ServiceCollectionExtensions.ConfigureServices(services);
                })
                .Build();

            // 启动 Host（后台服务生命周期管理；Phase 1 暂无后台服务）
            _host.Start();

            // 对照 WPF App.RegisterGlobalExceptionLogging()（App.xaml.cs line 339/346）：
            // spike 省略 DispatcherUnhandledException（Avalonia 无此事件）和 FirstChanceException
            // （仅用于诊断 WPF VisualParenting 异常）。保留 AppDomain + TaskScheduler 两个全局异常入口。
            RegisterGlobalExceptionLogging();

            // 对照 WPF App.OnStartup 中的 ServiceLocator.Initialize（App.xaml.cs line 616）：
            // 业务层（ForkPlus.Core）通过 ServiceLocator.Localization/UserSettings/GitEnvironment 等
            // 访问平台抽象，必须在所有业务调用前完成注入。QuickLaunch / Dialogs / GitCommands 等均依赖。
            InitializeServiceLocator();

            // 对照 WPF App.HandleCommandLineArguments()（App.xaml.cs line 1021）：
            // spike 省略 NamedPipe single-instance IPC，仅日志记录命令行参数供排查问题。
            HandleCommandLineArguments();

            // Phase 2.1：应用默认主题（Light）。
            // Phase 3 起会从 IUserSettings 读取用户上次选择的主题。
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(ForkPlus.UI.ThemeType.Light);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // 对照 WPF App.InitializeForkInstance()（App.xaml.cs line 874）：
                // Guid 为空表示首次启动，需弹出 WelcomeWindow 收集用户名/邮箱/默认克隆目录。
                // spike 版 WelcomeWindow 通过构造函数注入 callback 解耦 RepositoryManager /
                // Application.Current.Shutdown，故这里提供 3 个 callback：
                //   - onSetSourceDir：spike 跳过（RepositoryManager.Instance.SetSourceDirs 是 WPF-only）
                //   - onRescanRepositories：spike 跳过（RescanUserRepositoriesCommand 依赖 RepositoryUserControl）
                //   - onShutdown：错误时调用 desktop.Shutdown()
                // 用户取消 Welcome（按 Cancel）时调用 Shutdown 终止启动流程。
                if (string.IsNullOrEmpty(ForkPlusSettings.Default.Guid))
                {
                    var welcome = new WelcomeWindow(
                        onSetSourceDir: null,
                        onRescanRepositories: null,
                        onShutdown: () => ShutdownApplication());
                    welcome.ShowDialog(desktop.MainWindow);
                    // WelcomeWindow 是模态对话框，ShowDialog 会阻塞直到关闭。
                    // 用户取消（未设置 Guid）则 Shutdown 退出；用户提交则会写入 Guid。
                    if (string.IsNullOrEmpty(ForkPlusSettings.Default.Guid))
                    {
                        ShutdownApplication();
                        return;
                    }
                }

                // Phase 3.1：启动 MainWindow 作为主窗口（spike 骨架版）
                // Phase 1 的 AboutWindow 保留（通过菜单可达）
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                desktop.MainWindow = mainWindow;

                // Avalonia 11 的 Application 基类没有 OnExit 虚方法（WPF 才有
                // OnExit(ApplicationShutdownEventArgs)）。Exit 事件由 IControlledApplicationLifetime
                // 暴露，订阅后在应用退出时优雅关闭 Host，确保后台服务
                // （如 GitOperationQueue）正确释放资源。
                desktop.Exit += OnDesktopExit;
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// 对照 WPF App.RegisterGlobalExceptionLogging()（App.xaml.cs line 346）：
        /// 注册全局异常日志入口，确保未捕获异常也能写入 NLog。
        /// spike 省略 DispatcherUnhandledException（Avalonia 无此事件）和
        /// FirstChanceException（仅用于诊断 WPF VisualParenting 异常）。
        /// </summary>
        private void RegisterGlobalExceptionLogging()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        // 对照 WPF App.CurrentDomain_UnhandledException（App.xaml.cs line 359）
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                Log.Error("Unhandled AppDomain exception", ex);
            }
            else
            {
                Log.Error("Unhandled AppDomain exception: " + e.ExceptionObject);
            }
        }

        // 对照 WPF App.TaskScheduler_UnobservedTaskException（App.xaml.cs line 389）
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error("Unobserved task exception", e.Exception);
        }

        /// <summary>
        /// 对照 WPF App.OnStartup 中的 ServiceLocator.Initialize（App.xaml.cs line 616）：
        /// 把 DI 容器解析出的 Core 抽象实现注入到 ServiceLocator 静态定位器，
        /// 让业务层（ForkPlus.Core）能通过 ServiceLocator.Localization / .UserSettings / .GitEnvironment
        /// 等访问平台抽象。WPF 版在 OnStartup 用 9 个具体 Wpf* 类构造 ServiceLocator，
        /// Avalonia 版从 DI 容器解析（注册见 ServiceCollectionExtensions.ConfigureServices）。
        /// </summary>
        private void InitializeServiceLocator()
        {
            var sp = _host.Services;
            ServiceLocator.Initialize(
                dispatcher: sp.GetRequiredService<IDispatcher>(),
                designMode: sp.GetRequiredService<IDesignModeService>(),
                appContext: sp.GetRequiredService<IAppContext>(),
                clipboard: sp.GetRequiredService<IClipboardService>(),
                timer: sp.GetService<ITimerService>(),
                toast: sp.GetService<IToastNotificationService>(),
                windowManager: sp.GetService<IWindowManagerService>(),
                localization: sp.GetService<ILocalizationService>(),
                gitEnvironment: sp.GetService<IGitEnvironment>(),
                dialogs: sp.GetService<IDialogService>(),
                userSettings: sp.GetService<IUserSettings>(),
                accountManager: sp.GetService<IAccountManager>());
        }

        /// <summary>
        /// 对照 WPF App.HandleCommandLineArguments()（App.xaml.cs line 1021）：
        /// WPF 版用 NamedPipe IPC 实现 single-instance：若有同名进程已运行，把命令行参数
        /// 通过 pipe 转发到已运行进程后 Environment.Exit(0)。
        /// spike 简化：跨平台 NamedPipe single-instance 实现复杂且非启动流程核心，
        /// 这里仅日志记录命令行参数供后续 Phase 接入。task spec 允许"omit (or use named mutex)"。
        /// </summary>
        private void HandleCommandLineArguments()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args == null || args.Length <= 1)
            {
                return;
            }
            try
            {
                Log.Info("Command line args: " + string.Join(" ", args));
            }
            catch
            {
                // 日志失败不影响启动流程
            }
        }

        /// <summary>
        /// 对照 WPF App.DoShutdown()（App.xaml.cs line 947）：调用 Application.Shutdown()。
        /// Avalonia 11：Application 基类无 Shutdown() 方法，需通过
        /// IClassicDesktopStyleApplicationLifetime.Shutdown() 实现。
        /// </summary>
        private void ShutdownApplication()
        {
            (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }

        private void OnDesktopExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            // 对照 WPF App.OnExit（App.xaml.cs line 939）：ForkPlusSettings.Default.Save()
            // 确保用户偏好（主题 / 语言 / 仓库路径等）持久化到 settings.json。
            try
            {
                ForkPlusSettings.Default.Save();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save ForkPlusSettings on exit", ex);
            }

            _host?.StopAsync(TimeSpan.FromSeconds(5));
            _host?.Dispose();
        }
    }
}

