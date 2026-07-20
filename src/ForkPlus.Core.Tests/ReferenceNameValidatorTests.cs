using Xunit;

namespace ForkPlus.Tests
{
	public class ReferenceNameValidatorTests
	{
		[Theory]
		[InlineData("feature/new-ui")]
		[InlineData("release/2026.05")]
		[InlineData("bugfix_issue-123")]
		public void Validate_ReturnsNullForValidReferenceName(string referenceName)
		{
			Assert.Null(ReferenceNameValidator.Validate(referenceName));
		}

		[Theory]
		[InlineData("feature..name", "Name cannot contain '..'")]
		[InlineData("/feature", "Name cannot begin with '/'")]
		[InlineData("feature/.name", "Name cannot contain '/.'")]
		[InlineData("feature/name.lock", "Name cannot contain '/' and end with '.lock'")]
		[InlineData("@", "Name cannot be the single '@' character")]
		[InlineData("feature\\name", "Name cannot contain '\\'")]
		public void Validate_ReturnsExpectedErrorForInvalidReferenceName(string referenceName, string expected)
		{
			Assert.Equal(expected, ReferenceNameValidator.Validate(referenceName));
		}

		[Fact]
		public void ValidateGitFlow_RejectsLeadingDot()
		{
			Assert.Equal("Name cannot start with '.'", ReferenceNameValidator.ValidateGitFlow(".feature"));
		}

		[Fact]
		public void ValidateGitFlow_RejectsDotLockSuffix()
		{
			Assert.Equal("Name cannot end with '.lock'", ReferenceNameValidator.ValidateGitFlow("feature.lock"));
		}
	}
}
