using Xunit;

namespace ForkPlus.Tests
{
	public class AskPassParserTests
	{
		[Fact]
		public void ParseSshKey_ReturnsQuotedKeyPath()
		{
			string keyPath = AskPassParser.ParseSshKey("Enter passphrase for key '/home/user/.ssh/id_ed25519':");

			Assert.Equal("/home/user/.ssh/id_ed25519", keyPath);
		}

		[Fact]
		public void ParseSshKey_ReturnsNullForNonPassphrasePrompt()
		{
			Assert.Null(AskPassParser.ParseSshKey("Username for 'https://example.com':"));
		}

		[Fact]
		public void ParseSshKey_ReturnsNullForMalformedPrompt()
		{
			Assert.Null(AskPassParser.ParseSshKey("Enter passphrase for key without quotes"));
		}
	}
}
