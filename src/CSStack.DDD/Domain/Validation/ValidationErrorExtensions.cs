namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Provides extension methods for composing and enforcing validation errors.
	/// </summary>
	public static class ValidationErrorExtensions
	{
		/// <summary>
		/// Prefixes the path of each error with the specified member name.
		/// </summary>
		/// <remarks>
		/// Each level adds only the name of its own member, so a child does not need to know where it is used.
		/// For example, an error with the path <c>PostalCode</c> placed under <c>Address</c> gets the path <c>Address.PostalCode</c>.
		/// For collections, include the index in the name, such as <c>Lines[2]</c>.
		/// </remarks>
		/// <param name="errors">The errors reported by a child member.</param>
		/// <param name="name">The name of the member as seen from the parent.</param>
		/// <returns>The errors with the prefixed path.</returns>
		public static IEnumerable<ValidationError> Under(this IEnumerable<ValidationError> errors, string name)
		{
			return errors.Select(e => e with
			{
				Path = e.Path.Length == 0 ? name : $"{name}.{e.Path}",
			});
		}

		/// <summary>
		/// Throws a <see cref="DomainValidationException"/> containing all errors if there is at least one error.
		/// </summary>
		/// <param name="errors">The errors to check.</param>
		/// <exception cref="DomainValidationException">There is at least one error.</exception>
		public static void ThrowIfInvalid(this IEnumerable<ValidationError> errors)
		{
			IReadOnlyList<ValidationError> errorList = [.. errors];
			if(errorList.Count > 0)
			{
				throw new DomainValidationException(errorList);
			}
		}
	}
}
