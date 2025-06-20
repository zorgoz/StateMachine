namespace zorgoz.StateMachine
{
	/// <summary>
	/// Implements an event that equals to any event excluding null. 
	/// As it has maximum order, it will match last.
	/// This event can be as an "OnElse()"
	/// </summary>
	public sealed class AnyEvent : EventBase
    {
		internal AnyEvent()
		{
			Order = int.MaxValue;
		}

		/// <summary>
		/// Instance of AnyEvent
		/// </summary>
		public static AnyEvent Any = new AnyEvent();

		/// <summary>
		/// Will match any non-null event
		/// </summary>
		/// <param name="other"></param>
		/// <returns>True if other is not null</returns>
		public override bool Equals(EventBase other) => other != null;

		/// <summary>
		/// As this is called only if exact type matches, it will always return true
		/// </summary>
		/// <param name="other"></param>
		/// <returns>True</returns>
		public override bool Equivalent(EventBase other) => true;

		/// <summary>
		/// Simply an asterix
		/// </summary>
		/// <returns>An asterix</returns>
		public override string ToString() => "*";
	}
}
