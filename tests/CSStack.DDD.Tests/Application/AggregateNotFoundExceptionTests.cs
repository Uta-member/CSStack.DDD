using CSStack.DDD.Application;

namespace CSStack.DDD.Tests
{
	public sealed class AggregateNotFoundExceptionTests
	{
		[Fact]
		public void KeepsAggregateTypeAndId()
		{
			var id = TestId.New();

			var exception = new AggregateNotFoundException(typeof(Order), id);

			Assert.Equal(typeof(Order), exception.AggregateType);
			Assert.Equal(id, exception.AggregateId);
			Assert.Null(exception.InnerException);
		}

		[Fact]
		public void MessageContainsAggregateTypeAndId()
		{
			var id = TestId.New();

			var exception = new AggregateNotFoundException(typeof(Order), id);

			Assert.Contains(nameof(Order), exception.Message);
			Assert.Contains(id.ToString(), exception.Message);
		}
	}
}
