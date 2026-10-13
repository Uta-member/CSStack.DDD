using CSStack.DDD.Application;

namespace CSStack.DDD.Tests
{
	public sealed class ConcurrencyConflictExceptionTests
	{
		[Fact]
		public void KeepsAggregateTypeAndId()
		{
			var id = TestId.New();

			var exception = new ConcurrencyConflictException(typeof(Order), id);

			Assert.Equal(typeof(Order), exception.AggregateType);
			Assert.Equal(id, exception.AggregateId);
			Assert.Null(exception.InnerException);
		}

		[Fact]
		public void KeepsInnerException()
		{
			var inner = new InvalidOperationException();

			var exception = new ConcurrencyConflictException(typeof(Order), TestId.New(), inner);

			Assert.Same(inner, exception.InnerException);
		}

		[Fact]
		public void MessageContainsAggregateTypeAndId()
		{
			var id = TestId.New();

			var exception = new ConcurrencyConflictException(typeof(Order), id);

			Assert.Contains(nameof(Order), exception.Message);
			Assert.Contains(id.ToString(), exception.Message);
		}
	}
}
