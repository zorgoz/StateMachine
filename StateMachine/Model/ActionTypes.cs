namespace zorgoz.StateMachine.Model
{
	internal enum ActionTypes
	{
		None = 0,

		/// <summary>
		/// Exit, Entry, Execute: typeof(Action)
		/// <para/>Guard: Func&lt;bool>
		/// </summary>
		Plain,

		/// <summary>
		/// Exit, Entry: Action&lt;TransitionResult&lt;TState, TEvent>>
		/// <para/>Execute: Action&lt;Transition&lt;TState, TEvent>>
		/// <para/>Guard: Func&lt;Transition&lt;TState, TEvent>, bool>
		/// </summary>
		Statefull,

		/// <summary>
		/// Exit, Entry, Execute: Func&lt;CancellationToken, Task>
		/// <para/>Guard: Func&lt;CancellationToken, Task&lt;bool>>
		/// </summary>
		PlainTask,

		/// <summary>
		/// Exit, Entry: Func&lt;TransitionResult&lt;TState, TEvent>, CancellationToken, Task>
		/// <para/>Execute: Func&lt;Transition&lt;TState, TEvent>, CancellationToken, Task>
		/// <para/>Guard: Func&lt;Transition&lt;TState, TEvent>, CancellationToken, Task&lt;bool>>
		/// </summary>
		StatefullTask
	}
}
