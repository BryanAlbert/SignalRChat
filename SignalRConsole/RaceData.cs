using System;

namespace SignalRConsole
{
	public class RaceData
	{
		public RaceData()
		{
			Try = 1;
			Busy = true;
		}

		public RaceData(Tuple<int, Card> card) : this()
		{
			Operator = (int) card.Item2.Fact.Operator.FactOperator;
			Base = card.Item1;
			First = card.Item2.Fact.First;
			Second = card.Item2.Fact.Second;
		}


		public void Reset()
		{
			Try = 1;
			QuizCount = 0;
			Correct = 0;
			Wrong = 0;
			Score = 0;
			Time = 0;
			Busy = true;
		}

		public void SetCard(RaceData source)
		{
			Operator = source.Operator;
			Base = source.Base;
			First = source.First;
			Second = source.Second;
			Try = source.Try;
			Score = source.Score;
			Time = source.Time;
			Busy = source.Busy;
			QuizCount = source.QuizCount;
		}


		public int Operator { get; set; }
		public int Base { get; set; }
		public int First { get; set; }
		public int Second { get; set; }
		public int Try { get; set; } = 1;
		public int QuizCount { get; set; }
		public int Correct { get; set; }
		public int Wrong { get; set; }
		public int Score { get; set; }
		public int Time { get; set; }
		public bool Busy { get; set; }
	}
}
