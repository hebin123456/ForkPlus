using System;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public class Paginator<T> : IPaged<T>
	{
		protected readonly int _pageSize;

		private int _currentPage = 1;

		private object _lock = new object();

		private bool _hasNext = true;

		private readonly Func<int, int, ServiceResult<T[]>> _request;

		public bool HasNext
		{
			get
			{
				lock (_lock)
				{
					return _hasNext;
				}
			}
			set
			{
				lock (_lock)
				{
					_hasNext = value;
				}
			}
		}

		public Paginator(int pageSize, Func<int, int, ServiceResult<T[]>> request)
		{
			_pageSize = pageSize;
			_request = request;
		}

		public virtual ServiceResult<T[]> LoadNext()
		{
			ServiceResult<T[]> serviceResult = _request(_currentPage, _pageSize);
			if (serviceResult.Succeeded)
			{
				if (serviceResult.Result.Length < _pageSize)
				{
					HasNext = false;
				}
				_currentPage++;
			}
			return serviceResult;
		}
	}
}
