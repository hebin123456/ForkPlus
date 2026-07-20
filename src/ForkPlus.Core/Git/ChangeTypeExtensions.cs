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
    StatusType.Unmerged => IconKeys.StatusEdit,
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
