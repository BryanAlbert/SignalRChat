using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class Card
	{
		public Card()
		{
		}

		public Card(Fact fact)
		{
			Fact = fact;
			Correct = Quizzed = 0;
		}


		public Fact Fact { get; set; }
		public int Quizzed { get; set; }
		public int Correct { get; set; }
		public int TotalTime { get; set; }
		public int BestTime { get => Quizzed > 0 ? m_bestTime : 0; set => m_bestTime = value; }
		public int[] MergeQuizzed { get; set; }
		public int[] MergeCorrect { get; set; }
		public int[] MergeTime { get; set; }

		[JsonIgnore]
		public string Render => Fact.Render;
		[JsonIgnore]
		public double Average => Quizzed > 0 ? (double) Correct / Quizzed : 0.0;
		[JsonIgnore]
		public int AverageTime => Quizzed > 0 ? TotalTime / Quizzed : 0;
		[JsonIgnore]
		public double Length => Correct > 0 ? (double) Quizzed / Correct : System.Math.Max(Quizzed, 1.0);


		private int m_bestTime = 0;
	}
}