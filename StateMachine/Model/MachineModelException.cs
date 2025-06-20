using System;

namespace zorgoz.StateMachine.Model
{
	/// <summary>
	/// Exception thrown is machine model is inconsistent
	/// </summary>
	public class MachineModelException: Exception
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public MachineModelException() : base()
		{
		}

		protected MachineModelException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}

		public MachineModelException(string message) : base(message)
		{
		}

		public MachineModelException(string message, Exception innerException) : base(message, innerException)
		{
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}
}
