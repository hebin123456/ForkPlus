// QuickLaunchWindow.axaml.cs：命令面板窗口代码后台（spike 简化版）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/QuickLaunchWindow.xaml.cs（namespace ForkPlus.UI.QuickLaunch, 257 行）
//   - public partial class QuickLaunchWindow : CustomWindow
//   - 字段：_closing / _currentCommandProvider / _refreshCommandListAction / _showCheckout
//   - RepositoryUserControl => Application.Current.ActiveRepositoryUserControl()
//   - 构造函数：InitializeComponent + DesignTimeHelper + Owner + DelayedAction + Loaded + Deactivated +
//     CommandTextBox 事件 + Task.Run(RescanUserRepositoriesCommand)
//   - OnPreviewKeyDown: Escape/Return/Down/Up
//   - RepositoriesListBox_MouseUp: SubmitSelectedItem
//   - SubmitSelectedItem: ftrace/crash 调试命令 + PaletteCommandItem/RepositoryInfoItem/MoveNextArgument
//   - RefreshCommandList: 刷新 provider + ItemsSource + SelectNextRow
//   - RefreshCommandProvider: 按 ArgumentType 切换 provider
//   - CloseWindow / EnableDebugMode
//
// Avalonia 版差异（spike 简化策略，task spec 关键 API）：
//   1. namespace ForkPlus.UI.QuickLaunch -> ForkPlus.Avalonia.QuickLaunch
//   2. WPF CustomWindow (ForkPlus.UI) -> Avalonia CustomWindow (ForkPlus.Avalonia)
//   3. WPF Application.Current.ActiveRepositoryUserControl() -> MainWindow.ActiveRepositoryUserControl
//      （ApplicationExtensions.ActiveRepositoryUserControl 已注释，spike 用 MainWindow 静态属性替代，返回 null）
//   4. WPF base.Owner = MainWindow.Instance -> 跳过（Avalonia Window 无 Owner 属性，用 Show(owner) 设置）
//   5. WPF base.Dispatcher.Async -> Dispatcher.UIThread.Post（task spec 关键 API）
//   6. WPF Application.Current.Dispatcher.BeginInvoke -> Dispatcher.UIThread.Post
//   7. WPF CommandTextBox.CommandDescriptor -> _currentCommand 本地字段
//      （spike CommandTextBox 不暴露 CommandDescriptor 属性，用 SetArguments + 本地字段替代）
//   8. WPF CommandTextBox.SetCommandDescriptor(command) -> SetCurrentCommand(command) 本地方法
//      （调用 spike CommandTextBox.SetArguments(names) + 存储 CommandDescriptor 到 _currentCommand）
//   9. WPF CommandTextBox.MoveNextArgument(item) -> CommandTextBox.MoveNextArgument(item, title)
//      （spike 签名：(object argumentValue, string title = null)）
//  10. WPF CommandTextBox.CurrentCommandArgument (Argument) -> CurrentCommandArgument 属性
//      （spike CommandTextBox.CurrentCommandArgument 返回 string，本属性按 name 映射回 Argument）
//  11. WPF OnPreviewKeyDown (tunneling) -> AddHandler(KeyDownEvent, handler, RoutingStrategies.Tunnel)
//      （Avalonia 11 无 WPF 的 Preview tunneling 事件，用 RoutingStrategies.Tunnel 注册）
//  12. WPF PreferencesLocalization.Current -> ServiceLocator.Localization.Current
//  13. WPF RescanUserRepositoriesCommand().Execute(reset: false) -> Execute(null)
//      （spike RescanUserRepositoriesCommand.Execute(RepositoryUserControl? repo)）
//  14. WPF MainWindow.Commands.SendCrashReport.Execute() -> Execute(null)
//      （spike RelayCommand.Execute(object parameter) 需参数）
//  15. WPF RepositoryManagerUserControl.Commands.OpenRepository -> 跳过（WPF-only）
//  16. WPF Keyboard.IsKeyDown(Key.LeftCtrl) -> 跳过（WPF-only，且原代码结果未使用）
//  17. WPF MouseUp -> Avalonia PointerReleased
//  18. spike RefreshCommandProvider: RepositoryUserControl 为 null，RepositoryData/GitModule 不可访问，
//      仅保留 DefaultCommandProvider(null) 和 WorkspaceCommandProvider 分支

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Avalonia.Commands;
using ForkPlus.Avalonia.Views;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public partial class QuickLaunchWindow : global::ForkPlus.Avalonia.CustomWindow
    {
        private bool _closing;

        private ICommandProvider _currentCommandProvider = new DummyCommandProvider();

        // 对照 WPF: private readonly DelayedAction<bool> _refreshCommandListAction;
        private readonly DelayedAction<bool> _refreshCommandListAction;

        private bool _showCheckout;

        // spike: 替代 CommandTextBox.CommandDescriptor（spike CommandTextBox 不暴露此属性）
        private CommandDescriptor _currentCommand;

        // 对照 WPF: private RepositoryUserControl RepositoryUserControl => Application.Current.ActiveRepositoryUserControl();
        // spike: ApplicationExtensions.ActiveRepositoryUserControl 已注释，用 MainWindow.ActiveRepositoryUserControl 替代（返回 null）
        private object RepositoryUserControl => MainWindow.ActiveRepositoryUserControl;

        // 对照 WPF: CommandTextBox.CurrentCommandArgument (返回 Argument)
        // spike: CommandTextBox.CurrentCommandArgument 返回 string（参数名），本属性按 name 映射回 Argument
        private Argument CurrentCommandArgument
        {
            get
            {
                if (_currentCommand == null)
                {
                    return null;
                }
                string currentName = CommandTextBox.CurrentCommandArgument;
                if (string.IsNullOrEmpty(currentName))
                {
                    return null;
                }
                foreach (Argument arg in _currentCommand.Arguments)
                {
                    if (arg.Name == currentName)
                    {
                        return arg;
                    }
                }
                return null;
            }
        }

        public QuickLaunchWindow(bool showCheckout = false)
        {
            InitializeComponent();

            // 对照 WPF: if (global::ForkPlus.DesignTimeHelper.IsInDesignMode()) { ... return; }
            if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
            {
                Title = TranslateCurrent("Quick Launch");
                return;
            }

            // 对照 WPF: base.Owner = MainWindow.Instance;
            // spike: Avalonia Window 无 Owner 属性，跳过（用 Show(owner) 设置）

            _showCheckout = showCheckout;
            _refreshCommandListAction = new DelayedAction<bool>(RefreshCommandList, 0.1);

            // 对照 WPF: base.Loaded += delegate { _refreshCommandListAction.InvokeNow(parameter: false); };
            Loaded += delegate
            {
                _refreshCommandListAction.InvokeNow(false);
            };

            // 对照 WPF: base.Deactivated += delegate { base.Dispatcher.Async(delegate { CloseWindow(); }); };
            // spike: base.Dispatcher.Async -> Dispatcher.UIThread.Post
            Deactivated += delegate
            {
                Dispatcher.UIThread.Post(() => CloseWindow());
            };

            // 对照 WPF: AddHandler(KeyDownEvent, handler, RoutingStrategies.Tunnel)
            // spike: WPF OnPreviewKeyDown (tunneling) -> Avalonia 用 RoutingStrategies.Tunnel 注册
            // 拦截 Escape/Return/Down/Up 在 CommandTextBox 之前处理
            AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

            // 对照 WPF: commandTextBox.CommandArgumentsCompleted += delegate { CloseWindow(); Application.Current.Dispatcher.BeginInvoke(...) { CommandTextBox.CommandDescriptor.Converter(e, RepositoryUserControl); } };
            // spike: CommandTextBox.CommandDescriptor -> _currentCommand
            CommandTextBox.CommandArgumentsCompleted += (s, e) =>
            {
                CloseWindow();
                Dispatcher.UIThread.Post(() =>
                {
                    _currentCommand?.Converter(e, RepositoryUserControl);
                });
            };

            // 对照 WPF: CommandTextBox.TextChanged += delegate { _refreshCommandListAction.InvokeWithDelay(parameter: false); };
            CommandTextBox.TextChanged += delegate
            {
                _refreshCommandListAction.InvokeWithDelay(false);
            };

            // 对照 WPF: commandTextBox2.CommandArgumentChanged += delegate { _refreshCommandListAction.InvokeNow(parameter: false); };
            CommandTextBox.CommandArgumentChanged += delegate
            {
                _refreshCommandListAction.InvokeNow(false);
            };

            // 对照 WPF: Task.Run(delegate { new RescanUserRepositoriesCommand().Execute(reset: false); base.Dispatcher.Async(...) { _refreshCommandListAction.InvokeNow(...); } });
            // spike: Execute(reset: false) -> Execute(null)（spike RescanUserRepositoriesCommand.Execute(RepositoryUserControl? repo)）
            Task.Run(() =>
            {
                new RescanUserRepositoriesCommand().Execute(null);
                Dispatcher.UIThread.Post(() =>
                {
                    _refreshCommandListAction.InvokeNow(false);
                });
            });
        }

        // 对照 WPF: protected override void OnPreviewKeyDown(KeyEventArgs e)
        // spike: 用 AddHandler(Tunnel) 注册，签名改为 void(object, KeyEventArgs)
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseWindow();
                return;
            }
            if (e.Key == Key.Return)
            {
                e.Handled = true;
                SubmitSelectedItem();
            }
            else
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    RepositoriesListBox.SelectNextRow(RepositoriesListBox.SelectedIndex, true, (object x) => !(x is HeaderCommandProviderItem));
                    return;
                }
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    RepositoriesListBox.SelectPreviousRow(RepositoriesListBox.SelectedIndex, true, (object x) => !(x is HeaderCommandProviderItem));
                    return;
                }
            }
        }

        // 对照 WPF: private void RepositoriesListBox_MouseUp(object sender, MouseButtonEventArgs e)
        // spike: WPF MouseUp -> Avalonia PointerReleased
        private void RepositoriesListBox_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            SubmitSelectedItem();
        }

        // 对照 WPF: private void SubmitSelectedItem()
        private void SubmitSelectedItem()
        {
            if (CommandTextBox.Text == "ftrace")
            {
                EnableDebugMode();
                CloseWindow();
            }
            else if (CommandTextBox.Text == "crash")
            {
                // spike: Execute() -> Execute(null)（RelayCommand.Execute(object parameter) 需参数）
                MainWindow.Commands.SendCrashReport.Execute(null);
                CloseWindow();
            }
            else
            {
                if (!(RepositoriesListBox?.SelectedItem is CommandProviderItem commandProviderItem))
                {
                    return;
                }
                // spike: CommandTextBox.CommandDescriptor == null -> _currentCommand == null
                if (_currentCommand == null)
                {
                    if (commandProviderItem is PaletteCommandItem paletteCommandItem)
                    {
                        if (paletteCommandItem.Command.Arguments.Length == 0)
                        {
                            CloseWindow();
                            paletteCommandItem.Command.Converter(new object[0], RepositoryUserControl);
                        }
                        else
                        {
                            // spike: CommandTextBox.SetCommandDescriptor(command) -> SetCurrentCommand(command)
                            SetCurrentCommand(paletteCommandItem.Command);
                        }
                    }
                    else if (commandProviderItem is RepositoryInfoItem repositoryInfoItem)
                    {
                        // 对照 WPF: Keyboard.IsKeyDown(Key.LeftCtrl); CloseWindow(); RepositoryManagerUserControl.Commands.OpenRepository.Execute(repositoryInfoItem.Repository);
                        // spike: Keyboard.IsKeyDown WPF-only 跳过; RepositoryManagerUserControl.Commands WPF-only 跳过
                        CloseWindow();
                        Log.Error("OpenRepository not implemented in spike (RepositoryManagerUserControl.Commands WPF-only)");
                    }
                    else
                    {
                        CloseWindow();
                        Log.Error("root item must be a command or a repo");
                    }
                }
                else
                {
                    // spike: CommandTextBox.MoveNextArgument(commandProviderItem) -> MoveNextArgument(commandProviderItem, title)
                    CommandTextBox.MoveNextArgument(commandProviderItem, commandProviderItem.Title);
                }
            }
        }

        // 对照 WPF: private void RefreshCommandList(bool _)
        private void RefreshCommandList(bool _)
        {
            _currentCommandProvider = RefreshCommandProvider(CurrentCommandArgument);
            string filterString = CommandTextBox.Text.Trim().ToLower();
            _currentCommandProvider.Refresh(filterString);
            RepositoriesListBox.ItemsSource = _currentCommandProvider.Items;
            if (_showCheckout)
            {
                _showCheckout = false;
                // spike: _allCommands 为空（GetAllCommands 简化），"Checkout Branch" PaletteCommandItem 不存在
                // 跳过 WPF 的自动选中 + SubmitSelectedItem 逻辑
            }
            RepositoriesListBox.SelectNextRow(0, true, (object x) => !(x is HeaderCommandProviderItem));
        }

        // 对照 WPF: private ICommandProvider RefreshCommandProvider(Argument argument)
        // spike: RepositoryUserControl 为 null，RepositoryData/GitModule 不可访问
        //   仅保留 DefaultCommandProvider(null) 和 WorkspaceCommandProvider 分支
        private ICommandProvider RefreshCommandProvider(Argument argument)
        {
            if (argument == null)
            {
                if (_currentCommandProvider is DefaultCommandProvider)
                {
                    return _currentCommandProvider;
                }
                // spike: RepositoryUserControl?.RepositoryData 不可访问（RepositoryUserControl 为 null object）
                return new DefaultCommandProvider(null);
            }
            if (argument.Type == _currentCommandProvider.Type)
            {
                return _currentCommandProvider;
            }
            // spike: RepositoryUserControl 为 null，repositoryData/gitModule 不可访问
            // 跳过 WPF 的 RepositoryFile/Remote/Reference/GitFlow 分支（均依赖 RepositoryData + GitModule）
            if (argument.Type == ArgumentType.Workspace)
            {
                return new WorkspaceCommandProvider(ForkPlusSettings.Default.Workspaces.All);
            }
            return _currentCommandProvider;
        }

        // 对照 WPF: private void CloseWindow()
        private void CloseWindow()
        {
            if (_closing)
            {
                return;
            }
            _closing = true;
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to close window", ex);
            }
        }

        // 对照 WPF: private void EnableDebugMode()
        //   MainWindow.Commands.ToggleTraceElapsedTime.Execute();
        private void EnableDebugMode()
        {
            // spike: Execute() -> Execute(null)
            MainWindow.Commands.ToggleTraceElapsedTime.Execute(null);
        }

        // spike 辅助：替代 CommandTextBox.SetCommandDescriptor(CommandDescriptor)
        // 调用 spike CommandTextBox.SetArguments(string[]) + 存储 CommandDescriptor 到 _currentCommand
        private void SetCurrentCommand(CommandDescriptor command)
        {
            _currentCommand = command;
            if (command != null && command.Arguments.Length > 0)
            {
                // spike: 用 SetArguments(names) 替代 SetCommandDescriptor(command)
                CommandTextBox.SetArguments(MapArgumentNames(command.Arguments));
            }
            else
            {
                CommandTextBox.SetArguments(null);
            }
        }

        // spike 辅助：从 Argument[] 提取参数名数组
        private static string[] MapArgumentNames(Argument[] arguments)
        {
            string[] names = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                names[i] = arguments[i].Name;
            }
            return names;
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
        // spike: ServiceLocator.Localization.Current(text)（task spec 关键 API）
        private static string TranslateCurrent(string text)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.Current(text);
            }
            return text;
        }
    }
}
