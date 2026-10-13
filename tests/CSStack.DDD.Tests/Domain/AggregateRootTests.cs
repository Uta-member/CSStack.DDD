using System.Reflection;
using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class AggregateRootTests
	{
		[Fact]
		public void NewAggregateHasNoDomainEvents()
		{
			var order = new Order(TestId.New());

			Assert.Empty(order.DomainEvents);
			Assert.Empty(((IDomainEventSource)order).PullDomainEvents());
		}

		[Fact]
		public void DomainEventsAreRecordedInOrder()
		{
			var order = new Order(TestId.New());
			var first = TestEvent.Create("first");
			var second = TestEvent.Create("second");

			order.Raise(first);
			order.Raise(second);

			Assert.Equal([first, second], order.DomainEvents);
		}

		[Fact]
		public void DomainEventsCannotBeModifiedFromOutside()
		{
			var order = new Order(TestId.New());
			order.Raise(TestEvent.Create("first"));

			var events = Assert.IsAssignableFrom<ICollection<IDomainEvent>>(order.DomainEvents);
			Assert.True(events.IsReadOnly);
		}

		[Fact]
		public void PullDomainEventsReturnsRecordedEventsInOrder()
		{
			IDomainEventSource order = new Order(TestId.New());
			var first = TestEvent.Create("first");
			var second = TestEvent.Create("second");
			((Order)order).Raise(first);
			((Order)order).Raise(second);

			var pulled = order.PullDomainEvents();

			Assert.Equal([first, second], pulled);
		}

		[Fact]
		public void PullDomainEventsClearsRecordedEvents()
		{
			var order = new Order(TestId.New());
			order.Raise(TestEvent.Create("first"));

			((IDomainEventSource)order).PullDomainEvents();

			Assert.Empty(order.DomainEvents);
			Assert.Empty(((IDomainEventSource)order).PullDomainEvents());
		}

		[Fact]
		public void PulledEventsAreNotAffectedByEventsRecordedLater()
		{
			var order = new Order(TestId.New());
			var first = TestEvent.Create("first");
			var second = TestEvent.Create("second");
			order.Raise(first);

			var pulled = ((IDomainEventSource)order).PullDomainEvents();
			order.Raise(second);

			Assert.Equal([first], pulled);
			Assert.Equal([second], order.DomainEvents);
		}

		[Fact]
		public void PullDomainEventsIsNotPublicOnAggregate()
		{
			var method = typeof(Order).GetMethod(nameof(IDomainEventSource.PullDomainEvents), BindingFlags.Public | BindingFlags.Instance);

			Assert.Null(method);
		}
	}
}
