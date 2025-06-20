using System.Text;
using zorgoz.StateMachine;
using zorgoz.StateMachine.Observing;

namespace Tests.ExceptionHandling;

/// <summary>
/// Empty machine prepared with some generic methods to be passed to the prepared test instance 
/// </summary>
public class MyMachine : StateMachine<States, EventBase>
{
	public void EntryLogger(TransitionPathStep<States, EventBase> tr) => PathHistory.Append($"[+{tr.Target}]");

	public void ExitLogger(TransitionPathStep<States, EventBase> tr) => PathHistory.Append($"[-{tr.Target}]");

	public void ExecuteLogger(Transition<States, EventBase> tr) => PathHistory.Append($"[{tr.From}#{tr.To}]");

	public void ThrowsOnPayloadNotInt<ExceptionType>(Transition<States, EventBase> tr)
		where ExceptionType : Exception, new()
    {
		ExecuteLogger(tr);
		var s = int.TryParse((tr.On as UserAction).Payload, out var res);
		if (!s) throw new ExceptionType();
	}

	public void Throw<ExceptionType>(Transition<States, EventBase> tr)
		where ExceptionType : Exception, new()
	{
		ExecuteLogger(tr);
		throw new ExceptionType();
	}

	public readonly StringBuilder PathHistory = new StringBuilder();

	public void ClearHistory()
	{
		PathHistory.Clear();
	}

	public Task<States> FireDirect(EventBase evt) => base.Fire(evt);
}

public enum States
{
	S1 = 1,
	S2,
	E,
	E2,
	S11,
	S,
	S22,
	E1
}

public class UserAction : EventBase
{
	public UserAction() { }

	public UserAction(string payload)
	{
		Payload = payload;
	}

	public string Payload { get; set; } = string.Empty;

	public override bool Equivalent(EventBase other) => true;

	public override string ToString() => $"UserAction{(string.IsNullOrEmpty(Payload) ? "" : $"+{Payload}")}";
}
