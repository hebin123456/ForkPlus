using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitConfigTests
	{
		[Fact]
		public void GetString_WithSubsection_ReturnsValue()
		{
			GitConfig.Section[] sections =
			{
				new GitConfig.Section("remote", "origin",
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("url", "https://example.com/repo.git")
					})
			};
			GitConfig config = new GitConfig(sections);

			Assert.Equal("https://example.com/repo.git", config.GetString("remote", "origin", "url"));
		}

		[Fact]
		public void GetString_WithSubsection_MissingKey_ReturnsNull()
		{
			GitConfig.Section[] sections =
			{
				new GitConfig.Section("remote", "origin",
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("url", "https://example.com/repo.git")
					})
			};
			GitConfig config = new GitConfig(sections);

			Assert.Null(config.GetString("remote", "origin", "pushurl"));
		}

		[Fact]
		public void GetString_WithoutSubsection_ReturnsValue()
		{
			GitConfig.Section[] sections =
			{
				new GitConfig.Section("user", null,
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("name", "alice")
					})
			};
			GitConfig config = new GitConfig(sections);

			Assert.Equal("alice", config.GetString("user", null, "name"));
		}

		[Fact]
		public void GetString_MissingSection_ReturnsNull()
		{
			GitConfig config = new GitConfig(new GitConfig.Section[0]);

			Assert.Null(config.GetString("user", null, "name"));
		}

		[Fact]
		public void GitConfigEquals_SameConfig_ReturnsTrue()
		{
			GitConfig.Section[] sections =
			{
				new GitConfig.Section("remote", "origin",
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("url", "https://example.com/repo.git")
					})
			};
			GitConfig a = new GitConfig(sections);
			GitConfig b = new GitConfig(sections);

			Assert.True(a.GitConfigEquals(b));
		}

		[Fact]
		public void GitConfigEquals_DifferentVariableValue_ReturnsFalse()
		{
			GitConfig a = new GitConfig(new GitConfig.Section[]
			{
				new GitConfig.Section("user", null,
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("name", "alice")
					})
			});
			GitConfig b = new GitConfig(new GitConfig.Section[]
			{
				new GitConfig.Section("user", null,
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("name", "bob")
					})
			});

			Assert.False(a.GitConfigEquals(b));
		}

		[Fact]
		public void GitConfigEquals_DifferentSectionCount_ReturnsFalse()
		{
			GitConfig a = new GitConfig(new GitConfig.Section[]
			{
				new GitConfig.Section("user", null,
					new GitConfig.Variable[]
					{
						new GitConfig.Variable("name", "alice")
					})
			});
			GitConfig b = new GitConfig(new GitConfig.Section[0]);

			Assert.False(a.GitConfigEquals(b));
		}

		[Fact]
		public void Section_ToString_WithSubsection_ReturnsDottedName()
		{
			GitConfig.Section section = new GitConfig.Section("remote", "origin", new GitConfig.Variable[0]);

			Assert.Equal("remote.origin", section.ToString());
		}

		[Fact]
		public void Section_ToString_WithoutSubsection_ReturnsNameOnly()
		{
			GitConfig.Section section = new GitConfig.Section("user", null, new GitConfig.Variable[0]);

			Assert.Equal("user", section.ToString());
		}

		[Fact]
		public void Variable_ToString_ReturnsNameEqualsValue()
		{
			GitConfig.Variable variable = new GitConfig.Variable("url", "https://example.com/repo.git");

			Assert.Equal("url = https://example.com/repo.git", variable.ToString());
		}
	}
}
