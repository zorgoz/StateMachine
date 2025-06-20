using System;

namespace zorgoz.StateMachine
{
	/// <summary>
	/// Exception thrown when machine fails after started 
	/// </summary>
	public class MachineRuntimeException : Exception
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public MachineRuntimeException() : base()
		{
		}

		protected MachineRuntimeException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}

		public MachineRuntimeException(string message) : base(message)
		{
		}

		public MachineRuntimeException(string message, Exception innerException) : base(message, innerException)
		{
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}
}
