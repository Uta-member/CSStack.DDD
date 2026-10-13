using CSStack.DDD.Application;

namespace CSStack.DDD.Tests
{
	public sealed class RepositoryExtensionsTests
	{
		[Fact]
		public async Task GetAsyncReturnsAggregateIfFound()
		{
			IOrderRepository repository = new InMemoryOrderRepository();
			var order = new Order(TestId.New());
			repository.Save(order);

			// The type arguments are inferred from the repository interface of the aggregate.
			var found = await repository.GetAsync(order.Id, TestContext.Current.CancellationToken);

			Assert.Same(order, found);
		}

		[Fact]
		public async Task GetAsyncThrowsIfNotFound()
		{
			IOrderRepository repository = new InMemoryOrderRepository();
			var id = TestId.New();

			var exception = await Assert.ThrowsAsync<AggregateNotFoundException>(
				() => repository.GetAsync(id, TestContext.Current.CancellationToken));

			Assert.Equal(typeof(Order), exception.AggregateType);
			Assert.Equal(id, exception.AggregateId);
		}
	}
}
