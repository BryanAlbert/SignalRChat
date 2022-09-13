using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		public List<string> Devices { get; } = new List<string>();

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
						Dictionary<string, int> quizzed = new Dictionary<string, int>();
						Dictionary<string, int> correct = new Dictionary<string, int>();
						Dictionary<string, int> time = new Dictionary<string, int>();
						GetQuizzed(Users[0][1], kind, card, quizzed);
						GetCorrect(Users[0][1], kind, card, correct);
						GetTime(Users[0][1], kind, card, time);

						for (int index = 1; index < Users.Count; index++)
						{
							Card match = Users[index][1].Operators.FirstOrDefault(x => x.Name == kind.Name).
								Tables.FirstOrDefault(x => x.Base == table.Base).Cards.FirstOrDefault(x =>
								x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);

							if (!CheckCardTotals(Users[index][1], kind, card, match))
								return false;

							if (!GetQuizzed(Users[index][1], kind, card: match, quizzed: quizzed) ||
								!GetCorrect(Users[index][1], kind, match, correct) ||
								!GetTime(Users[index][1], kind, match, time))
							{
								return false;
							}
						}

						if (!CheckTotals(kind, card, quizzed.Sum(x => x.Value), correct.Sum(x => x.Value),
							time.Sum(x => x.Value)))
						{
							return false;
						}
					}
				}
			}

			return true;
		}

		private bool GetQuizzed(User user, OperatorTables kind, Card card, Dictionary<string, int> quizzed)
		{
			for (int index = 0; index < card.MergeQuizzed.Length; index++)
			{
				string device = user.MergeIndex.FirstOrDefault(x => x.Value == index).Key;
				if (!quizzed.ContainsKey(device))
				{
					quizzed[device] = card.MergeQuizzed[index];
					continue;
				}

				if (quizzed[device] != card.MergeQuizzed[index])
				{
					Console.WriteLine($"Error: MergeQuizzed mismatch for {device} in {user.FileName} for {kind.Name}," +
						$" card {card.Fact.First} by {card.Fact.Second}, should be {quizzed[device]} not {card.MergeQuizzed[index]}");
	
					return false;
				}
			}

			return true;
		}

		private bool GetCorrect(User user, OperatorTables kind, Card card, Dictionary<string, int> correct)
		{
			for (int index = 0; index < card.MergeCorrect.Length; index++)
			{
				string device = user.MergeIndex.FirstOrDefault(x => x.Value == index).Key;
				if (!correct.ContainsKey(device))
				{
					correct[device] = card.MergeCorrect[index];
					continue;
				}

				if (correct[device] != card.MergeCorrect[index])
				{
					Console.WriteLine($"Error: MergeCorrect mismatch for {device} in {user.FileName} for {kind.Name}," +
						$" card {card.Fact.First} by {card.Fact.Second}, should be {correct[device]} not {card.MergeCorrect[index]}");

					return false;
				}
			}

			return true;
		}

		private bool GetTime(User user, OperatorTables kind, Card card, Dictionary<string, int> time)
		{
			for (int index = 0; index < card.MergeTime.Length; index++)
			{
				string device = user.MergeIndex.FirstOrDefault(x => x.Value == index).Key;
				if (!time.ContainsKey(device))
				{
					time[device] = card.MergeTime[index];
					continue;
				}

				if (time[device] != card.MergeTime[index])
				{
					Console.WriteLine($"Error: MergeTime mismatch for {device} in {user.FileName} for {kind.Name}," +
						$" card {card.Fact.First} by {card.Fact.Second}, should be {time[device]} not {card.MergeTime[index]}");

					return false;
				}
			}

			return true;
		}

		private bool CheckCardTotals(User user, OperatorTables kind, Card card, Card match)
		{
			if (match.Quizzed != card.Quizzed)
			{
				Console.WriteLine($"Error: Quizzed mismatch in {user.FileName} for {kind.Name}," +
					$" card {match.Fact.First} by {match.Fact.Second}, Quizzed is {match.Quizzed} not {card.Quizzed}");

				return false;
			}

			if (match.Correct != card.Correct)
			{
				Console.WriteLine($"Error: Correct mismatch in {user.FileName} for {kind.Name}," +
					$" card {match.Fact.First} by {match.Fact.Second}, Correct is {match.Correct} not {card.Correct}");

				return false;
			}

			if (match.TotalTime != card.TotalTime)
			{
				Console.WriteLine($"Error: TotalTime mismatch in {user.FileName} for {kind.Name}," +
					$" card {match.Fact.First} by {match.Fact.Second}, Quizzed is {match.TotalTime} not {card.TotalTime}");

				return false;
			}

			return true;
		}

		private bool CheckTotals(OperatorTables kind, Card card, int quizzed, int correct, int time)
		{
			if (card.Quizzed != quizzed)
			{
				Console.WriteLine($"Error: MergeQuizzed total {quizzed} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Quizzed {card.Quizzed}.");

				return false;
			}

			if (card.Correct != correct)
			{
				Console.WriteLine($"Error: MergeCorrect total {correct} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Correct {card.Correct}.");

				return false;
			}

			if (card.TotalTime != time)
			{
				Console.WriteLine($"Error: MergeTotalTime total {time} does not match {kind.Name} card" +
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

				user[0].FileName = path;
				user[1].FileName = path + ".json";
				Devices.Add(user[1].DeviceId);

				Users.Add(user);
			}

			return true;
		}
	}
}
