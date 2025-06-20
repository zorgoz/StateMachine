using System;

namespace zorgoz.StateMachine
{
	/// <summary>
	/// Kind of action to suppress
	/// </summary>
	[Flags]
    public enum SuppressActions
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		None = 0,
		Exit,
		Entry,
		All = Entry | Exit
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}
}
