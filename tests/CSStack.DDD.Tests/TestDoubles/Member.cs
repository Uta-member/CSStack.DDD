using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class Member(TestId id, string name) : Entity<Member, TestId>
	{
		public override TestId Id { get; } = id;

		public string Name { get; } = name;
	}
}
