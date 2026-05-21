using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public interface IPaged<T>
	{
		bool HasNext { get; }

		ServiceResult<T[]> LoadNext();
	}
}
