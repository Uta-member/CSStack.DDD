using System.Reflection;
using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class RepositoryTests
	{
		[Theory]
		[InlineData(typeof(IRepository<,>))]
		[InlineData(typeof(IDeletableRepository<,>))]
		public void AggregateMustBeReferenceType(Type repositoryType)
		{
			var aggregate = repositoryType.GetGenericArguments()[0];

			Assert.True(aggregate.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint));
		}
	}
}
