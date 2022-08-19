using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static SignalRConsole.Commands;
using static SignalRConsole.Commands.ConnectionCommand;

namespace SignalRConsole
{
	public class ConsoleChat
	{
		public States State
		{
			get => m_state;
			set
			{
				if (value != States.Initializing)
				{
					Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
					m_console.SetCursorPosition(0, PromptLine);
					int padding = m_state == States.Initializing ? 0 : (m_state.ToString().Length) - value.ToString().Length + 2;
					if (value == States.Listening)
					{
						m_console.Write(padding > 0 ? $"{value}> {(padding > 0 ? new string(' ', padding) : "")}" : $"{value}> ");
						while (padding-- > 0)
							m_console.CursorLeft--;
					}
					else
					{
						m_console.Write($"{value}...{(padding > 0 ? new string(' ', padding) : "")}");
						m_console.SetCursorPosition(cursor.X, cursor.Y);
					}
				}

				m_state = value;
			}
		}


		public static SemaphoreSlim m_semaphoreSlim = new SemaphoreSlim(1, 1);


		public States PreviousState { get => m_states.Pop(); set => m_states.Push(value); }


		public async Task<int> RunAsync(Harness console)
		{
			m_console = console;
			if (!await StartServerAsync())
				return -1;

			if (!await LoadUsersAsync(m_console.NextArg()))
				return -2;

			InitializeCommands(m_hubConnection);
			await MonitorChannelsAsync();
			await MonitorFriendsAsync();

			DisplayMenu();

			while (true)
			{
				ConsoleKeyInfo menu = await ReadKeyAvailableAsync(() => State == States.Listening);
				try
				{
					switch (menu.Key)
					{
						case ConsoleKey.T:
							PrintTables();
							continue;
						case ConsoleKey.A:
							await AddFriendAsync();
							continue;
						case ConsoleKey.L:
							ListFriends();
							continue;
						case ConsoleKey.U:
							await UnfriendFriendAsync();
							continue;
						case ConsoleKey.C:
							await ChatFriendAsync();
							continue;
						case ConsoleKey.X:
							break;
						default:
							continue;
					}
				}
				catch (InvalidOperationException)
				{
					m_console.WriteLine("Disconnected from the server, reconnecting...");
					await StartServerAsync();
					DisplayMenu();
					continue;
				}
				catch (Exception exception)
				{
					State = States.Broken;
					ConsoleWriteLogLine($"Unfortunately, something broke. {exception.Message}");
					ConsoleWriteLogLine("\nFinished.");
					return -3;
				}

				break;
			}

			try
			{
				EraseLog();
				_ = MoveCursorToLog();
				foreach (Friend friend in m_user.Friends.Where(x => x.Blocked != true))
					await m_hubConnection.SendAsync(c_leaveChannel, friend.Id ?? MakeHandleChannelName(friend), Id);

				await m_hubConnection.SendAsync(c_leaveChannel, IdChannelName, Id);
				await m_hubConnection.SendAsync(c_leaveChannel, DeviceId, Id);
				await m_hubConnection.SendAsync(c_leaveChannel, HandleChannelName, Id);
				await m_hubConnection.SendAsync(c_leaveChannel, ChatChannelName, Id);
				await m_hubConnection.StopAsync();
			}
			catch (Exception exception)
			{
				m_console.WriteLine($"Exception shutting down: {exception.Message}");
			}
			finally
			{
				m_console.WriteLine("\nFinished.");
				m_console.Close();
			}

			return 0;
		}


		public const string c_register = "Register";
		public const string c_joinChannel = "JoinChannel";
		public const string c_joinedChannel = "JoinedChannel";
		public const string c_sendMessage = "SendMessage";
		public const string c_sentMessage = "SentMessage";
		public const string c_sendCommand = "SendCommand";
		public const string c_sentCommand = "SentCommand";
		public const string c_leaveChannel = "LeaveChannel";
		public const string c_leftChannel = "LeftChannel";
		public const string c_chatChannelName = "Chat";
		public const string c_delimiter = "\n";
		public const string c_fileExtension = ".qkr.json";
		public const string c_leaveChatCommand = "goodbye";

		public enum States
		{
			Initializing,
			Changing,
			Registering,
			Busy,
			Listening,
			Connecting,
			Chatting,
			Broken
		}


		private string RegistrationToken { get; set; }
		private string Handle => m_user?.Handle;
		private string Name => m_user?.Name;
		private string Id => m_user?.Id;
		private string DeviceId => m_user.DeviceId;
		private string Email => m_user?.Email;
		private string FileName => m_user.FileName;
		private string HandleChannelName => MakeHandleChannelName(Handle, Email);
		private string IdChannelName => m_user.Id;
		private string ChatChannelName => MakeChatChannelName(m_user);
		private string ActiveChatChannelName { get; set; }
		private Friend ActiveChatFriend { get; set; }
		private int NextLine { get; set; }
		private int PromptLine { get; set; }


		private void OnRegister(string token)
		{
			ConsoleColor color = m_console.ForegroundColor;
			m_console.ForegroundColor = ConsoleColor.Yellow;
			ConsoleWriteLogLine($"Registration token from server: {token}");
			m_console.ForegroundColor = color;
			RegistrationToken = token;
			State = States.Changing;
		}

		private async Task OnJoinedChannelAsync(string channel, string user)
		{
			if (user == Id)
				return;

			Friend friend = null;
			Debug.WriteLine($"{user} has joined the {string.Join('-', ParseChannelName(channel))} channel.");
			if (channel == IdChannelName || channel == HandleChannelName)
			{
				friend = m_user.Friends.FirstOrDefault(x => channel == IdChannelName ? x.Id == user : x.Handle == user);
			}
			else if (channel == ChatChannelName)
			{
				if (State == States.Listening)
				{
					await SendCommandAsync(CommandNames.Hello, Id, channel, user, m_user, true);
					ActiveChatChannelName = channel;
					ActiveChatFriend = m_user.Friends.FirstOrDefault(x => x.Id == user);
					_ = await MessageLoopAsync();
				}
				else
				{
					await SendCommandAsync(CommandNames.Hello, Id, channel, user, m_user, false);
				}

				return;
			}
			else
			{
				if (channel == user)
				{
					// a friend joined his own channel (and is now online)
					friend = m_user.Friends.FirstOrDefault(x => x.Id == channel);
				}
				else
				{
					string[] parts = ParseChannelName(channel);
					friend = m_user.Friends.FirstOrDefault(x => x.Email == parts[0] && x.Handle == parts[1]);
					if (friend != null && user == parts[1] && !friend.HelloInitiated)
					{
						// pending friend joined his Handle channel, we leave and rejoin to signal him to send Hello with null
						friend.HelloInitiated = true;
						await m_hubConnection.SendAsync(c_leaveChannel, channel, Id);
						await MonitorFriendAsync(friend);
					}

					// someone else joined a Handle channel we're monitoring, just ignore it
					return;
				}
			}

			if (friend?.HelloInitiated ?? false)
			{
				friend.HelloInitiated = false;
				return;
			}

			await SendCommandAsync(CommandNames.Hello, Id, channel, user, m_user, !friend?.Blocked);

			if (m_console.ScriptMode > 0)
			{
				// special messages for triggering while scripting
				if (friend == null)
					ConsoleWriteLogLine($"(Sent Hello null command to {user}.)");
				else if (friend.Blocked == true)
					ConsoleWriteLogLine($"(Sent Hello {!friend?.Blocked} command to {user}.)");
			}
		}

		private void OnSentMessage(string from, string message)
		{
			Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
			if (from == Id)
			{
				m_console.WriteLine($"You said: {message}");
			}
			else if (cursor.X == 0)
			{
				m_console.WriteLine($"{ActiveChatFriend.Handle} said: {message}");
			}
			else
			{
				m_console.SetCursorPosition(0, cursor.Y + 1);
				m_console.WriteLine($"{from} said: {message}");
				NextLine = m_console.CursorTop;
				m_console.SetCursorPosition(cursor.X, cursor.Y);
			}
		}

		private async Task OnSentCommandAsync(string from, string to, string json)
		{
			if (to == Id || to == Handle || to == DeviceId)
			{
				ConnectionCommand command = DeserializeCommand(json);
				await m_semaphoreSlim.WaitAsync();
				try
				{
					switch (command.CommandName)
					{
						case CommandNames.Hello:
							await HelloAsync(from, command);
							break;
						case CommandNames.Verify:
							await VerifyFriendAsync(from, command);
							break;
						case CommandNames.Merge:
							await MergeAccountsAsync(command, to);
							break;
						case CommandNames.Unrecognized:
						default:
							Debug.WriteLine($"Error in OnSentCommandAsync, unrecognized command: {command.CommandName}");
							break;
					}
				}
				finally
				{
					m_semaphoreSlim.Release();
				}
			} 
		}

		private async Task OnLeftChannelAsync(string channel, string user)
		{
			string[] parts = ParseChannelName(channel);
			Debug.WriteLine($"{user} has left the {string.Join('-', parts)} channel.");
			if (channel == IdChannelName)
			{
				Friend friend = m_user.Friends.FirstOrDefault(u => u.Id == user);
				if (friend != null && (!friend.Blocked ?? true))
				{
					ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Handle} is offline.");
					m_online.Remove(friend);
					if (ActiveChatFriend == friend)
					{
						await LeaveChatChannelAsync(send: true);
						DisplayMenu();
					}
				}
			}
		}

		private async Task<bool> StartServerAsync()
		{
			State = States.Initializing;
			m_console.WriteLine($"Initializing server and connecting to URL: {c_chatHubUrl}");
			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();

			_ = m_hubConnection.On<string>(c_register, (t) => OnRegister(t));
			_ = m_hubConnection.On(c_joinedChannel, (Action<string, string>) (async (c, u) => await OnJoinedChannelAsync(c, u)));
			_ = m_hubConnection.On(c_sentMessage, (Action<string, string>) ((f, m) => OnSentMessage(f, m)));
			_ = m_hubConnection.On(c_sentCommand, (Action<string, string, string>) (async (f, t, c) => await OnSentCommandAsync(f, t, c)));
			_ = m_hubConnection.On(c_leftChannel, (Action<string, string>) (async (c, u) => await OnLeftChannelAsync(c, u)));

			try
			{
				await m_hubConnection.StartAsync();
			}
			catch (Exception exception)
			{
				m_console.WriteLine($"Failed to connect to server (is the server running?), exception:" +
					$" {exception.Message}");
				State = States.Broken;
				return false;
			}

			return true;
		}

		private async Task<bool> LoadUsersAsync(string handle)
		{
			if (string.IsNullOrEmpty(handle))
			{
				if (!GetData("What is your handle (nickname)? ", out handle))
					return false;
			}
			else
			{
				m_console.WriteLine($"Hello {handle}!");
			}

			// if working folder is the executable folder, use .qkr.json extension so we don't try to load system files
			foreach (string fileName in Directory.EnumerateFiles(m_console.WorkingDirectory).
				Where(x => x.EndsWith(m_console.WorkingDirectory == "." ? c_fileExtension : "json")))
			{
				try
				{
					User user = JsonSerializer.Deserialize<User>(File.ReadAllText(fileName));
					user.FileName = fileName;
					m_users.Add(user);
				}
				catch (Exception exception)
				{
					m_console.WriteLine($"Exception loading {fileName}: {exception.Message}");
				}
			}

			m_user = m_users.FirstOrDefault(u => u.Handle == handle);
			if (m_user == null)
			{
				string email = await RegisterAsync();
				if (email == null)
					return false;

				if (!GetData("What is your name? ", out string name))
					return false;

				if (!GetData("What is your favorite color? ", out string color))
					return false;

				m_user = new User(handle, email, name, color, Path.Combine(m_console.WorkingDirectory, $"{handle}{c_fileExtension}"));
				m_users.Add(m_user);
				SaveUser();
			}
			else
			{
				if (m_user.Version != User.c_dataVersion)
					UdpateJson(m_user);
			}

			m_console.WriteLine($"Name: {Name}, Email: {Email}, Favorite color: {m_user.Color},");
			m_console.WriteLine($"Id: {Id}, Device Id: {m_user.DeviceId},");
			m_console.WriteLine($"Creation Date: {m_user.Created}, Modified Date: {m_user.Modified}");
			return true;
		}

		private void UdpateJson(User m_user)
		{
			// update no version to Version 1.0
			m_console.WriteLine($"{FileName} has Version {m_user.Version}, updating to version {User.c_dataVersion}");
			m_user.Version = User.c_dataVersion;
			if (m_user.DeviceId == null)
				m_user.DeviceId = Guid.NewGuid().ToString();

			if (m_user.Operators == null)
			{
				m_user.Operators = new List<OperatorTables>
				{
					new OperatorTables() { Name = "Addition", Tables = new List<FactTable>() },
					new OperatorTables() { Name = "Subtraction", Tables = new List<FactTable>() },
					new OperatorTables() { Name = "Multiplication", Tables = new List<FactTable>() },
					new OperatorTables() { Name = "Division", Tables = new List<FactTable>() }
				};
			}

			if (m_user.MergeIndex == null)
				m_user.MergeIndex = new Dictionary<string, int>();

			if (m_user.Color == null)
				m_user.Color = "White";

			if (m_user.Created == null)
				m_user.Created = File.GetLastWriteTimeUtc(FileName).ToString("s");

			if (m_user.Modified == null)
				m_user.Modified = File.GetCreationTimeUtc(FileName).ToString("s");

			SaveUser();
		}

		private async Task<string> RegisterAsync()
		{
			State = States.Registering;
			if (!GetData("What is your email address? ", out string email))
				return null;

			await m_hubConnection.SendAsync(c_register, email);
			if (!await WaitAsync("Waiting for the server"))
			{
				m_console.WriteLine("Timeout waiting for a response from the server, aborting.");
				return null;
			}

			State = States.Initializing;
			m_console.WriteLine("Enter the token returned from the server, Enter to abort: ");
			if (m_console.ScriptMode > 0)
			{
				m_console.WriteLine("(Script mode: skipping registration validation.)");
				return email;
			}

			string token;
			while (true)
			{
				token = m_console.ReadLine();
				if (token == RegistrationToken)
					return email;

				if (token == string.Empty)
					return null;

				m_console.WriteLine("Tokens do not match, please try again, Enter to abort: ");
			}
		}

		private async Task AddFriendAsync()
		{
			EraseLog();
			State = States.Busy;
			Point cursor = Point.Empty;
			while (true)
			{
				Point temp = ConsoleWriteLogRead("What is your friend's handle? ", out string handle);
				if (cursor.IsEmpty)
					cursor = temp;

				if (string.IsNullOrEmpty(handle))
					break;

				if (handle == Handle)
				{
					ConsoleWriteLogLine($"That's your handle!");
					continue;
				}

				Friend friend = m_user.Friends.FirstOrDefault(x => x.Handle == handle);
				if (friend != null)
				{
					if (!friend.Blocked.HasValue)
						ConsoleWriteLogLine($"You already asked {handle} to be your friend and we're waiting for a response.");
					else if (friend.Blocked.Value)
						ConsoleWriteLogLine($"You and {handle} are blocked. Both you and {handle} must unfriend before you can become friends.");
					else
						ConsoleWriteLogLine($"{handle} is already your friend!");

					break;
				}

				_ = ConsoleWriteLogRead("What is your friend's email? ", out string email);
				if (string.IsNullOrEmpty(email))
					break;

				friend = new Friend(handle, email);
				m_user.AddFriend(friend);
				SaveUser();
				ConsoleWriteLogLine($"A friend request has been sent to {handle}.");
				await MonitorFriendAsync(friend);
				break;
			}

			State = States.Listening;
			m_console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private void ListFriends()
		{
			EraseLog();
			if (m_user.Friends.Count == 0)
			{
				ConsoleWriteLogLine("You have no friends.");
				return;
			}

			ConsoleWriteLogLine("Friends:");
			foreach (Friend friend in m_user.Friends)
			{
				ConsoleWriteLogLine($"{friend.Handle},{(friend.Name != null ? $" {friend.Name}," : "")}" +
					$"{(friend.Email != null ? $" {friend.Email}," : "")}" +
					$"{(friend.Color != null ? $" {friend.Color}," : "")}" +
					$"{(friend.Created != null ? $" Created: {friend.Created}" :"")}" +
					$"{(friend.Modified != null ? $" Modified: {friend.Modified}" :"")}" +
					$"{(friend.Blocked.HasValue ? (friend.Blocked.Value ? " (blocked)" : "") : " (pending)")}" +
					$"{(m_online.Any(x => x.Id == friend.Id) ? " (online)" : "")}");
			}
		}

		private void PrintTables()
		{
			EraseLog();
			if (m_user.Operators.Any(x => x.Tables.Count > 0))
			{
				ConsoleWriteLogLine("Tables json:");
				ConsoleWriteLogLine(JsonSerializer.Serialize(m_user.Operators, m_serializerOptions));
				ConsoleWriteLogLine("MergeIndex json:");
				ConsoleWriteLogLine(JsonSerializer.Serialize(m_user.MergeIndex, m_serializerOptions));
			}
			else
			{
				ConsoleWriteLogLine($"{Handle} has no tables.");
			}
		}

		private async Task UnfriendFriendAsync()
		{
			EraseLog();
			if (m_user.Friends.Count == 0)
			{
				ConsoleWriteLogLine("You have no friends.");
				return;
			}

			State = States.Busy;
			Tuple<Point, Friend> result = await ChooseFriendAsync($"Whom would you like to unfriend" +
				$" (number, Enter to abort): ", m_user.Friends);

			if (result.Item2 != null)
			{
				await SendCommandAsync(CommandNames.Hello, Id, result.Item2.Id, result.Item2.Id, m_user, false);
				await UnfriendAsync(result.Item2);
				ConsoleWriteLogLine($"{result.Item2.Handle} has been unfriended.");
			}

			m_console.SetCursorPosition(result.Item1.X, result.Item1.Y);
			State = States.Listening;
		}

		private async Task ChatFriendAsync()
		{
			EraseLog();

			if (m_user.Friends.Count == 0)
			{
				ConsoleWriteLogLine("You have no friends.");
				return;
			}

			if (m_online.Count == 0)
			{
				ConsoleWriteLogLine("None of your friends is online.");
				return;
			}

			State = States.Busy;
			Tuple<Point, Friend> result = await ChooseFriendAsync("Whom would you like to chat with?" +
				" (number, Enter to abort): ", m_online);
			m_console.SetCursorPosition(result.Item1.X, result.Item1.Y);

			if (result.Item2 != null)
			{
				State = States.Connecting;
				ActiveChatChannelName = MakeChatChannelName(result.Item2);
				ActiveChatFriend = result.Item2;
				await m_hubConnection.SendAsync(c_joinChannel, ActiveChatChannelName, Id);
			}
			else
			{
				State = States.Listening;
			}
		}

		private async Task<bool> MessageLoopAsync()
		{
			EraseLog();
			State = States.Chatting;
			m_console.SetCursorPosition(0, NextLine);
			m_console.WriteLine($"Type messages, type '{c_leaveChatCommand}' to leave the chat.");
			bool success = true;
			while (true)
			{
				while (true)
				{
					if (State == States.Listening)
						return true;

					if (m_console.KeyAvailable)
						break;

					await Task.Delay(100);
				}

				NextLine = m_console.CursorTop;
				string message = m_console.ReadLine();
				if (m_waitForEnter)
				{
					m_waitForEnter = false;
					DisplayMenu();
					return true;
				}

				if (State != States.Chatting)
					return true;

				m_console.CursorTop = NextLine;

				try
				{
					if (message == c_leaveChatCommand)
					{
						await LeaveChatChannelAsync(send: true);
						break;
					}

					await m_hubConnection.SendAsync(c_sendMessage, Id, ActiveChatChannelName, message);
				}
				catch (Exception exception)
				{
					m_console.WriteLine($"Error sending message, exception: {exception.Message}");
					success = false;
					break;
				}
			}

			DisplayMenu();
			return success;
		}

		private async Task SendMergeAsync(Friend friend)
		{
			DateTime.TryParse(m_user.Created, out DateTime myCreated);
			DateTime.TryParse(friend.Created, out DateTime created);

			if (myCreated != created && !m_merged.Contains(friend.DeviceId))
			{
				ConsoleWriteLogLine($"{friend.Handle} is online on device {friend.DeviceId}, sending merge data...");
				m_merged.Add(friend.DeviceId);
				await SendMergeCommandAsync(Id, friend.Id, friend.DeviceId, m_user, null);
			}

			if (myCreated == DateTime.MinValue || created == DateTime.MinValue)
			{
				ConsoleWriteLogLine($"Error in CheckSendMerge, could not parse DateTime from {m_user.Created}" +
					$" and/or {friend.Created}");
			}
		}

		private async Task MergeAccountsAsync(ConnectionCommand merge, string to)
		{
			User user = merge.Merge;
			DateTime.TryParse(m_user.Created, out DateTime myCreated);
			DateTime.TryParse(user.Created, out DateTime created);
			if (myCreated == created || to != DeviceId)
				return;

			DateTime.TryParse(m_user.Modified, out DateTime myModified);
			DateTime.TryParse(user.Modified, out DateTime modified);
			bool updateId = m_user.Id != user.Id && myCreated > created;
			bool update = myModified < modified && (m_user.Name != user.Name || m_user.Handle != user.Handle ||
				m_user.Email != user.Email || m_user.Color != user.Color);

			if (updateId)
			{
				m_user.Id = user.Id;
				await m_hubConnection.SendAsync(c_joinChannel, IdChannelName, Id);
			}

			if (update)
			{
				m_user.Name = user.Name;
				m_user.Handle = user.Handle;
				m_user.Email = user.Email;
				m_user.Color = user.Color;
			}

			ConsoleWriteLogLine($"Merging data from {Handle}{(updateId ? $", Id {user.Id}," : "")} on device {user.DeviceId}...");
			m_merged.Add(user.DeviceId);
			update = MergeFriends(update, user);
			update = MergeTables(update, user);
			ConsoleWriteLogLine($"Merged data from {Handle} on device {user.DeviceId}.");
			if (update || updateId)
				SaveUser();
		}

		private bool MergeFriends(bool save, User user)
		{
			foreach (Friend friend in user.Friends)
			{
				Friend myFriend = m_user.Friends.FirstOrDefault(x => x.Id == friend.Id);
				if (myFriend == null)
				{
					m_user.AddFriend(friend);
					save = true;
				}
				else
				{
					if (DateTime.TryParse(myFriend.Modified, out DateTime myModified) &&
						DateTime.TryParse(friend.Modified, out DateTime modified))
					{
						if (myModified < modified)
						{
							m_user.Friends.Remove(myFriend);
							m_user.AddFriend(friend);
							save = true;
						}
					}
				}
			}

			return save;
		}

		private bool MergeTables(bool save, User user)
		{
			int myMergeIndex = GetMergeIndex(m_user.MergeIndex, user.DeviceId, ref save);
			int mergeIndex = GetMergeIndex(user.MergeIndex, m_user.DeviceId, ref save);

			foreach (OperatorTables math in user.Operators)
			{
				OperatorTables myOperator = m_user.Operators.FirstOrDefault(x => x.Name == math.Name);
				foreach (FactTable table in math.Tables)
				{
					FactTable myTable = myOperator.Tables.FirstOrDefault(x => x.Base == table.Base);
					if (myTable == null)
					{
						foreach (Card card in table.Cards)
							_ = MergeCards(save, card, mergeIndex, null, myMergeIndex);

						myOperator.Tables.Add(table);
						save = true;
					}
					else
					{
						foreach (Card card in table.Cards)
						{
							Card myCard = myTable.Cards.FirstOrDefault(x => x.Fact.First == card.Fact.First && x.Fact.Second == card.Fact.Second);
							if (myCard == null)
							{
								MergeCards(save, card, mergeIndex, null, myMergeIndex);
								myTable.Cards.Add(card);
								save = true;
							}
							else
							{
								save = MergeCards(save, card, mergeIndex, myCard, myMergeIndex);
								myCard.BestTime = card.BestTime == 0 || myCard.BestTime == 0 ?
									Math.Max(card.BestTime, myCard.BestTime) :
									Math.Min(card.BestTime, myCard.BestTime);
							}
						}
					}
				}
			}

			foreach (OperatorTables math in m_user.Operators)
				foreach (FactTable table in math.Tables)
					foreach (Card card in table.Cards.Where(x => (x.MergeQuizzed?.Length ?? 0) <= myMergeIndex))
						save = MergeCards(save, null, mergeIndex, card, myMergeIndex);

			return save;
		}

		private static bool MergeCards(bool save, Card card, int mergeIndex, Card myCard, int myMergeIndex)
		{
			if (card == null)
			{
				InitializeMergeProperties(myCard, myMergeIndex);
			}
			else if (myCard == null)
			{
				int quizzed = card.Quizzed - (card.MergeQuizzed?.Sum() ?? 0);
				int correct = card.Correct - (card.MergeCorrect?.Sum() ?? 0);
				int time = card.TotalTime - (card.MergeTime?.Sum() ?? 0);
				InitializeMergeProperties(card, myMergeIndex, true);
				card.Quizzed = card.MergeQuizzed[myMergeIndex] = quizzed;
				card.Correct = card.MergeCorrect[myMergeIndex] = correct;
				card.TotalTime = card.MergeTime[myMergeIndex] = time;
				save = true;
			}
			else
			{
				InitializeMergeProperties(card, mergeIndex);
				InitializeMergeProperties(myCard, myMergeIndex);
				int count = card.Quizzed - card.MergeQuizzed.Sum();
				myCard.Quizzed += count - myCard.MergeQuizzed[myMergeIndex];
				myCard.MergeQuizzed[myMergeIndex] = count;
				count = card.Correct - card.MergeCorrect.Sum();
				myCard.Correct += count - myCard.MergeCorrect[myMergeIndex];
				myCard.MergeCorrect[myMergeIndex] = count;
				count = card.TotalTime - card.MergeTime.Sum();
				myCard.TotalTime += count - myCard.MergeTime[myMergeIndex];
				myCard.MergeTime[myMergeIndex] = count;
				save = true;
			}

			return save;
		}

		private static void InitializeMergeProperties(Card card, int mergeIndex, bool clear = false)
		{
			if (card.MergeQuizzed == null || clear)
			{
				card.MergeQuizzed = new int[++mergeIndex];
				card.MergeCorrect = new int[mergeIndex];
				card.MergeTime = new int[mergeIndex];
			}
			else if (mergeIndex > (card.MergeQuizzed?.Length ?? 0) - 1)
			{
				card.MergeQuizzed = GrowArray(card.MergeQuizzed);
				card.MergeCorrect = GrowArray(card.MergeCorrect);
				card.MergeTime = GrowArray(card.MergeTime);
			}
		}

		private static int[] GrowArray(int[] mergeQuizzed)
		{
			int[] resized = new int[mergeQuizzed.Length + 1];
			for (int index = 0; index < mergeQuizzed.Length; index++)
				resized[index] = mergeQuizzed[index];

			return resized;
		}

		private int GetMergeIndex(Dictionary<string, int> ourMergeIndex, string theirDeviceId, ref bool save)
		{
			if (ourMergeIndex.ContainsKey(theirDeviceId))
				return ourMergeIndex[theirDeviceId];

			int mergeIndex = ourMergeIndex.Count > 0 ? ourMergeIndex.Values.Max() + 1 : 0;
			ourMergeIndex[theirDeviceId] = mergeIndex;
			save = true;
			return mergeIndex;
		}

		private async Task LeaveChatChannelAsync(bool send)
		{
			if (send)
			{
				await SendCommandAsync(CommandNames.Hello, Id, ActiveChatChannelName, ActiveChatFriend.Id, m_user, false);
			}

			if (ActiveChatChannelName != ChatChannelName)
				await m_hubConnection.SendAsync(c_leaveChannel, ActiveChatChannelName, Id);

			ActiveChatChannelName = null;
			ActiveChatFriend = null;
		}

		private void SaveUser()
		{
			m_user.Modified = DateTime.UtcNow.ToString("s");
			File.WriteAllText(FileName, JsonSerializer.Serialize(m_user, m_serializerOptions));
		}

		private async Task MonitorChannelsAsync()
		{
			await m_hubConnection.SendAsync(c_joinChannel, HandleChannelName, Handle);
			await m_hubConnection.SendAsync(c_joinChannel, IdChannelName, Id);
			await m_hubConnection.SendAsync(c_joinChannel, ChatChannelName, Id);
		}

		private async Task MonitorFriendsAsync()
		{
			foreach (Friend friend in m_user.Friends.Where(u => u.Blocked != true))
				await MonitorFriendAsync(friend);
		}

		private async Task MonitorFriendAsync(Friend friend)
		{
			if (!friend.Blocked.HasValue || friend.Id == null)
				await m_hubConnection.SendAsync(c_joinChannel, MakeHandleChannelName(friend), Handle);
			else
				await m_hubConnection.SendAsync(c_joinChannel, friend.Id, Id);
		}

		private async Task<Tuple<Point, Friend>> ChooseFriendAsync(string prompt, List<Friend> friends)
		{
			ConsoleWriteLogLine("Friends:");
			int index;
			for (index = 0; index < friends.Count; index++)
			{
				Friend friend = friends[index];
				ConsoleWriteLogLine($"{string.Format("{0:X}", index)}: {friend.Handle}");
			}

			Tuple<Point, ConsoleKeyInfo> result = await ConsoleWriteLogReadAsync(prompt);

			while (true)
			{
				if (result.Item2.Key == ConsoleKey.Enter)
					return new Tuple<Point, Friend>(result.Item1, null);

				Friend friend = int.TryParse(result.Item2.KeyChar.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
					out index) && index >= 0 && index < friends.Count ? friends[index] : null;

				if (friend != null)
					return new Tuple<Point, Friend>(result.Item1, friend);

				result = await ConsoleWriteLogReadAsync($"{result.Item2.KeyChar} not valid, enter a number between 1 and" +
					$" {Math.Min(friends.Count, 9)}, please try again: ");
			}
		}

		private async Task HelloAsync(string from, ConnectionCommand command)
		{
			if (State == States.Connecting)
			{
				if (command.Flag == true)
				{
					_ = await MessageLoopAsync();
				}
				else
				{
					await m_hubConnection.SendAsync(c_leaveChannel, ActiveChatChannelName, Id);
					ConsoleWriteLogLine($"{command.Racer.Handle} can't chat at the moment.");
					State = States.Listening;
				}
			}
			else if (State == States.Chatting && command.Flag == false)
			{
				if (m_console.CursorLeft > 0)
				{
					m_console.Write($" {ActiveChatFriend.Handle} has left the chat, hit Enter...");
					m_waitForEnter = true;
				}
				else
				{
					DisplayMenu();
				}

				await LeaveChatChannelAsync(send: false);
			}
			else
			{
				await CheckFriendshipAsync(from, command);
			}
		}

		private async Task CheckFriendshipAsync(string from, ConnectionCommand hello)
		{
			Tuple<Friend, Friend> update = UpdateFriendData(hello.Racer);
			Friend existing = update.Item1;
			Friend pending = update.Item2;
			if (from == Id || pending.Handle == Handle && pending.Name == Name && pending.Email == Email)
			{
				await SendMergeAsync(pending);
			}
			else if (hello.Flag == false || (!hello.Flag.HasValue && existing?.Blocked == false))
			{
				if (existing != null)
				{
					if (!existing.Blocked.HasValue)
					{
						ConsoleWriteLogLine($"{existing.Handle} has blocked you. {existing.Handle} must unfriend you" +
							$" before you can become friends.");
					}
					else if (existing.Blocked == false)
					{
						ConsoleWriteLogLine($"{existing.Handle} has unfriended you.");
						await UnfriendAsync(existing);
					}
				}

				return;
			}
			else if (!existing?.Blocked.HasValue ?? true)
			{
				if (hello.Flag == true)
				{
					if (existing == null)
					{
						// we unfriended him while he was away, send Hello with false
						await SendCommandAsync(CommandNames.Hello, Id, pending.Id, pending.Id, m_user, false);

						// special message for triggering while scripting
						if (m_console.ScriptMode > 0)
							ConsoleWriteLogLine($"(Sent unfriend command to {pending.Handle}.)");
					}
					else if (!existing.Blocked.HasValue)
					{
						// he accepted our friend request while we were away
						existing.Blocked = false;
						SaveUser();
						await SendCommandAsync(CommandNames.Hello, Id, existing.Id, existing.Id, m_user, true);
					}
				}
				else
				{
					await SendCommandAsync(CommandNames.Verify, Id, MakeHandleChannelName(existing ?? pending),
						from, m_user, null);
				}
			}

			if (existing != null && !m_online.Contains(existing))
			{
				ConsoleWriteLogLine($"Your {(existing.Blocked.HasValue ? "" : "(pending) ")}friend {existing.Handle} is online.");
				m_online.Add(existing);
				if (existing.Blocked == false)
					await SendCommandAsync(CommandNames.Hello, Id, existing.Id, existing.Id, m_user, true);
			}
		}

		private async Task VerifyFriendAsync(string from, ConnectionCommand verify)
		{
			Tuple<Friend, Friend> update = UpdateFriendData(verify.Racer);
			Friend existing = update.Item1;
			Friend pending = update.Item2;
			if (!verify.Flag.HasValue)
			{
				// add a Friends record to get notifications for him
				if (existing == null)
					m_user.AddFriend(pending);

				if (!m_online.Contains(pending))
					m_online.Add(pending);

				PreviousState = State;
				State = States.Busy;
				Tuple<Point, ConsoleKeyInfo> confirm = await ConsoleWriteLogReadAsync($"Accept friend request from" +
					$" {pending.Handle}, email address {pending.Email}? [y/n] ");
				State = PreviousState;
				pending.Blocked = confirm.Item2.Key != ConsoleKey.Y;
				m_user.Friends.Remove(existing);
				m_user.Friends.Remove(pending);
				m_user.AddFriend(pending);
				SaveUser();

				if (m_online.Contains(pending))
				{
					await SendCommandAsync(CommandNames.Verify, Id, HandleChannelName, from, m_user, !pending.Blocked);
				}

				if (!pending.Blocked ?? false)
					await m_hubConnection.SendAsync(c_joinChannel, verify.Racer);

				m_console.SetCursorPosition(confirm.Item1.X, confirm.Item1.Y);
				existing = pending;
			}
			else
			{
				if (existing != null)
					existing.Blocked = !verify.Flag;

				SaveUser();
				if (verify.Flag == false)
				{
					_ = m_online.Remove(existing);
					await m_hubConnection.SendAsync(c_leaveChannel, verify.Racer, Handle);
				}
			}

			if (existing != null)
				ConsoleWriteLogLine($"You and {existing.Handle} are {(existing.Blocked.Value ? "not" : "now")} friends!");
		}

		private async Task UnfriendAsync(Friend friend)
		{
			await m_hubConnection.SendAsync(c_leaveChannel, friend.Id ?? MakeHandleChannelName(friend), Id);
			_ = m_online.Remove(friend);
			_ = m_user.Friends.Remove(friend);
			SaveUser();
		}

		private Tuple<Friend, Friend> UpdateFriendData(Friend updated)
		{
			Friend friend = m_user.Friends.FirstOrDefault(x => x.Id == updated.Id) ??
				m_user.Friends.FirstOrDefault(x => x.Email == updated.Email && x.Handle == updated.Handle);

			if (friend != null)
			{
				if (friend.Handle != updated.Handle || friend.Name != updated.Name || friend.Color != updated.Color ||
					friend.Id != updated.Id || friend.Modified != updated.Modified)
				{
					friend.Handle = updated.Handle;
					friend.Email = updated.Email;
					friend.Name = updated.Name;
					friend.Color = updated.Color;
					friend.Id = updated.Id;
					friend.Created = updated.Created;
					friend.Modified = updated.Modified;
					SaveUser();
				}
			}

			return new Tuple<Friend, Friend>(friend, updated);
		}

		private async Task<bool> WaitAsync(string message, int intervalms = 100, int timeouts = 10)
		{
			TimeSpan interval = TimeSpan.FromMilliseconds(intervalms);
			DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(timeouts);
			m_console.Write(message);
			Point cursorPosition = new Point(m_console.CursorLeft, m_console.CursorTop);
			NextLine = cursorPosition.Y;
			char bullet = '.';
			for (int x = cursorPosition.X; State != States.Changing && DateTime.Now < timeout; x++)
			{
				if (x >= cursorPosition.X + 5)
				{
					bullet = bullet == '.' ? ' ' : '.';
					x = cursorPosition.X;
					m_console.SetCursorPosition(cursorPosition.X, cursorPosition.Y);
				}

				m_console.SetCursorPosition(x, cursorPosition.Y);
				m_console.Write(bullet);

				await Task.Delay(interval);
			}

			m_console.SetCursorPosition(0, NextLine + 1);
			return DateTime.Now < timeout;
		}

		private bool GetData(string prompt, out string data)
		{
			m_console.Write(prompt);
			data = m_console.ReadLine();
			return !string.IsNullOrEmpty(data);
		}

		private void DisplayMenu()
		{
			m_console.WriteLine("\nPick a command: t to list tables, a to add a friend, l to list friends,");
			m_console.WriteLine("u to unfriend a friend, c to chat, or x to exit");
			PromptLine = m_console.CursorTop;
			NextLine = PromptLine + 2;
			State = States.Listening;
		}

		private async Task<ConsoleKeyInfo> ReadKeyAvailableAsync(Func<bool> enable)
		{
			while (!enable() || !m_console.KeyAvailable)
				await Task.Delay(10);

			return m_console.ReadKey(intercept: true);
		}

		private void EraseLog()
		{
			if (m_console.ScriptMode > 1)
				return;

			Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
			NextLine = PromptLine + 2;
			Console.SetCursorPosition(0, NextLine);
			ConsoleColor color = m_console.ForegroundColor;
			Console.ForegroundColor = m_console.BackgroundColor;
			foreach (string line in m_log)
				Console.WriteLine(line);

			m_log.Clear();
			Console.ForegroundColor = color;
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private void ConsoleWriteLogLine(string line)
		{
			Point cursor = MoveCursorToLog();
			m_console.WriteLine(line);
			m_log.Add(line);
			m_console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private Point ConsoleWriteLogRead(string line, out string value)
		{
			Point cursor = MoveCursorToLog();
			m_console.Write(line);
			value = m_console.ReadLine();
			m_log.Add(line + value);
			return cursor;
		}

		private async Task<Tuple<Point, ConsoleKeyInfo>> ConsoleWriteLogReadAsync(string line)
		{
			Point cursor = MoveCursorToLog();
			m_console.Write(line);
			ConsoleKeyInfo confirm = await ReadKeyAvailableAsync(() => State != States.Listening);
			m_log.Add(line + confirm.KeyChar);
			return new Tuple<Point, ConsoleKeyInfo>(cursor, confirm);
		}

		private Point MoveCursorToLog()
		{
			Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
			m_console.SetCursorPosition(0, NextLine++);
			return cursor;
		}

		private string MakeHandleChannelName(Friend friend)
		{
			return $"{friend.Email}{c_delimiter}{friend.Handle}";
		}

		private string MakeHandleChannelName(string name, string email)
		{
			return $"{email}{c_delimiter}{name}";
		}

		private string MakeChatChannelName(User user)
		{
			return $"{user.Id}{c_delimiter}{c_chatChannelName}";
		}

		private string MakeChatChannelName(Friend friend)
		{
			return $"{friend.Id}{c_delimiter}{c_chatChannelName}";
		}

		private string[] ParseChannelName(string channelName)
		{
			return channelName.Split(c_delimiter);
		}


		private HubConnection m_hubConnection;
#if true
		private readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif

		private Harness m_console;
		private User m_user;
		private States m_state;
		private bool m_waitForEnter;
		private readonly List<User> m_users = new List<User>();
		private readonly List<Friend> m_online = new List<Friend>();
		private readonly List<string> m_log = new List<string>();
		private readonly Stack<States> m_states = new Stack<States>();
		private readonly List<string> m_merged = new List<string>();
		private readonly JsonSerializerOptions m_serializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true
		};
	}
}
