// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/SrcDirViewModel.cs（39 行）：
//   - public class SrcDirViewModel
//   - 属性：string Path / ImageSource SrcFolderIcon / string SrcFolderIconTooltip
//   - 构造函数：SrcFolderIsValid(path) → Theme.FolderIcon / Theme.WarningIcon
//   - SrcFolderIsValid：检查 path 不是 "c:" 也不是 %userprofile%
//
// Avalonia 版差异：
//   1. WPF ImageSource → Avalonia.Media.IImage
//   2. WPF Theme.FolderIcon / Theme.WarningIcon →
//      global::ForkPlus.Avalonia.Theme.FolderIcon / .WarningIcon（避免与 Control.Theme 冲突）
//   3. WPF path.TrimEnd("\\") → string.TrimEnd(char) 跨平台一致（C# API 不变）
//   4. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public class SrcDirViewModel
    {
        public string Path { get; }

        public IImage SrcFolderIcon { get; }

        public string SrcFolderIconTooltip { get; }

        public SrcDirViewModel(string path)
        {
            Path = path;
            if (SrcFolderIsValid(path))
            {
                SrcFolderIcon = global::ForkPlus.Avalonia.Theme.FolderIcon;
                SrcFolderIconTooltip = null;
            }
            else
            {
                SrcFolderIcon = global::ForkPlus.Avalonia.Theme.WarningIcon;
                SrcFolderIconTooltip = "'" + path + "' should not be used as a source directory. Please choose a subfolder instead";
            }
        }

        private static bool SrcFolderIsValid(string path)
        {
            string value = Environment.ExpandEnvironmentVariables("%userprofile%");
            if (!path.TrimEnd('\\').Equals("c:", StringComparison.OrdinalIgnoreCase))
            {
                return !path.TrimEnd('\\').Equals(value, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
