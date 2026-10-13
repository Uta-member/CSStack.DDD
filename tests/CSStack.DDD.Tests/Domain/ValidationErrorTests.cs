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
			Assert.Equal(new OutOfRange<int>(1, 10, 11), new OutOfRange<int>(1, 10, 11));
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
		}

		[Fact]
		public void ErrorsOfDifferentTypesAreNotEqual()
		{
			Assert.NotEqual<ValidationError>(new Required(), new InvalidFormat());
		}
	}
}
