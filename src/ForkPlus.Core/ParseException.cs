using System;

namespace ForkPlus
{
	internal class ParseException : Exception
	{
		public ParseException()
		{
		}

		public ParseException(string message)
			: base(message)
		{
		}
	}
}
