using System.ComponentModel;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ReferencePanelReferenceViewModel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ReferencePanelReferenceViewModel.cs（16 行）：
    //   - WPF ReferencePanelReferenceViewModel : INotifyPropertyChanged（abstract）
    //   - abstract string Name 属性
    //   - PropertyChanged 事件 + RaisePropertyChanged(string) protected 方法
    //
    // Avalonia 版差异（spike 简化策略，task spec：POCO 类）：
    //   1. WPF System.ComponentModel.INotifyPropertyChanged → 同（System.ComponentModel，无 UI 依赖）
    //   2. WPF abstract Name 属性 → 同
    //   3. WPF RaisePropertyChanged → 同
    //   4. spike 保持 POCO 形状（无 Avalonia 依赖，纯数据容器）
    //
    // spike 简化（task spec 关键 API）：
    //   - abstract ReferencePanelReferenceViewModel : INotifyPropertyChanged
    //   - abstract string Name 属性
    //   - RaisePropertyChanged(string) protected 方法
    public abstract class ReferencePanelReferenceViewModel : INotifyPropertyChanged
    {
        // 对照 WPF: public abstract string Name
        public abstract string Name { get; }

        // 对照 WPF: public event PropertyChangedEventHandler PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // 对照 WPF: protected void RaisePropertyChanged(string propertyName)
        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
