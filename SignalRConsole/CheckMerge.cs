using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SignalRConsole
{
	public class CheckMerge
	{
		public CheckMerge(string folder, string handle, string qkrAs, string qkrFolder)
		{
			Folder = folder;
			Handle = handle;
			QkrAs = qkrAs;
			QkrFolder = qkrFolder;
		}


		public string Folder { get; }
		public string QkrFolder { get; }
		public string Handle { get; }
		public string QkrAs { get; }
		public List<User[]> Users { get; } = new List<User[]>();
		public List<string> Devices { get; } = new List<string>();

		public bool RunCheck()
		{
			Console.WriteLine("\nRunning merge validity checks...");
			if (!LoadTables())
				return false;

			foreach (OperatorTables kind in Users[0][1].Operators)
			{
				foreach (FactTable table in kind.Tables)
				{
					foreach (Card card in table.Cards)
					{
						Console.WriteLine($"Checking {kind.Name} card {card.Fact.First} by {card.Fact.Second}");
						Dictionary<string, Tuple<int?, int?, int?, int?>> quizzed = new();
						Dictionary<string, Tuple<int?, int?, int?, int?>> correct = new();
						Dictionary<string, Tuple<int?, int?, int?, int?>> time = new();
						_ = GetData(Users[0], kind, card, (x) => x.Quizzed, "MergeQuizzed", (x, y) => x.MergeQuizzed[y], quizzed, table.Base);
						_ = GetData(Users[0], kind, card, (x) => x.Correct, "MergeCorrect", (x, y) => x.MergeCorrect[y], correct, table.Base);
						_ = GetData(Users[0], kind, card, (x) => x.TotalTime, "MergeTme", (x, y) => x.MergeTime[y], time, table.Base);

						for (int index = 1; index < Users.Count; index++)
						{
							Card match = Users[index][1].Operators.FirstOrDefault(x => x.Name == kind.Name).
								Tables.FirstOrDefault(x => x.Base == table.Base).Cards.FirstOrDefault(x =>
								x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);

							if (!CheckCardTotals(Users[index][1], kind, card, match) ||
								!GetData(Users[index], kind, match, (x) => x.Quizzed, "MergeQuizzed", (x, y) => x.MergeQuizzed[y], quizzed, table.Base) ||
								!GetData(Users[index], kind, match, (x) => x.Correct, "MergeCorrect", (x, y) => x.MergeCorrect[y], correct, table.Base) ||
								!GetData(Users[index], kind, match, (x) => x.TotalTime, "MergeTime", (x, y) => x.MergeTime[y], time, table.Base))
							{
								return false;
							}
						}

						if (!CheckTotals(kind, card, quizzed, correct, time) ||
							!CheckNewCounts(card, (x) => x.Quizzed, "Quizzed", "MergeQuizzed", "quizzed card", quizzed) ||
							!CheckNewCounts(card, (x) => x.Correct, "Correct", "MergeCorrect", "correct card", correct) ||
							!CheckNewCounts(card, (x) => x.TotalTime, "TotalTime", "MergeTime", "second", time))
						{
							return false;
						}
					}
				}
			}

			return true;
		}


		private static bool GetData(User[] user, OperatorTables kind, Card card, Func<Card, int> cardData, string mergeTitle,
			Func<Card, int, int> mergeData, Dictionary<string, Tuple<int?, int?, int?, int?>> data, int tableBase)
		{
			Card initialCard = user[0].Operators.FirstOrDefault(x =>
				x.Name == kind.Name).Tables.FirstOrDefault(x =>
				x.Base == tableBase)?.Cards.FirstOrDefault(x =>
				x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);

			if (initialCard != null && !GetData(user[0], kind, initialCard, cardData, mergeTitle, mergeData, data, true))
				return false;

			return GetData(user[1], kind, card, cardData, mergeTitle, mergeData, data, false);
		}

		private static bool GetData(User user, OperatorTables kind, Card card, Func<Card, int> cardData, string mergeTitle,
			Func<Card, int, int> mergeData, Dictionary<string, Tuple<int?, int?, int?, int?>> data, bool initialFile)
		{
			if (initialFile)
				UpdateDictionary(data, user.DeviceId, cardData(card), null, null, null);
			else
				UpdateDictionary(data, user.DeviceId, null, null, cardData(card), null);

			bool success = true;
			string device = "<not specified>";
			int index = 0;
			for (; index < card.MergeQuizzed?.Length; index++)
			{
				device = user.MergeIndex.FirstOrDefault(x => x.Value == index).Key;
				if (initialFile)
				{
					if (!data.ContainsKey(device) || data[device].Item2 == null)
					{
						UpdateDictionary(data, device, null, mergeData(card, index), null, null);
						continue;
					}

					if (!(success = data[device].Item2 == mergeData(card, index)))
						break;
				}
				else
				{
					if (!data.ContainsKey(device) || data[device].Item4 == null)
					{
						UpdateDictionary(data, device, null, null, null, mergeData(card, index));
						continue;
					}

					if (!(success = data[device].Item4 == mergeData(card, index)))
						break;
				}
			}

			if (success)
				return true;

			Console.WriteLine($"Error: {mergeTitle} mismatch for {device} in {user.FileName} for {kind.Name}, card" +
				$" {card.Fact.First} by {card.Fact.Second}, expected {(initialFile ? data[device].Item2 : data[device].Item4)}" +
				$" instead of {mergeData(card, index)}");

			return false;
		}

		private static void UpdateDictionary(Dictionary<string, Tuple<int?, int?, int?, int?>> dictionary, string deviceId,
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

		private static bool CheckCardTotals(User user, OperatorTables kind, Card card, Card match)
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

		private static bool CheckTotals(OperatorTables kind, Card card, Dictionary<string, Tuple<int?, int?, int?, int?>> quizzed,
			Dictionary<string, Tuple<int?, int?, int?, int?>> correct, Dictionary<string, Tuple<int?, int?, int?, int?>> time)
		{
			int? sum = quizzed.Sum(x => x.Value.Item4);
			if (card.Quizzed != sum)
			{
				Console.WriteLine($"Error: MergeQuizzed total {sum} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Quizzed value {card.Quizzed}.");

				return false;
			}

			sum = correct.Sum(x => x.Value.Item4);
			if (card.Correct != sum)
			{
				Console.WriteLine($"Error: MergeCorrect total {sum} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} Correct {card.Correct}.");

				return false;
			}

			sum = time.Sum(x => x.Value.Item4);
			if (card.TotalTime != sum)
			{
				Console.WriteLine($"Error: MergeTotalTime total {sum} does not match {kind.Name} card" +
					$" {card.Fact.First} by {card.Fact.Second} TotalTime {card.TotalTime}.");

				return false;
			}

			return true;
		}

		private static bool CheckNewCounts(Card card, Func<Card, int> cardData, string type, string mergeType, string noun,
			Dictionary<string, Tuple<int?, int?, int?, int?>> data)
		{
			int? initial = data.Sum(x => x.Value?.Item2);
			int total = initial.Value;
			Console.WriteLine($"Previous {type} value is {initial} (computed from {mergeType} values)," +
				$" current {type} value is {cardData(card)}");

			foreach (string deviceId in data.Keys)
			{
				int added = (data[deviceId]?.Item1 ?? 0) - initial.Value;
				total += added;
				Console.WriteLine($"Device {deviceId} added {added} {noun}{(added == 1 ? "" : "s")} and has" +
					$" {data[deviceId].Item4} {noun}{(data[deviceId].Item4 == 1 ? "" : "s")} total");

				if (added + (data[deviceId]?.Item2 ?? 0) != data[deviceId].Item4)
				{
					Console.WriteLine($"Error: {type} starting value {data[deviceId]?.Item2 ?? 0} plus the added value" +
						$" {added} does not match the {mergeType} value {data[deviceId].Item4}");
					return false;
				}
			}

			if (total != cardData(card))
			{
				Console.WriteLine($"Error: initial values plus added {noun}s is {total} which doesn't add up to {type} value {cardData(card)}");
				return false;
			}

			return true;
		}


		private bool LoadTables()
		{
			bool qkrMatch = false;
			foreach (string directory in Directory.GetDirectories(Folder))
			{
				string initialPath = null;
				string finalPath = null;
				string qkrAs = QkrAs?.ToLower();
				if (Path.GetFileName(directory).ToLower() == qkrAs)
				{
					qkrMatch = true;
					foreach (string file in Directory.GetFiles(directory))
					{
						if (Path.GetExtension(file) == ".qkr")
						{
							string subFilename = Path.GetFileNameWithoutExtension(file);
							string subSubFilename = Path.GetFileNameWithoutExtension(subFilename);
							if (subSubFilename == Handle)
								continue;

							if (Path.GetExtension(subFilename) == ".control")
								finalPath = Path.Combine(QkrFolder, $"{subSubFilename}.json");
							else
								initialPath = file;

							if (initialPath != null && finalPath != null)
								break;
						}
					}
				}
				else
				{
					initialPath = Path.Combine(directory, $"{Handle}.qkr");
					finalPath = initialPath + ".json";
				}

				if (initialPath == null || finalPath == null)
					throw new InvalidOperationException("Failed to identify initial and final json filenames.");

				Console.WriteLine($"Loading {initialPath} and {finalPath}...");
				User[] user = new User[]
				{
					JsonSerializer.Deserialize<User>(File.ReadAllText(initialPath)),
					JsonSerializer.Deserialize<User>(File.ReadAllText(finalPath))
				};

				if (user[1].Operators.Any(x => x.Tables.Any(y => y.Cards.Any(z => z.MergeQuizzed == null))))
				{
					Console.WriteLine($"Error: {Handle}.qkr.json has a null MergeQuizzed value, did you run the test?");
					return false;
				}

				user[0].FileName = initialPath;
				user[1].FileName = finalPath;
				Devices.Add(user[1].DeviceId);

				Users.Add(user);
			}

			if (QkrAs == null || qkrMatch)
				return true;

			Console.WriteLine($"Error: Folder match for -kqr folder {QkrAs} not found.");
			return false;
		}
	}
}
