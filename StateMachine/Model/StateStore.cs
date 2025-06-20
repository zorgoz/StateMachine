using System.Collections.Generic;
using System.Linq;

namespace zorgoz.StateMachine.Model
{
	internal class StateInfo<TState, TEvent>
	{
		public TState State { get; set; }
		public (object action, ActionTypes type) OnEntry { get; set; }
		public (object action, ActionTypes type) OnExit { get; set; }
	}

    internal class StateStore<TState, TEvent> : Dictionary<TState, StateInfo<TState, TEvent>>
	{
		public bool EnsureUniqueState(TState state)
		{
			if (Keys.Contains(state))
			{
				throw new MachineModelException($"Duplicate definition of state '{state}'");
			}

			return true;
		}
    }
}
