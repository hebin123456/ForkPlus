using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ForkPlus.Git
{
	[DebuggerDisplay("[{Status}{WorkingDirectoryStatus}] {Path}")]
	public class ChangedFile
	{
		public sealed class ChangedFileComparer : IComparer<ChangedFile>
		{
			private readonly StringComparer _stringComparer;

			public ChangedFileComparer(StringComparer stringComparer)
			{
				_stringComparer = stringComparer;
			}

			public int Compare(ChangedFile x, ChangedFile y)
			{
				int num = _stringComparer.Compare(x.Path, y.Path);
				if (num != 0)
				{
					return num;
				}
				int num2 = x.Staged.CompareTo(y.Staged);
				if (num2 != 0)
				{
					return num2;
				}
				int num3 = ((byte)x.ChangeType).CompareTo((byte)y.ChangeType);
				if (num3 != 0)
				{
					return num3;
				}
				return string.Compare(x.TreeIsh, y.TreeIsh);
			}
		}

		public static readonly ChangedFileComparer Comparer = new ChangedFileComparer(StringComparer.Ordinal);

		public StatusType Status { get; }

		public StatusType WorkingDirectoryStatus { get; }

		public ChangeType ChangeType { get; }

		[Null]
		public string OldPath { get; }

		public string Path { get; }

		public bool Staged { get; }

		public bool Tracked { get; }

		public bool New { get; }

		public bool IsDirectory { get; }

		[Null]
		public string TreeIsh { get; }

		[Null]
		public string FileMode { get; }

		public ChangedFile(string path, StatusType status, StatusType workingDirectoryStatus, ChangeType changeType, bool staged, bool isNew = false, bool tracked = true, [Null] string oldPath = null, [Null] string treeIsh = null, [Null] string fileMode = null)
		{
			Path = path;
			OldPath = oldPath;
			Status = status;
			WorkingDirectoryStatus = workingDirectoryStatus;
			Staged = staged;
			ChangeType = changeType;
			New = isNew;
			Tracked = tracked;
			TreeIsh = treeIsh;
			FileMode = fileMode;
			if (!staged)
			{
				switch (changeType)
				{
				}
			}
			if (staged)
			{
				switch (changeType)
				{
				}
			}
		}

		public ChangedFile(string path, StatusType status, StatusType workingDirectoryStatus = StatusType.None, [Null] string oldPath = null, [Null] string treeIsh = null, [Null] string fileMode = null)
			: this(path, status, workingDirectoryStatus, NewChangeType(status, workingDirectoryStatus), IsStaged(status, workingDirectoryStatus), IsNew(status, workingDirectoryStatus), IsTracked(workingDirectoryStatus), oldPath, treeIsh, fileMode)
		{
		}

		public ChangedFile(string directoryPath, bool staged)
		{
			Path = directoryPath;
			OldPath = null;
			Status = (staged ? StatusType.Unknown : StatusType.None);
			WorkingDirectoryStatus = (staged ? StatusType.None : StatusType.Unknown);
			Staged = staged;
			ChangeType = ChangeType.Unknown;
			IsDirectory = true;
			Tracked = false;
			New = false;
			TreeIsh = null;
			FileMode = null;
		}

		public bool IsUnmerged()
		{
			return IsUnmerged(Status, WorkingDirectoryStatus);
		}

		internal bool ChangedFileEquals(ChangedFile other)
		{
			if (Path == other.Path && Staged == other.Staged && ChangeType == other.ChangeType)
			{
				return TreeIsh == other.TreeIsh;
			}
			return false;
		}

		private static bool IsUnmerged(StatusType status, StatusType workingDirectoryStatus)
		{
			if ((status != StatusType.Deleted || workingDirectoryStatus != StatusType.Deleted) && (status != 0 || workingDirectoryStatus != StatusType.Unmerged) && (status != StatusType.Unmerged || workingDirectoryStatus != StatusType.Deleted) && (status != StatusType.Unmerged || workingDirectoryStatus != 0) && (status != StatusType.Deleted || workingDirectoryStatus != StatusType.Unmerged) && (status != 0 || workingDirectoryStatus != 0))
			{
				if (status == StatusType.Unmerged)
				{
					return workingDirectoryStatus == StatusType.Unmerged;
				}
				return false;
			}
			return true;
		}

		private static bool IsStaged(StatusType status, StatusType workingDirectoryStatus)
		{
			if (IsUnmerged(status, workingDirectoryStatus))
			{
				return false;
			}
			if (status != StatusType.Untracked && status != StatusType.Ignored && status != StatusType.None)
			{
				return true;
			}
			return false;
		}

		private static bool IsTracked(StatusType workingDirectoryStatus)
		{
			if (workingDirectoryStatus != StatusType.Untracked)
			{
				return workingDirectoryStatus != StatusType.Ignored;
			}
			return false;
		}

		private static bool IsNew(StatusType status, StatusType workingDirectoryStatus)
		{
			if (IsUnmerged(status, workingDirectoryStatus))
			{
				return false;
			}
			if (status == StatusType.Ignored || workingDirectoryStatus == StatusType.Ignored)
			{
				return true;
			}
			if (!IsTracked(workingDirectoryStatus))
			{
				return true;
			}
			if (status == StatusType.Added || status == StatusType.Renamed || status == StatusType.Copied)
			{
				return true;
			}
			return false;
		}

		private static ChangeType NewChangeType(StatusType status, StatusType workingDirectoryStatus)
		{
			if (status == StatusType.Untracked && workingDirectoryStatus == StatusType.Untracked)
			{
				return ChangeType.Added;
			}
			if (status == StatusType.Ignored && workingDirectoryStatus == StatusType.Ignored)
			{
				return ChangeType.Ignored;
			}
			if (status == StatusType.Unmerged || workingDirectoryStatus == StatusType.Unmerged || (status == StatusType.Added && workingDirectoryStatus == StatusType.Added) || (status == StatusType.Deleted && workingDirectoryStatus == StatusType.Deleted))
			{
				return ChangeType.Unmerged;
			}
			switch (workingDirectoryStatus)
			{
			case StatusType.Modified:
				return ChangeType.Modified;
			case StatusType.Added:
				return ChangeType.Added;
			case StatusType.Deleted:
				return ChangeType.Deleted;
			case StatusType.TypeChanged:
				return ChangeType.TypeChanged;
			default:
				Log.Error($"unknown unstaged case {workingDirectoryStatus}");
				break;
			case StatusType.None:
				break;
			}
			switch (status)
			{
			case StatusType.Added:
				return ChangeType.Added;
			case StatusType.Copied:
				return ChangeType.Copied;
			case StatusType.Deleted:
				return ChangeType.Deleted;
			case StatusType.Modified:
				return ChangeType.Modified;
			case StatusType.Renamed:
				return ChangeType.Renamed;
			case StatusType.TypeChanged:
				return ChangeType.TypeChanged;
			default:
				Log.Error($"unknown staged case {status}");
				break;
			case StatusType.None:
				break;
			}
			return ChangeType.Unknown;
		}
	}
}
