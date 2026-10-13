namespace CSStack.DDD.Tests
{
	public sealed class InMemoryOrderRepository : IOrderRepository
	{
		private readonly Dictionary<TestId, Order> _orders = [];

		public Task<Order?> FindAsync(TestId id, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_orders.TryGetValue(id, out var order) ? order : null);
		}

		public void Save(Order aggregate)
		{
			_orders[aggregate.Id] = aggregate;
		}
	}
}
