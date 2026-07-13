using ForkPlus.Git;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	public class RevisionSearchQueryCoderTests
	{
		[Fact]
		public void Encode_ReturnsJArrayWithOneEntryPerQuery()
		{
			RevisionSearchQuery[] queries =
			{
				new RevisionSearchQuery(RevisionSearchType.Message, RevisionSearchScope.Repository, "fix"),
				new RevisionSearchQuery(RevisionSearchType.Author, RevisionSearchScope.CurrentBranch, "alice")
			};

			JArray jArray = RevisionSearchQuery.Coder.Encode(queries);

			Assert.Equal(2, jArray.Count);
			Assert.Equal((long)RevisionSearchType.Message, jArray[0]["Type"].Value<long>());
			Assert.Equal((long)RevisionSearchScope.Repository, jArray[0]["Scope"].Value<long>());
			Assert.Equal("fix", jArray[0]["SearchString"].Value<string>());
			Assert.Equal((long)RevisionSearchType.Author, jArray[1]["Type"].Value<long>());
			Assert.Equal((long)RevisionSearchScope.CurrentBranch, jArray[1]["Scope"].Value<long>());
			Assert.Equal("alice", jArray[1]["SearchString"].Value<string>());
		}

		[Fact]
		public void Encode_EmptyArray_ReturnsEmptyJArray()
		{
			JArray jArray = RevisionSearchQuery.Coder.Encode(new RevisionSearchQuery[0]);

			Assert.NotNull(jArray);
			Assert.Empty(jArray);
		}

		[Fact]
		public void Decode_NullJArray_ReturnsEmptyArray()
		{
			RevisionSearchQuery[] result = RevisionSearchQuery.Coder.Decode(null);

			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public void Decode_EmptyJArray_ReturnsEmptyArray()
		{
			RevisionSearchQuery[] result = RevisionSearchQuery.Coder.Decode(new JArray());

			Assert.NotNull(result);
			Assert.Empty(result);
		}

		[Fact]
		public void EncodeThenDecode_RoundTripsAllPropertiesAcrossAllEnumValues()
		{
			RevisionSearchQuery[] queries =
			{
				new RevisionSearchQuery(RevisionSearchType.Message, RevisionSearchScope.Repository, "fix"),
				new RevisionSearchQuery(RevisionSearchType.Author, RevisionSearchScope.CurrentBranch, "alice"),
				new RevisionSearchQuery(RevisionSearchType.DiffPath, RevisionSearchScope.Repository, "src/file.cs"),
				new RevisionSearchQuery(RevisionSearchType.DiffContent, RevisionSearchScope.CurrentBranch, "TODO"),
				new RevisionSearchQuery(RevisionSearchType.All, RevisionSearchScope.Repository, "")
			};

			JArray jArray = RevisionSearchQuery.Coder.Encode(queries);
			RevisionSearchQuery[] decoded = RevisionSearchQuery.Coder.Decode(jArray);

			Assert.Equal(queries.Length, decoded.Length);
			for (int i = 0; i < queries.Length; i++)
			{
				Assert.Equal(queries[i].Type, decoded[i].Type);
				Assert.Equal(queries[i].Scope, decoded[i].Scope);
				Assert.Equal(queries[i].SearchString, decoded[i].SearchString);
			}
		}

		[Fact]
		public void Equals_BothNull_ReturnsTrue()
		{
			Assert.True(RevisionSearchQuery.Equals(null, null));
		}

		[Fact]
		public void Equals_LeftNullRightNonNull_ReturnsFalse()
		{
			RevisionSearchQuery query = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.Repository, "fix");

			Assert.False(RevisionSearchQuery.Equals(null, query));
		}

		[Fact]
		public void Equals_LeftNonNullRightNull_ReturnsFalse()
		{
			RevisionSearchQuery query = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.Repository, "fix");

			Assert.False(RevisionSearchQuery.Equals(query, null));
		}

		[Fact]
		public void Equals_SameProperties_ReturnsTrue()
		{
			RevisionSearchQuery a = new RevisionSearchQuery(
				RevisionSearchType.Author, RevisionSearchScope.CurrentBranch, "alice");
			RevisionSearchQuery b = new RevisionSearchQuery(
				RevisionSearchType.Author, RevisionSearchScope.CurrentBranch, "alice");

			Assert.True(RevisionSearchQuery.Equals(a, b));
		}

		[Fact]
		public void Equals_DifferentType_ReturnsFalse()
		{
			RevisionSearchQuery a = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.Repository, "fix");
			RevisionSearchQuery b = new RevisionSearchQuery(
				RevisionSearchType.Author, RevisionSearchScope.Repository, "fix");

			Assert.False(RevisionSearchQuery.Equals(a, b));
		}

		[Fact]
		public void Equals_DifferentScope_ReturnsFalse()
		{
			RevisionSearchQuery a = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.Repository, "fix");
			RevisionSearchQuery b = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.CurrentBranch, "fix");

			Assert.False(RevisionSearchQuery.Equals(a, b));
		}

		[Fact]
		public void Equals_DifferentSearchString_ReturnsFalse()
		{
			RevisionSearchQuery a = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.Repository, "fix");
			RevisionSearchQuery b = new RevisionSearchQuery(
				RevisionSearchType.Message, RevisionSearchScope.Repository, "wip");

			Assert.False(RevisionSearchQuery.Equals(a, b));
		}
	}
}
