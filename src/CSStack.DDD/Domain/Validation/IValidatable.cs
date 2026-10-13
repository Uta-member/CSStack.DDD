namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents an object that can diagnose whether its current state satisfies its rules.
	/// </summary>
	/// <remarks>
	/// <see cref="Validate"/> only reports problems and does not throw.
	/// Enforcing the rules is the responsibility of the operations of the entity (the gate),
	/// which typically call <see cref="ValidationErrorExtensions.ThrowIfInvalid"/> on the result.
	/// </remarks>
	public interface IValidatable
	{
		/// <summary>
		/// Diagnoses the current state and returns all rule violations.
		/// </summary>
		/// <remarks>
		/// Errors on the object itself have an empty <see cref="ValidationError.Path"/>.
		/// Errors of child members should be prefixed with the member name using <see cref="ValidationErrorExtensions.Under"/>.
		/// </remarks>
		/// <returns>The rule violations, or an empty list if the state is valid.</returns>
		IReadOnlyList<ValidationError> Validate();
	}
}
