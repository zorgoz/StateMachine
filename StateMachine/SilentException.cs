using System;

namespace zorgoz.StateMachine
{
	/// <summary>
	/// Such exceptions are swallowed by the machine and not emited on the exception observable. They are treated as exception in all other aspects.
	/// </summary>
	public class SilentException: Exception
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public SilentException() : base()
		{
		}

		protected SilentException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}

		public SilentException(string message) : base(message)
		{
		}

		public SilentException(string message, Exception innerException) : base(message, innerException)
		{
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}
}
