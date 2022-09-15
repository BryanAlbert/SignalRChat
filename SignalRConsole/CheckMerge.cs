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
						Console.WriteLine($"Checking {kind.Name} card {card.Fact.First} by {card.Fact.Second}");
						Dictionary<string, Tuple<int?, int?, int?, int?>> quizzed = new Dictionary<string, Tuple<int?, int?, int?, int?>>();
						_ = GetQuizzedData(Users[0], kind, card, quizzed, table.Base);

						Dictionary<string, int> correct = new Dictionary<string, int>();
						Dictionary<string, int> time = new Dictionary<string, int>();
						_ = GetCorrect(Users[0][1], kind, card, correct);
						_ = GetTime(Users[0][1], kind, card, time);

						for (int index = 1; index < Users.Count; index++)
						{
							Card match = Users[index][1].Operators.FirstOrDefault(x => x.Name == kind.Name).
								Tables.FirstOrDefault(x => x.Base == table.Base).Cards.FirstOrDefault(x =>
								x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);

							if (!CheckCardTotals(Users[index][1], kind, card, match) ||
								!GetQuizzedData(Users[index], kind, match, quizzed, table.Base) ||
								!GetCorrect(Users[index][1], kind, match, correct) ||
								!GetTime(Users[index][1], kind, match, time))
							{
								return false;
							}
						}

						if (!CheckNewCounts(card, quizzed) ||
							!CheckTotals(kind, card, quizzed, correct, time))
						{
							return false;
						}
					}
				}
			}

			return true;
		}

		private bool GetQuizzedData(User[] user, OperatorTables kind, Card card, Dictionary<string,
			Tuple<int?, int?, int?, int?>> quizzed, int tableBase)
		{
			Card initialCard = user[0].Operators.FirstOrDefault(x =>
				x.Name == kind.Name).Tables.FirstOrDefault(x =>
				x.Base == tableBase)?.Cards.FirstOrDefault(x =>
				x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);

			if (initialCard != null && !GetQuizzedData(user[0], kind, initialCard, quizzed, true, tableBase))
				return false;

			return GetQuizzedData(user[1], kind, card, quizzed, false, tableBase);
		}

		private bool GetQuizzedData(User user, OperatorTables kind, Card card, Dictionary<string,
			Tuple<int?, int?, int?, int?>> quizzed, bool initialFile = false, int? tableBase = null)
		{
			if (initialFile)
				UpdateDictionary(quizzed, user.DeviceId, card.Quizzed, null, null, null);
			else
				UpdateDictionary(quizzed, user.DeviceId, null, null, card.Quizzed, null);

			bool success = true;
			string device = "<not specified>";
			int index = 0;
			for (; index < card.MergeQuizzed?.Length; index++)
			{
				device = user.MergeIndex.FirstOrDefault(x => x.Value == index).Key;
				if (initialFile)
				{
					if (!quizzed.ContainsKey(device) || quizzed[device].Item2 == null)
					{
						UpdateDictionary(quizzed, device, null, card.MergeQuizzed[index], null, null);
						continue;
					}

					if (!(success = quizzed[device].Item2 == card.MergeQuizzed[index]))
						break;
				}
				else
				{
					if (!quizzed.ContainsKey(device) || quizzed[device].Item4 == null)
					{
						UpdateDictionary(quizzed, device, null, null, null, card.MergeQuizzed[index]);
						continue;
					}

					if (!(success = quizzed[device].Item4 == card.MergeQuizzed[index]))
						break;
				}
			}

			if (success)
				return true;

			Console.WriteLine($"Error: MergeQuizzed mismatch for {device} in {user.FileName} for {kind.Name}," +
				$" card {card.Fact.First} by {card.Fact.Second}, should be {quizzed[device].Item1} not {card.MergeQuizzed[index]}");

			return false;
		}

		private void UpdateDictionary(Dictionary<string, Tuple<int?, int?, int?, int?>> dictionary, string deviceId,
			int? initial, int? initialMerge, int? final, int? finalMerge)
		{
			if (!dictionary.ContainsKey(deviceId))
			{
				dictionary[deviceId] = new Tuple<int?, int?, int?, int?>(initial, initialMerge, final, finalMerge);
				return;
			}

			dictionary[deviceId] = new Tuple<int?, int?, int?, int?>(
				initial ?? dictionary[deviceId].Item1, initialMerge ?? dictionary[deviceId].Item2,
				final ?? dictionary[deviceId].Item3, finalMerge ?? dictionary[deviceId].Item4);
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

		private bool CheckTotals(OperatorTables kind, Card card, Dictionary<string, Tuple<int?, int?, int?, int?>> quizzed,
			Dictionary<string, int> correct, Dictionary<string, int> time)
		{
			int? sum = quizzed.Sum(x => x.Value.Item4);
			if (card.Quizzed != sum)
			{
				Console.WriteLine($"Error: MergeQuizzed total {sum} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Quizzed value {card.Quizzed}.");

				return false;
			}

			if (card.Correct != correct.Sum(x => x.Value))
			{
				Console.WriteLine($"Error: MergeCorrect total {correct} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Correct {card.Correct}.");

				return false;
			}

			if (card.TotalTime != time.Sum(x => x.Value))
			{
				Console.WriteLine($"Error: MergeTotalTime total {time} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} TotalTime {card.TotalTime}.");

				return false;
			}

			return true;
		}

		private bool CheckNewCounts(Card card, Dictionary<string, Tuple<int?, int?, int?, int?>> quizzed)
		{
			int? initial = quizzed.Sum(x => x.Value?.Item2);
			int total = initial.Value;
			Console.WriteLine($"Last Quizzed value is {initial} (computed from MergeQuizzed values), current Quizzed value is {card.Quizzed}");
			foreach (string deviceId in quizzed.Keys)
			{
				int added = (quizzed[deviceId]?.Item1 ?? 0) - initial.Value;
				total += added;
				Console.WriteLine($"Device {deviceId} added {added} quizzed {(added == 1 ? "card" : "cards")} and has quizzed" +
					$" {quizzed[deviceId].Item4} {(quizzed[deviceId].Item4 == 1 ? "card" : "cards")} total");

				if (added + (quizzed[deviceId]?.Item2 ?? 0) != quizzed[deviceId].Item4)
				{
					Console.WriteLine($"Error: starting value {quizzed[deviceId]?.Item2 ?? 0} plus the added value" +
						$" {added} does not match the MergeQuizzed value {quizzed[deviceId].Item4}");
				}
			}

			if (total != card.Quizzed)
			{
				Console.WriteLine($"Error: Card totals {total} don't add up to Quizzed value {card.Quizzed}");
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
