using System.Collections.Generic;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public static class IPagedExtensions
	{
		public static ServiceResult<T[]> LoadAll<T>(this IPaged<T> paginator)
		{
			int num = 20;
			List<T> list = new List<T>();
			while (paginator.HasNext && num-- > 0)
			{
				ServiceResult<T[]> serviceResult = paginator.LoadNext();
				if (!serviceResult.Succeeded)
				{
					return serviceResult;
				}
				list.AddRange(serviceResult.Result);
			}
			return ServiceResult<T[]>.Success(list.ToArray());
		}
	}
}
