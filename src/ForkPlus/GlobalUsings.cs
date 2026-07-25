// 阶段 5：全局 using 指令。
// WPF→Avalonia 迁移期，大量桥接扩展方法（WpfBridgeExtensions 等）位于 ForkPlus.UI 命名空间。
// C# 扩展方法解析要求显式 using，父命名空间查找不生效，故全局引入以避免逐文件添加。
// 阶段 6 完成迁移后，桥接扩展方法被原生 API 替换，本文件可删除。
global using ForkPlus.UI;
