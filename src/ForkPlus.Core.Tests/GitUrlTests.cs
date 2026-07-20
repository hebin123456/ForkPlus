using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitUrlTests
	{
		[Fact]
		public void HttpsGithubUrl_ParsesExpectedFields()
		{
			var url = new GitUrl("https://github.com/owner/repo.git");

			Assert.Equal(GitUrl.NetworkProtocol.Https, url.Protocol);
			Assert.Equal("github.com", url.Host);
			Assert.Equal("repo", url.RepositoryName);
			Assert.True(url.IsValid);
			Assert.Equal(RemoteType.Github, url.RemoteType);
		}

		[Fact]
		public void SshGithubUrl_ParsesExpectedFields()
		{
			var url = new GitUrl("git@github.com:owner/repo.git");

			Assert.Equal(GitUrl.NetworkProtocol.Ssh, url.Protocol);
			Assert.Equal("github.com", url.Host);
			Assert.Equal("repo", url.RepositoryName);
			Assert.True(url.IsValid);
		}

		[Fact]
		public void HttpsGitlabUrl_DetectsGitlabRemoteType()
		{
			var url = new GitUrl("https://gitlab.com/owner/repo");

			Assert.Equal(RemoteType.Gitlab, url.RemoteType);
			Assert.Equal("gitlab.com", url.Host);
			Assert.Equal("repo", url.RepositoryName);
		}

		[Fact]
		public void RepositoryName_DoesNotIncludeGitSuffix()
		{
			var url = new GitUrl("https://github.com/owner/myrepo.git");

			Assert.Equal("myrepo", url.RepositoryName);
			Assert.DoesNotContain(".git", url.RepositoryName);
		}

		[Fact]
		public void Slug_ReturnsPathWithoutLeadingSlash()
		{
			var url = new GitUrl("https://github.com/owner/repo.git");

			Assert.Equal("owner/repo", url.Slug);
		}

		[Fact]
		public void InvalidUrl_HasInvalidFlagAndNullRepositoryName()
		{
			var url = new GitUrl("not-a-valid-url");

			Assert.False(url.IsValid);
			Assert.Null(url.RepositoryName);
			Assert.Equal(GitUrl.NetworkProtocol.Other, url.Protocol);
		}
	}
}
