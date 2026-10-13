using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class ValidationErrorTests
	{
		[Fact]
		public void PathIsEmptyByDefault()
		{
			Assert.Equal("", new Required().Path);
		}

		[Fact]
		public void ErrorsWithSameTypeValuesAndPathAreEqual()
		{
			Assert.Equal(new TooLong(20, 25) { Path = "Name" }, new TooLong(20, 25) { Path = "Name" });
			Assert.Equal(new BelowMinimum(1, 0), new BelowMinimum(1, 0));
			Assert.Equal(new AboveMaximum(10, 11), new AboveMaximum(10, 11));
		}

		[Fact]
		public void ErrorsWithDifferentPathsAreNotEqual()
		{
			Assert.NotEqual(new Required() { Path = "Name" }, new Required() { Path = "Email" });
		}

		[Fact]
		public void ErrorsWithDifferentValuesAreNotEqual()
		{
			Assert.NotEqual(new TooLong(20, 25), new TooLong(20, 26));
			Assert.NotEqual(new TooShort(3, 1), new TooShort(4, 1));
			Assert.NotEqual(new BelowMinimum(1, 0), new BelowMinimum(1, -1));
			Assert.NotEqual(new AboveMaximum(10, 11), new AboveMaximum(9, 11));
		}

		[Fact]
		public void ErrorsOfDifferentTypesAreNotEqual()
		{
			Assert.NotEqual<ValidationError>(new Required(), new InvalidFormat());
			Assert.NotEqual<ValidationError>(new BelowMinimum(1, 1), new AboveMaximum(1, 1));
		}
	}
}
