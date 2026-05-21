using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Utils.Http
{
	[DebuggerDisplay("{Connection.ServerUrl}")]
	public abstract class RestClientBase
	{
		public readonly Connection Connection;

		public RestClientBase(Connection connection)
		{
			Connection = connection;
		}

		protected ServiceResult<T> Request<T>(ApiRequest request, Func<JObject, T> decoder)
		{
			ServiceResult<object> jsonResponse = Connection.JsonRequest(request);
			return Decode(jsonResponse, decoder);
		}

		protected ServiceResult<T> Request<T>(string slug, Func<JObject, T> decoder)
		{
			return Request(new ApiRequest(slug), decoder);
		}

		protected ServiceResult<T[]> RequestArray<T>(ApiRequest request, Func<JArray, T[]> decoder)
		{
			ServiceResult<object> jsonResponse = Connection.JsonRequest(request);
			return DecodeArray(jsonResponse, decoder);
		}

		protected ServiceResult<T[]> RequestArray<T, X>(ApiRequest request, Func<JObject, X> decoder, Func<X, T[]> extractor)
		{
			ServiceResult<object> jsonResponse = Connection.JsonRequest(request);
			ServiceResult<X> serviceResult = Decode(jsonResponse, decoder);
			if (!serviceResult.Succeeded)
			{
				return ServiceResult<T[]>.Failure(serviceResult.Error);
			}
			return ServiceResult<T[]>.Success(extractor(serviceResult.Result));
		}

		protected ServiceResult<T> Decode<T>(ServiceResult<object> jsonResponse, Func<JObject, T> decoder)
		{
			if (!jsonResponse.Succeeded)
			{
				if (jsonResponse.Error is ServiceError.RemoteServiceJsonError jsonError)
				{
					return DecodeJsonError<T>(jsonError);
				}
				return ServiceResult<T>.Failure(jsonResponse.Error);
			}
			if (!(jsonResponse.Result is JObject arg))
			{
				return ServiceResult<T>.Failure(new ServiceError.ParseError("The result is not a json object"));
			}
			T val = decoder(arg);
			if (val == null)
			{
				return ServiceResult<T>.Failure(new ServiceError.ParseError(typeof(T).Name + " json"));
			}
			return ServiceResult<T>.Success(val);
		}

		protected virtual ServiceResult<T> DecodeJsonError<T>(ServiceError.RemoteServiceJsonError jsonError)
		{
			return ServiceResult<T>.Failure(jsonError);
		}

		protected ServiceResult<T[]> DecodeArray<T>(ServiceResult<object> jsonResponse, Func<JArray, T[]> decoder)
		{
			if (!jsonResponse.Succeeded)
			{
				if (jsonResponse.Error is ServiceError.RemoteServiceJsonError jsonError)
				{
					return DecodeJsonError<T[]>(jsonError);
				}
				return ServiceResult<T[]>.Failure(jsonResponse.Error);
			}
			if (!(jsonResponse.Result is JArray arg))
			{
				return ServiceResult<T[]>.Failure(new ServiceError.ParseError("The result is not a json array"));
			}
			T[] array = decoder(arg);
			if (array == null)
			{
				return ServiceResult<T[]>.Failure(new ServiceError.ParseError(typeof(T).Name + " array json"));
			}
			return ServiceResult<T[]>.Success(array);
		}
	}
}
