using System;
using System.Collections.Generic;

namespace SignalRConsole
{
	public class Operator
	{
		public string Name { get; set; }
		public FactOperator FactOperator { get; set; }
		public string Symbol { get; set; }
		public Func<int, int, Tuple<int, int>> Create { get; set; }
		public Func<int, int, int> Compute { get; set; }
		public Func<List<int>, Fact, bool> Filter { get; set; }
	}


	public enum FactOperator
	{
		None,
		Multiplication,
		Division,
		Addition,
		Subtraction
	}
}
