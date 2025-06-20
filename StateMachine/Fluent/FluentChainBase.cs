using System;

namespace zorgoz.StateMachine.Fluent
{
	/// <summary>
	/// Base class for the fluent chain
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public class FluentChainBase<TState, TEvent> 
		where TState : Enum
		where TEvent : EventBase
	{
		/// <summary>
		/// The machine being configured
		/// </summary>
		internal protected StateMachine<TState, TEvent> Machine { get; }

		internal FluentChainBase(StateMachine<TState, TEvent> machine)
		{
			Machine = machine;
		}
    }
}
