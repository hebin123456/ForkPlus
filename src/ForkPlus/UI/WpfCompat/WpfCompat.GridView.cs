// WPF → Avalonia 迁移兼容层：WPF ListView.GridView 占位 shim。
// Avalonia 12 无 GridView/列宽 API；迁移期保住调用面（列宽调参 no-op），
// XAML 侧的列布局已由 Grid 模板承担。
// Migration note：接入 Avalonia TableView 后用原生列宽替换本 shim。

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.UI.WpfCompat
{
    /// <summary>WPF System.Windows.Controls.GridViewColumn shim（列宽记账）。</summary>
    public class GridViewColumn
    {
        public double Width { get; set; } = double.NaN;
        public double ActualWidth => double.IsNaN(Width) ? 0 : Width;
        public object Header { get; set; }
        /// <summary>WPF CellTemplate（Avalonia 侧由模板承担，仅记账）。</summary>
        public object CellTemplate { get; set; }
        /// <summary>WPF DisplayMemberBinding（仅记账，不参与绑定）。</summary>
        public object DisplayMemberBinding { get; set; }
        /// <summary>WPF ActualWidth 近似值（shim 无真实布局，恒 NaN→0）。</summary>
        public Rect Bounds { get; } = default;
    }

    /// <summary>WPF System.Windows.Controls.GridView shim。</summary>
    public class GridView
    {
        public List<GridViewColumn> Columns { get; } = new();
        public GridViewColumnHeader ColumnHeaderContainerStyle { get; set; }
    }

    /// <summary>占位（WPF GridViewColumnHeader）。</summary>
    public class GridViewColumnHeader
    {
        public object ContainerStyle { get; set; }
        public object Content { get; set; }
        /// <summary>Migration note：WPF GridViewColumnHeader.Column（所属列）。迁移期记账占位。</summary>
        public GridViewColumn Column { get; set; }
    }

    /// <summary>WPF ListView.View / AvailableWidth 的附加扩展。</summary>
    public static class GridViewCompat
    {
        private static readonly ConditionalWeakTable<ItemsControl, StrongBox<GridView>> Views = new();
        private static readonly ConditionalWeakTable<ItemsControl, StrongBox<double>> AvailWidths = new();

        /// <summary>WPF listView.View as GridView。首次取时创建 2 列占位。</summary>
        public static GridView GetGridView(this ItemsControl listView)
        {
            if (listView == null) return null;
            if (!Views.TryGetValue(listView, out var box))
            {
                box = new StrongBox<GridView>(new GridView());
                box.Value.Columns.Add(new GridViewColumn());
                box.Value.Columns.Add(new GridViewColumn());
                Views.Add(listView, box);
            }
            return box.Value;
        }

        /// <summary>WPF listView.View = gridView。</summary>
        public static void SetGridView(this ItemsControl listView, GridView view)
        {
            if (listView == null) return;
            Views.Remove(listView);
            if (view != null) Views.Add(listView, new StrongBox<GridView>(view));
        }

        /// <summary>WPF ListView.AvailableWidth（shim：以当前 Bounds.Width 近似）。</summary>
        public static double GetAvailableWidth(this ItemsControl listView)
            => listView?.Bounds.Width ?? 0;
    }
}
