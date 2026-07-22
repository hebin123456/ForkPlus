// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/CustomCommandActionViewModel.cs（69 行）：
//   - public class CustomCommandActionViewModel : INotifyPropertyChanged
//   - 字段：CustomCommandAction _action
//   - 属性：Title / Details / ToolTip / Action（set 时根据 Action 类型设置 Title/Details）
//   - Action set 时用 PreferencesLocalization.Current(...) 翻译 Title/Details
//   - 构造函数：CustomCommandActionViewModel(CustomCommandAction action)
//   - NotifyPropertyChanged 辅助方法
//
// Avalonia 版差异：
//   1. PreferencesLocalization.Current(...) → 本工程 PreferencesLocalization.Current(...)
//      （spike 版委托到 ServiceLocator.Localization）
//   2. INotifyPropertyChanged → System.ComponentModel.INotifyPropertyChanged（不变）
//   3. CustomCommandAction / ProcessCustomCommandAction / ShCustomCommandAction /
//      UrlCustomCommandAction / CancelCustomCommandAction → ForkPlus.UI.CustomCommands
//      （已迁入 ForkPlus.Core，Avalonia 工程可引用）
//   4. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.ComponentModel;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public class CustomCommandActionViewModel : INotifyPropertyChanged
    {
        private CustomCommandAction _action;

        public string Title { get; private set; }

        public string Details { get; private set; }

        public string ToolTip { get; private set; }

        public CustomCommandAction Action
        {
            get
            {
                return _action;
            }
            set
            {
                _action = value;
                if (Action is ProcessCustomCommandAction processCustomCommandAction)
                {
                    Title = PreferencesLocalization.Current("Start Process");
                    Details = processCustomCommandAction.Path + " " + processCustomCommandAction.Parameters;
                }
                else if (Action is ShCustomCommandAction)
                {
                    Title = PreferencesLocalization.Current("Bash Command");
                    Details = PreferencesLocalization.Current("Run bash Script");
                }
                else if (Action is UrlCustomCommandAction urlCustomCommandAction)
                {
                    Title = PreferencesLocalization.Current("Open Url");
                    Details = urlCustomCommandAction.Url;
                }
                else
                {
                    if (!(Action is CancelCustomCommandAction))
                    {
                        throw new InvalidOperationException();
                    }
                    Title = PreferencesLocalization.Current("Cancel");
                    Details = PreferencesLocalization.Current("close dialog");
                }
                ToolTip = Title + "\n" + Details;
                NotifyPropertyChanged("Title");
                NotifyPropertyChanged("Details");
                NotifyPropertyChanged("ToolTip");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CustomCommandActionViewModel(CustomCommandAction action)
        {
            Action = action;
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
