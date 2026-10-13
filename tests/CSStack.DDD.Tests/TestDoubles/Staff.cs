using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	// Another entity type that shares the identifier type with Member.
	public sealed class Staff(TestId id) : Entity<Staff, TestId>
	{
		public override TestId Id { get; } = id;
	}
}
