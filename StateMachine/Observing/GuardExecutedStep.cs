using System;

namespace zorgoz.StateMachine.Observing
{
	/// <summary>
	/// Contains information abot the guard executed
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class GuardExecutedStep<TState, TEvent> : ITransitionStep<TState, TEvent>
		where TState: Enum
		where TEvent: EventBase
	{
		/// <summary>
		/// The actual state being visited
		/// </summary>
		public TState Target { get; set; }

		/// <summary>
		/// Event
		/// </summary>
		public TEvent On { get; set; }

		/// <summary>
		/// Result of the guard
		/// </summary>
		public bool Result { get; set; }

		/// <summary>
		/// Guard target state
		/// </summary>
		public TState GuardTarget { get; set; }

		/// <summary>
		/// Initiating machine
		/// </summary>
		public StateMachine<TState, TEvent> Machine { get; set; }

		/// <summary>
		/// ToString implementation
		/// </summary>
		/// <returns></returns>
		public override string ToString() => $"{Machine?.QuotedName}: G({Target}({On})->{GuardTarget})={Result}";
	}
}
