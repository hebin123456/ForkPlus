// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/CustomCommandViewModel.cs（284 行）：
//   - public class CustomCommandViewModel : INotifyPropertyChanged
//   - 字段：CustomCommandTarget _target / CustomCommandActionViewModel _actionViewModel /
//     CustomCommandUIViewModel _uiViewModel / bool _shared / CustomCommandOS _os /
//     ActionType _actionType / bool _localBranchReferenceTarget / _remoteBranchReferenceTarget /
//     _tagReferenceTarget / string _name / Inline[] _variablesInlines
//   - 属性：Target / CustomCommand / ActionViewModel / UIViewModel / Shared / OS / Version /
//     ActionType / LocalBranchReferenceTarget / RemoteBranchReferenceTarget / TagReferenceTarget /
//     Name / VariablesInlines / VariablesString / Details
//   - Target set 时调 _target.CreateVariablesList() 设置 VariablesInlines
//   - CustomCommand getter：根据 ActionType 构建 CustomCommand（action 或 ui + refTargets）
//   - VariablesString：string.Join("", _variablesInlines.Map(x => (x as Run).Text))
//   - Details：ActionType switch { UI => "ui: " + DialogTitle, Action => Details, _ => throw }
//   - 构造函数：CustomCommandViewModel(CustomCommand) 或全参数版
//   - CreateDefaultCustomCommandUI：默认 "Execute custom command" 对话框
//   - NotifyPropertyChanged 用 [CallerMemberName]
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF Inline[]（System.Windows.Documents.Inline）→ spike 改为 string[]
//      （Inline 是 WPF FlowDocument 类型，Avalonia 无等价物；spike 阶段变量列表仅存字符串，
//       Phase 3.9b 可用 Avalonia FormattedText 或 Runs 集合重新实现富文本显示）
//   2. WPF _target.CreateVariablesList()（CustomCommandTargetExtensions，WPF 工程）→
//      spike 本地 CreateVariablesList(target) 方法（返回 string[]，仅变量名列表）
//   3. WPF VariablesString: _variablesInlines.Map(x => (x as Run).Text) →
//      spike string.Join("", _variablesInlines)（string[] 直接拼接）
//   4. WPF [Null] 属性 → spike 移除（[Null] 在 Core 是 internal，Avalonia 不可访问）
//   5. WPF CannotReachHereException → spike InvalidOperationException
//      （CannotReachHereException 在 Core 是 internal，Avalonia 不可访问）
//   6. WPF PreferencesLocalization → 本工程 PreferencesLocalization
//      （spike 版委托到 ServiceLocator.Localization）
//   7. INotifyPropertyChanged / CallerMemberName → System.ComponentModel / System.Runtime.CompilerServices
//   8. CustomCommand / CustomCommandTarget / CustomCommandAction / CustomCommandUI /
//      CustomCommandOS / CustomCommandRefTarget / UrlCustomCommandAction / CancelCustomCommandAction →
//      ForkPlus.UI.CustomCommands（已迁入 ForkPlus.Core）
//   9. ContainsItem / Map → ForkPlus.Core 扩展方法（public，Avalonia 可引用）
//  10. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public class CustomCommandViewModel : INotifyPropertyChanged
    {
        private static readonly string UrlCommandDefaultUrl = "https://hebin.me";

        private CustomCommandTarget _target;

        private CustomCommandActionViewModel _actionViewModel;

        private CustomCommandUIViewModel _uiViewModel;

        private bool _shared;

        private CustomCommandOS _os;

        private ActionType _actionType;

        private bool _localBranchReferenceTarget;

        private bool _remoteBranchReferenceTarget;

        private bool _tagReferenceTarget;

        private string _name;

        // 对照 WPF: private Inline[] _variablesInlines;
        // spike 版：WPF Inline[] → string[]（变量名列表）
        private string[] _variablesInlines;

        public CustomCommandTarget Target
        {
            get
            {
                return _target;
            }
            set
            {
                _target = value;
                VariablesInlines = CreateVariablesList(_target);
                NotifyPropertyChanged("Target");
            }
        }

        public CustomCommand CustomCommand
        {
            get
            {
                CustomCommandAction action = null;
                CustomCommandUI ui = null;
                if (ActionType == ActionType.Action)
                {
                    action = ActionViewModel.Action;
                }
                else if (ActionType == ActionType.UI)
                {
                    List<CustomCommandUI.Button> list = new List<CustomCommandUI.Button>(2);
                    if (UIViewModel.Button1Enabled)
                    {
                        list.Add(new CustomCommandUI.Button(UIViewModel.Button1Title, UIViewModel.Button1ActionViewModel.Action));
                    }
                    if (UIViewModel.Button2Enabled)
                    {
                        list.Add(new CustomCommandUI.Button(UIViewModel.Button2Title, UIViewModel.Button2ActionViewModel.Action));
                    }
                    ui = new CustomCommandUI(UIViewModel.DialogTitle, UIViewModel.DialogDescription, UIViewModel.Controls, list.ToArray());
                }
                List<CustomCommandRefTarget> list2 = null;
                if (Target == CustomCommandTarget.Reference)
                {
                    list2 = new List<CustomCommandRefTarget>();
                    if (LocalBranchReferenceTarget)
                    {
                        list2.Add(CustomCommandRefTarget.LocalBranch);
                    }
                    if (RemoteBranchReferenceTarget)
                    {
                        list2.Add(CustomCommandRefTarget.RemoteBranch);
                    }
                    if (TagReferenceTarget)
                    {
                        list2.Add(CustomCommandRefTarget.Tag);
                    }
                }
                int version = ((Version > 2) ? Version : 2);
                return new CustomCommand(Target, list2?.ToArray(), Name, action, ui, OS, Shared, version);
            }
        }

        public CustomCommandActionViewModel ActionViewModel
        {
            get
            {
                return _actionViewModel;
            }
            set
            {
                _actionViewModel = value;
                NotifyPropertyChanged("ActionViewModel");
            }
        }

        public CustomCommandUIViewModel UIViewModel
        {
            get
            {
                return _uiViewModel;
            }
            set
            {
                _uiViewModel = value;
                NotifyPropertyChanged("UIViewModel");
            }
        }

        public bool Shared
        {
            get
            {
                return _shared;
            }
            set
            {
                _shared = value;
                NotifyPropertyChanged("Shared");
            }
        }

        public CustomCommandOS OS
        {
            get
            {
                return _os;
            }
            set
            {
                _os = value;
                NotifyPropertyChanged("OS");
            }
        }

        public int Version { get; }

        public ActionType ActionType
        {
            get
            {
                return _actionType;
            }
            set
            {
                _actionType = value;
                NotifyPropertyChanged("ActionType");
                NotifyPropertyChanged("Details");
            }
        }

        public bool LocalBranchReferenceTarget
        {
            get
            {
                return _localBranchReferenceTarget;
            }
            set
            {
                _localBranchReferenceTarget = value;
                NotifyPropertyChanged("LocalBranchReferenceTarget");
            }
        }

        public bool RemoteBranchReferenceTarget
        {
            get
            {
                return _remoteBranchReferenceTarget;
            }
            set
            {
                _remoteBranchReferenceTarget = value;
                NotifyPropertyChanged("RemoteBranchReferenceTarget");
            }
        }

        public bool TagReferenceTarget
        {
            get
            {
                return _tagReferenceTarget;
            }
            set
            {
                _tagReferenceTarget = value;
                NotifyPropertyChanged("TagReferenceTarget");
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                NotifyPropertyChanged("Name");
            }
        }

        // 对照 WPF: public Inline[] VariablesInlines
        // spike 版：WPF Inline[] → string[]
        public string[] VariablesInlines
        {
            get
            {
                return _variablesInlines;
            }
            set
            {
                _variablesInlines = value;
                NotifyPropertyChanged("VariablesInlines");
            }
        }

        // 对照 WPF: public string VariablesString => string.Join("", _variablesInlines.Map((Inline x) => (x as Run).Text));
        // spike 版：string[] 直接拼接（无需 Map + Run.Text 提取）
        public string VariablesString => string.Join("", _variablesInlines);

        // 对照 WPF: public string Details => ActionType switch { ... _ => throw new CannotReachHereException() };
        // spike 版：CannotReachHereException → InvalidOperationException（前者在 Core 是 internal）
        public string Details => ActionType switch
        {
            ActionType.UI => "ui: " + UIViewModel.DialogTitle,
            ActionType.Action => ActionViewModel.Details,
            _ => throw new InvalidOperationException(),
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public CustomCommandViewModel(CustomCommand command)
            : this(command.Target, command.Name, command.Action, command.ReferenceTargets, command.UI, command.OS, command.Shared, command.Version)
        {
        }

        // 对照 WPF: public CustomCommandViewModel(..., [Null] CustomCommandAction action, [Null] CustomCommandRefTarget[] referenceTargets, ...)
        // spike 版：移除 [Null] 属性（Core internal 不可访问）
        public CustomCommandViewModel(CustomCommandTarget target, string name, CustomCommandAction action = null, CustomCommandRefTarget[] referenceTargets = null, CustomCommandUI ui = null, CustomCommandOS osType = CustomCommandOS.Any, bool shared = false, int version = 2)
        {
            Target = target;
            Name = name;
            Shared = shared;
            OS = osType;
            Version = version;
            ActionType = ((action == null) ? ActionType.UI : ActionType.Action);
            ActionViewModel = new CustomCommandActionViewModel(action ?? new UrlCustomCommandAction(UrlCommandDefaultUrl));
            UIViewModel = new CustomCommandUIViewModel(ui ?? CreateDefaultCustomCommandUI(), this);
            if (Target == CustomCommandTarget.Reference)
            {
                LocalBranchReferenceTarget = referenceTargets?.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.LocalBranch) ?? false;
                RemoteBranchReferenceTarget = referenceTargets?.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.RemoteBranch) ?? false;
                TagReferenceTarget = referenceTargets?.ContainsItem((CustomCommandRefTarget x) => x == CustomCommandRefTarget.Tag) ?? false;
            }
            else
            {
                LocalBranchReferenceTarget = true;
                RemoteBranchReferenceTarget = true;
            }
        }

        public void RefreshDetails()
        {
            NotifyPropertyChanged("Details");
        }

        private static CustomCommandUI CreateDefaultCustomCommandUI()
        {
            string description = "Are you sure you want to execute custom command?";
            CustomCommandUI.Button button = new CustomCommandUI.Button("OK", new UrlCustomCommandAction(UrlCommandDefaultUrl));
            CustomCommandUI.Button button2 = new CustomCommandUI.Button("Cancel", new CancelCustomCommandAction());
            return new CustomCommandUI("Execute custom command", description, new CustomCommandUI.Control[0], new CustomCommandUI.Button[2] { button, button2 });
        }

        // 对照 WPF: CustomCommandTargetExtensions.CreateVariablesList(this CustomCommandTarget target, ...)
        // spike 版：本地简化实现，返回 string[]（变量名列表）。
        // WPF 版返回 Inline[]（Run 元素含变量名+描述+换行），spike 阶段仅返回变量名字符串数组。
        // Phase 3.9b 可用 Avalonia FormattedText 或 TextBlock.Inlines 重新实现富文本显示。
        private static string[] CreateVariablesList(CustomCommandTarget target)
        {
            List<string> list = new List<string>();
            switch (target)
            {
                case CustomCommandTarget.Revision:
                    list.Add("${repo:name}");
                    list.Add("${sha}");
                    list.Add("${sha:abbr}");
                    list.Add("$repository");
                    list.Add("$SHA");
                    list.Add("$sha");
                    break;
                case CustomCommandTarget.Repository:
                    list.Add("${repo:name}");
                    list.Add("$repository");
                    break;
                case CustomCommandTarget.RepositoryFile:
                    list.Add("${repo:name}");
                    list.Add("${file}");
                    list.Add("${file:name}");
                    list.Add("${sha}");
                    list.Add("${sha:abbr}");
                    list.Add("$repository");
                    list.Add("$SHA");
                    list.Add("$sha");
                    list.Add("$filepath");
                    list.Add("$filename");
                    break;
                case CustomCommandTarget.Reference:
                    list.Add("${repo:name}");
                    list.Add("${sha}");
                    list.Add("${sha:abbr}");
                    list.Add("${ref}");
                    list.Add("${ref:short}");
                    list.Add("${ref:full}");
                    list.Add("$repository");
                    list.Add("$SHA");
                    list.Add("$sha");
                    list.Add("$name");
                    list.Add("$shortname");
                    list.Add("$fullreference");
                    break;
                case CustomCommandTarget.Submodule:
                    list.Add("${repo:name}");
                    list.Add("${submodule}");
                    list.Add("$path");
                    list.Add("$name");
                    break;
            }
            return list.ToArray();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
