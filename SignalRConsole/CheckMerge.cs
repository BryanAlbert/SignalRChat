using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SignalRConsole
{
	public class CheckMerge
	{
		public CheckMerge(string folder, string handle)
		{
			Folder = folder;
			Handle = handle;
		}

		public string Folder { get; }
		public string Handle { get; }
		public List<User[]> Users { get; } = new List<User[]>();

		internal bool RunCheck()
		{
			if (!LoadTables())
				return false;

			foreach (OperatorTables kind in Users[0][1].Operators)
			{
				foreach (FactTable table in kind.Tables)
				{
					foreach (Card card in table.Cards)
					{
						int totalQuizzed = card.MergeQuizzed[0];
						int totalCorrect = card.MergeCorrect[0];
						int totalTime = card.MergeTime[0];

						for (int index = 1; index < Users.Count; index++)
						{
							Card match = Users[index][1].Operators.FirstOrDefault(x => x.Name == kind.Name).
								Tables.FirstOrDefault(x => x.Base == table.Base).Cards.FirstOrDefault(x =>
								x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);

							if (!CheckCardTotals(index, kind, card, match))
								return false;

							totalQuizzed += match.MergeQuizzed[0];
							totalCorrect += match.MergeCorrect[0];
							totalTime += match.MergeTime[0];
						}

						if (!CheckTotals(kind, card, totalQuizzed, totalCorrect, totalTime))
							return false;
					}
				}
			}

			return true;
		}

		private bool CheckCardTotals(int index, OperatorTables kind, Card card, Card match)
		{
			if (match.Quizzed != card.Quizzed)
			{
				Console.WriteLine($"Error: Quizzed mismatch in {Users[index][1].FileName} for {kind.Name}," +
					$" card {match.Fact.First} by {match.Fact.Second}, Quizzed is {match.Quizzed} not {card.Quizzed}");

				return false;
			}

			if (match.Correct != card.Correct)
			{
				Console.WriteLine($"Error: Correct mismatch in {Users[index][1].FileName} for {kind.Name}," +
					$" card {match.Fact.First} by {match.Fact.Second}, Correct is {match.Correct} not {card.Correct}");

				return false;
			}

			if (match.TotalTime != card.TotalTime)
			{
				Console.WriteLine($"Error: TotalTime mismatch in {Users[index][1].FileName} for {kind.Name}," +
					$" card {match.Fact.First} by {match.Fact.Second}, Quizzed is {match.TotalTime} not {card.TotalTime}");

				return false;
			}

			return true;
		}

		private bool CheckTotals(OperatorTables kind, Card card, int totalQuizzed, int totalCorrect, int totalTime)
		{
			if (card.Quizzed != totalQuizzed)
			{
				Console.WriteLine($"Error: MergeQuizzed total {totalQuizzed} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Quizzed {card.Quizzed}.");

				return false;
			}

			if (card.Correct != totalCorrect)
			{
				Console.WriteLine($"Error: MergeCorrect total {totalCorrect} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Correct {card.Correct}.");

				return false;
			}

			if (card.TotalTime != totalTime)
			{
				Console.WriteLine($"Error: MergeTotalTime total {totalTime} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} TotalTime {card.TotalTime}.");

				return false;
			}

			return true;
		}

		private bool LoadTables()
		{
			foreach (string directory in Directory.GetDirectories(Folder))
			{
				string path = Path.Combine(directory, $"{Handle}.qkr");
				Console.WriteLine($"Loading {path} and {Handle}.qkr.json...");
				User[] user = new User[]
				{
					JsonSerializer.Deserialize<User>(File.ReadAllText(path)),
					JsonSerializer.Deserialize<User>(File.ReadAllText(path + ".json"))
				};

				if (user[1].Operators.Any(x => x.Tables.Any(y => y.Cards.Any(z => z.MergeQuizzed == null))))
				{
					Console.WriteLine($"Error: {Handle}.qkr.json has a null MergeQuizzed value, did you run the test?");
					return false;
				}

				user[1].FileName = path + ".json";

				Users.Add(user);
			}

			return true;
		}
	}
}
