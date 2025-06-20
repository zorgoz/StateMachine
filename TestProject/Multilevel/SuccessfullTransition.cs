using System.Reactive.Subjects;
using zorgoz.StateMachine.Fluent;
using zorgoz.StateMachine.Model;

namespace Tests.ExceptionHandling.Multilevel;

[TestClass]
public class SuccessfullTransition
{
	private Subject<UserAction> EventStream;
	private UserAction Action;
	private MyMachine Machine;

	[TestInitialize]
	public async Task Setup()
	{
		EventStream = new Subject<UserAction>();
		Action = new UserAction();
		Machine = new MyMachine();

        await Prepare().ConfigureAwait(false);
    }

	[TestCleanup]
	public void Stop()
	{
		Machine.Stop(true);
	}

	/// <summary>
	/// Machine is set up for following scenario
	/// <image url="$(SolutionDir)\Documentation\Images\MultilevelSuccess.png" />
	/// </summary>
	private async Task Prepare()
	{
        Machine.ClearHistory();

		Machine.SuperState(States.S1, MemoryType.None).WithSubStates(States.S11, States.E1, States.S2);
		Machine.SuperState(States.S11, MemoryType.None).WithSubStates(States.S);
		Machine.SuperState(States.S2, MemoryType.None).WithSubStates(States.S22);
		Machine.SuperState(States.E1, MemoryType.None).WithSubStates(States.E);

		Machine.In(States.S)
			.Entry(Machine.EntryLogger)
			.Exit(Machine.ExitLogger)
			.On(UserAction.AnyEvent)
			.Goto(States.S22)
			.Execute(Machine.ExecuteLogger)
			;

		foreach (var state in new[] { States.S2, States.S1, States.S11, States.S22, States.E1, States.E, States.E2 })
		{
			Machine.In(state)
				.Entry(Machine.EntryLogger)
				.Exit(Machine.ExitLogger)
				;
		}

		Machine.Initialize(States.S1, true);
		await Machine.Start(EventStream).ConfigureAwait(false);
	}

	[TestMethod, Description("Traverse multiple levels")]
	public async Task OnIOException_ReachingStateS22()
	{
		var result = await Machine.FireDirect(Action).ConfigureAwait(false);

		Assert.AreEqual(States.S22, result);
		Assert.AreEqual("[-S][-S11][S#S22][+S2][+S22]", Machine.PathHistory.ToString());
	}
}
