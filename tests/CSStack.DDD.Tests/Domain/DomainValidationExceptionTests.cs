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
		}

		[Fact]
		public void MessageIsNotEmpty()
		{
			var exception = new DomainValidationException([new Required()]);

			Assert.False(string.IsNullOrEmpty(exception.Message));
		}
	}
}
