using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class DictionaryExtensionsTests
	{
		[Fact]
		public void Map_TransformsEntriesToStrings()
		{
			Dictionary<string, int> source = new Dictionary<string, int>
			{
				{ "a", 1 },
				{ "b", 2 },
			};

			string[] result = source.Map(kv => $"{kv.Key}:{kv.Value}");

			Assert.Equal(new[] { "a:1", "b:2" }, result);
		}

		[Fact]
		public void Map_EmptyDictionary_ReturnsEmptyArray()
		{
			Dictionary<string, int> source = new Dictionary<string, int>();

			string[] result = source.Map(kv => $"{kv.Key}:{kv.Value}");

			Assert.Empty(result);
		}

		[Fact]
		public void Map_PreservesCount()
		{
			Dictionary<string, int> source = new Dictionary<string, int>
			{
				{ "x", 10 },
				{ "y", 20 },
				{ "z", 30 },
			};

			int[] result = source.Map(kv => kv.Value);

			Assert.Equal(3, result.Length);
			Assert.Equal(new[] { 10, 20, 30 }, result);
		}
	}
}
