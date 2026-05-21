using System;
using System.Net.Http;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class BitbucketAuthenticationService : RestClientBase
	{
		private class Coder
		{
			[Null]
			public static OAuthToken DecodeAccessToken(JObject json)
			{
				string @string = json.GetString("access_token");
				if (@string == null)
				{
					Log.Warn("Cannot parse OAuthToken json");
					return null;
				}
				string string2 = json.GetString("refresh_token");
				DateTime? expirationTimeUTC = null;
				int? num = json["expires_in"]?.Value<int>();
				if (num.HasValue)
				{
					int valueOrDefault = num.GetValueOrDefault();
					expirationTimeUTC = DateTime.UtcNow.AddSeconds(valueOrDefault);
				}
				return new OAuthToken(@string, string2, expirationTimeUTC);
			}
		}

		public BitbucketAuthenticationService(Connection connection)
			: base(connection)
		{
		}

		public ServiceResult<OAuthToken> GetAccessToken(string code)
		{
			ApiRequest apiRequest = new ApiRequest(HttpMethod.Post, "/site/oauth2/access_token");
			apiRequest.AddParameter("grant_type", "authorization_code");
			apiRequest.AddParameter("code", code);
			apiRequest.AddParameter("redirect_uri", BitbucketConsts.CallbackUri);
			ServiceResult<object> jsonResponse = Connection.JsonRequest(apiRequest);
			return Decode(jsonResponse, Coder.DecodeAccessToken);
		}

		public ServiceResult<OAuthToken> RefreshToken(string refreshToken)
		{
			ApiRequest apiRequest = new ApiRequest(HttpMethod.Post, "/site/oauth2/access_token");
			apiRequest.AddParameter("grant_type", "refresh_token");
			apiRequest.AddParameter("refresh_token", refreshToken);
			ServiceResult<object> jsonResponse = Connection.JsonRequest(apiRequest);
			return Decode(jsonResponse, Coder.DecodeAccessToken);
		}
	}
}
