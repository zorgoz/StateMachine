using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zorgoz.StateMachine.Model;

namespace zorgoz.StateMachine
{
	public abstract partial class StateMachine<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		private void EnsureClean(bool yes)
		{
			if (yes ? hierarchy.Count > 0 : hierarchy.Count == 0)
			{
				throw new MachineModelException("You have to set default error state as wery first!");
			}
		}

		private void EnsureInitialized(bool yes)
		{
			if (yes ? MachineStatus < Status.Initialized : MachineStatus >= Status.Initialized)
			{
				throw new MachineRuntimeException($"Machine {QuotedName} is {(yes ? "not yet" : "already")} initialized!");
			}
		}

		private void EnsureStarted(bool yes)
		{
			if (yes ? MachineStatus < Status.Started : MachineStatus >= Status.Started)
			{
				throw new MachineRuntimeException($"Machine {QuotedName} is {(yes ? "not yet" : "already")} started!");
			}
		}

		private void EnsureNoSelfWhenExceptionOnNull()
		{
			foreach (var transition in transitions)
			{
				if (transition.On == null && transition.WhenExceptions.HasTargetState(default(TState)))
				{
					throw new MachineModelException($"Null transitions cannot have WhenException internal! {transition}");
				}
			}
		}

		internal void EnsureNoNonsenseNullTransition(TransitionInfo<TState, TEvent> transition)
		{
			if (transition.On is null)
			{
				// No null on supersates
				if (hierarchy.Any(h => transition.From.Equals(h.Super)))
				{
					throw new MachineModelException($"Superstates cannot be source of null transitions! {transition}");
				}

				// No obviously infinite loops
				if (transition.From.Equals(transition.To) && transition.WhenExceptions.HasTargetState(transition.To))
				{
					throw new MachineModelException($"Cannot add a null transition where 'from', 'to' and 'exception' states are the same! {transition}");
				}

				if (
					transition.From.Equals(transition.To)
					|| transition.To.Equals(default(TState))
					|| transition.WhenExceptions.HasTargetState(transition.From)
					)
				{
					throw new MachineModelException($"Null transition cannot lead to its starting state! {transition}");
				}
			}
		}

		internal void EnsureNoConflicts(TransitionInfo<TState, TEvent> transition)
		{
			if (transition.On is null && transition.Guard.type == ActionTypes.None && transitions.Any(x => x.From.Equals(transition.From)))
				throw new MachineModelException($"Cannot add unguarded null transition on state '{transition.From}', because there is already a transition from it.");

			if (transitions.Any(x => x.From.Equals(transition.From) && x.On is null && x.Guard.type == ActionTypes.None))
				throw new MachineModelException($"Cannot add any other transition on state '{transition.From}', because there is already an unguarded null transition defined on it.");

			if (transitions.Any(x => x.From.Equals(transition.From) && EventBase.Equals(x.On, transition.On) && x.Guard.type == ActionTypes.None))
				throw new MachineModelException($"Cannot add transition '{transition.From}({transition.On})' because there is already an unguarded transition from same state on same event!");

			if (transition.Guard.type == ActionTypes.None && transitions.Any(x => x.From.Equals(transition.From) && EventBase.Equals(x.On, transition.On)))
				throw new MachineModelException($"Cannot add unguarded transition '{transition.From}({transition.On})' because there is already a transition from same state on same event!");
		}
	}
}
