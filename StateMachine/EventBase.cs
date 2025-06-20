using System;

namespace zorgoz.StateMachine
{
	/// <summary>
	/// Base class for machine trigger events
	/// </summary>
	public abstract class EventBase : IEquatable<EventBase>
	{
		/// <summary>
		/// A staticly instantiated AnyEvent
		/// </summary>
		public static readonly AnyEvent AnyEvent = new AnyEvent();

		/// <summary>
		/// The higher order events are evaluated later
		/// </summary>
		public int Order { get; protected set; }

		/// <summary>
		/// This method is called only if there is a need to compare two real events having the same exact type.
		/// Descendants can also implement universal triggers using it.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public abstract bool Equivalent(EventBase other);

		/// <summary>
		/// Equality check
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public virtual bool Equals(EventBase other) =>
			ReferenceEquals(this, other)
			|| (
				!(other is null)
				&& GetType().Equals(other.GetType())
				&& Equivalent(other)
			);

		/// <summary>
		/// Equality check
		/// </summary>
		/// <param name="one"></param>
		/// <param name="other"></param>
		/// <returns></returns>
		internal static bool Equals(EventBase one, EventBase other) =>
			ReferenceEquals(one, other)
			|| (
				!(one is null)
				&& !(other is null)
				&& one.Equals(other)
			);

		/// <summary>
		/// ToString implementation
		/// </summary>
		/// <returns></returns>
		public abstract override string ToString();
	}
}
