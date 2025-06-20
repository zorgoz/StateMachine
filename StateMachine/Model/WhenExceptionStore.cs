using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace zorgoz.StateMachine.Model
{
	internal class WhenExceptionStore<TState>
		where TState : Enum
	{
		private readonly OrderedDictionary store = new OrderedDictionary();

		public void AddOrReplace<ExceptionType>(TState state)
			where ExceptionType : Exception
			=> store[typeof(ExceptionType)] = state;

		public (Type exceptionType, TState state) GetMatching(Exception ex)
		{
			var found = store
					.OfType<DictionaryEntry>()
					.FirstOrDefault(x => ((Type)x.Key).IsAssignableFrom(ex.GetType()));

			return ((Type)found.Key, (TState)found.Value);
		}

		public WhenExceptionStore<TState> Clone()
		{
			var result = new WhenExceptionStore<TState>();

			foreach (DictionaryEntry x in store) result.store.Add(x.Key, x.Value);

			return result;
		}

		public int Count => store.Count;

		public bool HasTargetState(TState state) =>
					store
					.OfType<DictionaryEntry>()
					.Any(x => ((TState)x.Value).Equals(state));
	}
}
