using System;
using System.Threading;
using System.Threading.Tasks;
using zorgoz.StateMachine.Model;

namespace zorgoz.StateMachine.Fluent
{
	/// <summary>
	/// Chain base class for transitions
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public abstract class TransitionBase<TState, TEvent> : FluentChainBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal TransitionInfo<TState, TEvent> definition;
		internal TransitionBase<TState, TEvent> parent;

		internal TransitionBase(StateMachine<TState, TEvent> machine, TransitionBase<TState, TEvent> parent) : base(machine)
		{
			this.parent = parent;
			definition = parent?.definition;
		}
	}

	/// <summary>
	/// Chain base class for Goto definition
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public abstract class Gotoable<TState, TEvent> : TransitionBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal Gotoable(StateMachine<TState, TEvent> machine, TransitionBase<TState, TEvent> parent) : base(machine, parent)
		{
		}
	}

	/// <summary>
	/// Chain class for event definition
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class OnEvent<TState, TEvent> : Gotoable<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal OnEvent(InEntryExit<TState, TEvent> that, TEvent @event) : base(that.Machine, null)
		{
			var h = Machine.hierarchy.GetFor(that.definition.State);

			definition = new TransitionInfo<TState, TEvent>
			{
				On = @event,
				From = that.definition.State,
				WhenExceptions = h == null ? Machine.transitions.DefaultErrorStates.Clone() : h.WhenExceptions.Clone()
			};
		}

		internal OnEvent(TransitionBase<TState, TEvent> that, TEvent @event) : base(that.Machine, null)
		{
			var h = Machine.hierarchy.GetFor(that.definition.To);

			definition = new TransitionInfo<TState, TEvent>
			{
				On = @event,
				From = that.definition.From,
				WhenExceptions = h == null ? Machine.transitions.DefaultErrorStates.Clone() : h.WhenExceptions.Clone()
			};
		}
	}

	/// <summary>
	/// Chain class for guard definition
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class Guard<TState, TEvent> : Gotoable<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal Guard(OnEvent<TState, TEvent> on, object action, ActionTypes type) : base(on.Machine, on)
		{
			definition.Guard = (action, type);
		}
	}

	/// <summary>
	/// Chain class for goto defintion
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class Goto<TState, TEvent> : TransitionBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal Goto(Gotoable<TState, TEvent> gotoable, TState state) : base(gotoable.Machine, gotoable)
		{
			definition.To = state;

			Machine.EnsureNoConflicts(definition);
			Machine.EnsureNoNonsenseNullTransition(definition);

			Machine.transitions.Add(definition);
		}
	}

	/// <summary>
	/// Chain class for transition action execution
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class Execute<TState, TEvent> : TransitionBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal Execute(Goto<TState, TEvent> @goto, object action, ActionTypes type) : base(@goto.Machine, @goto)
		{
			definition.OnExecute = (action, type);
		}

		internal Execute<TState, TEvent> Define<ExceptionType>(TState gotoState)
			where ExceptionType : Exception
		{
			definition.WhenExceptions.AddOrReplace<ExceptionType>(gotoState);

			Machine.EnsureNoNonsenseNullTransition(definition);

			return this;
		}

		/// <summary>
		/// Defines destination state on specific exception type.
		/// </summary>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <param name="gotoState">State to go to</param>
		/// <returns></returns>
		public Execute<TState, TEvent> WhenException<ExceptionType>(TState gotoState)
			where ExceptionType : Exception => Define<ExceptionType>(gotoState);

		/// <summary>
		/// Defines internal transition on specific exception type.
		/// </summary>
		/// <typeparam name="ExceptionType"></typeparam>
		/// <returns></returns>
		public Execute<TState, TEvent> WhenException<ExceptionType>()
			where ExceptionType : Exception => Define<ExceptionType>(default(TState));
	}

	/// <summary>
	/// Chain class for exception state definition.
	/// </summary>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public sealed class WhenException<TState, TEvent> : TransitionBase<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		internal WhenException(Execute<TState, TEvent> execute) : base(execute.Machine, execute) { }
	}

	/// <summary>
	/// Extensinon method holder 
	/// </summary>
	public static class TransitionExtensions
	{
		#region .On, .Always
		/// <summary>
		/// Defines event trigger for transition
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="event">Triggering event</param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> On<TState, TEvent>(this InEntryExit<TState, TEvent> that, TEvent @event)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, @event);

		/// <summary>
		/// Defines null transition
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> Immediately<TState, TEvent>(this InEntryExit<TState, TEvent> that)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, null);
		#endregion

		#region .If .Goto .Execute .WhenException
		/// <summary>
		/// Defines guard function for transition
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="guard">Guard function</param>
		/// <returns></returns>
		public static Guard<TState, TEvent> If<TState, TEvent>(this OnEvent<TState, TEvent> that, Func<bool> guard)
			where TState : Enum
			where TEvent : EventBase
			=> new Guard<TState, TEvent>(that, guard, ActionTypes.Plain);

		/// <summary>
		/// Defines guard function for transition
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="guard">Guard function</param>
		/// <returns></returns>
		public static Guard<TState, TEvent> If<TState, TEvent>(this OnEvent<TState, TEvent> that, Func<Transition<TState, TEvent>, bool> guard)
			where TState : Enum
			where TEvent : EventBase
			=> new Guard<TState, TEvent>(that, guard, ActionTypes.Statefull);

		/// <summary>
		/// Defines guard function for transition
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="guard">Guard function</param>
		/// <returns></returns>
		public static Guard<TState, TEvent> If<TState, TEvent>(this OnEvent<TState, TEvent> that, Func<CancellationToken, Task<bool>> guard)
			where TState : Enum
			where TEvent : EventBase
			=> new Guard<TState, TEvent>(that, guard, ActionTypes.PlainTask);

		/// <summary>
		/// Defines guard function for transition
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="guard">Guard function</param>
		/// <returns></returns>
		public static Guard<TState, TEvent> If<TState, TEvent>(this OnEvent<TState, TEvent> that, Func<Transition<TState, TEvent>, CancellationToken, Task<bool>> guard)
			where TState : Enum
			where TEvent : EventBase
			=> new Guard<TState, TEvent>(that, guard, ActionTypes.StatefullTask);

		/// <summary>
		/// Defines transition target
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="state">Targeted state. If state is self, all exit and entry events are performed besides the transition action. To execute only transition action, use <see cref="Goto{TState, TEvent}(Gotoable{TState, TEvent})"/> instead </param>
		/// <returns></returns>
		public static Goto<TState, TEvent> Goto<TState, TEvent>(this Gotoable<TState, TEvent> that, TState state)
			where TState : Enum
			where TEvent : EventBase
			=> new Goto<TState, TEvent>(that, state);

		/// <summary>
		/// Defines internal transition state.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <returns></returns>
		public static Goto<TState, TEvent> Goto<TState, TEvent>(this Gotoable<TState, TEvent> that)
			where TState : Enum
			where TEvent : EventBase
			=> new Goto<TState, TEvent>(that, default);

		/// <summary>
		/// Defines transition action
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to perform</param>
		/// <returns></returns>
		public static Execute<TState, TEvent> Execute<TState, TEvent>(this Goto<TState, TEvent> that, Action action)
			where TState : Enum
			where TEvent : EventBase
			=> new Execute<TState, TEvent>(that, action, ActionTypes.Plain);

		/// <summary>
		/// Defines transition action
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to perform</param>
		/// <returns></returns>
		public static Execute<TState, TEvent> Execute<TState, TEvent>(this Goto<TState, TEvent> that, Action<Transition<TState, TEvent>> action)
			where TState : Enum
			where TEvent : EventBase
			=> new Execute<TState, TEvent>(that, action, ActionTypes.Statefull);

		/// <summary>
		/// Defines transition action
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to perform</param>
		/// <returns></returns>
		public static Execute<TState, TEvent> Execute<TState, TEvent>(this Goto<TState, TEvent> that, Func<CancellationToken, Task> action)
			where TState : Enum
			where TEvent : EventBase
			=> new Execute<TState, TEvent>(that, action, ActionTypes.PlainTask);

		/// <summary>
		/// Defines transition action
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="action">Action to perform</param>
		/// <returns></returns>
		public static Execute<TState, TEvent> Execute<TState, TEvent>(this Goto<TState, TEvent> that, Func<Transition<TState, TEvent>, CancellationToken, Task> action)
			where TState : Enum
			where TEvent : EventBase
			=> new Execute<TState, TEvent>(that, action, ActionTypes.StatefullTask);

		/// <summary>
		/// Defines staying in the current state if exception occures during transition action. No Exit and Entry actions are executed.
		/// This definition will close the exception definition chaining as it reacts on all exceptions not defined before.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <returns></returns>
		public static WhenException<TState, TEvent> WhenException<TState, TEvent>(this Execute<TState, TEvent> that)
			where TState : Enum
			where TEvent : EventBase
			=> new WhenException<TState, TEvent>(that.Define<Exception>(default));

		/// <summary>
		/// Defines state to target if exception occures during transition action. Recalculated Entry and Exit actions are executed even if target state is self.
		/// This definition will close the exception definition chaining as it reacts on all exceptions not defined before.
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="state">State to target. If you want the machine to stay in the current state, use <see cref="WhenException{TState, TEvent}(Fluent.Execute{TState, TEvent})"/> instead.</param>
		/// <returns></returns>
		public static WhenException<TState, TEvent> WhenException<TState, TEvent>(this Execute<TState, TEvent> that, TState state)
			where TState : Enum
			where TEvent : EventBase
			=> new WhenException<TState, TEvent>(that.Define<Exception>(state));
		#endregion
	}

	/// <summary>
	/// Extension method holder
	/// </summary>
	public static class RestartChainExtensions
	{
		/// <summary>
		/// Restarts chaining afer Goto with a new trigger event on the same state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="event">Triggring event</param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> On<TState, TEvent>(this Goto<TState, TEvent> that, TEvent @event)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, @event);

		/// <summary>
		/// Restarts chaining after transition action definition with a new trigger event on the same state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="event">Triggering event</param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> On<TState, TEvent>(this Execute<TState, TEvent> that, TEvent @event)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, @event);

		/// <summary>
		/// Restarts chaining after an exception state definition with a new trigger event on the same state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <param name="event">Triggering event</param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> On<TState, TEvent>(this WhenException<TState, TEvent> that, TEvent @event)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, @event);

		/// <summary>
		/// Restarts chaining afer Goto with a null transition on the same state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> Immediately<TState, TEvent>(this Goto<TState, TEvent> that)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, null);

		/// <summary>
		/// Restarts chaining after transition action definition with a null transition on the same state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> Immediately<TState, TEvent>(this Execute<TState, TEvent> that)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, null);

		/// <summary>
		/// Restarts chaining after an exception state definition with a null transition on the same state
		/// </summary>
		/// <typeparam name="TState"></typeparam>
		/// <typeparam name="TEvent"></typeparam>
		/// <param name="that"></param>
		/// <returns></returns>
		public static OnEvent<TState, TEvent> Immediately<TState, TEvent>(this WhenException<TState, TEvent> that)
			where TState : Enum
			where TEvent : EventBase
			=> new OnEvent<TState, TEvent>(that, null);
	}
}
