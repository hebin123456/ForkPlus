using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.7b：Avalonia 版 LongOperationWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/LongOperationWindow.xaml.cs（83 行）：
    //   - public partial class LongOperationWindow : ForkPlusDialogWindow
    //   - 字段：Func<Task> _operation / Exception _exception
    //   - 构造函数 (title, description, Action operation): 转 Func<Task> 调用下一构造
    //   - 构造函数 (title, description, Func<Task> operation):
    //     * base(preventMainWindowRefresh: false)  // spike 基类不支持此参数
    //     * InitializeComponent()
    //     * DialogTitle = Translate(title)
    //     * DialogDescription = Translate(description)
    //     * ShowSubmitButton = false
    //     * ShowCancelButton = false
    //     * MessageTextBlock.Text = Translate("This operation is taking longer than usual. Please wait.")
    //     * Loaded += LongOperationWindow_Loaded
    //   - 静态 Run(title, description, Action operation): new + ShowDialog + 异常重抛
    //   - 静态 RunAsync(title, description, Func<Task> operation): new + ShowDialog + 异常重抛
    //   - LongOperationWindow_Loaded: Dispatcher.BeginInvoke(Idle, async delegate { try await _operation(); catch _exception; finally CloseWithOk(); })
    //
    // 调用方（WPF 版，同步阻塞）：
    //   LongOperationWindow.Run("Title", "Description", () => DoSomething());
    //
    // 调用方（Avalonia 版，async/await）：
    //   await LongOperationWindow.RunAsync(owner, "Title", "Description", async () => await DoSomethingAsync());
    public partial class LongOperationWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private Func<Task> _operation;
        private Exception _exception;

        public LongOperationWindow(string title, string description, Action operation)
            : this(title, description, operation == null ? null : new Func<Task>(delegate
            {
                operation();
                return Task.CompletedTask;
            }))
        {
        }

        public LongOperationWindow(string title, string description, Func<Task> operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            ShowFooter = false; // 对照 WPF: ShowSubmitButton=false + ShowCancelButton=false
            InitializeComponent();
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription
            string t = Translate(title);
            Title = t;
            DialogTitle = t;
            DialogDescription = Translate(description);

            // 对照 WPF: MessageTextBlock.Text = Translate("This operation is taking longer than usual. Please wait.")
            MessageTextBlock.Text = Translate("This operation is taking longer than usual. Please wait.");

            // 对照 WPF: Loaded += LongOperationWindow_Loaded
            Loaded += LongOperationWindow_Loaded;
        }

        // 对照 WPF: public static void Run(string title, string description, Action operation)
        // Avalonia 11 的 ShowDialog 是 async Task<object>，不能像 WPF 那样同步阻塞。
        // 调用方需要 await RunAsync。
        public static async Task RunAsync(global::Avalonia.Controls.Window owner, string title, string description, Action operation)
        {
            var window = new LongOperationWindow(title, description, operation);
            await window.ShowDialog(owner);
            if (window._exception != null)
            {
                ExceptionDispatchInfo.Capture(window._exception).Throw();
            }
        }

        // 对照 WPF: public static void RunAsync(string title, string description, Func<Task> operation)
        public static async Task RunAsync(global::Avalonia.Controls.Window owner, string title, string description, Func<Task> operation)
        {
            var window = new LongOperationWindow(title, description, operation);
            await window.ShowDialog(owner);
            if (window._exception != null)
            {
                ExceptionDispatchInfo.Capture(window._exception).Throw();
            }
        }

        // 对照 WPF: private void LongOperationWindow_Loaded(object sender, RoutedEventArgs e)
        private void LongOperationWindow_Loaded(object sender, EventArgs e)
        {
            // 对照 WPF: Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, async delegate {...})
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await _operation();
                }
                catch (Exception ex)
                {
                    _exception = ex;
                }
                finally
                {
                    CloseWithOk();
                }
            }, DispatcherPriority.ApplicationIdle);
        }

        // 对照 WPF: private static string Translate(string text)
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }
}
