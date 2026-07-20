namespace ForkPlus
{
	public class Credential
	{
		private readonly string _applicationName;

		private readonly string _userName;

		private readonly string _password;

		private readonly CredentialType _credentialType;

		public CredentialType CredentialType => _credentialType;

		public string ApplicationName => _applicationName;

		public string UserName => _userName;

		public string Password => _password;

		public Credential(CredentialType credentialType, string applicationName, string userName, string password)
		{
			_applicationName = applicationName;
			_userName = userName;
			_password = password;
			_credentialType = credentialType;
		}

		public override string ToString()
		{
			return $"CredentialType: {CredentialType}, ApplicationName: {ApplicationName}, UserName: {UserName}, Password: {Password}";
		}
	}
}
