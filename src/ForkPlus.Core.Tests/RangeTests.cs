using Xunit;

namespace ForkPlus.Tests
{
	public class RangeTests
	{
		[Fact]
		public void Zero_HasZeroStartEndLengthAndIsEmpty()
		{
			Assert.Equal(0, ForkPlus.Range.Zero.Start);
			Assert.Equal(0, ForkPlus.Range.Zero.End);
			Assert.Equal(0, ForkPlus.Range.Zero.Length);
			Assert.True(ForkPlus.Range.Zero.IsEmpty);
		}

		[Fact]
		public void Constructor_SetsStartEndAndDerivedLength()
		{
			var range = new ForkPlus.Range(2, 5);

			Assert.Equal(2, range.Start);
			Assert.Equal(5, range.End);
			Assert.Equal(3, range.Length);
			Assert.False(range.IsEmpty);
		}

		[Fact]
		public void Constructor_StartEqualsEnd_IsEmptyWithZeroLength()
		{
			var range = new ForkPlus.Range(3, 3);

			Assert.True(range.IsEmpty);
			Assert.Equal(0, range.Length);
		}
	}
}
