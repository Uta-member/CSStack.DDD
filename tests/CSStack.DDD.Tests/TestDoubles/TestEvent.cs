using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed record TestEvent(DateTimeOffset OccurredAt, string Name) : DomainEvent(OccurredAt)
	{
		public static TestEvent Create(string name)
		{
			return new TestEvent(DateTimeOffset.UnixEpoch, name);
		}
	}
}
