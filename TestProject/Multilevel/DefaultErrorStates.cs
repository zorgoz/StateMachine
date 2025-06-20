using System.Reactive.Subjects;
using zorgoz.StateMachine.Fluent;
using zorgoz.StateMachine.Model;

namespace Tests.ExceptionHandling.Multilevel;

/// <summary>
/// This setup is testing if the machine is honoring the different paths based on the exception thrown during transition execution
/// </summary>
[TestClass]
public class DefaultErrorStates
{
	private Subject<UserAction> EventStream;
	private UserAction Action;
	private MyMachine Machine;

	[TestInitialize]
	public void Setup()
	{
		EventStream = new Subject<UserAction>();
		Action = new UserAction();
		Machine = new MyMachine();
	}

	[TestCleanup]
	public void Stop()
	{
		Machine.Stop(true);
	}

	private async Task Prepare()
	{
		Machine.ClearHistory();

		Machine.Initialize(States.S1, true);
		await Machine.Start(EventStream).ConfigureAwait(false);
	}

	/// <summary>
	/// Machine is set up for following scenario
	/// <image url="$(SolutionDir)\Documentation\Images\DefaultErrorStates.png" />
	/// </summary>
	/// <typeparam name="TException">Exception to throw on transition</typeparam>
	/// <param name="addErrorStatesToS11"></param>
	private void PrepareToThrow<TException>(bool addErrorStatesToS11)
		where TException : Exception, new()
	{
		var super = Machine.SuperState(States.S1, MemoryType.None).WithSubStates(States.S11, States.E1, States.S2);
		if (addErrorStatesToS11)
		{
			super
				.WhenException<IOException>(States.E)
				.WhenException(States.E2)
				;
		}

		Machine.SuperState(States.S11, MemoryType.None).WithSubStates(States.S).InheritExceptionStates();
		Machine.SuperState(States.S2, MemoryType.None).WithSubStates(States.S22);
		Machine.SuperState(States.E1, MemoryType.None).WithSubStates(States.E);

		Machine.In(States.S)
			.Entry(Machine.EntryLogger)
			.Exit(Machine.ExitLogger)
			.On(UserAction.AnyEvent)
			.Goto(States.S22)
			.Execute(Machine.Throw<TException>)
			;

		foreach (var state in new[] { States.S2, States.S1, States.S11, States.S22, States.E1, States.E, States.E2 })
		{
			Machine.In(state)
				.Entry(Machine.EntryLogger)
				.Exit(Machine.ExitLogger)
				;
		}
	}

	[TestMethod, Description("Not prepared with exception handling, but exception is thrown, which redirects to default error state which is to stay")]
	public async Task OnException_DefaultErrorStateIsStay()
	{
		PrepareToThrow<Exception>(false);

		await Prepare().ConfigureAwait(false);

		var result = await Machine.FireDirect(Action).ConfigureAwait(false);

		Assert.AreEqual(States.S, result);
		Assert.AreEqual("[-S][-S11][S#S22]", Machine.PathHistory.ToString());
	}

	[TestMethod, Description("Use superstate level error state for IOException")]
	public async Task OnIOException_DefaultError_E()
	{
		PrepareToThrow<FileNotFoundException>(true);

		await Prepare().ConfigureAwait(false);

		var result = await Machine.FireDirect(Action).ConfigureAwait(false);

		Assert.AreEqual(States.E, result);
		Assert.AreEqual("[-S][-S11][S#S22][+E1][+E]", Machine.PathHistory.ToString());
	}

	[TestMethod, Description("Use superstate level error state for Exception")]
	public async Task OnOtherException_DefaultError_E2()
	{
		PrepareToThrow<NotFiniteNumberException>(true);

		await Prepare().ConfigureAwait(false);

		var result = await Machine.FireDirect(Action).ConfigureAwait(false);

		Assert.AreEqual(States.E2, result);
		Assert.AreEqual("[-S][-S11][S#S22][-S1][+E2]", Machine.PathHistory.ToString());
	}
}
