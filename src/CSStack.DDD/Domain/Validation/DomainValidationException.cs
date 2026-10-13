namespace CSStack.DDD.Domain
{
	/// <summary>
	/// The exception that is thrown when an operation of the domain is rejected because of validation errors.
	/// </summary>
	public sealed class DomainValidationException : Exception
	{
		/// <summary>
		/// Gets the validation errors that caused the operation to be rejected.
		/// </summary>
		public IReadOnlyList<ValidationError> Errors { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="DomainValidationException"/> class with the specified errors.
		/// </summary>
		/// <param name="errors">The validation errors that caused the operation to be rejected.</param>
		public DomainValidationException(IEnumerable<ValidationError> errors) : base("One or more validation errors occurred.")
		{
			Errors = errors.ToList();
		}
	}
}
