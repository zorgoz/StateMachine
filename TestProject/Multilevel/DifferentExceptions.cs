using System.Reactive.Subjects;
using zorgoz.StateMachine.Fluent;
using zorgoz.StateMachine.Model;

namespace Tests.ExceptionHandling.Multilevel;

/// <summary>
/// This setup is testing if the machine is honoring the different paths based on the exception thrown during transition execution
/// </summary>
[TestClass]
public class DifferentExceptions
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
	/// <image url="$(SolutionDir)\Documentation\Images\MultilevelExceptions.png" />
	/// </summary>
	/// <typeparam name="TException">Exception to throw on transition</typeparam>
	private void PrepareToThrow<TException>()
		where TException : Exception, new()
	{
		Machine.SuperState(States.S1, MemoryType.None).WithSubStates(States.S11, States.E1, States.S2);
		Machine.SuperState(States.S11, MemoryType.None).WithSubStates(States.S);
		Machine.SuperState(States.S2, MemoryType.None).WithSubStates(States.S22);
		Machine.SuperState(States.E1, MemoryType.None).WithSubStates(States.E);

		Machine.In(States.S)
			.Entry(Machine.EntryLogger)
			.Exit(Machine.ExitLogger)
			.On(UserAction.AnyEvent)
			.Goto(States.S22)
			.Execute(Machine.Throw<TException>)
			.WhenException<IOException>(States.E1)
			.WhenException(States.E2)
			;

		foreach (var state in new[] { States.S2, States.S1, States.S11, States.S22, States.E1, States.E, States.E2 })
		{
			Machine.In(state)
				.Entry(Machine.EntryLogger)
				.Exit(Machine.ExitLogger)
				;
		}
	}

	[TestMethod, Description("If IOException is thrown on transition, machine is directed to state E")]
	public async Task OnIOException_RedirectToStateE()
	{
		PrepareToThrow<FileNotFoundException>();

		await Prepare().ConfigureAwait(false);

		var result = await Machine.FireDirect(Action).ConfigureAwait(false);

		Assert.AreEqual(States.E, result);
		Assert.AreEqual("[-S][-S11][S#S22][+E1][+E]", Machine.PathHistory.ToString());
	}

	[TestMethod, Description("If other than IOException is thrown on transition, machine is directed to state E2")]
	public async Task OnOtherException_RedirectToStateE2()
	{
		PrepareToThrow<NotFiniteNumberException>();

		await Prepare().ConfigureAwait(false);

		var result = await Machine.FireDirect(Action).ConfigureAwait(false);

		Assert.AreEqual(States.E2, result, $"Not directed to state E2, but to state {result}");
		Assert.AreEqual("[-S][-S11][S#S22][-S1][+E2]", Machine.PathHistory.ToString());
	}
}
