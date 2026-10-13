using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class DomainExceptionTests
	{
		[Fact]
		public void MessageIsKept()
		{
			var exception = new TestDomainException("The requester cannot approve their own request.");

			Assert.Equal("The requester cannot approve their own request.", exception.Message);
			Assert.Null(exception.InnerException);
		}

		[Fact]
		public void InnerExceptionIsKept()
		{
			var inner = new InvalidOperationException();

			var exception = new TestDomainException("Rejected.", inner);

			Assert.Same(inner, exception.InnerException);
		}

		[Fact]
		public void DomainValidationExceptionIsDomainException()
		{
			Exception exception = new DomainValidationException([new Required()]);

			Assert.IsAssignableFrom<DomainException>(exception);
		}
	}
}
