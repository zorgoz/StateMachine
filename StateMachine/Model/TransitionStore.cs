using System;
using System.Collections.Generic;

namespace zorgoz.StateMachine.Model
{
	internal class TransitionInfo<TState, TEvent> : Transition<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		public (object action, ActionTypes type) Guard { get; set; }
		public (object action, ActionTypes type) OnExecute { get; set; }
		public WhenExceptionStore<TState> WhenExceptions { get; set; } = new WhenExceptionStore<TState>();
	}

	internal class TransitionStore<TState, TEvent> : List<TransitionInfo<TState, TEvent>>
		where TState : Enum
		where TEvent : EventBase
	{
		public WhenExceptionStore<TState> DefaultErrorStates { get; set; } = new WhenExceptionStore<TState>();
	}
}
