// Win32 IFileDialog COM 互操作：替代 WindowsAPICodePack（其强依赖 WPF 已隔离）。
// 保持同步对话框 API（ForkPlus 的调用链均为同步），Windows 之外平台返回取消。
// Migration note：跨平台时改用 Avalonia StorageProvider（需要把调用链异步化）。

using System;
using System.Runtime.InteropServices;

namespace ForkPlus.UI.Win32
{
    internal enum FOS : uint
    {
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_PATHMUSTEXIST = 0x00000800,
        FOS_FILEMUSTEXIST = 0x00001000,
        FOS_OVERWRITEPROMPT = 0x00000002,
        FOS_NOCHANGEDIR = 0x00000008,
    }

    internal enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000,
    }

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileDialog
    {
        // WPF 版 IFileDialog 继承自 IModalWindow（Show 为其唯一方法）。
        // 此处将 Show 平铺进本接口，vtable 布局与 COM 定义一致。
        [PreserveSig] int Show(IntPtr hwndOwner);

        // IFileDialog
        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(in Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOpenDialog : IFileDialog
    {
        void GetResults(out IntPtr ppenum);
        void GetSelectedItems(out IntPtr ppsai);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        void BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    internal static class FileDialogInterop
    {
        private static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
        private static readonly Guid CLSID_FileSaveDialog = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");
        private static readonly Guid IID_IFileDialog = new("42f85136-db7e-439c-85f1-e4075d135fc8");
        private static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        public static bool IsWindows => OperatingSystem.IsWindows();

        private static IFileDialog CreateOpen() => (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOpenDialog));
        private static IFileDialog CreateSave() => (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileSaveDialog));

        /// <summary>打开文件 / 目录选择框。返回 true 表示用户确认且 path 非空。</summary>
        public static bool ShowOpenDialog(IntPtr owner, string title, string initialDirectory, bool folderPicker,
            (string name, string spec)[] filters, out string path)
        {
            path = null;
            if (!IsWindows) return false;
            try
            {
                var dlg = CreateOpen();
                FOS options = FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST | FOS.FOS_NOCHANGEDIR;
                if (folderPicker) options |= FOS.FOS_PICKFOLDERS;
                else options |= FOS.FOS_FILEMUSTEXIST;
                dlg.SetOptions(options);
                if (!string.IsNullOrEmpty(title)) dlg.SetTitle(title);
                if (!string.IsNullOrEmpty(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
                    dlg.SetFolder(CreateItem(initialDirectory));
                if (filters != null && filters.Length > 0)
                {
                    var specs = new COMDLG_FILTERSPEC[filters.Length];
                    for (int i = 0; i < filters.Length; i++) specs[i] = new COMDLG_FILTERSPEC { pszName = filters[i].name, pszSpec = filters[i].spec };
                    dlg.SetFileTypes((uint)specs.Length, specs);
                }
                if (dlg.Show(owner) != 0) return false; // S_OK 才继续
                dlg.GetResult(out var item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out path);
                return !string.IsNullOrEmpty(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>打开保存框。返回 true 表示用户确认且 path 非空。</summary>
        public static bool ShowSaveDialog(IntPtr owner, string title, string initialDirectory, string defaultFileName,
            (string name, string spec)[] filters, out string path)
        {
            path = null;
            if (!IsWindows) return false;
            try
            {
                var dlg = CreateSave();
                dlg.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST | FOS.FOS_OVERWRITEPROMPT | FOS.FOS_NOCHANGEDIR);
                if (!string.IsNullOrEmpty(title)) dlg.SetTitle(title);
                if (!string.IsNullOrEmpty(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
                    dlg.SetFolder(CreateItem(initialDirectory));
                if (!string.IsNullOrEmpty(defaultFileName)) dlg.SetFileName(defaultFileName);
                if (filters != null && filters.Length > 0)
                {
                    var specs = new COMDLG_FILTERSPEC[filters.Length];
                    for (int i = 0; i < filters.Length; i++) specs[i] = new COMDLG_FILTERSPEC { pszName = filters[i].name, pszSpec = filters[i].spec };
                    dlg.SetFileTypes((uint)specs.Length, specs);
                }
                if (dlg.Show(owner) != 0) return false;
                dlg.GetResult(out var item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out path);
                return !string.IsNullOrEmpty(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static IShellItem CreateItem(string path)
        {
            SHCreateItemFromParsingName(path, IntPtr.Zero, IID_IShellItem, out object result);
            return (IShellItem)result;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    }
}
