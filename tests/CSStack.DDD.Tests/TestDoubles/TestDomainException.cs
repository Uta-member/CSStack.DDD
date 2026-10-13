using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class TestDomainException : DomainException
	{
		public TestDomainException(string message) : base(message)
		{
		}

		public TestDomainException(string message, Exception? innerException) : base(message, innerException)
		{
		}
	}
}
