// WPF → Avalonia 迁移兼容层 第五部分：WPF DependencyProperty.Register 风格属性注册。
// WPF: DependencyProperty.Register(name, propertyType, ownerType, new PropertyMetadata(default, changed))
// Avalonia 12: AvaloniaProperty.Register<TOwner, TValue>(name, defaultValue)，变更回调走 Changed 订阅。

using System;
using Avalonia;

namespace ForkPlus.UI.WpfCompat
{
    /// <summary>WPF DependencyProperty.Register 近似封装。</summary>
    public static class WpfPropertyCompat
    {
        /// <summary>
        /// 注册 StyledProperty 并可选挂变更回调（对应 WPF PropertyMetadata(default, OnXxxChanged)）。
        /// </summary>
        public static StyledProperty<TValue> Register<TOwner, TValue>(
            string name, TValue defaultValue = default,
            Action<TOwner, AvaloniaPropertyChangedEventArgs> changed = null)
            where TOwner : AvaloniaObject
        {
            // v4.0.5：AVP1001（Register 应在静态构造/静态初始化器中调用）在此抑制——
            // 本封装层的全部调用点都是 WPF 风格的 static readonly 字段初始化器 / 静态构造函数
            // （CustomWindow / GraphCellView / AiActionButton / ContributionHeatmap 等，已逐一核对），
            // 语义上满足告警要求（类型加载期一次性注册），分析器只是无法跨方法验证封装层调用点。
#pragma warning disable AVP1001
            var property = AvaloniaProperty.Register<TOwner, TValue>(name, defaultValue);
#pragma warning restore AVP1001
            if (changed != null)
            {
                // Avalonia 12 的 AvaloniaProperty<T>.Changed 是 IObservable<AvaloniaPropertyChangedEventArgs<T>>，
                // lambda 直订阅在本编译环境不匹配（ObservableExtensions 不可用），用显式 IObserver 适配。
                property.Changed.Subscribe(new ActionObserver<AvaloniaPropertyChangedEventArgs<TValue>>(e =>
                {
                    if (e.Sender is TOwner owner) changed(owner, e);
                }));
            }
            return property;
        }

        /// <summary>Action → IObserver 适配器（IObservable.Subscribe 用）。</summary>
        private sealed class ActionObserver<TArg> : IObserver<TArg>
        {
            private readonly Action<TArg> _onNext;
            public ActionObserver(Action<TArg> onNext) { _onNext = onNext; }
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(TArg value) => _onNext(value);
        }
    }
}
