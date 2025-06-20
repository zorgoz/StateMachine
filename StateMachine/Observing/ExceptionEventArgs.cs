using System;

namespace zorgoz.StateMachine.Observing
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	/// <summary>
	/// Situation when this exception was caught
	/// </summary>
	public enum ExceptionSource { WhileGuard = 1, WhileTransition, WhileEntry, WhileExit, Internal }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

	/// <summary>
	/// Exception details
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class ExceptionEventArgs<TState, TEvent>
		where TState: Enum
		where TEvent: EventBase
	{
		/// <summary>
		/// State being visited or started from
		/// </summary>
		public TState State { get; set; }

		/// <summary>
		/// The original transition definition resulting in this exception
		/// </summary>
		public Transition<TState, TEvent> OriginalTransition { get; set; }

		/// <summary>
		/// Situation when this exception was caught
		/// </summary>
		public ExceptionSource Source { get; set; }

		/// <summary>
		/// Actual exception caught
		/// </summary>
		public Exception Exception { get; set; }
	}
}
