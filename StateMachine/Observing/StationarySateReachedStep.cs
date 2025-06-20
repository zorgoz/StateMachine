using System;

namespace zorgoz.StateMachine.Observing
{
	/// <summary>
	/// Machine reached stationary state
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class StationarySateReachedStep<TState, TEvent> : ITransitionStep<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		/// <summary>
		/// The actual state being reached
		/// </summary>
		public TState Target { get; set; }

		/// <summary>
		/// Event
		/// </summary>
		public TEvent On { get; set; }

		/// <summary>
		/// Initiating machine
		/// </summary>
		public StateMachine<TState, TEvent> Machine { get; set; }

		/// <summary>
		/// ToString implementation
		/// </summary>
		/// <returns></returns>
		public override string ToString() => $"{Machine?.QuotedName}: ({On})=>{Target}.";
	}
}
