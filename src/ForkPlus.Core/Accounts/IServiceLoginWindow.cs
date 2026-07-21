using ForkPlus.Accounts;

namespace ForkPlus.Accounts
{
    /// <summary>
    /// 登录窗口契约：所有账户登录窗口（GitHubLoginWindow/GitLabLoginWindow/...）实现此接口，
    /// 用于在登录完成后暴露构造的 <see cref="Account"/> 实例。
    ///
    /// 迁移历史：原 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/IServiceLoginWindow.cs（9 行），
    /// 依赖的 <see cref="Account"/> 已在 Core（Phase 0），接口本身无 WPF 依赖，
    /// Phase 4.21b 整体迁入 Core 工程，namespace 从 ForkPlus.UI.Dialogs.Accounts 改为 ForkPlus.Accounts，
    /// 供 WPF / Avalonia / 测试工程共享。
    /// </summary>
    public interface IServiceLoginWindow
    {
        /// <summary>登录窗口构造或登录成功后持有的账户实例（可能为 null 表示取消登录）。</summary>
        Account Account { get; }
    }
}
