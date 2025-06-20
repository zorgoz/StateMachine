using System;
using System.Threading;
using System.Threading.Tasks;
using zorgoz.StateMachine.Model;
using zorgoz.StateMachine.Observing;

namespace zorgoz.StateMachine.Fluent
{
	/// <summary>
	/// Chaining class for Entry and Exit actions
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public abstract class InEntryExit<TState, TEvent> : FluentChainBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal StateInfo<TState, TEvent> definition;

		internal InEntryExit(StateMachine<TState, TEvent> machine) : base(machine)
		{
		}
	}

	/// <summary>
	/// Chaining class for starting transition definition on a machine
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class In<TState, TEvent> : InEntryExit<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal In(StateMachine<TState, TEvent> machine, TState state) : base(machine)
		{
			Machine.states.EnsureUniqueState(state);

			definition = new StateInfo<TState, TEvent>
			{
				State = state
			};

			Machine.states.Add(state, definition);
		}
    }

	/// <summary>
	/// Chaining class for Entry actions
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class OnEntry<TState, TEvent> : InEntryExit<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal OnEntry(InEntryExit<TState, TEvent> that, object action, ActionTypes actionType) : base(that.Machine)
		{
			definition = that.definition;

			if (definition.OnEntry.action != null)
			{
				throw new MachineModelException($"Action '{that.definition.State}.Entry' already defined!");
			}

			definition.OnEntry = (action, actionType);
		}
	}

	/// <summary>
	/// Chaining class for Exit actions
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class OnExit<TState, TEvent> : InEntryExit<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal OnExit(InEntryExit<TState, TEvent> that, object action, ActionTypes actionType) : base(that.Machine)
		{
			definition = that.definition;

			if (definition.OnExit.action != null)
			{
				throw new MachineModelException($"Action '{that.definition.State}.Exit' already defined!");
			}

			definition.OnExit = (action, actionType);
		}
	}

	/// <summary>
	/// Extension method holder 
	/// </summary>
	public static class MachineExtensions
	{
		/// <summary>
		/// Starts a definition on a state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="machine"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public static In<TState, TEvent> In<TState, TEvent>(this StateMachine<TState, TEvent> machine, TState state)
			where TState : Enum
			where TEvent : EventBase
			=> new In<TState, TEvent>(machine, state);
	}

	/// <summary>
	/// Extensio method holder
	/// </summary>
	public static class InEntryExitExtensions
	{
		#region .Entry
		/// <summary>
		/// On entry action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		/// <returns></returns>
		public static OnEntry<TState, TEvent> Entry<TState, TEvent>(this InEntryExit<TState, TEvent> that, Action action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEntry<TState, TEvent>(that, action, ActionTypes.Plain);

		/// <summary>
		/// On entry action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		/// <returns></returns>
		public static OnEntry<TState, TEvent> Entry<TState, TEvent>(this InEntryExit<TState, TEvent> that, Action<TransitionPathStep<TState, TEvent>> action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEntry<TState, TEvent>(that, action, ActionTypes.Statefull);

		/// <summary>
		/// On entry action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		/// <returns></returns>
		public static OnEntry<TState, TEvent> Entry<TState, TEvent>(this InEntryExit<TState, TEvent> that, Func<CancellationToken, Task> action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEntry<TState, TEvent>(that, action, ActionTypes.PlainTask);

		/// <summary>
		/// On entry action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		/// <returns></returns>
		public static OnEntry<TState, TEvent> Entry<TState, TEvent>(this InEntryExit<TState, TEvent> that, Func<TransitionPathStep<TState, TEvent>, CancellationToken, Task> action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEntry<TState, TEvent>(that, action, ActionTypes.StatefullTask);
		#endregion

		#region .Exit
		/// <summary>
		/// On exit action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		/// <returns></returns>
		public static OnExit<TState, TEvent> Exit<TState, TEvent>(this InEntryExit<TState, TEvent> that, Action action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnExit<TState, TEvent>(that, action, ActionTypes.Plain);

		/// <summary>
		/// On exit action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		public static OnExit<TState, TEvent> Exit<TState, TEvent>(this InEntryExit<TState, TEvent> that, Action<TransitionPathStep<TState, TEvent>> action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnExit<TState, TEvent>(that, action, ActionTypes.Statefull);

		/// <summary>
		/// On exit action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		public static OnExit<TState, TEvent> Exit<TState, TEvent>(this InEntryExit<TState, TEvent> that, Func<CancellationToken, Task> action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnExit<TState, TEvent>(that, action, ActionTypes.PlainTask);

		/// <summary>
		/// On exit action definition   
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to execute</param>
		public static OnExit<TState, TEvent> Exit<TState, TEvent>(this InEntryExit<TState, TEvent> that, Func<TransitionPathStep<TState, TEvent>, CancellationToken, Task> action)
			where TState : Enum
			where TEvent : EventBase
			=> new OnExit<TState, TEvent>(that, action, ActionTypes.StatefullTask);
		#endregion
	}
}
