using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class Fact
	{
		public Fact()
		{
		}

		public Fact(RaceData raceData)
		{
			First = raceData.First;
			Second = raceData.Second;
		}

		public Fact(int first, int second, Operator factOperator)
		{
			System.Tuple<int, int> arguments = Arithmetic.Operator[factOperator.FactOperator].Create(first, second);
			First = arguments.Item1;
			Second = arguments.Item2;
			Operator = factOperator;
		}


		public int First { get; set; }
		public int Second { get; set; }

		[JsonIgnore]
		public Operator Operator { get; set; }
		[JsonIgnore]
		public string Render => $"{First} {Operator.Symbol} {Second}";
		[JsonIgnore]
		public int Answer => Arithmetic.Operator[Operator.FactOperator].Compute(First, Second);
	}
}