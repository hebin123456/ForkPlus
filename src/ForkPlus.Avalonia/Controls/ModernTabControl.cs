using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ModernTabControl（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ModernTabControl.cs（115 行）：
    //   - WPF ModernTabControl : TabControl
    //   - [TemplatePart] PART_IndicatorBorder (Border)
    //   - _indicatorBorder / _isTabIndicatorInitialized / _previousTabIndex / _indicatorWidth
    //   - OnApplyTemplate：获取 PART_IndicatorBorder
    //   - OnRenderSizeChanged：初始化 indicator 位置 + 宽度
    //   - OnSelectionChanged：动画过渡 indicator 到新位置 + 宽度
    //   - UpdateTabIndicatorPosition：TranslateTransform + DoubleAnimation (QuadraticEase, 200ms)
    //   - UpdateTabIndicatorWidth：Width DoubleAnimation (QuadraticEase, 200ms)
    //   - GetTabXCoordinate：累加 TabItem.ActualWidth
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TabControl）：
    //   1. 基类 TabControl → Avalonia.Controls.TabControl（API 一致）
    //   2. WPF [TemplatePart] + OnApplyTemplate → spike 跳过
    //      （spike 不依赖 ControlTemplate，indicator 由外部 axaml 提供）
    //   3. WPF OnRenderSizeChanged (SizeChangedInfo) → Avalonia SizeChanged 事件
    //   4. WPF OnSelectionChanged (SelectionChangedEventArgs) → Avalonia SelectionChanged 事件
    //   5. WPF TranslateTransform + DoubleAnimation (200ms QuadraticEase)
    //      → spike 用 RenderTransform + 简化动画（Avalonia 11 Animation API 差异大）
    //   6. WPF TabItem.ActualWidth → Avalonia TabItem.Bounds.Width
    //   7. spike 跳过动画细节（spike 简化策略：继承 TabControl 即可）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TabControl
    //   - UpdateTabIndicatorPosition / UpdateTabIndicatorWidth 公共方法
    //     （由外部 axaml 中的 indicator Border 调用）
    public class ModernTabControl : TabControl
    {
        // 对照 WPF: private const string IndicatorBorder = "PART_IndicatorBorder"
        private const string IndicatorBorder = "PART_IndicatorBorder";

        // 对照 WPF: private Border _indicatorBorder
        private Border _indicatorBorder;

        // 对照 WPF: private bool _isTabIndicatorInitialized
        private bool _isTabIndicatorInitialized;

        // 对照 WPF: private int _previousTabIndex
        private int _previousTabIndex;

        // 对照 WPF: private double _indicatorWidth
        private double _indicatorWidth;

        public ModernTabControl()
        {
            // 对照 WPF: OnRenderSizeChanged + OnSelectionChanged
            // spike 版：订阅 SizeChanged + SelectionChanged 事件
            SizeChanged += ModernTabControl_SizeChanged;
            SelectionChanged += ModernTabControl_SelectionChanged;
        }

        // 对照 WPF: public override void OnApplyTemplate()
        //   base.OnApplyTemplate();
        //   _indicatorBorder = GetTemplateChild("PART_IndicatorBorder") as Border;
        // spike 版：保留 OnApplyTemplate 虚方法（Avalonia 也有）
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _indicatorBorder = e.NameScope.Find(IndicatorBorder) as Border;
        }

        // 对照 WPF: protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        //   if (base.SelectedItem is TabItem nextTabItem && !_isTabIndicatorInitialized) {
        //     _isTabIndicatorInitialized = true;
        //     UpdateTabIndicatorPosition(withAnimation: false);
        //     UpdateTabIndicatorWidth(nextTabItem, withAnimation: false); }
        private void ModernTabControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SelectedItem is TabItem nextTabItem && !_isTabIndicatorInitialized)
            {
                _isTabIndicatorInitialized = true;
                UpdateTabIndicatorPosition(false);
                UpdateTabIndicatorWidth(nextTabItem, false);
            }
        }

        // 对照 WPF: protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        //   e.Handled = true;
        //   if (_isTabIndicatorInitialized && e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem) {
        //     UpdateTabIndicatorPosition(withAnimation: true);
        //     UpdateTabIndicatorWidth(nextTabItem, withAnimation: true); }
        private void ModernTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isTabIndicatorInitialized && e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem nextTabItem)
            {
                UpdateTabIndicatorPosition(true);
                UpdateTabIndicatorWidth(nextTabItem, true);
            }
        }

        // 对照 WPF: private void UpdateTabIndicatorPosition(bool withAnimation)
        //   TranslateTransform + DoubleAnimation (QuadraticEase, 200ms)
        // spike 版简化：直接设置 TranslateTransform.X（跳过动画）
        public void UpdateTabIndicatorPosition(bool withAnimation)
        {
            if (_isTabIndicatorInitialized && _indicatorBorder != null)
            {
                double tabXCoordinate = GetTabXCoordinate(SelectedIndex);
                // spike 版：直接设置 TranslateTransform.X（跳过 DoubleAnimation）
                var translateTransform = new TranslateTransform
                {
                    X = tabXCoordinate
                };
                _indicatorBorder.RenderTransform = translateTransform;
                _previousTabIndex = Math.Max(0, Math.Min(SelectedIndex, Math.Max(0, Items.Count - 1)));
            }
        }

        // 对照 WPF: private void UpdateTabIndicatorWidth(TabItem nextTabItem, bool withAnimation)
        //   DoubleAnimation (QuadraticEase, 200ms)
        // spike 版简化：直接设置 Width（跳过动画）
        public void UpdateTabIndicatorWidth(TabItem nextTabItem, bool withAnimation)
        {
            if (_isTabIndicatorInitialized && _indicatorBorder != null && nextTabItem != null)
            {
                // 对照 WPF: nextTabItem.ActualWidth
                // spike 版：用 Bounds.Width 替代 ActualWidth
                double width = nextTabItem.Bounds.Width;
                _indicatorBorder.Width = width;
                _indicatorWidth = width;
            }
        }

        // 对照 WPF: private double GetTabXCoordinate(int tabIndex)
        //   double num = 0.0;
        //   if (tabIndex <= 0 || base.Items.Count == 0) return num;
        //   int safeTabIndex = Math.Min(tabIndex, base.Items.Count);
        //   for (int i = 0; i < safeTabIndex; i++) {
        //     if (base.Items[i] is TabItem tabItem) num += tabItem.ActualWidth; }
        //   return num;
        private double GetTabXCoordinate(int tabIndex)
        {
            double num = 0.0;
            if (tabIndex <= 0 || Items.Count == 0)
            {
                return num;
            }
            int safeTabIndex = Math.Min(tabIndex, Items.Count);
            for (int i = 0; i < safeTabIndex; i++)
            {
                if (Items[i] is TabItem tabItem)
                {
                    // 对照 WPF: tabItem.ActualWidth
                    // spike 版：用 Bounds.Width 替代 ActualWidth
                    num += tabItem.Bounds.Width;
                }
            }
            return num;
        }
    }
}
