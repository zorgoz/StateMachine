using System;
using zorgoz.StateMachine.Model;

namespace zorgoz.StateMachine.Fluent
{
	/// <summary>
	/// Superstate definition
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class SuperState<TState, TEvent> : FluentChainBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal readonly SuperStateDefinition<TState> definition;

		internal SuperState(StateMachine<TState, TEvent> machine, TState state, MemoryType memory): base(machine)
		{
			EnsureNoStatesDefined();

			Machine.hierarchy.EnsureNoSuperState(state);

			definition = new SuperStateDefinition<TState>
			{
				Super = state,
				Memory = memory,
				WhenExceptions = machine.transitions.DefaultErrorStates.Clone()
			};

			Machine.hierarchy.Add(definition);
		}

		private void EnsureNoStatesDefined()
		{
			if (Machine.states.Count > 0)
			{
				throw new MachineModelException("Please define hierarchy first!");
			}
		}

		private void EnsureNoSuperState(TState state)
		{
			if (Machine.hierarchy.IsSuperState(state))
			{
				throw new MachineModelException($"State '{state}' is already defined as superstate. You have to define outmost superstates first!");
			}
		}

		internal SuperState<TState, TEvent> SetSubStates(TState initialState, params TState[] states)
		{
			Machine.hierarchy.EnsureNoSubState(initialState);

			EnsureNoSuperState(initialState);
			definition.Initial = initialState;

			foreach (var state in states)
			{
				EnsureNoSuperState(state);
				Machine.hierarchy.EnsureNoSubState(state);
				definition.SubStates.Add(state);
			}

			return this;
		}

		internal SuperStateWhenException<TState, TEvent> SetInheritWhenException()
		{ 
			definition.WhenExceptions = Machine.hierarchy.GetFor(definition.Super)?.WhenExceptions.Clone() ?? Machine.transitions.DefaultErrorStates.Clone();

			return new SuperStateWhenException<TState, TEvent>(this);
		}

		internal SuperStateWhenException<TState, TEvent> SetWhenException<ExceptionType>(TState state)
			where ExceptionType : Exception
		{
			definition.WhenExceptions.AddOrReplace<ExceptionType>(state);

			return new SuperStateWhenException<TState, TEvent>(this);
		}

		/// <summary>
		/// Sets error state on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public SuperStateWhenException<TState, TEvent> WhenException<ExceptionType>(TState state)
			where ExceptionType : Exception
			=> SetWhenException<ExceptionType>(state);

		/// <summary>
		/// Sets internal transition as error transition on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public SuperStateWhenException<TState, TEvent> WhenException<ExceptionType>()
			where ExceptionType : Exception
			=> SetWhenException<ExceptionType>(default(TState));

		/// <summary>
		/// Sets error state on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public SuperStateWhenException<TState, TEvent> WhenException(TState state)
			=> SetWhenException<Exception>(state);

		/// <summary>
		/// Sets internal transition as error transition on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public SuperStateWhenException<TState, TEvent> WhenException()
			=> SetWhenException<Exception>(default(TState));
	}

	/// <summary>
	/// Super state Exception chain
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class SuperStateWhenException<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal readonly SuperState<TState, TEvent> super;

		internal SuperStateWhenException(SuperState<TState, TEvent> super)
		{
			this.super = super;
		}

		/// <summary>
		/// Sets error state on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public SuperStateWhenException<TState, TEvent> WhenException<ExceptionType>(TState state)
			where ExceptionType : Exception
			=> super.SetWhenException<ExceptionType>(state);

		/// <summary>
		/// Sets internal transition as error transition on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <returns></returns>
		public SuperStateWhenException<TState, TEvent> WhenException<ExceptionType>()
			where ExceptionType : Exception
			=> super.SetWhenException<ExceptionType>(default(TState));
	}

	/// <summary>
	/// Extension method store
	/// </summary>
	public static class SuperStateExtensions
	{
		/// <summary>
		/// Starts a superstate definition on the specific machine
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="machine">Machine</param>
		/// <param name="onState">State declared as superstate</param>
		/// <param name="memory">Memory capability of this superstate</param>
		/// <returns></returns>
		public static SuperState<TState, TEvent> SuperState<TState, TEvent>(this StateMachine<TState, TEvent> machine, TState onState, MemoryType memory)
			where TState : Enum
			where TEvent : EventBase
			=> new SuperState<TState, TEvent>(machine, onState, memory);

		/// <summary>
		/// Adds substates to the superstate. Substates can be supersattes on their own.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="super">Superstate</param>
		/// <param name="initialState">Initial state of the superstate</param>
		/// <param name="otherStates">Other states of the superstate</param>
		/// <returns></returns>
		public static SuperState<TState, TEvent> WithSubStates<TState, TEvent>(this SuperState<TState, TEvent> super, TState initialState, params TState[] otherStates)
			where TState : Enum
			where TEvent : EventBase
			=> super.SetSubStates(initialState, otherStates);

		/// <summary>
		/// Sets error state on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="super"></param>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public static void WhenException<TState, TEvent>(this SuperStateWhenException<TState, TEvent> super, TState state)
			where TState : Enum
			where TEvent : EventBase
			=> super.super.SetWhenException<Exception>(state);

		/// <summary>
		/// Sets internal transition as error transition on that specific superstate.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="super"></param>
		/// <returns></returns>
		public static void WhenException<TState, TEvent>(this SuperStateWhenException<TState, TEvent> super)
			where TState : Enum
			where TEvent : EventBase
			=> super.super.SetWhenException<Exception>(default(TState));

		/// <summary>
		/// If applied, the superstate will inherit WhenException setting from its super state.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="super"></param>
		/// <param name="state">Error state to use</param>
		/// <returns></returns>
		public static SuperStateWhenException<TState, TEvent> InheritExceptionStates<TState, TEvent>(this SuperState<TState, TEvent> super)
			where TState : Enum
			where TEvent : EventBase
			=> super.SetInheritWhenException();
	}
}
