using System.Net.Http;
using System.Net.Http.Headers;

namespace ForkPlus.Accounts
{
	public class GitLabPrivateAccessTokenAuthentication : PrivateAccessTokenAuthentication
	{
		public GitLabPrivateAccessTokenAuthentication([Null] string serverUrl, [Null] string username, [Null] string token = null)
			: base(serverUrl, username, token)
		{
		}

		protected override void DoAuthorize(HttpRequestMessage request)
		{
			((HttpHeaders)request.Headers).Add("Private-Token", Token);
		}
	}
}
