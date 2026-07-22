using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/ControlTemplateExtensions.cs（18 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - TryFindName<T>(this ControlTemplate source, string name, FrameworkElement templatedParent, out T match)
    //     → source.FindName(name, templatedParent) 在模板 NameScope 中查找命名元素
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. ControlTemplate → Avalonia.Controls.Primitives.ControlTemplate
    //      （Avalonia 11 中 ControlTemplate 是 IControlTemplate 实现类，不再是 WPF 的 Content-laden 类型）
    //   2. FrameworkElement → Avalonia.Controls.Control（spike 规范：FrameworkElement → Control）
    //   3. ControlTemplate.FindName(name, templatedParent) → INameScope.Find(name)
    //      WPF 通过 ControlTemplate 内部 NameScope 查找；Avalonia 11 ControlTemplate 是
    //      IControlTemplate（Func<Control, INameScope>），无 FindName 方法，
    //      改为从 templatedParent 的 INameScope 查找（通过 NameScope.GetNameScope 附加属性获取）
    //   4. 实际使用场景：OnApplyTemplate 中查找 PART_ 模板部件，
    //      Avalonia 习惯用 TemplatedControl.GetTemplateChild(name)，本扩展方法保留供兼容调用
    public static class ControlTemplateExtensions
    {
        public static bool TryFindName<T>(this object source, string name, Control templatedParent, out T match) where T : class
        {
            // spike: WPF ControlTemplate.FindName(name, templatedParent) → Avalonia INameScope.Find(name)
            INameScope nameScope = null;
            if (templatedParent is INameScope ns)
            {
                nameScope = ns;
            }
            else if (templatedParent != null)
            {
                nameScope = NameScope.GetNameScope(templatedParent);
            }
            object obj = nameScope?.Find(name);
            match = obj as T;
            return match != null;
        }
    }
}
