using System.Net.Http;
using System.Net.Http.Headers;

namespace ForkPlus.Accounts
{
	public class GitHubAccessTokenAuthentication : PrivateAccessTokenAuthentication
	{
		public GitHubAccessTokenAuthentication([Null] string serverUrl, string username, [Null] string token = null)
			: base(serverUrl, username, token)
		{
		}

		protected override void DoAuthorize(HttpRequestMessage request)
		{
			((HttpHeaders)request.Headers).Add("Authorization", "token " + Token);
		}
	}
}
