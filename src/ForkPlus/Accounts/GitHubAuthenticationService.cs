using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Accounts
{
	public class GitHubAuthenticationService : RestClientBase
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
				return new OAuthToken(@string, null, null);
			}
		}

		public GitHubAuthenticationService(Connection connection)
			: base(connection)
		{
		}

		public ServiceResult<OAuthToken> GetAccessToken(string code, string state)
		{
			ApiRequest apiRequest = new ApiRequest("/login/oauth/access_token");
			apiRequest.AddParameter("client_id", GitHubConsts.ClientId);
			apiRequest.AddParameter("client_secret", GitHubConsts.ClientSecret);
			apiRequest.AddParameter("code", code);
			apiRequest.AddParameter("state", state);
			return Request(apiRequest, Coder.DecodeAccessToken);
		}
	}
}
