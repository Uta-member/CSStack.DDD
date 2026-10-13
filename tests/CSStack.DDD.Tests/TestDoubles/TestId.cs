using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed record TestId(Guid Value) : ISingleValueObject<TestId, Guid>
	{
		public static TestId New()
		{
			return new TestId(Guid.NewGuid());
		}
	}
}
