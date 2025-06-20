using System;

namespace zorgoz.StateMachine.Observing
{
	/// <summary>
	/// Contains information about the transition action executed
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class TransitionActionExecutedStep<TState, TEvent> : ITransitionStep<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		/// <summary>
		/// State being visited
		/// </summary>
		public TState Target { get; set; }

		/// <summary>
		/// Event
		/// </summary>
		public TEvent On { get; set; }

		/// <summary>
		/// The action thrown exception
		/// </summary>
		public Exception ThrownException { get; set; }

		/// <summary>
		/// Was heading to state
		/// </summary>
		public TState HeadingTo { get; set; }

		/// <summary>
		/// Initiating machine
		/// </summary>
		public StateMachine<TState, TEvent> Machine { get; set; }

		/// <summary>
		/// ToString implementation
		/// </summary>
		/// <returns></returns>
		public override string ToString() => $"{Machine?.QuotedName}: T({Target}({On})->{HeadingTo})={(ThrownException != null ? "Ex!" : "Ok.")}";
	}
}
