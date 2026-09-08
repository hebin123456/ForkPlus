// WPF → Avalonia 迁移兼容层 第四部分：第二批高频 API shim
// （TryFindResource / Owner / ContainerFromElement / DoDragDrop / Cursors / Imaging stub）
// 全部带 Migration note标记，收尾阶段逐个替换为原生实现。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    // （TryFindResource / SetResourceReference 见 WpfCompat.Controls.cs 的 ResourceCompat）

    // ===== WPF System.Windows.Media.BitmapImage（XAML 可声明的 IImage 资源）=====

    /// <summary>
    /// WPF BitmapImage shim：主题字典里 `<BitmapImage x:Key="CommitIcon" UriSource="pack://..."/>`
    /// 迁移为 `<wpf:BitmapImage x:Key="CommitIcon" UriSource="avares://..."/>`。
    /// Avalonia 的 Bitmap 无 XAML 可设属性（构造函数要求流/文件名），x:String 存 URI
    /// 又会让 `{DynamicResource XxxIcon}` 绑定 Image.Source 时 InvalidCastException（string→IImage），
    /// 故用本类补齐"可声明+可设 UriSource"的 IImage。
    /// </summary>
    public class BitmapImage : global::Avalonia.Media.IImage
    {
        private global::Avalonia.Media.Imaging.Bitmap _bitmap;

        private global::System.Uri _uriSource;

        public global::System.Uri UriSource
        {
            get => _uriSource;
            set
            {
                if (object.Equals(_uriSource, value))
                {
                    return;
                }
                _uriSource = value;
                _bitmap = (value == null) ? null : new global::Avalonia.Media.Imaging.Bitmap(
                    global::Avalonia.Platform.AssetLoader.Open(value));
            }
        }

        public global::Avalonia.Size Size => _bitmap?.Size ?? default;

        public void Draw(global::Avalonia.Media.DrawingContext context, global::Avalonia.Rect sourceRect, global::Avalonia.Rect destRect)
        {
            // Bitmap 的 Draw 是 IImage 显式接口实现，不能直接调用成员
            (_bitmap as global::Avalonia.Media.IImage)?.Draw(context, sourceRect, destRect);
        }
    }

    // ===== WPF Window.Owner（Avalonia 只能经 ShowDialog(owner) 设置）=====

    public static class WindowOwnerCompat
    {
        private static readonly ConditionalWeakTable<Window, StrongBox<Window>> Owners = new();

        /// <summary>
        /// WPF w.Owner = owner。Avalonia 的 Owner 无公开 setter；
        /// 这里记表供 WindowDialogCompat.ShowDialog 优先使用（不设置也能找活动窗口兜底）。
        /// </summary>
        public static void SetOwnerCompat(this Window self, Window owner)
        {
            Owners.Remove(self);
            Owners.Add(self, new StrongBox<Window>(owner));
        }

        /// <summary>
        /// Migration note：WPF 对象初始化器 { Owner = owner, WindowStartupLocation = CenterOwner } 的等价物。
        /// Avalonia 对话框居中由 ShowDialog(owner) 保证，这里只登记 owner 并返回自身以便链式调用。
        /// </summary>
        public static Window SetOwnerAndCenter(this Window self, Window owner)
        {
            self.SetOwnerCompat(owner);
            return self;
        }

        internal static Window TryGetOwner(Window self)
            => Owners.TryGetValue(self, out var box) ? box.Value : null;

        /// <summary>
        /// Migration note：WPF 非模态窗口 { Owner=.., WindowStartupLocation=CenterOwner } 的等价物。
        /// Avalonia 的 CenterOwner 只在 ShowDialog(owner) 模态路径生效；非模态 Show() 不定位时
        /// 由窗口管理器随意摆放（多显示器下常落到另一块屏），CenterScreen 则永远回主屏——
        /// 主窗口在副屏时弹窗"跑到另一个显示器"的根因。
        /// 这里在 Show() 前订阅 Opened，把窗口居中到 owner（或主窗口）所在屏幕的工作区
        /// （复用 WindowDialogCompat.CenterToOwnerScreenOnOpened，含 DPI 物理像素换算）。
        /// </summary>
        public static void ShowAtOwnerScreen(this Window self)
        {
            if (self == null)
            {
                return;
            }
            Window owner = TryGetOwner(self) ?? WpfApp.MainWindow;
            WindowDialogCompat.CenterToOwnerScreenOnOpened(self, owner);
            self.Show();
        }
    }

    // ===== WPF ItemsControl.ContainerFromElement =====

    public static class ItemsControlCompat
    {
        /// <summary>
        /// WPF ItemsControl.ContainerFromElement(element)：从内部元素找到条目容器。
        /// 实现：沿可视树向上找 ItemContainerGenerator 生成的容器。
        /// </summary>
        public static Control ContainerFromElement(this ItemsControl itemsControl, Visual element)
        {
            if (itemsControl == null || element == null) return null;
            var containers = itemsControl.GetRealizedContainers()?.ToList()
                ?? new List<Control>();
            for (var v = element; v != null; v = v.GetVisualParent())
                if (containers.Contains(v))
                    return v as Control;
            return null;
        }
    }

    // ===== WPF UIElement.BringIntoView() =====
    // Avalonia 原生已提供 ControlExtensions.BringIntoView(Control)（Avalonia.Controls），
    // 无需兼容层，调用点直接使用原生扩展。

    // ===== WPF DragDrop.DoDragDrop（阻塞式）→ Avalonia 12 DoDragDropAsync =====
    // 注意：与 WpfCompat.Controls.cs 的 DragDropCompat（读取侧）区分，本类只负责"发起拖放"。

    public static class DragDropLauncher
    {
        private static readonly ConditionalWeakTable<InputElement, StrongBox<PointerPressedEventArgs>> LastPress = new();

        /// <summary>
        /// WPF DragDrop.DoDragDrop(source, data, effects)（阻塞直到放下）。
        /// Avalonia 12 的 DoDragDropAsync 必须拿 PointerPressedEventArgs；
        /// 这里对 source 挂 Tunnel 记录最近一次按下，首次手势可能不触发（Migration note：改为事件内 await）。
        /// </summary>
        public static DragDropEffects DoDragDrop(InputElement source, object data, DragDropEffects allowedEffects)
        {
            if (source == null) return DragDropEffects.None;
            if (!LastPress.TryGetValue(source, out var box))
            {
                box = new StrongBox<PointerPressedEventArgs>();
                LastPress.Add(source, box);
                source.AddHandler(InputElement.PointerPressedEvent, (_, e) => box.Value = e,
                    global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
                return DragDropEffects.None; // 本次手势没有按下记录，跳过（下次起生效）
            }
            if (box.Value == null) return DragDropEffects.None;

            var transfer = ToTransfer(data);
            if (transfer == null) return DragDropEffects.None;

            _ = global::Avalonia.Input.DragDrop.DoDragDropAsync(box.Value, transfer, allowedEffects);
            return DragDropEffects.None; // 异步结果不回传（WPF 语义近似），Migration note
        }

        private static IDataTransfer ToTransfer(object data)
        {
            // Avalonia 12：DataObject 已废弃（error 级），统一改用 DataTransfer + DataTransferItem。
            switch (data)
            {
                case IDataTransfer t:
                    return t;
                case string[] files:
                {
                    var df = new global::Avalonia.Input.DataTransfer();
                    // 文件列表编码为换行分隔文本（WpfDataObject.GetFileDropList 侧对称解码）
                    var fmt = global::Avalonia.Input.DataFormat.CreateStringApplicationFormat("FileDrop");
                    df.Add(global::Avalonia.Input.DataTransferItem.Create(fmt, string.Join("\n", files)));
                    return df;
                }
                case string text:
                {
                    var dt = new global::Avalonia.Input.DataTransfer();
                    dt.Add(global::Avalonia.Input.DataTransferItem.CreateText(text));
                    return dt;
                }
                default:
                {
                    // 自定义对象：序列化到字符串格式，跨进程不可用（Migration note：进程内拖放改用 InProcess 格式）
                    var dobj = new global::Avalonia.Input.DataTransfer();
                    var fmt2 = global::Avalonia.Input.DataFormat.CreateStringApplicationFormat("ForkPlusItem");
                    dobj.Add(global::Avalonia.Input.DataTransferItem.Create(fmt2, data?.ToString() ?? ""));
                    return dobj;
                }
            }
        }
    }

    // ===== WPF System.Windows.Input.Cursors =====

    /// <summary>WPF Cursors 静态类（Avalonia 用 new Cursor(StandardCursorType.X)）。
    /// 注：Avalonia 无对角光标，用角标光标近似（SizeNWSE→TopLeftCorner，SizeNESW→TopRightCorner）。</summary>
    public static class Cursors
    {
        public static Cursor Hand => new Cursor(StandardCursorType.Hand);
        public static Cursor Arrow => new Cursor(StandardCursorType.Arrow);
        public static Cursor Wait => new Cursor(StandardCursorType.Wait);
        public static Cursor IBeam => new Cursor(StandardCursorType.Ibeam);
        public static Cursor Cross => new Cursor(StandardCursorType.Cross);
        public static Cursor SizeAll => new Cursor(StandardCursorType.SizeAll);
        public static Cursor SizeNESW => new Cursor(StandardCursorType.TopRightCorner);
        public static Cursor SizeNS => new Cursor(StandardCursorType.SizeNorthSouth);
        public static Cursor SizeNWSE => new Cursor(StandardCursorType.TopLeftCorner);
        public static Cursor SizeWE => new Cursor(StandardCursorType.SizeWestEast);
        public static Cursor No => new Cursor(StandardCursorType.No);
        public static Cursor None => new Cursor(StandardCursorType.None);
    }
}

// ===== WPF System.Windows.Media.Imaging 形状 stub（图标提取等 Win32 互操作用）=====

namespace System.Windows.Media.Imaging
{
    using ForkPlus.UI.WpfCompat;

    /// <summary>WPF Int32Rect。</summary>
    public struct Int32Rect
    {
        public int X, Y, Width, Height;
        public static Int32Rect Empty => default;
    }

    /// <summary>WPF BitmapSizeOptions（仅形状兼容）。</summary>
    public sealed class BitmapSizeOptions
    {
        public static BitmapSizeOptions FromEmptyOptions() => new BitmapSizeOptions();
    }

    public enum BitmapCreateOptions { None, DelayCreation, IgnoreColorProfile, PreservePixelFormat }
    public enum BitmapCacheOption { Default, OnDemand, OnLoad, None }

    /// <summary>
    /// WPF System.Windows.Media.Imaging 静态类。
    /// Migration note（根因修复）：原 stub 返回 null，Windows 上文件类型图标（提交列表/暂存区）
    /// 全部空白。现走 GDI Bitmap → LockBits → Avalonia WriteableBitmap 真实转换。
    /// </summary>
    public static class Imaging
    {
        public static global::Avalonia.Media.IImage CreateBitmapSourceFromHIcon(IntPtr hIcon, Int32Rect sourceRect, BitmapSizeOptions sizeOptions)
        {
            if (hIcon == IntPtr.Zero || !global::System.OperatingSystem.IsWindows())
            {
                return null;
            }
            try
            {
                // Icon.FromHandle 包装不接管所有权（DestroyIcon 由调用方负责），Dispose 仅释放包装。
                using global::System.Drawing.Icon icon = global::System.Drawing.Icon.FromHandle(hIcon);
                using global::System.Drawing.Bitmap bmp = icon.ToBitmap();
                return GdiBitmapToAvalonia(bmp);
            }
            catch
            {
                return null; // 与 WPF 行为一致：失败返回 null，调用方已有 null 兜底
            }
        }

        public static global::Avalonia.Media.IImage CreateBitmapSourceFromHBitmap(IntPtr bitmap, Int32Rect sourceRect, BitmapSizeOptions sizeOptions)
        {
            if (bitmap == IntPtr.Zero || !global::System.OperatingSystem.IsWindows())
            {
                return null;
            }
            try
            {
                using global::System.Drawing.Bitmap bmp = global::System.Drawing.Image.FromHbitmap(bitmap);
                return GdiBitmapToAvalonia(bmp);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// GDI 32bppArgb（直通 alpha）→ Avalonia Bgra8888+Premul（逐像素预乘），行距按各自 Stride/RowBytes 拷贝。
        /// </summary>
        private static unsafe global::Avalonia.Media.IImage GdiBitmapToAvalonia(global::System.Drawing.Bitmap bmp)
        {
            if (bmp.Width <= 0 || bmp.Height <= 0)
            {
                return null;
            }
            var rect = new global::System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            global::System.Drawing.Imaging.BitmapData data = bmp.LockBits(
                rect,
                global::System.Drawing.Imaging.ImageLockMode.ReadOnly,
                global::System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var wb = new global::Avalonia.Media.Imaging.WriteableBitmap(
                    new global::Avalonia.PixelSize(bmp.Width, bmp.Height),
                    new global::Avalonia.Vector(96.0, 96.0),
                    global::Avalonia.Platform.PixelFormat.Bgra8888,
                    global::Avalonia.Platform.AlphaFormat.Premul);
                using (global::Avalonia.Platform.ILockedFramebuffer frame = wb.Lock())
                {
                    byte* src = (byte*)data.Scan0;
                    byte* dst = (byte*)frame.Address;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        byte* s = src + (long)y * data.Stride;
                        byte* d = dst + (long)y * frame.RowBytes;
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            byte b = s[0], g = s[1], r = s[2], a = s[3];
                            d[0] = (byte)(b * a / 255);
                            d[1] = (byte)(g * a / 255);
                            d[2] = (byte)(r * a / 255);
                            d[3] = a;
                            s += 4;
                            d += 4;
                        }
                    }
                }
                return wb;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }
}
