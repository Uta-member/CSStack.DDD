using System.Reflection;
using CSStack.DDD.Domain;

namespace CSStack.DDD.Tests
{
	public sealed class AssemblyTests
	{
		private static readonly AssemblyName Name = typeof(IEntity<,>).Assembly.GetName();

		[Fact]
		public void IsStrongNamedWithPublishedKey()
		{
			// Changing the key changes the assembly identity, which breaks every assembly that references this one.
			Assert.Equal("9f050175924426b8", Convert.ToHexString(Name.GetPublicKeyToken()!).ToLowerInvariant());
		}

		[Fact]
		public void AssemblyVersionHasOnlyMajorVersion()
		{
			var version = Name.Version!;

			Assert.Equal((0, 0, 0), (version.Minor, version.Build, version.Revision));
		}
	}
}
