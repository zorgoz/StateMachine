using System;
using System.Collections.Generic;
using System.Linq;

namespace zorgoz.StateMachine.Model
{
	/// <summary>
	/// Memory capability of a superstate
	/// </summary>
	public enum MemoryType
	{
		/// <summary>
		/// Each time the transition enters the superstate, the machine will enter the superstates initial state
		/// </summary>
		None,

		/// <summary>
		/// Each time the transition ends at the superstate, the machine will enter in the state where 
		/// it had left the superstate, or in the initial state if it was not yet visited
		/// </summary>
		Deep
	}

    internal class SuperStateDefinition<TState>
		where TState : Enum
    {
		public TState Super { get; set; }
		public MemoryType Memory { get; set; } = MemoryType.None;
		public TState Initial { get; set; }
		public List<TState> SubStates { get; } = new List<TState>();
		public WhenExceptionStore<TState> WhenExceptions { get; set; } = new WhenExceptionStore<TState>();

		public TState MemorizedState { get; set; }

		public bool HasState(TState state) => Super.Equals(state) || HasSubState(state);
		public bool HasSubState(TState state) => Initial.Equals(state) || SubStates.Contains(state);
	}

	internal class Hierarchy<TState> : List<SuperStateDefinition<TState>>
		where TState : Enum
	{
		public bool IsSuperState(TState state) => this.Any(x => x.Super.Equals(state));

		public bool EnsureNoSuperState(TState state)
		{
			if (IsSuperState(state))
			{
				throw new MachineModelException($"State '{state}' is already superstate");
			}

			return true;
		}

		public bool EnsureNoSubState(TState state)
		{
			if (this.Any(x => x.Initial.Equals(state) || x.SubStates.Contains(state)))
			{
				throw new MachineModelException($"State '{state}' is already substate in a hierarchy");
			}

			return true;
		}

		public SuperStateDefinition<TState> GetFor(TState state) => this.FirstOrDefault(x => x.HasSubState(state));
	}
}
