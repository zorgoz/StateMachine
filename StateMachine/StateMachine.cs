using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using zorgoz.StateMachine.Model;
using zorgoz.StateMachine.Observing;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Tests")]
[assembly: InternalsVisibleTo("LINQPadQuery")]
#endif

namespace zorgoz.StateMachine
{
	public abstract partial class StateMachine<TState, TEvent>
		where TState : Enum
		where TEvent : EventBase
	{
		/// <summary>
		/// Machine status
		/// </summary>
		public enum Status
		{
			/// <summary>
			/// Machine created or stopped
			/// </summary>
			Created = 0,

			/// <summary>
			/// Machine initialisation data set
			/// </summary>
			Initialized,

			/// <summary>
			/// Machine performed initialisation steps and is ready to process events
			/// </summary>
			Started
		}

		internal readonly Hierarchy<TState> hierarchy = new Hierarchy<TState>();
		internal readonly StateStore<TState, TEvent> states = new StateStore<TState, TEvent>();
		internal readonly TransitionStore<TState, TEvent> transitions = new TransitionStore<TState, TEvent>();
		private IDisposable observer;
		private readonly CancellationTokenSource cts;
		private (TState state, bool suppressed) initialState;

		/// <summary>
		/// Current status of the machine
		/// </summary>
		public Status MachineStatus { get; private set; }

		/// <summary>
		/// Machine name if provifded in constructor
		/// </summary>
		protected string Name { get; }
		internal string QuotedName => Name == null ? "(no name)" : $"'{Name}'";

		/// <summary>
		/// Last exception catched
		/// </summary>
		public ExceptionEventArgs<TState, TEvent> LastException { get; private set; }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="name">Machine informational name</param>
		/// <param name="topToken">If provided, machine token will be linket do it</param>
		protected StateMachine(string name, CancellationToken topToken)
		{
			Name = name;

			cts = topToken == default ? cts = new CancellationTokenSource() : CancellationTokenSource.CreateLinkedTokenSource(topToken);

			if (Enum.GetValues(typeof(TState)).Cast<TState>().Contains(default))
			{
				throw new MachineModelException("States enumeration cannot contain zero-valued element. Please ensure that all values are greater than zero!");
			}

			MachineStatus = Status.Created;
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		protected StateMachine()
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="name">Name of the machine</param>
		protected StateMachine(string name) : this(name, default)
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="topToken">Top level cancellation token</param>
		protected StateMachine(CancellationToken topToken) : this(null, topToken)
		{
		}

		/// <summary>
		/// Observable of exceptions buubled up to the machine
		/// </summary>
		public IObservable<ExceptionEventArgs<TState, TEvent>> OnException => exceptionStream.AsObservable();
		private readonly Subject<ExceptionEventArgs<TState, TEvent>> exceptionStream = new Subject<ExceptionEventArgs<TState, TEvent>>();

		/// <summary>
		/// Observable
		/// </summary>
		public IObservable<ITransitionStep<TState, TEvent>> OnTransitionStep => transitionStepStream.AsObservable();
		private readonly Subject<ITransitionStep<TState, TEvent>> transitionStepStream = new Subject<ITransitionStep<TState, TEvent>>();

		/// <summary>
		/// Gets the current state of the machine
		/// </summary>
		public TState CurrentState { get; private set; }

		/// <summary>
		/// Limit of null-driven transitions  
		/// </summary>
		protected int StepLimit { get; set; } = ushort.MaxValue;

		/// <summary>
		/// Initializes the state machine in the given state. No actual action is performed at this step.
		/// </summary>
		/// <param name="initialState">State</param>
		/// <param name="suppressEntry">If true, entry actions ere performed during actual initiation.</param>
		public void Initialize(TState initialState, bool suppressEntry)
		{
			EnsureInitialized(false);
			EnsureNoSelfWhenExceptionOnNull();

			CreateOutmostHierarchy();

			this.initialState = (initialState, suppressEntry);

			MachineStatus = Status.Initialized;
		}

		private void CreateOutmostHierarchy()
		{
			var remaining = new SuperStateDefinition<TState> { WhenExceptions = transitions.DefaultErrorStates.Clone() };

			foreach (TState state in Enum.GetValues(typeof(TState)))
			{
				if (!hierarchy.Any(x => x.HasState(state))) remaining.SubStates.Add(state);
			}

			foreach (var h in hierarchy)
			{
				if(!hierarchy.Any(x => x.HasSubState(h.Super))) remaining.SubStates.Add(h.Super);
			}

			if (remaining.SubStates.Count > 0)
			{
				hierarchy.Insert(0, remaining);
			}
		}

		/// <summary>
		/// Starts the machine from the initialisation state.
		/// The machine is subscribing subscribes to the observable at this step.
		/// If not set as suppressed at initialisation, it will perform all entry actions in the path from the outmost world. 
		/// </summary>
		/// <param name="eventStream">Observable of events</param>
		public async Task<TState> Start(IObservable<TEvent> eventStream)
		{
			EnsureInitialized(true);
			EnsureStarted(false);

			var traverse = GetTouchedStatesBetween(default, initialState.state);

			var transitionResult = new TransitionPathStep<TState, TEvent>
			{
				Machine = this,
				From = default,
				To = initialState.state,
				On = null,
				WhenException = null,
			};

			if (!initialState.suppressed)
			{
				foreach (var state in traverse.Entries)
				{
					transitionResult.Target = state;
					await ExecuteOnEntry(state, transitionResult).ConfigureAwait(false);
				}
			}

			CurrentState = traverse.Entries.Last();

			await FireNull().ConfigureAwait(false);

			MachineStatus = Status.Started;

			observer = eventStream
				.Select(e => Observable.FromAsync(() => Fire(e)))
				.Concat()
				.Subscribe();

			return CurrentState;
		}

		/// <summary>
		/// Stops the state machine and puts it in it's unitialized state
		/// </summary>
		/// <param name="cleanup">Should reset all internals</param>
		public void Stop(bool cleanup = false)
		{
			cts?.Cancel();

			observer?.Dispose();

			if (cleanup)
			{
				states.Clear();
				transitions.Clear();
				hierarchy.Clear();
			}

			MachineStatus = Status.Created;
		}

		/// <summary>
		/// Sets the default error state used if a transaction execution throws exception, unless it is overridden with <see cref="WhenException(TState)"/>
		/// </summary>
		/// <remarks>This setting is effective from where it is set. Previously defined transitions are not effected. Exit and Entry actions are not in its scope.</remarks>
		/// <param name="gotoState">State to go to</param>
		public void WhenException(TState gotoState)
		{
			EnsureClean(true);

			EnsureInitialized(false);

			transitions.DefaultErrorStates.AddOrReplace<Exception>(gotoState);
		}

		/// <summary>
		/// Sets the default error state used if a transaction execution throws exception, unless it is overridden with <see cref="WhenException(TState)"/>
		/// </summary>
		/// <typeparam name="ExceptionType">Exception type to follow</typeparam>
		/// <remarks>This setting is effective from where it is set. Previously defined transitions are not effected. Exit and Entry actions are not in its scope.</remarks>
		/// <param name="gotoState">State to go to</param>
		public void WhenException<ExceptionType>(TState gotoState)
			where ExceptionType: Exception
		{
			EnsureClean(true);

			EnsureInitialized(false);

			transitions.DefaultErrorStates.AddOrReplace<ExceptionType>(gotoState);
		}
	}
}
