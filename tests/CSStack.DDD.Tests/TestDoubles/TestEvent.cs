using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed record TestEvent(Guid EventId, DateTimeOffset OccurredAt, string Name) : IDomainEvent
	{
		public static TestEvent Create(string name)
		{
			return new TestEvent(Guid.NewGuid(), DateTimeOffset.UnixEpoch, name);
		}
	}
}
