using System;
using zorgoz.StateMachine.Observing;

namespace zorgoz.StateMachine
{
	/// <summary>
	/// Class represents the base for transition information
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class Transition<TState, TEvent>
		where TState: Enum
		where TEvent: EventBase
	{
		/// <summary>
		/// Starting state
		/// </summary>
		public TState From { get; set; }

		/// <summary>
		/// Original goal state
		/// </summary>
		public TState To { get; set; }

		/// <summary>
		/// Event
		/// </summary>
		public TEvent On { get; set; }

		internal Transition<TState, TEvent> Copy(TEvent on) => new Transition<TState, TEvent> { From = From, To = To, On = on };

		internal TransitionPathStep<TState, TEvent> AsPathStep(StateMachine<TState, TEvent> machine, TState target, TEvent on, Exception onException, bool isEntry)
			=> new TransitionPathStep<TState, TEvent>(machine, this, target, on, onException, isEntry);

		/// <summary>
		/// ToString
		/// </summary>
		/// <returns></returns>
		public override string ToString() => $"Transition {From}({On})->{To}";
	}
}
