using ForkPlus.Avalonia.Views.UserControls;

namespace ForkPlus.Avalonia.Commands
{
    // spike 命令基础设施：所有 ForkPlus 命令的基接口。
    // 对照 WPF src/ForkPlus/UI/Commands/IForkPlusCommand.cs（WPF 中为空标记接口）。
    // spike 阶段补充 Id / Header / Execute / CanExecute 四个成员，供命令容器统一调度。
    // - RepositoryUserControl 依赖 → nullable 参数
    // - WPF MainWindow.Instance / RepositoryData 等调用 → 调用方注入或省略
    public interface IForkPlusCommand
    {
        string Id { get; }
        string Header { get; }
        void Execute(RepositoryUserControl? repo);
        bool CanExecute(RepositoryUserControl? repo);
    }
}
