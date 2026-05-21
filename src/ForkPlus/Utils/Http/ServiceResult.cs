namespace ForkPlus.Utils.Http
{
	public class ServiceResult
	{
		public bool Succeeded { get; }

		[Null]
		public ServiceError Error { get; }

		public ServiceResult(bool succeeded, [Null] ServiceError error)
		{
			Succeeded = succeeded;
			Error = error;
		}
	}
	public class ServiceResult<T> : ServiceResult
	{
		[Null]
		public T Result { get; }

		public ServiceResult(bool success, [Null] T result, [Null] ServiceError error)
			: base(success, error)
		{
			Result = result;
		}

		public static ServiceResult<T> Success(T result)
		{
			return new ServiceResult<T>(success: true, result, null);
		}

		public static ServiceResult<T> Failure(ServiceError error)
		{
			return new ServiceResult<T>(success: false, default(T), error);
		}
	}
}
