using System;

namespace zorgoz.StateMachine.Observing
{
	/// <summary>
	/// Common base for trnasition steps
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public interface ITransitionStep<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		/// <summary>
		/// Initiating machine
		/// </summary>
		StateMachine<TState, TEvent> Machine { get; set; }

		/// <summary>
		/// The actual state being visited
		/// </summary>
		TState Target { get; set; }

		/// <summary>
		/// Event
		/// </summary>
		TEvent On { get; set; }
	}
}
