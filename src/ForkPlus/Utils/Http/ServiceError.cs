using System.Net;
using ForkPlus.Services;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Utils.Http
{
	public abstract class ServiceError
	{
		public class Cancelled : ServiceError
		{
			public override string FriendlyMessage => ServiceLocator.Localization.Current("Cancelled");
		}

		public class NotFound : ServiceError
		{
			public override string FriendlyMessage => "Not found (404)";

			public NotFound(string url)
			{
				Log.Warn("Not Found (404): " + url);
			}
		}

		public class HttpError : ServiceError
		{
			public override string FriendlyMessage => $"HTTP Error {(int)ErrorCode} ({ErrorCode})";

			public HttpStatusCode ErrorCode { get; }

			public HttpError(HttpStatusCode errorCode)
			{
				ErrorCode = errorCode;
			}
		}

		public class EmptyPaginatorError : ServiceError
		{
			public override string FriendlyMessage => "Paginator has no items";
		}

		public class JsonParseError : ServiceError
		{
			public override string FriendlyMessage => "Cannot parse JSON in server response";
		}

		public class AuthorizationLoadingError : ServiceError
		{
			public override string FriendlyMessage => "Cannot read authorization from Credential Manager";
		}

		public class ParseError : ServiceError
		{
			public override string FriendlyMessage => "Parse Error: " + Message + ". Contact support.";

			public string Message { get; }

			public ParseError(string message)
			{
				Message = message;
			}
		}

		public class UnknownError : ServiceError
		{
			public override string FriendlyMessage => "Error: " + Message;

			public string Message { get; }

			public UnknownError(string message)
			{
				Message = message;
			}
		}

		public class RemoteServiceJsonError : ServiceError
		{
			public override string FriendlyMessage => ServiceLocator.Localization.Current("Remote service returned an error response.");

			public JContainer Json { get; }

			public RemoteServiceJsonError(JContainer json)
			{
				Json = json;
			}
		}

		public class RemoteServiceError : ServiceError
		{
			private readonly string _message;

			public override string FriendlyMessage => _message;

			public RemoteServiceError(string message)
			{
				_message = message;
			}
		}

		public abstract string FriendlyMessage { get; }
	}
}
