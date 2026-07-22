using ForkPlus.Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 SidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/SidebarItem.cs（18 行）：
    //   - WPF SidebarItem : MultiselectionTreeViewItem（abstract）
    //   - static readonly string DragItemsFormat = "SidebarTreeView"
    //   - SidebarItem Parent { get; }（构造函数注入，区别于 ParentItem）
    //   - override bool IsFocusable => !ShowExpander
    //   - 构造函数 SidebarItem(string title, SidebarItem parent)
    //   - 拖拽方法（StartDrag/GetDropEffect/Drop/GetDataObject）继承自 WPF MultiselectionTreeViewItem
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF MultiselectionTreeViewItem（ForkPlus.UI.Controls）→ Avalonia spike
    //      MultiselectionTreeViewItem（ForkPlus.Avalonia.Controls）
    //   2. spike MultiselectionTreeViewItem 未实现 StartDrag/GetDropEffect/Drop/GetDataObject
    //      （WPF 依赖 DragDrop/DragEventArgs/DataObject），spike 在本类声明虚方法空实现供子类 override
    //   3. WPF DependencyObject dragSource → object（spike 替代 WPF 依赖类型）
    //   4. WPF DragEventArgs → object（spike 省略拖拽事件类型）
    //   5. WPF DragDropEffects（enum）返回值 → int（0 = None 等价）
    //   6. WPF IDataObject → object（spike 省略数据对象类型）
    //   7. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - abstract 基类继承 MultiselectionTreeViewItem
    //   - Parent 属性 + DragItemsFormat 常量
    //   - IsFocusable override
    //   - 拖拽虚方法空实现（供子类 override 保留空体 + 注释）
    public abstract class SidebarItem : MultiselectionTreeViewItem
    {
        // 对照 WPF: public static readonly string DragItemsFormat = "SidebarTreeView";
        public static readonly string DragItemsFormat = "SidebarTreeView";

        // 对照 WPF: public SidebarItem Parent { get; }
        // 注：与 MultiselectionTreeViewItem.ParentItem 不同，Parent 是构造注入的类型化引用
        public SidebarItem Parent { get; }

        // 对照 WPF: public override bool IsFocusable => !ShowExpander;
        public override bool IsFocusable => !ShowExpander;

        // 对照 WPF: public SidebarItem(string title, SidebarItem parent)
        public SidebarItem(string title, SidebarItem parent)
        {
            Title = title;
            Parent = parent;
        }

        // spike: 拖拽逻辑省略（WPF DragDrop/DragEventArgs/DataObject 依赖未迁移）
        // 以下虚方法为空实现，供子类 override 但保留空体。
        // 对照 WPF MultiselectionTreeViewItem:
        //   public virtual void StartDrag(DependencyObject dragSource, MultiselectionTreeViewItem[] nodes)
        //   public virtual DragDropEffects GetDropEffect(DragEventArgs e, int index)
        //   public virtual void Drop(DragEventArgs e, int index)
        //   protected virtual IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
        public virtual void StartDrag(object dragSource, MultiselectionTreeViewItem[] nodes)
        {
            // spike: WPF DragDrop.DoDragDrop 省略
        }

        public virtual int GetDropEffect(object e, int index)
        {
            // spike: WPF DragDropEffects.None 等价 0
            return 0;
        }

        public virtual void Drop(object e, int index)
        {
            // spike: WPF Drop 逻辑省略
        }

        protected virtual object GetDataObject(MultiselectionTreeViewItem[] nodes)
        {
            // spike: WPF DataObject 省略
            return null;
        }
    }
}
