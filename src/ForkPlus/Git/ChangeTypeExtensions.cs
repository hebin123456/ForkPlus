using System;
using ForkPlus.Services;

namespace ForkPlus.Git
{
 public static class ChangeTypeExtensions
 {
  public static string GetIconKey(this ChangeType changeType)
  {
   return changeType switch
   {
    ChangeType.Added => IconKeys.StatusAdd,
    ChangeType.Untracked => IconKeys.StatusAdd,
    ChangeType.Modified => IconKeys.StatusEdit,
    ChangeType.Copied => IconKeys.StatusCopy,
    ChangeType.Deleted => IconKeys.StatusRemove,
    ChangeType.Renamed => IconKeys.StatusRename,
    ChangeType.Unmerged => IconKeys.Warning,
    ChangeType.TypeChanged => IconKeys.StatusEdit,
    ChangeType.Unknown => IconKeys.StatusEdit,
    ChangeType.Ignored => IconKeys.StatusAdd,
    // v3.8.0：未变更文件不显示状态图标（XAML DataTrigger 会折叠 Image 列）
    ChangeType.Unchanged => null,
    _ => null,
   };
  }

  public static string GetIconKey(this StatusType statusType)
  {
   return statusType switch
   {
    StatusType.Added => IconKeys.StatusAdd,
    StatusType.Broken => IconKeys.StatusEdit,
    StatusType.Copied => IconKeys.StatusCopy,
    StatusType.Deleted => IconKeys.StatusRemove,
    StatusType.Modified => IconKeys.StatusEdit,
    StatusType.Ignored => IconKeys.StatusAdd,
    StatusType.Renamed => IconKeys.StatusRename,
     StatusType.TypeChanged => IconKeys.StatusEdit,
     // 修复（2026-09-29，"变更文件树/列表里冲突文件显示普通铅笔（编辑）图标而非冲突标识"）：
     // 原来映射到 StatusEdit（铅笔），与 ChangeType.Unmerged => IconKeys.Warning 不一致，
     // 且 BridgeExtensions 的图标桥接里没有 StatusEdit→Warning 的路径 → 冲突状态一律显示铅笔。
     StatusType.Unmerged => IconKeys.Warning,
    StatusType.Untracked => IconKeys.StatusAdd,
    StatusType.Unknown => IconKeys.StatusEdit,
    StatusType.None => IconKeys.StatusEdit,
    _ => null,
   };
  }

  public static string ToFriendlyName(this StatusType statusType)
  {
   return statusType switch
   {
    StatusType.Added => "added",
    StatusType.Broken => "broken",
    StatusType.Copied => "copied",
    StatusType.Deleted => "deleted",
    StatusType.Modified => "modified",
    StatusType.Ignored => "ignored",
    StatusType.Renamed => "renamed",
    StatusType.TypeChanged => "typechanged",
    StatusType.Unmerged => "modified",
    StatusType.Untracked => "untracked",
    StatusType.Unknown => "unknown",
    StatusType.None => "none",
    _ => null,
   };
  }
 }
}
