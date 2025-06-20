using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using zorgoz.StateMachine.Model;
using zorgoz.StateMachine.Observing;

namespace zorgoz.StateMachine
{
	/// <summary>
	/// Abstract hierarchical state machine
	/// </summary>
	/// <remarks>
	/// Machine supports:
	/// <ul>
	/// <li>Null transitions with step limit</li>
	/// <li>Transition evaulation priorisation</li>
	/// <li>Complex error handling using multilevel error transitions</li>
	/// <li>Internal and self-transitions</li>
	/// <li>Asnyc passive worker, supporting both async ans legacy handlers</li>
	/// <li>Enum states and custom event class hierarchy derivable from <see cref="EventBase"/> class</li>
	/// <li>Observable event source</li>
	/// </ul>
	/// </remarks>
	/// <typeparam name="TState"></typeparam>
	/// <typeparam name="TEvent"></typeparam>
	public abstract partial class StateMachine<TState, TEvent>: IDisposable
	{
		private async Task<(bool result, bool exception)> ExecuteGuard(TransitionInfo<TState, TEvent> transition, TEvent actualEvent)
		{
			try
			{
				switch (transition.Guard.type)
				{
					case ActionTypes.None:
						return (true, false);
					case ActionTypes.Plain:
						return (((Func<bool>)transition.Guard.action)(), false);
					case ActionTypes.Statefull:
						return (((Func<Transition<TState, TEvent>, bool>)transition.Guard.action)(transition.Copy(actualEvent)), false);
					case ActionTypes.PlainTask:
						return (await ((Func<CancellationToken, Task<bool>>)transition.Guard.action)(cts.Token).ConfigureAwait(false), false);
					case ActionTypes.StatefullTask:
						return (await ((Func<Transition<TState, TEvent>, CancellationToken, Task<bool>>)transition.Guard.action)(transition.Copy(actualEvent), cts.Token).ConfigureAwait(false), false);
				}
			}
			catch (Exception ex)
			{
				LastException = new ExceptionEventArgs<TState, TEvent>
				{
					Exception = ex,
					Source = ExceptionSource.WhileGuard,
					OriginalTransition = transition
				};

				if(!(ex is SilentException)) exceptionStream.OnNext(LastException);

				return (false, true);
			}

			return (false, false);
		}

		private async Task ExecuteOnExit(TState state, TransitionPathStep<TState, TEvent> transition)
		{
			transitionStepStream.OnNext(transition);

			if (!states.ContainsKey(state)) return;

			try
			{
				switch (states[state].OnExit.type)
				{
					case ActionTypes.Plain:
						((Action)states[state].OnExit.action)();
						break;
					case ActionTypes.Statefull:
						((Action<TransitionPathStep<TState, TEvent>>)states[state].OnExit.action)(transition);
						break;
					case ActionTypes.PlainTask:
						await ((Func<CancellationToken, Task>)states[state].OnExit.action)(cts.Token).ConfigureAwait(false);
						break;
					case ActionTypes.StatefullTask:
						await ((Func<TransitionPathStep<TState, TEvent>, CancellationToken, Task>)states[state].OnExit.action)(transition, cts.Token).ConfigureAwait(false);
						break;
				}
			}
			catch (Exception ex)
			{
				LastException = new ExceptionEventArgs<TState, TEvent>
				{
					State = state,
					Exception = ex,
					Source = ExceptionSource.WhileExit,
					OriginalTransition = transition
				};

				if (!(ex is SilentException)) exceptionStream.OnNext(LastException);
			}
		}

		private async Task<(TState target, bool stay, Exception exception)> ExecuteTransition(TransitionInfo<TState, TEvent> transition, TEvent theEvent)
		{
			if (transition.OnExecute.type != ActionTypes.None)
			{
				try
				{
					switch (transition.OnExecute.type)
					{
						case ActionTypes.Plain:
							((Action)transition.OnExecute.action)();
							break;
						case ActionTypes.Statefull:
							((Action<Transition<TState, TEvent>>)transition.OnExecute.action)(transition.Copy(theEvent));
							break;
						case ActionTypes.PlainTask:
							await ((Func<CancellationToken, Task>)transition.OnExecute.action)(cts.Token).ConfigureAwait(false);
							break;
						case ActionTypes.StatefullTask:
							await ((Func<Transition<TState, TEvent>, CancellationToken, Task>)transition.OnExecute.action)(transition.Copy(theEvent), cts.Token).ConfigureAwait(false);
							break;
					}
				}
				catch (Exception ex)
				{
					LastException = new ExceptionEventArgs<TState, TEvent>
					{
						Exception = ex,
						Source = ExceptionSource.WhileTransition,
						OriginalTransition = transition
					};

					if (!(ex is SilentException)) exceptionStream.OnNext(LastException);

					var whenException = transition.WhenExceptions.GetMatching(ex).state;

					return whenException.Equals(default(TState))
						? (transition.From, true, ex) // internal return to where it came from
						: (whenException, false, ex); // regular goto error state
				}
			}

			return (transition.To.Equals(default(TState)) ? transition.From : transition.To, transition.To.Equals(default(TState)), null);
		}

		private async Task ExecuteOnEntry(TState state, TransitionPathStep<TState, TEvent> transition)
		{
			transitionStepStream.OnNext(transition);

			if (!states.ContainsKey(state)) return;

			try
			{
				switch (states[state].OnEntry.type)
				{
					case ActionTypes.Plain:
						((Action)states[state].OnEntry.action)();
						break;
					case ActionTypes.Statefull:
						((Action<TransitionPathStep<TState, TEvent>>)states[state].OnEntry.action)(transition);
						break;
					case ActionTypes.PlainTask:
						await ((Func<CancellationToken, Task>)states[state].OnEntry.action)(cts.Token).ConfigureAwait(false);
						break;
					case ActionTypes.StatefullTask:
						await ((Func<TransitionPathStep<TState, TEvent>, CancellationToken, Task>)states[state].OnEntry.action)(transition, cts.Token).ConfigureAwait(false);
						break;
				}
			}
			catch (Exception ex)
			{
				LastException = new ExceptionEventArgs<TState, TEvent>
				{
					State = state,
					Exception = ex,
					Source = ExceptionSource.WhileEntry,
					OriginalTransition = transition
				};

				if (!(ex is SilentException)) exceptionStream.OnNext(LastException);
			}
		}

		/// <summary>
		/// Excecutes the transition and gets the result consisting of:
		/// - the state reached which can be: self, or an other state
		/// - whether the transition action has thrown exception
		/// </summary>
		/// <param name="transition"></param>
		/// <param name="theEvent"></param>
		/// <param name="suppress"></param>
		/// <returns></returns>
		private async Task<TState> Execute(TransitionInfo<TState, TEvent> transition, TEvent theEvent, SuppressActions suppress)
		{
			// first calculate all Exit and Entry events in the path
			var path = GetTouchedStatesBetween(transition.From, transition.To);

			// If not suppressed, execute them
			if ((suppress & SuppressActions.Exit) != SuppressActions.Exit	// exit actions not suppressed explicitelly
				&& !transition.To.Equals(default(TState)))                  // not internal transition
			{
				if (!transition.From.Equals(CurrentState)) // transition from superstate
				{
					path.Exits.InsertRange(0, GetTouchedStatesBetween(CurrentState, transition.From).Exits);
				}

				var transitionResult = transition.AsPathStep(this, transition.To, theEvent, null, false);
				await ExecuteExitPath(transitionResult, path).ConfigureAwait(false);
			}

			// Execute transition action
			(TState state, bool stay, Exception exception) target = (transition.To, false, null);

			target = await ExecuteTransition(transition, theEvent).ConfigureAwait(false);

			// If the resulting transition is internal, just quit
			if (target.stay)
			{
				return target.state;
			}

			// notify subscribers
			transitionStepStream.OnNext(new TransitionActionExecutedStep<TState, TEvent>
			{
				Machine = this,
				On = theEvent,
				Target = CurrentState,
				HeadingTo = transition.To,
				ThrownException = target.exception
			});

			// If we are going to an error state recalculate path from the common ancestor
			if (target.exception != null)
			{
				path = GetTouchedStatesBetween(path.CommonAncestor, target.state);

				var transitionResult = transition.AsPathStep(this, target.state, theEvent, target.exception, false);
				transitionResult.To = target.state;

				if ((suppress & SuppressActions.Exit) != SuppressActions.Exit)   // exit actions not suppressed explicitelly
				{
					await ExecuteExitPath(transitionResult, path).ConfigureAwait(false);
				}
			}

			if ((suppress & SuppressActions.Entry) != SuppressActions.Entry) // entry actions not suppressed explicitelly
			{
				foreach (var state in path.Entries)
				{
					var transitionResult = transition.AsPathStep(this, state, theEvent, target.exception, true);
					transitionResult.To = target.state;

					await ExecuteOnEntry(state, transitionResult).ConfigureAwait(false);
				}
			}

			return target.state.Equals(default(TState)) ? transition.From : path.Entries.Last();
		}

		private async Task ExecuteExitPath(TransitionPathStep<TState, TEvent> transitionResult, TransitionTouchedStates<TState> path)
		{
			SuperStateDefinition<TState> h = null;
			for (int i = 0; i < path.Exits.Count; i++)
			{
				var state = path.Exits[i];

				(transitionResult = transitionResult.Clone()).Target = state;

				await ExecuteOnExit(state, transitionResult).ConfigureAwait(false);
				if (i > 0 && (h = hierarchy.FirstOrDefault(x => x.Super.Equals(state))) != null) // leaving a super state
				{
					h.MemorizedState = path.Exits[i - 1];
				}
			}
		}

		/// <summary>
		/// Find best matching transition for event
		/// </summary>
		/// <param name="theEvent"></param>
		/// <returns></returns>
		private IEnumerable<TransitionInfo<TState, TEvent>> GetBestTransitionsForEvent(TEvent theEvent)
		{
			var matches = transitions.Where(x => CurrentState.Equals(x.From) && EventBase.Equals(x.On, theEvent));

			if(matches.Any()) return matches;

			SuperStateDefinition<TState> h;
			var state = CurrentState;

			while ((h = hierarchy.FirstOrDefault(x => x.HasSubState(state))) != null && !state.Equals(default(TState)))
			{
				matches = transitions.Where(x => x.From.Equals(h.Super) && x.On.Equals(theEvent));
				if (matches.Any()) return matches;

				state = h.Super;
			}

			return null;
		}

		private async Task<bool> PerformFire(TEvent @event)
		{
			var transitions = GetBestTransitionsForEvent(@event);

			if(transitions is null) return false;

			foreach (var transition in transitions.OrderBy(x => x.On?.Order))
			{
				var (result, exception) = await ExecuteGuard(transition, @event).ConfigureAwait(false);

				transitionStepStream.OnNext(new GuardExecutedStep<TState, TEvent> {
					Machine = this,
					Target = transition.From,
					GuardTarget = transition.To,
					On = @event,
					Result = result
				});

				if (result)
				{
					CurrentState = await Execute(transition, @event, SuppressActions.None).ConfigureAwait(false);

					transitionStepStream.OnNext(new StationarySateReachedStep<TState, TEvent>
					{
						Machine = this,
						Target = CurrentState,
						On = @event
					});

					return true;
				}
			}
			return false;
		}

		private async Task FireNull()
		{
			var limit = StepLimit;
			while (--limit > 0 && await PerformFire(null).ConfigureAwait(false)) { }

			if (limit == 0)
			{
				throw new MachineRuntimeException($"Reached the limit of {StepLimit} null transitions. Please check machine model!");
			}
		}

		private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

		/// <summary>
		/// Performs the firing of the event.
		/// </summary>
		/// <param name="theEvent">Event to be fired</param>
		/// <returns>False only if engine-internal exception is thrown.</returns>
		protected async Task<TState> Fire(TEvent theEvent)
		{
			await semaphoreSlim.WaitAsync().ConfigureAwait(true);
			try
			{
				if (await PerformFire(theEvent).ConfigureAwait(false))
				{
					await FireNull().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				exceptionStream.OnNext(new ExceptionEventArgs<TState, TEvent>
				{
					Exception = ex,
					State = CurrentState
				});
			}
			finally
			{
				semaphoreSlim.Release();
			}

			return CurrentState;
		}

		internal TransitionTouchedStates<TState> GetTouchedStatesBetween(TState from, TState to)
		{
			SuperStateDefinition<TState> h;

			IEnumerable<TState> ToOutmost(TState fromState)
			{
				yield return fromState;

				do
				{
					h = hierarchy.FirstOrDefault(x => x.HasSubState(fromState));
					if (h?.Initial.Equals(default(TState)) == false) yield return fromState = h.Super;
				}
				while (h?.Super.Equals(default(TState)) == false);

				yield return default;
			}

			var result = new TransitionTouchedStates<TState>
			{
				CommonAncestor = hierarchy.First(x => x.HasSubState(from)).Super
			};

			if (to.Equals(default(TState))) return result;

			if (to.Equals(from))
			{
				result.Entries.Add(to);
				result.Exits.Add(from);

				return result;
			}

			result.Exits.AddRange(ToOutmost(from));
			result.Entries.AddRange(ToOutmost(to));

			var last_e = result.Entries.Last();
			var last_x = result.Exits.Last();

			while (result.Entries.Count > 0 && result.Exits.Count > 0 && last_e.Equals(last_x))
			{
				result.CommonAncestor = last_e;
				result.Entries.Remove(last_e);
				result.Exits.Remove(last_x);

				last_e = result.Entries.LastOrDefault();
				last_x = result.Exits.LastOrDefault();
			}

			result.Entries.Reverse();

			while ((h = hierarchy.FirstOrDefault(x => x.Super.Equals(to))) != null) // The target is a super state
			{
				to = h.Memory == MemoryType.None || h.MemorizedState.Equals(default(TState)) ? h.Initial : h.MemorizedState;
				result.Entries.Add(to);
			}

			return result;
		}

		/// <summary>
		/// Forcefully moves the machine into a specific state
		/// </summary>
		/// <param name="forcedState">State to move to</param>
		/// <param name="suppress">Should suppress exit and/or entry actions when transiting forcefully</param>
		public async Task<TState> Goto(TState forcedState, SuppressActions suppress)
		{
			EnsureStarted(true);

			await semaphoreSlim.WaitAsync().ConfigureAwait(true);
			try
			{
				await Execute(new TransitionInfo<TState, TEvent>
				{
					From = CurrentState,
					To = forcedState,
					OnExecute = (null, ActionTypes.None)
				}, null, suppress).ConfigureAwait(false);
			}
			finally
			{
				semaphoreSlim.Release();
			}

			return CurrentState;
		}

		/// <summary>
		/// Disposes all managed resources.
		/// </summary>
		public void Dispose()
		{
			transitionStepStream.OnCompleted();
			exceptionStream.OnCompleted();

			observer?.Dispose();
			cts.Dispose();
			semaphoreSlim.Dispose();
		}
	}
}
