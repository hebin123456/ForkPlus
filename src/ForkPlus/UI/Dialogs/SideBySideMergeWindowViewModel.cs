namespace ForkPlus.UI.Dialogs
{
	// 阶段 3：承接 SideBySideMerge 的提交允许判定。
	// IsSubmitAllowed 依赖 _mergeMode（Text/Binary）与复选框三态状态，由 View 推入。
	// Text 模式：mergeConflict.IsResolved。Binary 模式：AllLocal/AllRemote 互斥选中。
	// MergeCodeEditor/AvalonEdit/MergeConflictView 等重度 WPF 逻辑全留 View。
	internal sealed class SideBySideMergeWindowViewModel
	{
		private bool _isTextMode;
		private bool _isBinaryMode;
		private bool _mergeConflictResolved;
		private bool? _allLocalChecked;
		private bool? _allRemoteChecked;

		public bool IsTextMode
		{
			set => _isTextMode = value;
		}

		public bool IsBinaryMode
		{
			set => _isBinaryMode = value;
		}

		public bool MergeConflictResolved
		{
			set => _mergeConflictResolved = value;
		}

		public bool? AllLocalChecked
		{
			set => _allLocalChecked = value;
		}

		public bool? AllRemoteChecked
		{
			set => _allRemoteChecked = value;
		}

		public bool IsSubmitAllowed
		{
			get
			{
				if (_isTextMode)
				{
					return _mergeConflictResolved;
				}
				if (_isBinaryMode)
				{
					if (!_allLocalChecked.GetValueOrDefault() || _allRemoteChecked != false)
					{
						if (_allRemoteChecked.GetValueOrDefault())
						{
							return _allLocalChecked == false;
						}
						return false;
					}
					return true;
				}
				return false;
			}
		}
	}
}
