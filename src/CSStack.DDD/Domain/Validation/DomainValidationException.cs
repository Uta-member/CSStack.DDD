namespace CSStack.DDD.Domain
{
	/// <summary>
	/// The exception that is thrown when an operation of the domain is rejected because of validation errors.
	/// </summary>
	/// <remarks>
	/// The message lists the paths and type names of the first ten errors in order, followed by the number of remaining errors.
	/// Errors with an empty path are listed by type name alone. Error property values are not included.
	/// All errors are retained in <see cref="Errors"/>. Messages for users should be composed by the presentation layer.
	/// </remarks>
	public sealed class DomainValidationException : DomainException
	{
		/// <summary>
		/// Gets the validation errors that caused the operation to be rejected.
		/// </summary>
		public IReadOnlyList<ValidationError> Errors { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="DomainValidationException"/> class with the specified errors.
		/// </summary>
		/// <param name="errors">The validation errors that caused the operation to be rejected.</param>
		public DomainValidationException(IEnumerable<ValidationError> errors) : this(errors.ToList())
		{
		}

		private DomainValidationException(List<ValidationError> errors) : base(CreateMessage(errors))
		{
			Errors = errors;
		}

		private static string CreateMessage(IReadOnlyList<ValidationError> errors)
		{
			if(errors.Count == 0)
			{
				return "One or more validation errors occurred.";
			}

			const int maxErrorsInMessage = 10;
			var details = string.Join(", ", errors.Take(maxErrorsInMessage).Select(error =>
				string.IsNullOrEmpty(error.Path) ? error.GetType().Name : $"{error.Path} ({error.GetType().Name})"));
			if(errors.Count > maxErrorsInMessage)
			{
				details += $", ... ({errors.Count - maxErrorsInMessage} more)";
			}

			return $"One or more validation errors occurred: {details}";
		}
	}
}
