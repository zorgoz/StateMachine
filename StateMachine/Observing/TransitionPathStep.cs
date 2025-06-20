using System;

namespace zorgoz.StateMachine.Observing
{
	/// <summary>
	/// Class passed to the statefull event handlers during transition.
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class TransitionPathStep<TState, TEvent> : Transition<TState, TEvent>, ITransitionStep<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		/// <summary>
		/// Entry or exit step
		/// </summary>
		public bool IsEntry { get; set; }

		/// <summary>
		/// Is this transition result of an exception
		/// </summary>
		public Exception WhenException { get; set; }

		/// <summary>
		/// The actual state being visited
		/// </summary>
		public TState Target { get; set; }

		internal TransitionPathStep()
		{

		}

		internal TransitionPathStep(StateMachine<TState, TEvent> machine, Transition<TState, TEvent> transition, TState target, TEvent on, Exception onException, bool isEntry)
		{
			Machine = machine;
			Target = target;
			From = transition.From;
			To = transition.To;
			On = on;
			WhenException = onException;
			IsEntry = isEntry;
		}

		internal TransitionPathStep<TState, TEvent> Clone()
			=> (TransitionPathStep<TState, TEvent>)MemberwiseClone();

		/// <summary>
		/// Initiating machine
		/// </summary>
		public StateMachine<TState, TEvent> Machine { get; set; }

		/// <summary>
		/// ToString
		/// </summary>
		/// <returns></returns>
		public override string ToString() => $"{Machine?.QuotedName}: {From}({On}{(WhenException != null? "!" : string.Empty)})..{( IsEntry ? $"->{Target}" : $"{Target}->")}...{To}";
	}
}
