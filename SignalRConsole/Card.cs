namespace SignalRConsole
{
	public class Card
	{
		public Card()
		{
		}


		public Fact Fact { get; set; }
		public int Quizzed { get; set; }
		public int Correct { get; set; }
		public int TotalTime { get; set; }
		public int MergeQuizzed { get; set; }
		public int MergeCorrect { get; set; }
		public int MergeTime { get; set; }
		public int BestTime { get; set; }
	}
}