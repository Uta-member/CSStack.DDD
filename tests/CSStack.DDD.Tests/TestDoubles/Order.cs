using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class Order(TestId id) : AggregateRoot<Order, TestId>
	{
		public override TestId Id { get; } = id;

		public void Raise(IDomainEvent domainEvent)
		{
			AddDomainEvent(domainEvent);
		}
	}
}
