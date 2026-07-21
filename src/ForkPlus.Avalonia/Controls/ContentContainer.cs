using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ContentContainer（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ContentContainer.cs（29 行）：
    //   - WPF ContentContainer : Grid
    //   - private UIElement _childControl
    //   - ShowControl(UIElement control)：移除旧 _childControl + TryAddChild 新 control
    //   - ShowContent()：移除 _childControl + 置空
    //   - VisualTreeAttachmentHelper.TryAddChild 处理子控件挂载
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ContentControl）：
    //   1. WPF Grid 基类 → Avalonia ContentControl（task spec 明确要求）
    //   2. WPF base.Children.Remove + VisualTreeAttachmentHelper.TryAddChild
    //      → Avalonia ContentControl.Content 直接赋值（API 简化）
    //   3. WPF UIElement (System.Windows.UIElement) → Avalonia.IControl / Control
    //   4. spike 用 object 类型兼容 ContentControl.Content（Avalonia Content 接受 object）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ContentControl
    //   - ShowControl(object control) 设置 Content
    //   - ShowContent() 清空 Content
    public class ContentContainer : ContentControl
    {
        // 对照 WPF: private UIElement _childControl
        // spike 版：用 ContentControl.Content 直接托管，无需额外字段

        // 对照 WPF: public void ShowControl(UIElement control)
        //   base.Children.Remove(_childControl);
        //   if (!VisualTreeAttachmentHelper.TryAddChild(this, control, ...)) { _childControl = null; return; }
        //   _childControl = control;
        // spike 版：直接赋值 Content（Avalonia ContentControl 自动管理子控件）
        public void ShowControl(object control)
        {
            Content = control;
        }

        // 对照 WPF: public void ShowContent()
        //   if (_childControl != null) { base.Children.Remove(_childControl); _childControl = null; }
        // spike 版：直接清空 Content
        public void ShowContent()
        {
            Content = null;
        }
    }
}
