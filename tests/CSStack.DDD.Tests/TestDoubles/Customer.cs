using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	// A non-sealed entity, which an ORM can derive a proxy class from.
	public class Customer(TestId id) : Entity<Customer, TestId>
	{
		public override TestId Id { get; } = id;
	}
}
