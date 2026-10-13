using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class DomainEventTests
	{
		[Fact]
		public void NewEventsHaveDistinctNonEmptyIdentifiers()
		{
			var first = TestEvent.Create("same fact");
			var second = TestEvent.Create("same fact");

			Assert.NotEqual(Guid.Empty, first.EventId);
			Assert.NotEqual(Guid.Empty, second.EventId);
			Assert.NotEqual(first.EventId, second.EventId);
		}

		[Fact]
		public void OccurrenceTimeIsSuppliedByCaller()
		{
			var occurredAt = new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.FromHours(9));
			IDomainEvent domainEvent = new TestEvent(occurredAt, "imported fact");

			Assert.Equal(occurredAt, domainEvent.OccurredAt);
			Assert.Equal(occurredAt.Offset, domainEvent.OccurredAt.Offset);
			Assert.NotEqual(Guid.Empty, domainEvent.EventId);
		}

		[Fact]
		public void StoredIdentifierCanBeRestored()
		{
			var eventId = Guid.NewGuid();
			var domainEvent = new TestEvent(DateTimeOffset.UnixEpoch, "restored fact") { EventId = eventId };

			Assert.Equal(eventId, ((IDomainEvent)domainEvent).EventId);
		}

		[Fact]
		public void RecordCopyPreservesIdentifierAndOccurrenceTime()
		{
			var original = TestEvent.Create("original");

			var copy = original with { Name = "copy" };

			Assert.NotSame(original, copy);
			Assert.Equal(original.EventId, copy.EventId);
			Assert.Equal(original.OccurredAt, copy.OccurredAt);
			Assert.Equal("original", original.Name);
			Assert.Equal("copy", copy.Name);
		}
	}
}
