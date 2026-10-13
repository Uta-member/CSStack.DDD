using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class ValidationErrorExtensionsTests
	{
		[Fact]
		public void UnderSetsNameAsPathOfErrorOnObjectItself()
		{
			ValidationError[] errors = [new Required()];

			var result = errors.Under("Name");

			Assert.Equal([new Required() { Path = "Name" }], result);
		}

		[Fact]
		public void UnderPrefixesExistingPathWithName()
		{
			ValidationError[] errors = [new InvalidFormat() { Path = "PostalCode" }];

			var result = errors.Under("Address");

			Assert.Equal([new InvalidFormat() { Path = "Address.PostalCode" }], result);
		}

		[Fact]
		public void UnderComposesPathsAcrossLevels()
		{
			ValidationError[] errors = [new TooLong(3, 4)];

			var result = errors.Under("Quantity").Under("Lines[2]");

			Assert.Equal([new TooLong(3, 4) { Path = "Lines[2].Quantity" }], result);
		}

		[Fact]
		public void UnderKeepsTypeValuesAndOrderOfErrors()
		{
			ValidationError[] errors =
			[
				new TooLong(20, 25),
				new Required() { Path = "First" },
				new OutOfRange<int>(1, 10, 0),
			];

			var result = errors.Under("Name");

			Assert.Equal(
				[
					new TooLong(20, 25) { Path = "Name" },
					new Required() { Path = "Name.First" },
					new OutOfRange<int>(1, 10, 0) { Path = "Name" },
				],
				result);
		}

		[Fact]
		public void UnderDoesNotModifyOriginalErrors()
		{
			ValidationError[] errors = [new Required()];

			_ = errors.Under("Name").ToList();

			Assert.Equal("", errors[0].Path);
		}

		[Fact]
		public void UnderReturnsEmptyForNoErrors()
		{
			Assert.Empty(Array.Empty<ValidationError>().Under("Name"));
		}

		[Fact]
		public void ThrowIfInvalidDoesNotThrowForNoErrors()
		{
			Array.Empty<ValidationError>().ThrowIfInvalid();
		}

		[Fact]
		public void ThrowIfInvalidThrowsWithAllErrors()
		{
			ValidationError[] errors =
			[
				new Required() { Path = "Name" },
				new InvalidFormat() { Path = "Email" },
			];

			var exception = Assert.Throws<DomainValidationException>(() => errors.ThrowIfInvalid());

			Assert.Equal(errors, exception.Errors);
		}

		[Fact]
		public void ThrowIfInvalidEnumeratesErrorsOnce()
		{
			var count = 0;
			IEnumerable<ValidationError> Errors()
			{
				count++;
				yield return new Required();
			}

			var exception = Assert.Throws<DomainValidationException>(() => Errors().ThrowIfInvalid());

			Assert.Equal(1, count);
			Assert.Single(exception.Errors);
		}
	}
}
