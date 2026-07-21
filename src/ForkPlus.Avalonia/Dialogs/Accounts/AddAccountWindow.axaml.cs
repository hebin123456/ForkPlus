using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    // Phase 4.29b：Avalonia 版 AddAccountWindow（真实迁移版，对照 WPF AddAccountWindow.xaml.cs 89 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/AddAccountWindow.xaml.cs：
    //   - public partial class AddAccountWindow : ForkPlusDialogWindow
    //   - 嵌套 ServiceViewModel: RemoteType / ServiceName / ImageSource Icon
    //   - 静态 _servicesViewModels: 7 个 RemoteType
    //   - IsSubmitAllowed override: SelectedService != null
    //   - OnSubmit: SelectedService.ServiceType.GetLoginWindow().ShowDialog() → 成功则 CloseWithOk()
    //   - ServicesListBox_MouseDoubleClick: 同 OnSubmit
    //   - ServicesListBox_SelectionChanged: UpdateSubmitButton()
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. ServiceViewModel.Icon (ImageSource) → ServiceViewModel.IconKey (string)
    //      spike 版省略 PNG 资源，用 IconKey 显示文字标识（Phase 4.0c 加回图标）
    //   3. ShowDialog().GetValueOrDefault() → await ShowDialog<bool?>(owner)
    //      需要 OnSubmit 改为 async void（Avalonia 事件处理器支持 async void）
    //   4. MouseDoubleClick → DoubleTapped 事件（Avalonia 同名）
    //   5. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    public partial class AddAccountWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: public class ServiceViewModel : INotifyPropertyChanged
        public class ServiceViewModel : INotifyPropertyChanged
        {
            public RemoteType ServiceType { get; }

            public string ServiceName => ServiceType.FriendlyName();

            // 对照 WPF: public ImageSource Icon => ServiceType.Icon();
            // Avalonia spike 版：用 IconKey 字符串替代 ImageSource
            public string IconKey => ServiceType.GetIconKey();

            public event PropertyChangedEventHandler? PropertyChanged;

            public ServiceViewModel(RemoteType serviceType)
            {
                ServiceType = serviceType;
            }
        }

        // 对照 WPF: private static readonly ServiceViewModel[] _servicesViewModels
        private static readonly ServiceViewModel[] _servicesViewModels = new ServiceViewModel[7]
        {
            new ServiceViewModel(RemoteType.Bitbucket),
            new ServiceViewModel(RemoteType.BitbucketServer),
            new ServiceViewModel(RemoteType.Gitea),
            new ServiceViewModel(RemoteType.Github),
            new ServiceViewModel(RemoteType.GithubEnterprise),
            new ServiceViewModel(RemoteType.Gitlab),
            new ServiceViewModel(RemoteType.GitlabServer)
        };

        private ServiceViewModel? SelectedService => ServicesListBox.SelectedItem as ServiceViewModel;

        protected override bool IsSubmitAllowed
        {
            get
            {
                ClearStatus();
                return SelectedService != null && base.IsSubmitAllowed;
            }
        }

        // onAccountChanged 回调：登录成功后通知调用方刷新远端列表（解耦 MainWindow 依赖）
        private readonly Action? _onAccountChanged;

        public AddAccountWindow(Action? onAccountChanged = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Current("Log in");
            SubmitButtonTitle = Translate("Log in");
            CancelButtonTitle = Translate("Cancel");

            _onAccountChanged = onAccountChanged;
            ServicesListBox.ItemsSource = _servicesViewModels;
        }

        // 对照 WPF: protected override void OnSubmit()
        // Avalonia: 改为 async void 以支持 await ShowDialog<bool?>(owner)
        protected override async void OnSubmit()
        {
            await OpenLoginWindowAsync();
        }

        // 对照 WPF: ServicesListBox_MouseDoubleClick
        public async void ServicesListBox_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            await OpenLoginWindowAsync();
        }

        private async Task OpenLoginWindowAsync()
        {
            ServiceViewModel? selected = SelectedService;
            if (selected == null)
            {
                return;
            }
            // 对照 WPF: ForkPlusDialogWindow loginWindow = SelectedService.ServiceType.GetLoginWindow();
            ForkPlusDialogWindow? loginWindow = selected.ServiceType.GetLoginWindow(null, _onAccountChanged);
            if (loginWindow == null)
            {
                return;
            }
            // 对照 WPF: loginWindow.ShowDialog().GetValueOrDefault()
            // Avalonia 11: await window.ShowDialog<bool?>(owner)
            bool? result = await loginWindow.ShowDialog<bool?>(this);
            if (result.GetValueOrDefault())
            {
                CloseWithOk();
            }
        }

        // 对照 WPF: ServicesListBox_SelectionChanged
        public void ServicesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
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
