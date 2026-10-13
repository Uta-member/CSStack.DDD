namespace CSStack.DDD.Tests
{
	public sealed class EntityTests
	{
		[Fact]
		public void EntitiesWithSameIdAreEqualEvenIfOtherAttributesDiffer()
		{
			var id = TestId.New();
			var a = new Member(id, "Alice");
			var b = new Member(id, "Bob");

			Assert.True(a.Equals(b));
			Assert.True(a.Equals((object)b));
			Assert.True(a == b);
			Assert.False(a != b);
		}

		[Fact]
		public void EntitiesWithDifferentIdsAreNotEqual()
		{
			var a = new Member(TestId.New(), "Alice");
			var b = new Member(TestId.New(), "Alice");

			Assert.False(a.Equals(b));
			Assert.False(a.Equals((object)b));
			Assert.False(a == b);
			Assert.True(a != b);
		}

		[Fact]
		public void EntityIsEqualToItself()
		{
			var a = new Member(TestId.New(), "Alice");

			Assert.True(a.Equals(a));
#pragma warning disable CS1718 // Comparison made to same variable
			Assert.True(a == a);
#pragma warning restore CS1718
		}

		[Fact]
		public void EntitiesOfDifferentTypesWithSameIdAreNotEqual()
		{
			var id = TestId.New();
			var member = new Member(id, "Alice");
			var staff = new Staff(id);

			Assert.False(member.Equals((object)staff));
			Assert.False(staff.Equals((object)member));
		}

		[Fact]
		public void EntityIsNotEqualToNull()
		{
			var a = new Member(TestId.New(), "Alice");

			Assert.False(a.Equals(null));
			Assert.False(a.Equals((object?)null));
			Assert.False(a == null);
			Assert.False(null == a);
			Assert.True(a != null);
		}

		[Fact]
		public void NullEntitiesAreEqual()
		{
			Member? a = null;
			Member? b = null;

			Assert.True(a == b);
			Assert.False(a != b);
		}

		[Fact]
		public void ProxyOfEntityIsEqualToEntityWithSameId()
		{
			var id = TestId.New();
			var entity = new Customer(id);
			var proxy = new CustomerProxy(id);

			Assert.True(entity.Equals(proxy));
			Assert.True(proxy.Equals(entity));
			Assert.True(entity.Equals((object)proxy));
			Assert.True(proxy.Equals((object)entity));
			Assert.True(entity == proxy);
		}

		[Fact]
		public void HashCodeIsDerivedFromId()
		{
			var id = TestId.New();
			var a = new Member(id, "Alice");
			var b = new Member(id, "Bob");

			Assert.Equal(id.GetHashCode(), a.GetHashCode());
			Assert.Equal(a.GetHashCode(), b.GetHashCode());
		}

		[Fact]
		public void HashSetTreatsEntitiesWithSameIdAsOne()
		{
			var id = TestId.New();
			HashSet<Member> set =
			[
				new Member(id, "Alice"),
				new Member(id, "Bob"),
				new Member(TestId.New(), "Carol"),
			];

			Assert.Equal(2, set.Count);
		}
	}
}
