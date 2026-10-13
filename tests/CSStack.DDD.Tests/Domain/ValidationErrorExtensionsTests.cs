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
				new BelowMinimum(1, 0),
			];

			var result = errors.Under("Name");

			Assert.Equal(
				[
					new TooLong(20, 25) { Path = "Name" },
					new Required() { Path = "Name.First" },
					new BelowMinimum(1, 0) { Path = "Name" },
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

		[Theory]
		[InlineData(0, "Lines[0]")]
		[InlineData(2, "Lines[2]")]
		[InlineData(int.MaxValue, "Lines[2147483647]")]
		public void UnderWithIndexSetsPathOfErrorOnElementItself(int index, string path)
		{
			ValidationError[] errors = [new Required()];

			Assert.Equal([new Required() { Path = path }], errors.Under("Lines", index));
		}

		[Fact]
		public void UnderWithIndexComposesNestedCollectionPaths()
		{
			ValidationError[] errors = [new BelowMinimum(1, 0) { Path = "Quantity" }];

			var result = errors.Under("Lines", 2).Under("Orders", 1).Under("Customer");

			Assert.Equal([new BelowMinimum(1, 0) { Path = "Customer.Orders[1].Lines[2].Quantity" }], result);
			Assert.Equal("Quantity", errors[0].Path);
		}

		[Fact]
		public void UnderWithIndexKeepsTypeValuesAndOrderOfErrors()
		{
			ValidationError[] errors = [new TooLong(20, 25), new Required() { Path = "Product" }];

			Assert.Equal(
				[new TooLong(20, 25) { Path = "Lines[2]" }, new Required() { Path = "Lines[2].Product" }],
				errors.Under("Lines", 2));
		}

		[Fact]
		public void UnderWithIndexReturnsEmptyForNoErrors()
		{
			Assert.Empty(Array.Empty<ValidationError>().Under("Lines", 0));
		}

		[Theory]
		[InlineData(-1)]
		[InlineData(int.MinValue)]
		public void UnderWithIndexRejectsNegativeIndexImmediately(int index)
		{
			var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Array.Empty<ValidationError>().Under("Lines", index));

			Assert.Equal("index", exception.ParamName);
			Assert.Equal(index, exception.ActualValue);
		}

		[Fact]
		public void UnderWithIndexDefersEnumeration()
		{
			var count = 0;
			IEnumerable<ValidationError> Errors()
			{
				count++;
				yield return new Required();
			}

			var result = Errors().Under("Lines", 2);

			Assert.Equal(0, count);
			Assert.Equal([new Required() { Path = "Lines[2]" }], result);
			Assert.Equal(1, count);
		}

		[Fact]
		public void UnderWithIndexCombinesElementErrorsUsingTheirOriginalPositions()
		{
			ValidationError[][] elementErrors =
			[
				[new Required()],
				[],
				[new BelowMinimum(1, 0) { Path = "Quantity" }, new InvalidFormat() { Path = "Product" }],
			];

			var result = elementErrors.SelectMany((errors, i) => errors.Under("Lines", i));
			var exception = Assert.Throws<DomainValidationException>(() => result.ThrowIfInvalid());

			Assert.Equal(
				[
					new Required() { Path = "Lines[0]" },
					new BelowMinimum(1, 0) { Path = "Lines[2].Quantity" },
					new InvalidFormat() { Path = "Lines[2].Product" },
				],
				exception.Errors);
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
