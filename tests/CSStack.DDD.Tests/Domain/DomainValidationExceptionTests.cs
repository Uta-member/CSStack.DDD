using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class DomainValidationExceptionTests
	{
		[Fact]
		public void ErrorsAreKeptInOrder()
		{
			ValidationError[] errors =
			[
				new Required() { Path = "Name" },
				new TooLong(20, 25) { Path = "Name" },
			];

			var exception = new DomainValidationException(errors);

			Assert.Equal(errors, exception.Errors);
		}

		[Fact]
		public void ErrorsAreNotAffectedByChangesToSource()
		{
			List<ValidationError> errors = [new Required()];

			var exception = new DomainValidationException(errors);
			errors.Add(new InvalidFormat());

			Assert.Equal([new Required()], exception.Errors);
			Assert.Equal("One or more validation errors occurred: Required", exception.Message);
		}

		[Fact]
		public void MessageIncludesPathsAndTypeNamesInOrder()
		{
			var exception = new DomainValidationException(
			[
				new TooLong(20, 25) { Path = "Name" },
				new InvalidFormat() { Path = "Email" },
				new Required() { Path = "Name" },
			]);

			Assert.Equal("One or more validation errors occurred: Name (TooLong), Email (InvalidFormat), Name (Required)", exception.Message);
		}

		[Fact]
		public void MessageIncludesNestedAndIndexedPaths()
		{
			var exception = new DomainValidationException(
			[
				new InvalidFormat() { Path = "Address.PostalCode" },
				new BelowMinimum(1, 0) { Path = "Lines[2].Quantity" },
			]);

			Assert.Equal("One or more validation errors occurred: Address.PostalCode (InvalidFormat), Lines[2].Quantity (BelowMinimum)", exception.Message);
		}

		[Fact]
		public void MessageIncludesOnlyTypeNameForEmptyPath()
		{
			var exception = new DomainValidationException([new Required()]);

			Assert.Equal("One or more validation errors occurred: Required", exception.Message);
		}

		[Fact]
		public void MessageIncludesCustomErrorTypeWithoutPropertyValues()
		{
			var exception = new DomainValidationException([new CustomError("private value") { Path = "Name" }]);

			Assert.Equal("One or more validation errors occurred: Name (CustomError)", exception.Message);
		}

		[Fact]
		public void MessageKeepsOriginalSummaryForNoErrors()
		{
			var exception = new DomainValidationException([]);

			Assert.Equal("One or more validation errors occurred.", exception.Message);
			Assert.Empty(exception.Errors);
		}

		[Theory]
		[InlineData(10, "")]
		[InlineData(11, ", ... (1 more)")]
		[InlineData(100, ", ... (90 more)")]
		public void MessageShowsAtMostTenErrorsAndKeepsAllErrors(int count, string suffix)
		{
			var errors = Enumerable.Range(0, count)
				.Select(i => new Required() { Path = $"Lines[{i}]" }).ToArray();

			var exception = new DomainValidationException(errors);

			Assert.Equal("One or more validation errors occurred: " +
				"Lines[0] (Required), Lines[1] (Required), Lines[2] (Required), Lines[3] (Required), Lines[4] (Required), " +
				"Lines[5] (Required), Lines[6] (Required), Lines[7] (Required), Lines[8] (Required), Lines[9] (Required)" + suffix,
				exception.Message);
			Assert.Equal(errors, exception.Errors);
		}

		[Fact]
		public void ErrorsAreEnumeratedOnceForMessageAndErrors()
		{
			var count = 0;
			IEnumerable<ValidationError> Errors()
			{
				count++;
				if(count > 1)
				{
					throw new InvalidOperationException("The errors can only be enumerated once.");
				}
				yield return new Required() { Path = "Name" };
			}

			var exception = new DomainValidationException(Errors());

			Assert.Equal(1, count);
			Assert.Equal([new Required() { Path = "Name" }], exception.Errors);
			Assert.Equal("One or more validation errors occurred: Name (Required)", exception.Message);
		}

		private sealed record CustomError(string Value) : ValidationError;
	}
}
