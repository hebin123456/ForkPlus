using System;

namespace ForkPlus.Jobs
{
	public class CancelHandler
	{
		private object _updateLock = new object();

		private Action _cancellationAction;

		private bool _isCanceled;

		public bool IsCanceled
		{
			get
			{
				lock (_updateLock)
				{
					return _isCanceled;
				}
			}
		}

		public void SetCancellationAction(Action cancellationAction)
		{
			lock (_updateLock)
			{
				_cancellationAction = cancellationAction;
			}
		}

		public void Cancel()
		{
			lock (_updateLock)
			{
				_cancellationAction?.Invoke();
				_isCanceled = true;
			}
		}
	}
}
