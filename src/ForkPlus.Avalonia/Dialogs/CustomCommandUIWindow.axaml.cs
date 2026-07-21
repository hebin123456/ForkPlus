using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.45b：Avalonia 版 CustomCommandUIWindow（真实迁移版，对照 WPF CustomCommandUIWindow.xaml.cs 294 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CustomCommandUIWindow.xaml.cs：
    //   - public partial class CustomCommandUIWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / string _customCommandName /
    //     CustomCommandUI _customCommandUI / CustomCommandEnvironment _env /
    //     CustomCommandUI.Button button1 / CustomCommandUI.Button button2
    //   - 构造函数：
    //     * DialogTitle = _env.ReplaceVariablesWithValues(customCommandUI.Title)
    //     * DialogDescription = _env.ReplaceVariablesWithValues(customCommandUI.Description)
    //     * ShowSubmitButton = false / ShowCancelButton = false（按 Buttons 数量动态显示）
    //     * CreateControls(customCommandUI.Controls)：遍历 4 种 Control 类型动态构造
    //       - GenericTextBox → PlaceholderTextBox（Text + Placeholder）
    //       - PathTextBox → PathTextBoxUserControl（SaveFile/OpenFile/OpenDirectory dialog）
    //       - Dropdown → ReferenceDropdownUserControl
    //       - CheckBox → CustomCommandCheckBox（CheckedValue/UncheckedValue）
    //     * Buttons[0] → button1 = SubmitButton；Buttons[1] → button2 = CancelButton
    //   - OnSubmit / OnCancel：遍历 ControlsContainer.Children 按 num 索引读取值
    //     → List<CustomCommandEnvironment.Parameter> → CustomCommandEnvironment env → button.Action.Execute
    //
    // Avalonia 版差异：
    //   1. spike 模式结构：根 Grid 4 行 = Header / Description / Content / Footer
    //   2. RepositoryUserControl 参数 → 注入 GitModule + RepositoryReferences? +
    //      Action<GitCommandResult>? onCompleted 回调
    //   3. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   4. PathTextBoxUserControl → spike 版用 TextBox + 浏览按钮简化
    //   5. ReferenceDropdownUserControl → spike 版用 ComboBox 占位
    //   6. CustomCommandCheckBox → Avalonia CheckBox + CheckedValue/UncheckedValue 跟踪
    //   7. ControlsContainer.Children 按索引访问 → spike 版用 List 跟踪 (Control 实例 + 值读取 Func)
    //   8. SubmitButtonTitle / CancelButtonTitle → 同步注入的 button1/button2.Title
    //   9. spike 版：OnSubmit/OnCancel 末尾调 onCompleted 回调通知调用方
    public partial class CustomCommandUIWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences? _references;
        private readonly string _customCommandName;
        private readonly CustomCommandUI _customCommandUI;
        private readonly CustomCommandEnvironment _env;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 对照 WPF: private readonly CustomCommandUI.Button button1 / button2;
        private readonly CustomCommandUI.Button _button1;
        private readonly CustomCommandUI.Button _button2;

        // spike 版：用 List<ControlSlot> 替代 ControlsContainer.Children 按索引访问
        private readonly List<ControlSlot> _controlSlots = new List<ControlSlot>();

        public CustomCommandUIWindow(
            GitModule gitModule,
            RepositoryReferences? references,
            string customCommandName,
            CustomCommandUI customCommandUI,
            CustomCommandEnvironment env,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references;
            _customCommandName = customCommandName ?? throw new ArgumentNullException(nameof(customCommandName));
            _customCommandUI = customCommandUI ?? throw new ArgumentNullException(nameof(customCommandUI));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _onCompleted = onCompleted;

            // 对照 WPF: base.DialogTitle = _env.ReplaceVariablesWithValues(customCommandUI.Title);
            DialogTitle = _env.ReplaceVariablesWithValues(customCommandUI.Title ?? "");
            DialogDescription = _env.ReplaceVariablesWithValues(customCommandUI.Description ?? "");
            Title = DialogTitle;

            // 对照 WPF: base.ShowSubmitButton = false; base.ShowCancelButton = false;
            ShowSubmitButton = false;
            ShowCancelButton = false;

            // 对照 WPF: if (customCommandUI.Controls.Length != 0) CreateControls(customCommandUI.Controls);
            if (_customCommandUI.Controls != null && _customCommandUI.Controls.Length != 0)
            {
                CreateControls(_customCommandUI.Controls);
            }

            // 对照 WPF: if (customCommandUI.Buttons.Length != 0) button1 = Buttons.FirstItem() + ShowSubmitButton=true
            if (_customCommandUI.Buttons != null && _customCommandUI.Buttons.Length != 0)
            {
                _button1 = _customCommandUI.Buttons[0];
                SubmitButtonTitle = _env.ReplaceVariablesWithValues(_button1.Title ?? "");
                ShowSubmitButton = true;
            }
            // 对照 WPF: if (customCommandUI.Buttons.Length > 1) button2 = Buttons.LastItem() + ShowCancelButton=true
            if (_customCommandUI.Buttons != null && _customCommandUI.Buttons.Length > 1)
            {
                _button2 = _customCommandUI.Buttons.Last();
                CancelButtonTitle = _env.ReplaceVariablesWithValues(_button2.Title ?? "");
                ShowCancelButton = true;
            }
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (_button1 == null)
            {
                return;
            }
            // 对照 WPF: if (button1.Action is CancelCustomCommandAction) base.OnCancel(); return;
            // Avalonia spike：CancelCustomCommandAction 在 WPF 工程，spike 改用 TypeKey == "cancel" 判断
            if (string.Equals(_button1.Action?.TypeKey, "cancel", StringComparison.Ordinal))
            {
                base.OnCancel();
                try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("CustomCommandUIWindow onCompleted callback failed", ex); }
                return;
            }

            // 对照 WPF: List<CustomCommandEnvironment.Parameter> list = new List<...>(_env.Parameters);
            //          遍历 _customCommandUI.Controls 按 num 索引读取值
            List<CustomCommandEnvironment.Parameter> list = new List<CustomCommandEnvironment.Parameter>(_env.Parameters);
            foreach (ControlSlot slot in _controlSlots)
            {
                CustomCommandEnvironment.Parameter param = slot.GetParameter();
                if (param != null)
                {
                    list.Add(param);
                }
            }

            // 对照 WPF: CustomCommandEnvironment env = new CustomCommandEnvironment(_env.GitModule, list.ToArray());
            CustomCommandEnvironment env = new CustomCommandEnvironment(_env.GitModule, list.ToArray());
            try
            {
                _button1.Action.Execute(null, _customCommandName, env);
            }
            catch (Exception ex)
            {
                Log.Error("CustomCommandUI button1 action failed", ex);
            }
            base.OnSubmit();
            try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("CustomCommandUIWindow onCompleted callback failed", ex); }
        }

        // 对照 WPF: protected override void OnCancel()
        protected override void OnCancel()
        {
            if (_button2 == null)
            {
                base.OnCancel();
                return;
            }
            // 对照 WPF: if (button2.Action is CancelCustomCommandAction) base.OnCancel(); return;
            // Avalonia spike：CancelCustomCommandAction 在 WPF 工程，spike 改用 TypeKey == "cancel" 判断
            if (string.Equals(_button2.Action?.TypeKey, "cancel", StringComparison.Ordinal))
            {
                base.OnCancel();
                try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("CustomCommandUIWindow onCompleted callback failed", ex); }
                return;
            }

            List<CustomCommandEnvironment.Parameter> list = new List<CustomCommandEnvironment.Parameter>(_env.Parameters);
            foreach (ControlSlot slot in _controlSlots)
            {
                CustomCommandEnvironment.Parameter param = slot.GetParameter();
                if (param != null)
                {
                    list.Add(param);
                }
            }

            CustomCommandEnvironment env = new CustomCommandEnvironment(_env.GitModule, list.ToArray());
            try
            {
                _button2.Action.Execute(null, _customCommandName, env);
            }
            catch (Exception ex)
            {
                Log.Error("CustomCommandUI button2 action failed", ex);
            }
            Close();
            try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("CustomCommandUIWindow onCompleted callback failed", ex); }
        }

        // 对照 WPF: private void CreateControls(CustomCommandUI.Control[] controls)
        private void CreateControls(CustomCommandUI.Control[] controls)
        {
            int rowIndex = 0;
            foreach (CustomCommandUI.Control control in controls)
            {
                if (control is CustomCommandUI.Control.GenericTextBox genericTextBox)
                {
                    TextBlock titleBlock = CreateTitleTextBlock(genericTextBox.Title, rowIndex);
                    ControlsContainer.Children.Add(titleBlock);
                    TextBox textBox = CreateGenericTextBox(genericTextBox);
                    ControlsContainer.Children.Add(textBox);
                    // spike 版：捕获 textBox 实例，提交时读取 Text
                    _controlSlots.Add(new ControlSlot(delegate
                    {
                        return new CustomCommandEnvironment.TextParameter(textBox.Text ?? "");
                    }));
                }
                else if (control is CustomCommandUI.Control.PathTextBox pathTextBox)
                {
                    TextBlock titleBlock = CreateTitleTextBlock(pathTextBox.Title, rowIndex);
                    ControlsContainer.Children.Add(titleBlock);
                    TextBox textBox = CreatePathTextBox(pathTextBox);
                    ControlsContainer.Children.Add(textBox);
                    _controlSlots.Add(new ControlSlot(delegate
                    {
                        return new CustomCommandEnvironment.PathParameter(textBox.Text ?? "");
                    }));
                }
                else if (control is CustomCommandUI.Control.Dropdown dropdown)
                {
                    TextBlock titleBlock = CreateTitleTextBlock(dropdown.Title, rowIndex);
                    ControlsContainer.Children.Add(titleBlock);
                    ComboBox comboBox = CreateReferenceDropdown(dropdown);
                    ControlsContainer.Children.Add(comboBox);
                    // spike 版：ComboBox.SelectedItem 是 Reference（若调用方已注入 _references 可填充）
                    _controlSlots.Add(new ControlSlot(delegate
                    {
                        return new CustomCommandEnvironment.ReferenceParameter(comboBox.SelectedItem as Reference);
                    }));
                }
                else if (control is CustomCommandUI.Control.CheckBox checkBox)
                {
                    CheckBox cb = CreateCheckBox(checkBox);
                    ControlsContainer.Children.Add(cb);
                    _controlSlots.Add(new ControlSlot(delegate
                    {
                        return cb.IsChecked.GetValueOrDefault()
                            ? new CustomCommandEnvironment.OptionalTextParameter(checkBox.CheckedValue ?? "")
                            : new CustomCommandEnvironment.OptionalTextParameter(checkBox.UncheckedValue ?? "");
                    }));
                }
                rowIndex++;
            }
        }

        // 对照 WPF: private TextBlock CreateTitleTextBlock(string title, int rowIndex)
        private static TextBlock CreateTitleTextBlock(string title, int rowIndex)
        {
            return new TextBlock
            {
                Text = (title ?? "") + ":",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 2),
                FontSize = 13
            };
        }

        // 对照 WPF: private PlaceholderTextBox CreateGenericTextBox(GenericTextBox genericTextBox, int rowIndex)
        // Avalonia: PlaceholderTextBox.Placeholder → TextBox Watermark
        private static TextBox CreateGenericTextBox(CustomCommandUI.Control.GenericTextBox genericTextBox)
        {
            return new TextBox
            {
                Text = genericTextBox.Text ?? "",
                Watermark = genericTextBox.Placeholder ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(4, 2, 4, 2)
            };
        }

        // 对照 WPF: private PathTextBoxUserControl CreatePathTextBox(PathTextBox pathTextBox, int rowIndex)
        // spike 版：用普通 TextBox + 计算默认路径（不弹文件对话框）
        private TextBox CreatePathTextBox(CustomCommandUI.Control.PathTextBox pathTextBox)
        {
            string text = "";
            string defaultDir = pathTextBox.DefaultDirectory;
            if (defaultDir != null)
            {
                if (!Path.IsPathRooted(defaultDir))
                {
                    defaultDir = Path.Combine(_env.GitModule.Path, defaultDir);
                }
                text = (pathTextBox.PathDialogType == CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory)
                    ? defaultDir
                    : Path.Combine(defaultDir, pathTextBox.FileName ?? "");
            }
            else
            {
                string fileName = pathTextBox.FileName;
                text = (fileName == null || pathTextBox.PathDialogType == CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory)
                    ? ""
                    : Path.Combine(_env.GitModule.Path, fileName);
            }

            return new TextBox
            {
                Text = text,
                Watermark = "File or directory path",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        // 对照 WPF: private ReferenceDropdownUserControl CreateReferenceDropdown(Dropdown dropdown, int rowIndex)
        // spike 版：用 ComboBox 占位（spike 不接入 RepositoryData）
        private ComboBox CreateReferenceDropdown(CustomCommandUI.Control.Dropdown dropdown)
        {
            ComboBox comboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
                PlaceholderText = "Select reference..."
            };
            // spike 版：若调用方注入 _references，把 Items 填进去（spike 用 Reference.ShortName 显示）
            if (_references?.Items != null)
            {
                foreach (Reference r in _references.Items)
                {
                    comboBox.Items.Add(r);
                }
            }
            return comboBox;
        }

        // 对照 WPF: private CustomCommandCheckBox CreateCheckBox(CheckBox checkBox, int rowIndex)
        // spike 版：用 Avalonia CheckBox + Content=Title
        private static CheckBox CreateCheckBox(CustomCommandUI.Control.CheckBox checkBox)
        {
            return new CheckBox
            {
                Content = checkBox.Title,
                IsChecked = checkBox.DefaultValue,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4),
                FontSize = 13
            };
        }

        // spike 版：ControlSlot 封装 (value getter)，替代 WPF 按 num 索引遍历 ControlsContainer.Children
        private sealed class ControlSlot
        {
            private readonly Func<CustomCommandEnvironment.Parameter> _getParameter;

            public ControlSlot(Func<CustomCommandEnvironment.Parameter> getParameter)
            {
                _getParameter = getParameter ?? throw new ArgumentNullException(nameof(getParameter));
            }

            public CustomCommandEnvironment.Parameter GetParameter() => _getParameter();
        }
    }
}
