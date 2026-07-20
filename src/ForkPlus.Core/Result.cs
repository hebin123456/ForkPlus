namespace ForkPlus
{
	public struct Result<TValue, TError> where TError : class
	{
		public readonly TValue Value;

		[Null]
		public readonly TError Error;

		public bool IsOk => Error == null;

		public static Result<TValue, TError> Ok(TValue val)
		{
			return new Result<TValue, TError>(val, null);
		}

		public static Result<TValue, TError> Err(TError error)
		{
			return new Result<TValue, TError>(default(TValue), error);
		}

		private Result(TValue value, [Null] TError error)
		{
			Value = value;
			Error = error;
		}
	}
}
