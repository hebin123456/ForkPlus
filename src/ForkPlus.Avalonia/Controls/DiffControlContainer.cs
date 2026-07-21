using System;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DiffControlContainer（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DiffControlContainer.cs（80 行）：
    //   - WPF DiffControlContainer : Grid, ILocalizableControl
    //   - Grid 2 行：Row 0 (Auto) FileControlHeaderUserControl（默认 Collapse）
    //                  Row 1 (*)    动态切换的 _subView（ShowSubView<TChild>）
    //   - IFileDiffControlSubControl 接口：ControlWillBeRemovedFromFileDiffControl 回调
    //   - ShowSubView<TChild>(factory, initialize) 动态装载子视图
    //   - AttachSubView 用 VisualTreeAttachmentHelper.TryAddChild 挂载
    //   - ApplyLocalization 转发到 Header + _subView
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ContentControl）：
    //   1. WPF Grid 基类 → Avalonia ContentControl（spike 单 Content 子视图）
    //   2. WPF DependencyProperty → spike 用 plain property（无 styled property 必要）
    //   3. WPF VisualTreeAttachmentHelper.TryAddChild → Avalonia ContentControl.Content 直接赋值
    //   4. WPF ILocalizableControl.ApplyLocalization → spike 跳过（无 Header 子控件）
    //   5. WPF FileControlHeaderUserControl Header 属性 → spike 跳过（Header 由子类自行管理）
    //   6. spike 用 ContentControl.Content 单子视图替代 Grid + 双行布局
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ContentControl
    //   - ShowSubView<TChild>(factory, initialize) 动态装载子视图
    //   - IFileDiffControlSubControl 接口保持（ControlWillBeRemovedFromFileDiffControl）
    public class DiffControlContainer : ContentControl
    {
        // 对照 WPF: public interface IFileDiffControlSubControl
        public interface IFileDiffControlSubControl
        {
            void ControlWillBeRemovedFromFileDiffControl();
        }

        // 对照 WPF: private FrameworkElement _subView
        // spike: ContentControl.Content 直接承载，无需 _subView 字段，但仍保留以兼容旧路径
        private Control _subView;

        // 对照 WPF: public void ShowSubView<TChild>(Func<TChild> factory, Action<TChild, FileControlHeaderUserControl> initialize)
        // spike: initialize 参数简化为 Action<TChild>（无 FileControlHeaderUserControl）
        public void ShowSubView<TChild>(Func<TChild> factory, Action<TChild> initialize = null) where TChild : Control
        {
            if (_subView == null)
            {
                _subView = factory();
                if (!AttachSubView(_subView))
                {
                    _subView = null;
                    return;
                }
            }
            else if (!(_subView is TChild))
            {
                if (_subView is IFileDiffControlSubControl fileDiffControlSubControl)
                {
                    fileDiffControlSubControl.ControlWillBeRemovedFromFileDiffControl();
                }
                _subView = factory();
                if (!AttachSubView(_subView))
                {
                    _subView = null;
                    return;
                }
            }
            initialize?.Invoke(_subView as TChild);
        }

        // 对照 WPF: private bool AttachSubView(FrameworkElement subView)
        // spike: ContentControl.Content 直接赋值（替代 VisualTreeAttachmentHelper.TryAddChild）
        private bool AttachSubView(Control subView)
        {
            if (subView == null)
            {
                return false;
            }
            Content = subView;
            return true;
        }
    }
}
