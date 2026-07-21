using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 ReferenceDropdownUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ReferenceDropdownUserControl.xaml.cs（95 行）：
    //   - 内部 DropdownItem 类（INotifyPropertyChanged）：Reference + Icon(ImageSource) + Title
    //   - 构造函数 ReferenceDropdownUserControl(RepositoryData, CustomCommandUI.Control.Dropdown)
    //   - CreateItemsSource：按 filter 过滤 references，分支/tag/remote 分配图标
    //     - refs/heads/ → Theme.BranchIcon
    //     - refs/tags/ → Theme.TagIcon
    //     - refs/remotes/ → Remote.GetIconImage() / Theme.RemoteIcon
    //   - GetRemoteIcon：按 remote name 匹配 Remote.GetIconImage()
    //   - SelectedReference 属性（ComboBox.SelectedItem as DropdownItem）?.Reference
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF ImageSource Icon → string IconEmoji（spike 用 emoji）
    //     - refs/heads/ → 🌿（分支）
    //     - refs/tags/ → 🏷（tag）
    //     - refs/remotes/ → 📡（remote）
    //   - WPF RepositoryData / CustomCommandUI.Control.Dropdown → task spec API
    //   - WPF Theme.BranchIcon/TagIcon/RemoteIcon → emoji
    //   - task spec 关键 API：SetReferences(Reference[]) / SelectedReference / SelectionChanged
    //
    // spike 简化：
    //   - SetReferences(Reference[]) 公共方法（task spec 关键 API）
    //   - SelectedReference 属性（task spec 关键 API）
    //   - SelectionChanged 事件（task spec 关键 API）
    //   - DropdownItem POCO：Reference + IconEmoji + Title
    //   - 按 FullReference 前缀分配 emoji 图标
    public partial class ReferenceDropdownUserControl : UserControl
    {
        // ===== DropdownItem POCO（对照 WPF: DropdownItem : INotifyPropertyChanged）=====
        // spike 版简化为纯 POCO（无需 INotifyPropertyChanged，ComboBox 不依赖属性变更通知）
        public class DropdownItem
        {
            // 引用（对照 WPF: Reference 属性，来自 ForkPlus.Git Core 命名空间）
            public Reference Reference { get; set; }

            // 图标 emoji（对照 WPF: Icon ImageSource）
            public string IconEmoji { get; set; } = "";

            // 标题（对照 WPF: Title => reference.Name）
            public string Title { get; set; } = "";

            public DropdownItem(Reference reference, string iconEmoji)
            {
                Reference = reference;
                IconEmoji = iconEmoji;
                Title = reference?.Name ?? string.Empty;
            }
        }

        // ===== 公共事件（task spec 关键 API）=====
        public event EventHandler<RoutedEventArgs> SelectionChanged;

        // ===== 公共属性（task spec 关键 API）=====
        // 当前选中引用（对照 WPF: SelectedReference => ComboBox.SelectedItem as DropdownItem?.Reference）
        public Reference SelectedReference
        {
            get
            {
                return (ReferenceComboBox?.SelectedItem as DropdownItem)?.Reference;
            }
        }

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        private Reference[] _references;
        private string _filter;

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public ReferenceDropdownUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== SetReferences(Reference[])（task spec 关键 API）=====
        // 对照 WPF: 构造函数内 CreateItemsSource(repositoryData.References.Items)
        // spike 版：SetReferences 方法注入，内部调 CreateItemsSource
        public void SetReferences(Reference[] refs)
        {
            _references = refs ?? Array.Empty<Reference>();
            _filter = null; // spike 版默认无过滤
            if (ReferenceComboBox != null)
            {
                ReferenceComboBox.ItemsSource = CreateItemsSource();
                if (ReferenceComboBox.ItemCount > 0)
                {
                    ReferenceComboBox.SelectedIndex = 0;
                }
            }
        }

        // ===== SetFilter(string)（spike 新增，设置过滤）=====
        // 对照 WPF: 构造函数 _filter = dropdown.Filter
        public void SetFilter(string filter)
        {
            _filter = filter;
            if (_references != null && ReferenceComboBox != null)
            {
                ReferenceComboBox.ItemsSource = CreateItemsSource();
                if (ReferenceComboBox.ItemCount > 0)
                {
                    ReferenceComboBox.SelectedIndex = 0;
                }
            }
        }

        // ===== CreateItemsSource（对照 WPF，spike 用 emoji 替代 ImageSource）=====
        // WPF: 按 _filter 过滤 + 分配 Theme.BranchIcon/TagIcon/RemoteIcon
        // spike: 按 _filter 过滤 + 分配 emoji（🌿/🏷/📡）
        private DropdownItem[] CreateItemsSource()
        {
            if (_references == null || _references.Length == 0)
            {
                return Array.Empty<DropdownItem>();
            }

            // 解析 filter（空格分隔，对照 WPF）
            string[] filterParts = string.IsNullOrEmpty(_filter)
                ? Array.Empty<string>()
                : _filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            List<DropdownItem> list = new List<DropdownItem>(_references.Length);
            foreach (Reference reference in _references)
            {
                // 对照 WPF: 按 filter 过滤
                if (filterParts.Length > 0)
                {
                    bool matched = false;
                    foreach (string part in filterParts)
                    {
                        if (reference.FullReference.StartsWith(part))
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched) continue;
                }

                // 对照 WPF: 按 FullReference 前缀分配图标
                string iconEmoji = GetReferenceEmoji(reference.FullReference);
                list.Add(new DropdownItem(reference, iconEmoji));
            }
            return list.ToArray();
        }

        // ===== GetReferenceEmoji（对照 WPF: Theme.BranchIcon/TagIcon/RemoteIcon → emoji）=====
        // WPF: refs/heads/ → Theme.BranchIcon, refs/tags/ → Theme.TagIcon,
        //      refs/remotes/ → Remote.GetIconImage() / Theme.RemoteIcon
        // spike: refs/heads/ → 🌿, refs/tags/ → 🏷, refs/remotes/ → 📡
        private static string GetReferenceEmoji(string fullReference)
        {
            if (string.IsNullOrEmpty(fullReference)) return "📌";

            if (fullReference.StartsWith("refs/heads/"))
            {
                return "🌿"; // 分支
            }
            if (fullReference.StartsWith("refs/tags/"))
            {
                return "🏷"; // tag
            }
            if (fullReference.StartsWith("refs/remotes/"))
            {
                return "📡"; // remote
            }
            return "📌"; // 其他
        }

        // ===== ReferenceComboBox_SelectionChanged（对照 WPF ComboBox 选择变更）=====
        private void ReferenceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, new RoutedEventArgs());
        }
    }
}
