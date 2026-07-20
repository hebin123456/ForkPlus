using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class RangeExtensionsTests
	{
		[Fact]
		public void Contains_IsInclusiveAtStartAndExclusiveAtEnd()
		{
			var range = new ForkPlus.Range(2, 5);

			Assert.False(range.Contains(1));
			Assert.True(range.Contains(2));
			Assert.True(range.Contains(4));
			Assert.False(range.Contains(5));
		}

		[Theory]
		[InlineData(0, 3, 2, 5, true)]
		[InlineData(0, 3, 3, 5, false)]
		[InlineData(3, 5, 0, 3, false)]
		[InlineData(1, 4, 2, 3, true)]
		public void Overlaps_UsesHalfOpenRanges(int start, int end, int otherStart, int otherEnd, bool expected)
		{
			Assert.Equal(expected, new ForkPlus.Range(start, end).Overlaps(new ForkPlus.Range(otherStart, otherEnd)));
		}

		[Fact]
		public void Map_ProjectsEachValueInRange()
		{
			string[] result = new ForkPlus.Range(3, 6).Map((int value) => "v" + value);

			Assert.Equal(new[] { "v3", "v4", "v5" }, result);
		}

		[Fact]
		public void Merge_SplitsFullRangeByOverlappingRanges()
		{
			var emitted = new List<ForkPlus.Range>();
			var fullRange = new ForkPlus.Range(0, 10);
			var ranges = new[]
			{
				new List<ForkPlus.Range> { new ForkPlus.Range(2, 5) },
				new List<ForkPlus.Range> { new ForkPlus.Range(4, 8) }
			};

			fullRange.Merge(ranges, (range, _, _, _) => emitted.Add(range));

			Assert.Equal(new[]
			{
				new ForkPlus.Range(0, 2),
				new ForkPlus.Range(2, 4),
				new ForkPlus.Range(4, 5),
				new ForkPlus.Range(5, 8),
				new ForkPlus.Range(8, 10)
			}, emitted);
		}
	}
}
