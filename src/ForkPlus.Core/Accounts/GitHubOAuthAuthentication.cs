using System.Net.Http;
using System.Net.Http.Headers;
using ForkPlus.Utils.Http;

namespace ForkPlus.Accounts
{
	public class GitHubOAuthAuthentication : PrivateAccessTokenAuthentication
	{
		public override AuthenticationType AuthenticationType => AuthenticationType.OAuth;

		public GitHubOAuthAuthentication(string serverUrl, string username, [Null] string token = null)
			: base(serverUrl, username, token)
		{
		}

		protected override void DoAuthorize(HttpRequestMessage request)
		{
			((HttpHeaders)request.Headers).Add("Authorization", "token " + Token);
		}

		protected override string Key(string host, string username)
		{
			return "fork:" + host + "." + username + ".oauth_token";
		}
	}
}
