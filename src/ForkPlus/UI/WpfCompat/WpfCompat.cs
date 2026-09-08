// WPF → Avalonia 迁移兼容层（v4.0 跨平台重构的人工收尾部分）
// 说明：以下类型在 WPF 中存在、Avalonia 12 无对应物，按"最小可用"原则重建。
// 每一处都有 Migration note标记，后续应逐步替换为 Avalonia 原生实现。

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    // ===== WPF 事件参数与委托 =====

    /// <summary>WPF System.Windows.RequestNavigateEventArgs。</summary>
    public class RequestNavigateEventArgs : EventArgs
    {
        public Uri Uri { get; set; }
        public bool Handled { get; set; }
        public RequestNavigateEventArgs() { }
        public RequestNavigateEventArgs(Uri uri) { Uri = uri; }
    }

    /// <summary>WPF ContextMenu 事件参数（Avalonia 用 ContextRequested 替代）。</summary>
    public class ContextMenuEventArgs : global::Avalonia.Input.ContextRequestedEventArgs
    {
        public object TargetElement { get; set; }
    }

    /// <summary>统一走 Avalonia ContextRequestedEventArgs 签名，lambda 兼容。</summary>
    public delegate void ContextMenuEventHandler(object sender, global::Avalonia.Input.ContextRequestedEventArgs e);

    /// <summary>WPF GiveFeedbackEventArgs（拖放光标反馈，Avalonia 无此事件）。</summary>
    public class GiveFeedbackEventArgs : EventArgs
    {
        public DragDropEffects DragEffects { get; set; }
    }

    /// <summary>WPF DataObjectPastingEventArgs（粘贴拦截，Avalonia TextBox 无此事件）。</summary>
    public class DataObjectPastingEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public string TextToPaste { get; set; }
        /// <summary>WPF e.DataObject 等价物：粘贴内容的包装。</summary>
        public PastingDataObject DataObject { get; set; }
        public void CancelCommand() { Cancel = true; }
    }

    /// <summary>粘贴内容包装（对应 WPF IDataObject 的只读视图）。</summary>
    public sealed class PastingDataObject
    {
        private string _text;
        public PastingDataObject(string text) { _text = text; }
        public bool GetDataPresent(Type t) => t == typeof(string);
        public bool GetDataPresent(string format) => format == DataFormats.Text || format == DataFormats.UnicodeText || format == "System.String";
        public object GetData(Type t) => _text;
        public object GetData(string format) => GetDataPresent(format) ? _text : null;
        public void SetData(string format, object data) { if (data is string s) _text = s; }
    }

    /// <summary>
    /// WPF DataObject.AddPastingHandler 的等价安装器。
    /// 以隧道方式截获 Ctrl+V / Shift+Ins，读剪贴板后回调处理器；
    /// 处理器可改写文本（写回 DataObject）或 CancelCommand 接管粘贴。
    /// Migration note：Avalonia TextBox 无原生粘贴拦截事件，此为行为近似实现。
    /// </summary>
    public static class PasteGuard
    {
        public static void AddPastingHandler(this TextBox textBox, EventHandler<DataObjectPastingEventArgs> handler)
        {
            if (textBox == null || handler == null) return;
            textBox.AddHandler(InputElement.KeyDownEvent, async (object s, KeyEventArgs e) =>
            {
                bool pasteKey = (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                             || (e.Key == Key.Insert && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                if (!pasteKey) return;
                var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
                if (clipboard == null) return;
                string text = await clipboard.TryGetTextAsync();
                if (text == null) return;
                var args = new DataObjectPastingEventArgs { DataObject = new PastingDataObject(text) };
                handler(textBox, args);
                e.Handled = true; // 阻断默认粘贴，由 shim 统一插入
                if (args.Cancel) return; // 处理器已自行处理
                string toInsert = args.DataObject.GetData(DataFormats.Text) as string ?? text;
                int caret = textBox.CaretIndex;
                int start = textBox.SelectionStart, len = textBox.SelectionEnd - textBox.SelectionStart;
                if (len > 0) { textBox.Text = textBox.Text.Remove(start, len); caret = start; }
                textBox.Text = textBox.Text.Insert(caret, toInsert);
                textBox.CaretIndex = caret + toInsert.Length;
            }, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>WPF ToolTipEventArgs。</summary>
    public class ToolTipEventArgs : EventArgs
    {
        public bool IsOpen { get; set; }
    }

    /// <summary>WPF RoutedPropertyChangedEventArgs&lt;T&gt;（Slider/ScrollBar 值变化等）。</summary>
    public class RoutedPropertyChangedEventArgs<T> : EventArgs
    {
        public T OldValue { get; }
        public T NewValue { get; }
        public object Source { get; set; }
        public RoutedPropertyChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>WPF IWeakEventListener（与 WeakEventManager 配套）。</summary>
    public interface IWeakEventListener
    {
        bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e);
    }

    // ===== WPF 拼写检查 no-op 降级（Avalonia 无内置拼写检查）=====

    /// <summary>WPF SpellingError 降级 shim：Suggestions 恒为空。</summary>
    public class SpellingError
    {
        public IEnumerable<string> Suggestions { get; } = Array.Empty<string>();
    }

    /// <summary>WPF EditingCommands 降级 shim。</summary>
    public static class EditingCommands
    {
        private sealed class NoopCommand : System.Windows.Input.ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => false;
            public void Execute(object parameter) { }
        }
        public static System.Windows.Input.ICommand CorrectSpellingError { get; } = new NoopCommand();
        public static System.Windows.Input.ICommand IgnoreSpellingError { get; } = new NoopCommand();
    }

    /// <summary>WPF System.Windows.SpellCheck no-op shim。</summary>
    public class SpellCheck
    {
        public bool IsEnabled { get; set; }
    }

    /// <summary>WPF XmlLanguage shim（仅保留语言标签语义）。</summary>
    public class XmlLanguage
    {
        public string IetfLanguageTag { get; }
        private XmlLanguage(string tag) { IetfLanguageTag = tag; }
        public static XmlLanguage GetLanguage(string ietfLanguageTag) => new XmlLanguage(ietfLanguageTag);
    }

    /// <summary>WPF SystemSounds shim（Avalonia 无系统音效 API，静默）。</summary>
    public static class SystemSounds
    {
        public sealed class SoundPlayer
        {
            internal SoundPlayer() { }
            public void Play() { }
        }
        public static SoundPlayer Beep { get; } = new SoundPlayer();
        public static SoundPlayer Exclamation { get; } = new SoundPlayer();
        public static SoundPlayer Asterisk { get; } = new SoundPlayer();
        public static SoundPlayer Hand { get; } = new SoundPlayer();
        public static SoundPlayer Question { get; } = new SoundPlayer();
    }

    /// <summary>TextBox.GetSpellingError 扩展：恒返回 null（无拼写引擎）。</summary>
    public static class SpellCheckExtensions
    {
        public static SpellingError GetSpellingError(this TextBox textBox, int caretIndex) => null;
    }

    // ===== 颜色工具（OxyPlot.Wpf 命名空间的扩展移到 WpfCompat）=====
    // 注意：ToOxyColor(Color) 直接复用 OxyPlot.Avalonia.ConverterExtensions，避免扩展方法二义性。

    /// <summary>WPF System.Windows.Media.ColorConverter shim（十六进制解析）。</summary>
    public static class ColorConverter
    {
        public static object ConvertFromString(string s)
        {
            // Migration note：WPF 允许省略 # 前缀（6/8 位十六进制直接解析）；Avalonia Color.Parse
            // 必须带 #。原实现对已带 # 的串再前置 # 会得到 "##54A353"（FormatException）。
            s = s.Trim();
            if (!s.StartsWith("#") && (s.Length == 6 || s.Length == 8))
            {
                s = "#" + s;
            }
            return global::Avalonia.Media.Color.Parse(s);
        }
    }

    /// <summary>
    /// WPF ListCollectionView shim：支持 Filter + Refresh 的可绑定集合视图。
    /// Migration note：WPF 的 ItemsControl 接受任意 IEnumerable 视图（Reset 通知即可）；
    /// Avalonia 的 ItemsSourceView 契约是"实现了 INotifyCollectionChanged 就必须实现 IList"，
    /// 否则抛 ArgumentException: Collection implements INotifyCollectionChanged but not IList
    /// （统计窗口 CodeLinesRefListBox.ItemsSource 实测崩溃）。因此本 shim 物化过滤结果为
    /// 只读非泛型 IList；Filter 赋值 / Refresh() 时重算缓存并广播 Reset。
    /// </summary>
    public class ListCollectionView : System.Collections.IList, System.Collections.Specialized.INotifyCollectionChanged
    {
        private readonly System.Collections.IEnumerable _source;
        private readonly System.Collections.Generic.List<object> _view = new();
        private System.Predicate<object> _filter;

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler CollectionChanged;

        public ListCollectionView(System.Collections.IEnumerable source)
        {
            _source = source ?? throw new System.ArgumentNullException(nameof(source));
            Rebuild();
        }

        /// <summary>过滤谓词。赋值即刷新（与 WPF 设置 Filter 自动刷新一致）。</summary>
        public System.Predicate<object> Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                Rebuild();
                OnCollectionChanged();
            }
        }

        public void Refresh()
        {
            Rebuild();
            OnCollectionChanged();
        }

        private void Rebuild()
        {
            _view.Clear();
            foreach (var item in _source)
                if (_filter == null || _filter(item))
                    _view.Add(item);
        }

        private void OnCollectionChanged()
        {
            CollectionChanged?.Invoke(this, new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }

        // ----- IEnumerable（Avalonia ItemsSourceView 经 IList 读）-----
        public System.Collections.IEnumerator GetEnumerator() => _view.GetEnumerator();

        // ----- ICollection / IList：只读物化视图（ItemsSourceView 只读访问即可）-----
        public int Count => _view.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(System.Array array, int index)
        {
            for (int i = 0; i < _view.Count; i++)
                array.SetValue(_view[i], index + i);
        }
        public bool IsFixedSize => false;
        public bool IsReadOnly => true;
        public object this[int index]
        {
            get => _view[index];
            set => throw new System.NotSupportedException("ListCollectionView is a read-only materialized view.");
        }
        public int Add(object value) => throw new System.NotSupportedException("ListCollectionView is a read-only materialized view.");
        public void Clear() => throw new System.NotSupportedException("ListCollectionView is a read-only materialized view.");
        public void Insert(int index, object value) => throw new System.NotSupportedException("ListCollectionView is a read-only materialized view.");
        public void Remove(object value) => throw new System.NotSupportedException("ListCollectionView is a read-only materialized view.");
        public void RemoveAt(int index) => throw new System.NotSupportedException("ListCollectionView is a read-only materialized view.");
        public bool Contains(object value) => _view.Contains(value);
        public int IndexOf(object value) => _view.IndexOf(value);
    }

    // ===== 路由命令体系（WPF RoutedCommand/CommandBinding 的按键路由替代）=====

    public class ExecutedEventArgs : EventArgs
    {
        public object Parameter { get; set; }
        public object Source { get; set; }
    }

    public delegate void ExecutedRoutedEventHandler(object sender, ExecutedEventArgs e);

    /// <summary>WPF RoutedCommand 等价物：承载名称与手势集合。</summary>
    public class RoutedCommand
    {
        public string Name { get; set; } = string.Empty;
        public List<KeyGesture> InputGestures { get; } = new List<KeyGesture>();
        public override string ToString() => Name;
    }

    /// <summary>WPF CommandBinding：命令与处理器的绑定。</summary>
    public class CommandBinding
    {
        public RoutedCommand Command { get; }
        public System.Windows.Input.ICommand CommandInterface { get; }
        public ExecutedRoutedEventHandler Executed { get; }
        public CommandBinding(RoutedCommand command, ExecutedRoutedEventHandler executed)
        {
            Command = command;
            Executed = executed;
        }

        /// <summary>支持 ApplicationCommands 等 ICommand 形态的构造（CustomWindow 用）。</summary>
        public CommandBinding(System.Windows.Input.ICommand command, ExecutedRoutedEventHandler executed)
        {
            CommandInterface = command;
            Executed = executed;
        }
    }

    /// <summary>
    /// 按键路由器：把 WPF CommandBindings 的语义搬到 Avalonia。
    /// WPF 原语义（CommandDevice/CommandManager）有三条关键规则：
    /// 1. 手势翻译发生在 KeyDown 冒泡完成之后（PostProcessInput），控件先消费按键；
    /// 2. 已被控件标记 Handled 的按键不再触发命令（搜索框 Enter 等不被劫持）；
    /// 3. CommandBinding 挂在哪个控件上，仅当焦点位于该控件子树内才生效
    ///    （WPF 从焦点沿祖先路由 Executed，Window 级绑定覆盖全窗口）。
    /// 迁移修复：旧实现用 TopLevel 隧道阶段抢先匹配所有控件的手势，等于全局快捷键，
    /// 导致任意界面按 Enter 都弹出提交独立窗口、搜索框 Enter 失效等副作用。
    /// Migration note：长期应改为 Avalonia HotKey / KeyBinding 原生方案。
    /// </summary>
    public static class CommandRouter
    {
        private sealed class Entry
        {
            public CommandBinding Binding;
            public Interactive Host;
        }

        private static readonly ConditionalWeakTable<TopLevel, List<Entry>> _bindings = new();
        private static readonly ConditionalWeakTable<TopLevel, object> _installed = new();

        public static void AddCommandBinding(this Interactive host, CommandBinding binding)
        {
            if (host == null || binding == null) return;
            var tl = TopLevel.GetTopLevel(host);
            if (tl == null)
            {
                // 尚未挂到视觉树：延迟到 AttachedToVisualTree 后注册
                if (host is Control c)
                {
                    void Attached(object s, VisualTreeAttachmentEventArgs e)
                    {
                        c.AttachedToVisualTree -= Attached;
                        AddCommandBinding(host, binding);
                    }
                    c.AttachedToVisualTree += Attached;
                }
                return;
            }
            Install(tl);
            var list = _bindings.GetOrCreateValue(tl);
            list.Add(new Entry { Binding = binding, Host = host });
        }

        private static void Install(TopLevel tl)
        {
            if (_installed.TryGetValue(tl, out _)) return;
            _installed.Add(tl, new object());
            // WPF 在 KeyDown 冒泡结束后才翻译手势；用冒泡阶段替代旧的隧道抢先执行。
            tl.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
            // 例外（模块28 快捷键全量测试发现的迁移回归，2026-09-07）：带修饰键的 Tab。
            // WPF CommandManager 在 Preview（隧道）阶段匹配手势，永远先于焦点导航；
            // 迁移后只在冒泡阶段翻译，而 Avalonia 的 KeyboardNavigation 类处理在事件源
            // 层就消费 Ctrl+Tab / Ctrl+Shift+Tab 移动焦点（探针实证：焦点跑到 MenuItem、
            // e.Handled=true，TopLevel 冒泡处理器收不到）→ Ctrl+Tab 切 tab 整体失效。
            // 最小修复：隧道阶段仅拦截"带修饰键的 Tab"做同样的手势翻译（其余手势维持
            // 冒泡语义不变——14/17 模块用例已验证的既有行为）。
            tl.AddHandler(InputElement.KeyDownEvent, OnKeyDownTunnelTab, RoutingStrategies.Tunnel);
        }

        /// <summary>隧道阶段：仅翻译带修饰键的 Tab 手势（SelectNextTab/SelectPreviousTab），
        /// 防被焦点导航类处理消费。不带修饰键的 Tab（纯焦点导航）与其他键一律放行。</summary>
        private static void OnKeyDownTunnelTab(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            if (e.Key != Key.Tab) return;
            if (e.KeyModifiers == KeyModifiers.None) return;
            TranslateGesture(sender, e);
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            // 规则 2：控件已处理（如搜索框的 Enter）则不再翻译手势。
            if (e.Handled) return;
            TranslateGesture(sender, e);
        }

        private static void TranslateGesture(object sender, KeyEventArgs e)
        {
            if (sender is not TopLevel tl) return;
            if (!_bindings.TryGetValue(tl, out var list)) return;

            var source = e.Source as Visual ?? tl.FocusManager?.GetFocusedElement() as Visual;
            if (source == null) return;

            Entry best = null;
            int bestDepth = -1;
            foreach (var entry in list)
            {
                var b = entry.Binding;
                if (b.Command == null && b.CommandInterface == null) continue;
                if (!MatchesGesture(b, e)) continue;
                // 规则 3：焦点必须位于注册控件子树内，否则该绑定不参与匹配。
                if (!IsWithinHost(entry.Host, source)) continue;
                // 嵌套宿主（如 StageFileUserControl 在 CommitUserControl 内）最深者优先，
                // 对应 WPF 从焦点向上路由时最近的 CommandBinding 先命中。
                int depth = GetDepth(entry.Host);
                if (depth > bestDepth)
                {
                    best = entry;
                    bestDepth = depth;
                }
            }

            if (best == null) return;
            var command = best.Binding.Command ?? (object)best.Binding.CommandInterface;
            best.Binding.Executed?.Invoke(command, new ExecutedEventArgs { Source = e.Source, Parameter = null });
            e.Handled = true;
        }

        private static bool MatchesGesture(CommandBinding b, KeyEventArgs e)
        {
            if (b.Command != null)
            {
                if (b.Command.InputGestures == null) return false;
                foreach (var g in b.Command.InputGestures)
                {
                    if (Matches(g, e)) return true;
                }
            }
            else if (b.CommandInterface is RoutedCommand rc && rc.InputGestures != null)
            {
                foreach (var g in rc.InputGestures)
                {
                    if (Matches(g, e)) return true;
                }
            }
            return false;
        }

        /// <summary>事件源是否位于宿主控件内：先查视觉祖先链，再查逻辑祖先链
        /// （Popup 内容视觉上在 PopupRoot，逻辑上仍归属主窗口，与 WPF 路由一致）。</summary>
        private static bool IsWithinHost(Interactive host, Visual source)
        {
            if (host is not Visual hostVisual) return false;
            if (ReferenceEquals(hostVisual, source)) return true;
            for (var v = source; v != null; v = v.GetVisualParent())
            {
                if (ReferenceEquals(v, hostVisual)) return true;
            }
            for (var l = source as global::Avalonia.LogicalTree.ILogical; l != null; l = l.LogicalParent)
            {
                if (ReferenceEquals(l, hostVisual)) return true;
            }
            return false;
        }

        private static int GetDepth(Interactive host)
        {
            if (host is not Visual v) return 0;
            int depth = 0;
            for (var a = v.GetVisualParent(); a != null; a = a.GetVisualParent()) depth++;
            return depth;
        }

        private static bool Matches(KeyGesture gesture, KeyEventArgs e)
        {
            if (gesture == null || e.Key == Key.None) return false;
            if (gesture.Key != e.Key) return false;
            var mods = e.KeyModifiers;
            var want = gesture.KeyModifiers;
            return mods == want;
        }
    }

    // ===== Adorner 装饰器体系 =====

    /// <summary>
    /// WPF Adorner 等价物：覆盖在被装饰元素之上的自绘控件。
    /// 基类用 Control 而非 Panel：Avalonia 12 把 Panel.Render 标记为 sealed，
    /// 自绘型装饰器（DragAdorner 等）必须从未密封 Render 的 Control 派生才能 override Render；
    /// 子控件承载型装饰器（CustomAdorner 等）通过 VisualChildren 列表挂子级，布局由各自的
    /// MeasureOverride/ArrangeOverride 手工驱动（与 WPF 原实现一致）。
    /// </summary>
    public abstract class Adorner : global::Avalonia.Controls.Control
    {
        public InputElement AdornedElement { get; }
        protected Adorner(InputElement adornedElement)
        {
            AdornedElement = adornedElement;
            IsHitTestVisible = false;
        }

        // Bug 修复（2026-09-07，"重命名仓库，仓库名没有选中整个名字，只是一个很窄的小框"）：
        // AdornerLayer.RepositionAll() 原本把装饰器 Width/Height 强设为被装饰元素当前
        // Bounds —— 对重命名编辑器（CustomAdorner）是错的：进入编辑态时 ETB 模板里的
        // TextBlock 隐藏、自动宽度的 ETB 塌缩为 0 宽，RepositionAll 随之把编辑框压成
        // 0 宽窄条。WPF 原版 AdornerLayer 从不强制装饰器尺寸（装饰器按内容自量）。
        // true（默认）= 维持既有行为（DropPlace/DragAndDrop 等覆盖被装饰元素自身的装饰器）；
        // false = 只跟随被装饰元素定位，尺寸由装饰器内容自量（重命名编辑框按名字宽度）。
        public virtual bool TracksAdornedElementSize => true;

        // ===== WPF 视觉/逻辑子级管理 API 的 VisualChildren 化 shim =====
        protected void AddVisualChild(global::Avalonia.Controls.Control child)
        {
            if (child != null && !VisualChildren.Contains(child)) VisualChildren.Add(child);
        }
        protected void RemoveVisualChild(global::Avalonia.Controls.Control child)
        {
            if (child != null) VisualChildren.Remove(child);
        }
        // Bug 修复（2026-09-04，"代码悬停/选区的暂存/丢弃浮窗丢失"）：
        // 此前 AddLogicalChild 是空实现。Avalonia 的样式（ControlTheme/Style）只在控件
        // 挂入"逻辑树"（AttachedToLogicalTree → ApplyStyling）时应用；仅经 VisualChildren
        // 挂接的子控件拿不到主题 → TemplatedControl 无模板 → 测量 0×0。WPF 原版
        // Adorner.AddLogicalChild 真正挂逻辑树（隐式样式经逻辑树解析），语义等价物是把
        // 子控件加入 LogicalChildren 集合（ContentControl 承载 Content 就是同一机制）。
        protected void AddLogicalChild(global::Avalonia.Controls.Control child)
        {
            if (child != null && !LogicalChildren.Contains(child)) LogicalChildren.Add(child);
        }
        protected void RemoveLogicalChild(global::Avalonia.Controls.Control child)
        {
            if (child != null) LogicalChildren.Remove(child);
        }
        protected virtual int VisualChildrenCount => VisualChildren.Count;
        protected virtual Visual GetVisualChild(int index) => VisualChildren[index];
    }

    /// <summary>
    /// WPF AdornerLayer 等价物：每个 TopLevel 一个覆盖层 Canvas，
    /// 装饰器按被装饰元素在覆盖层中的坐标摆放。
    /// </summary>
    public sealed class AdornerLayer : Canvas
    {
        private static readonly ConditionalWeakTable<TopLevel, AdornerLayer> _layers = new();
        private readonly List<(Visual adorned, Control adorner)> _items = new();

        private AdornerLayer()
        {
            IsHitTestVisible = true;
            ZIndex = 4096;
        }

        public static AdornerLayer GetAdornerLayer(Visual visual)
        {
            if (visual == null) return null;
            var tl = TopLevel.GetTopLevel(visual);
            if (tl == null) return null;
            var layer = _layers.GetValue(tl, _ => new AdornerLayer());
            if (layer.Parent == null)
            {
                // 把 TopLevel 的内容包进 Grid，再叠加装饰层
                if (tl is ContentControl cc && cc.Content != null && !(cc.Content is Grid g && g.Tag == layer))
                {
                    var old = cc.Content;
                    cc.Content = null;
                    var grid = new Grid { Tag = layer };
                    if (old is Control oldCtl)
                    {
                        oldCtl.SetValue(Panel.ZIndexProperty, 0);
                        grid.Children.Add(oldCtl);
                    }
                    grid.Children.Add(layer);
                    cc.Content = grid;
                }
                tl.LayoutUpdated += (_, _) => layer.RepositionAll();
            }
            return layer;
        }

        public void Add(Visual adorner)
        {
            if (adorner is Control ctl && !_items.Exists(i => i.adorner == ctl))
            {
                var adorned = (adorner as Adorner)?.AdornedElement as Visual ?? adorner;
                _items.Add((adorned, ctl));
                Children.Add(ctl);
                RepositionAll();
            }
        }

        public void Remove(Visual adorner)
        {
            if (adorner is Control ctl)
            {
                _items.RemoveAll(i => i.adorner == ctl);
                Children.Remove(ctl);
            }
        }

        public void Update()
        {
            RepositionAll();
            InvalidateVisual();
        }

        private void RepositionAll()
        {
            foreach (var (adorned, adorner) in _items)
            {
                if (adorned == null || adorner == null) continue;
                var origin = adorned.TranslatePoint(new Point(), this) ?? new Point();
                var size = adorned.Bounds.Size;
                if (adorner is Adorner a && a.AdornedElement is Visual av) size = av.Bounds.Size;
                if (adorner.Tag is Point offset)
                {
                    adorner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    size = adorner.DesiredSize;
                    adorner.SetValue(LeftProperty, origin.X + offset.X);
                    adorner.SetValue(TopProperty, origin.Y + offset.Y);
                }
                else
                {
                    adorner.SetValue(LeftProperty, origin.X);
                    adorner.SetValue(TopProperty, origin.Y);
                }
                if (adorner is Adorner a2 && !a2.TracksAdornedElementSize)
                {
                    // 自量装饰器（重命名编辑框等）：不设 Width/Height（见 Adorner.TracksAdornedElementSize
                    // 注释），只按图层剩余宽度设 MaxWidth —— 对齐 WPF 原版"装饰器可用尺寸 =
                    // 图层尺寸"的语义：超长名字把编辑框限制在窗口内（TextBox 内部横向滚动），
                    // 而不是溢出到窗口外。
                    adorner.MaxWidth = Math.Max(0.0, Bounds.Width - origin.X);
                    continue;
                }
                adorner.Width = size.Width;
                adorner.Height = size.Height;
            }
        }
    }

    // ===== 弱事件管理器（WPF WeakEventManager&lt;T,E&gt;）=====

    /// <summary>
    /// WPF WeakEventManager&lt;TInstance,TArgs&gt; 等价物。
    /// 通过反射订阅实例事件，以弱引用持有订阅者，避免 NotificationCenter 长生命周期造成的泄漏。
    /// </summary>
    public static class WeakEventManager<TInstance, TArgs>
        where TInstance : class
    {
        private sealed class Entry
        {
            public WeakReference Target;
            public MethodInfo Method;
            public Delegate StrongHandler; // 静态 lambda 无目标，强持有
            public void Invoke(object sender, object args)
            {
                if (StrongHandler != null) { StrongHandler.DynamicInvoke(sender, args); return; }
                var t = Target?.Target;
                if (t == null) return;
                Method?.Invoke(t, new[] { sender, args });
            }
        }

        private static readonly ConditionalWeakTable<TInstance, List<(EventInfo ev, Delegate d, Entry entry)>> _map = new();

        public static void AddHandler(TInstance instance, string eventName, Delegate handler)
        {
            if (instance == null || handler == null) return;
            var ev = typeof(TInstance).GetEvent(eventName) ?? instance.GetType().GetEvent(eventName);
            if (ev == null) return;
            var entry = new Entry
            {
                Target = handler.Target != null ? new WeakReference(handler.Target) : null,
                Method = handler.Method,
                StrongHandler = handler.Target == null ? handler : null
            };
            var forwarder = typeof(Forwarder).GetMethod(nameof(Forwarder.OnEvent))
                .MakeGenericMethod(ev.EventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType);
            var proxy = Delegate.CreateDelegate(ev.EventHandlerType, new Forwarder(entry), forwarder);
            ev.AddEventHandler(instance, proxy);
            var list = _map.GetOrCreateValue(instance);
            list.Add((ev, proxy, entry));
        }

        public static void RemoveHandler(TInstance instance, string eventName, Delegate handler)
        {
            if (instance == null || !_map.TryGetValue(instance, out var list)) return;
            list.RemoveAll(x =>
            {
                if (x.ev.Name != eventName) return false;
                if (handler != null && x.entry.Method != handler.Method) return false;
                x.ev.RemoveEventHandler(instance, x.d);
                return true;
            });
        }

        private sealed class Forwarder
        {
            private readonly Entry _entry;
            public Forwarder(Entry entry) { _entry = entry; }
            [Preserve]
            public void OnEvent<TA>(object sender, TA args) => _entry.Invoke(sender, args);
        }
    }
}

// 占位属性，防止 linker 裁剪反射转发方法
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class PreserveAttribute : Attribute { }
}
