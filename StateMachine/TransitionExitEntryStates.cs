using System.Collections.Generic;

namespace zorgoz.StateMachine
{
	internal class TransitionTouchedStates<TState>
    {
		public TState CommonAncestor { get; set; }
		public List<TState> Exits = new List<TState>();
		public List<TState> Entries = new List<TState>();
	}
}
