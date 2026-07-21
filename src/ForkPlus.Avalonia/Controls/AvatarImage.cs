using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AvatarImage（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AvatarImage.cs（90 行）：
    //   - WPF AvatarImage : System.Windows.Controls.Image
    //   - DependencyProperty.Register 注册 UserIdentityProperty / UrlProperty
    //   - 重写 OnPropertyChanged(DependencyPropertyChangedEventArgs) 监听属性变更
    //   - 公共方法：ShowAvatarUrl / ShowAvatarNoCache / SetImage / ShowAvatar
    //   - 通过 AvatarManager.Default.RequestAvatar(this, ...) 异步加载头像
    //   - SetImage 仅在 UserIdentity 一致时才设置 Source（防止并发请求错乱）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 System.Windows.Controls.Image → Avalonia.Controls.Image
    //   2. DependencyProperty.Register → StyledProperty<T>.Register
    //   3. OnPropertyChanged(DependencyPropertyChangedEventArgs) → OnPropertyChanged(AvaloniaPropertyChangedEventArgs)
    //      （Avalonia 11 单一变更通知事件，e.Property 字段标识变更的属性）
    //   4. ImageSource → Avalonia.Media.IImage（Avalonia 用 IImage 接口抽象位图/绘图）
    //   5. SetImage 内的 UserIdentity 一致性校验保持不变（纯逻辑零成本复用）
    //
    // spike 简化：
    //   - 仅保留 UserIdentity / Url 两个核心 StyledProperty 与对应的 ShowAvatar 调用
    //   - 实际 HTTP 下载与缓存由 AvatarManager 负责（独立 spike 文件）
    public class AvatarImage : Image
    {
        // 对照 WPF: DependencyProperty.Register("UserIdentity", typeof(UserIdentity), typeof(AvatarImage), ...)
        public static readonly StyledProperty<UserIdentity> UserIdentityProperty =
            AvaloniaProperty.Register<AvatarImage, UserIdentity>(nameof(UserIdentity));

        // 对照 WPF: DependencyProperty.Register("Url", typeof(string), typeof(AvatarImage), ...)
        public static readonly StyledProperty<string> UrlProperty =
            AvaloniaProperty.Register<AvatarImage, string>(nameof(Url));

        public UserIdentity UserIdentity
        {
            get => GetValue(UserIdentityProperty);
            set => SetValue(UserIdentityProperty, value);
        }

        public string Url
        {
            get => GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        // 对照 WPF: protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        // Avalonia 11 用 AvaloniaPropertyChangedEventArgs，e.Property 是 AvaloniaProperty
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == UserIdentityProperty)
            {
                ShowAvatar(UserIdentity);
            }
            else if (change.Property == UrlProperty)
            {
                ShowAvatarUrl(Url);
            }
        }

        // 对照 WPF: public void ShowAvatarUrl(string url)
        public void ShowAvatarUrl(string url)
        {
            if (url == null)
            {
                Source = null;
            }
            else
            {
                AvatarManager.Default.RequestAvatar(this, url);
            }
        }

        // 对照 WPF: public void ShowAvatarNoCache(UserIdentity userIdentity)
        //   spike 版用 new AvatarManager() 跳过缓存，与 WPF 一致
        public void ShowAvatarNoCache(UserIdentity userIdentity)
        {
            new AvatarManager().RequestAvatar(this, userIdentity);
        }

        // 对照 WPF: public void SetImage(ImageSource imageSource, UserIdentity userIdentity)
        //   仅在当前 UserIdentity 与请求时一致时才赋值（防止并发请求错乱）
        public void SetImage(IImage imageSource, UserIdentity userIdentity)
        {
            if (UserIdentity?.Name == userIdentity?.Name
                && UserIdentity?.Email?.ToLower() == userIdentity?.Email?.ToLower())
            {
                Source = imageSource;
            }
        }

        // 对照 WPF: private void ShowAvatar(UserIdentity userIdentity)
        private void ShowAvatar(UserIdentity userIdentity)
        {
            if (userIdentity == null)
            {
                Source = null;
            }
            else
            {
                AvatarManager.Default.RequestAvatar(this, userIdentity);
            }
        }
    }
}
