using System.Net.Http;

namespace ForkPlus.Utils.Http
{
	public interface IRestServiceAuthentication
	{
		AuthenticationType AuthenticationType { get; }

		[Null]
		string Username { get; }

		bool Authorize(HttpRequestMessage request);

		[Null]
		string GetHttpsPassword();

		void Destroy();
	}
}
