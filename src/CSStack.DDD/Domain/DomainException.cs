namespace CSStack.DDD.Domain
{
	/// <summary>
	/// The base class for exceptions that are thrown when the domain rejects an operation.
	/// </summary>
	/// <remarks>
	/// Derive the exceptions for business rules that are not about the input (for example, "the requester cannot approve their own request") from this class.
	/// <see cref="DomainValidationException"/> also derives from it,
	/// so that the upper layers can handle every operation rejected by the domain with a single type (for example, by returning HTTP 400 or 422).
	/// </remarks>
	public abstract class DomainException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DomainException"/> class with the specified message.
		/// </summary>
		/// <param name="message">The message that describes why the operation was rejected.</param>
		protected DomainException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DomainException"/> class with the specified message and inner exception.
		/// </summary>
		/// <param name="message">The message that describes why the operation was rejected.</param>
		/// <param name="innerException">The exception that caused this exception, if any.</param>
		protected DomainException(string message, Exception? innerException) : base(message, innerException)
		{
		}
	}
}
