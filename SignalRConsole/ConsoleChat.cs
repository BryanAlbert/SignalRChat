using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static SignalRConsole.Commands;
using static SignalRConsole.Commands.ConnectionCommand;

namespace SignalRConsole
{
	public class ConsoleChat
	{
		public ConsoleChat()
		{
			MyRaceData = new RaceData();
			OpponentRaceData = new RaceData();
		}


		public static int GetDigitCount(double number)
		{
			return (int) Math.Log10(Math.Max(number, 1.0)) + 1;
		}


		private static readonly SemaphoreSlim m_commandSemaphore = new SemaphoreSlim(1, 1);
		private static readonly SemaphoreSlim m_consoleSemaphore = new SemaphoreSlim(1, 1);
		private static readonly SemaphoreSlim m_loopSemaphore = new SemaphoreSlim(1, 1);


		public States State
		{
			get => m_state;
			set
			{
				if (value != States.Initializing && PromptLine >= 0)
				{
					try
					{
						m_consoleSemaphore.Wait();
						Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
						m_console.SetCursorPosition(0, PromptLine);
						int padding = m_state == States.Initializing ? 0 : (m_stateLabels[m_state].Length) -
							m_stateLabels[value].Length + 2;

						if (value == States.Listening)
						{
							m_console.Write(padding > 0 ? $"{m_stateLabels[value]}>" +
								$" {(padding > 0 ? new string(' ', padding) : "")}" : $"{value}> ");
							while (padding-- > 0)
								m_console.CursorLeft--;
						}
						else
						{
							m_console.Write($"{m_stateLabels[value]}...{(padding > 0 ? new string(' ', padding) : "")}");
							m_console.SetCursorPosition(cursor.X, cursor.Y);
						}
					}
					finally
					{
						_ = m_consoleSemaphore.Release();
					}
				}

				m_state = value;
			}
		}


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
					await WaitWriteLineAsync("Disconnected from the server, reconnecting...");
					_ = await StartServerAsync();
					DisplayMenu();
					continue;
				}
				catch (Exception exception)
				{
					State = States.Broken;
					await WaitWriteLineAsync($"Unfortunately, something broke. {exception.Message}");
					await WaitWriteLineAsync("Finished.");
					return -3;
				}

				break;
			}

			try
			{
				try
				{
					m_consoleSemaphore.Wait();
					WriteLine();
					EraseLog();
				}
				finally
				{
					_ = m_consoleSemaphore.Release();
				}

				foreach (Friend friend in m_user.Friends.Where(x => x.Blocked != true))
					await m_hubConnection.SendAsync(c_leaveChannel, friend.Id ?? MakeHandleChannelName(friend), Id);

				// if we change our Id in a merge, it leaves us listening to the DeviceId channel, so leave that channel
				await m_hubConnection.SendAsync(c_leaveChannel, DeviceId, Id);
				await m_hubConnection.SendAsync(c_leaveChannel, IdChannelName, Id);
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
				m_console.WriteLine("Finished.");
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
		public const string c_leaveChatCommand = "goodbye.";
		public const string c_raceCommand = "race!";
		public readonly Dictionary<States, string> m_stateLabels = new Dictionary<States, string>
		{
			{ States.Initializing, "Initializing" },
			{ States.Changing, "Changing" },
			{ States.Registering, "Registering" },
			{ States.Busy, "Busy" },
			{ States.Listening, "Listening" },
			{ States.Connecting, "Connecting" },
			{ States.Chatting, "Chatting" },
			{ States.Broken, "Broken" },
			{ States.FriendAway, "Friend Away" },
			{ States.RaceInitializing, "Race Initializing" },
			{ States.Racing, "Racing" },
			{ States.RacingWaiting, "Racing" },
			{ States.Resetting, "Resetting" }
		};

		public enum States
		{
			Initializing,
			Changing,
			Registering,
			Busy,
			Listening,
			Connecting,
			Chatting,
			Broken,
			FriendAway,
			RaceInitializing,
			Racing,
			RacingWaiting,
			Resetting
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
		private bool HaveTables => m_user.Operators.Any(x => x.Tables.Any(y => y.Cards.Count > 0));
		private bool HaveIntersection { get; set; }
		private bool IsStateRacing => State == States.RaceInitializing || State == States.Racing || State == States.RacingWaiting;
		private bool IsRaceLeader { get; set; }
		private int PromptLine { get; set; }
		private int ScoreboardLine { get; set; }
		private int OutputLine { get; set; }
		private int LogTop { get; set; }
		private int LogBottom { get; set; }
		private bool Interrupt { get; set; }
		private States PreviousState { get => m_states.Pop(); set => m_states.Push(value); }
		private bool ShowCountdown { get; set; }
		private DateTime ExpireTime { get; set; }
		private int TimeLeft { get; set; }
		private string AnswerPrompt { get; set; }
		private int MyRaceScore
		{
			get => m_myRaceScore;
			set
			{
				m_myRaceScore = value;
				UpdateScoreboard();
			}
		}

		private int OpponentRaceScore
		{
			get => m_opponentRaceScore;
			set
			{
				m_opponentRaceScore = value;
				UpdateScoreboard();
			}
		}

		private RaceData MyRaceData
		{
			get => m_myRaceData;
			set
			{
				m_myRaceData = value;
				UpdateScoreboard();
			}
		}

		private RaceData OpponentRaceData
		{
			get => m_opponentRaceData;
			set
			{
				m_opponentRaceData = value;
				UpdateScoreboard();
			}
		}

		private Dictionary<string, List<int>> MyTableList
		{
			get
			{
				if (m_myTables == null)
				{
					m_myTables = new Dictionary<string, List<int>>();
					m_user.Operators.ForEach(o => MyTableList[o.Name] = o.Tables.Select(t => t.Base).ToList());
				}

				return m_myTables;
			}
		}

		private Dictionary<string, List<int>> OpponentTableList { get; set; }


		private void OnRegister(string token)
		{
			ConsoleColor color = ConsoleColor.Gray;
			try
			{
				m_consoleSemaphore.Wait();
				color = m_console.ForegroundColor;
				m_console.ForegroundColor = ConsoleColor.Yellow;
				WriteLogLine($"Registration token from server: {token}");
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
				m_console.ForegroundColor = color;
			}

			RegistrationToken = token;
			State = States.Changing;
		}

		private async Task OnChannelJoinedAsync(string channel, string user)
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
					// chat requested, signal ready to chat, he'll respond with the TableList command
					IsRaceLeader = false;
					await SendCommandAsync(CommandNames.Hello, Id, channel, user, m_user, true);
					ActiveChatChannelName = channel;
					ActiveChatFriend = m_user.Friends.FirstOrDefault(x => x.Id == user);
				}
				else
				{
					// can't chat right now
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

			if (channel != Id || friend?.Blocked != false)
			{
				// don't send Hello true on our own channel (it will be sent on the friend's channel)
				await SendCommandAsync(CommandNames.Hello, Id, channel, user, m_user, !friend?.Blocked);
			}

			if (m_console.ScriptMode > 0)
			{
				// special output for triggering while scripting
				if (friend == null)
					WaitWriteLogLine($"(Sent Hello null command to {user}.)");
				else if (friend.Blocked == true)
					WaitWriteLogLine($"(Sent Hello {!friend?.Blocked} command to {user}.)");
			}
		}

		private void OnMessageReceived(string from, string message)
		{
			try
			{
				m_consoleSemaphore.Wait();
				if (from == Id)
				{
					m_console.CursorTop = OutputLine;
					WriteLine($"You said: {message}");
					Interrupt = false;
				}
				else if (Console.CursorLeft == 0)
				{
					WriteLine($"{ActiveChatFriend.Handle} said: {message}");
				}
				else
				{
					Point cursor;
					if (Interrupt)
					{
						cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
					}
					else
					{
						Interrupt = true;
						cursor = TrimLog();
						OutputLine++;
					}

					cursor.Y -= Console.CursorTop - CheckScrollWindow(OutputLine).Y;
					m_console.SetCursorPosition(0, OutputLine);
					WriteLine($"{ActiveChatFriend.Handle} said: {message}");
					m_console.SetCursorPosition(cursor.X, cursor.Y);
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private async Task OnCommandReceivedAsync(string from, string to, string json)
		{
			if (to == Id || to == Handle || to == DeviceId)
			{
				ConnectionCommand command = DeserializeCommand(json);
				if (Echo != null || command.CommandName == CommandNames.Echo && command.Flag == true)
					WriteLogLine($"{ActiveChatFriend?.Handle ?? from}: {json}");

				await m_commandSemaphore.WaitAsync();
				try
				{
					switch (command.CommandName)
					{
						case CommandNames.Away:
							ProcessAwayCommand();
							break;
						case CommandNames.TableList:
							await ProcessTableListCommandAsync(from, command.Tables);
							break;
						case CommandNames.InitiateRace:
							await ProcessInitiateRaceCommandAsync();
							break;
						case CommandNames.StartRace:
							await ProcessStartRaceCommandAsync();
							break;
						case CommandNames.RaceCard:
							_ = await ProcessRaceCardCommandAsync(command.RaceData);
							break;
						case CommandNames.CardResult:
							ProcessCardResultCommand(command.RaceData);
							break;
						case CommandNames.Reset:
							await ProcessResetCommandAsync();
							break;
						case CommandNames.NavigateBack:
							await ProcessNavigateBackCommandAsync();
							break;
						case CommandNames.Hello:
							await ProcessHelloCommandAsync(from, command);
							break;
						case CommandNames.Verify:
							await ProcessVerifyCommandAsync(from, command);
							break;
						case CommandNames.Merge:
							await ProcessMergeCommandAsync(command, to);
							break;
						case CommandNames.Echo:
							Echo = command.Flag == true ? ((x) => WriteLogLine($"{Handle}: {x}")) : (Action<string>) null;
							break;
						case CommandNames.Unrecognized:
						default:
							Debug.WriteLine($"Error in OnSentCommandAsync, unrecognized command: {command.CommandName}");
							break;
					}
				}
				finally
				{
					if (m_commandSemaphore.CurrentCount == 0)
						_ = m_commandSemaphore.Release();
				}
			}
		}

		private async Task OnChannelLeftAsync(string channel, string user)
		{
			string[] parts = ParseChannelName(channel);
			Debug.WriteLine($"{user} has left the {string.Join('-', parts)} channel.");
			if (channel == IdChannelName)
			{
				Friend friend = m_user.Friends.FirstOrDefault(u => u.Id == user);
				if (friend != null && friend.Blocked != true)
				{
					try
					{
						await m_consoleSemaphore.WaitAsync();
						WriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Handle} is offline.");
						_ = m_online.Remove(friend);
						if (ActiveChatFriend == friend)
						{
							await LeaveChatChannelAsync(sendHello: true);
							DisplayMenu();
						}
					}
					finally
					{
						_ = m_consoleSemaphore.Release();
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
			_ = m_hubConnection.On(c_joinedChannel, (Action<string, string>) (async (c, u) => await OnChannelJoinedAsync(c, u)));
			_ = m_hubConnection.On(c_sentMessage, (Action<string, string>) ((f, m) => OnMessageReceived(f, m)));
			_ = m_hubConnection.On(c_sentCommand, (Action<string, string, string>) (async (f, t, c) => await OnCommandReceivedAsync(f, t, c)));
			_ = m_hubConnection.On(c_leftChannel, (Action<string, string>) (async (c, u) => await OnChannelLeftAsync(c, u)));

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
				if (m_user.DataVersion != User.c_dataVersion)
					UpgradeJson(m_user);
			}

			m_console.WriteLine($"Name: {Name}, Email: {Email}, Favorite color: {m_user.Color},");
			m_console.WriteLine($"Id: {Id},");
			m_console.WriteLine($"Device Id: {m_user.DeviceId},");
			m_console.WriteLine($"Creation Date: {m_user.Created}, Modified Date: {m_user.Modified}");
			return true;
		}

		private void UpgradeJson(User m_user)
		{
			// upgrade old version to Version 1.1
			m_console.WriteLine($"{FileName} has DataVersion {m_user.DataVersion}, updating to {User.c_dataVersion}");
			m_user.DataVersion = User.c_dataVersion;
			m_user.DeviceId ??= Guid.NewGuid().ToString();
			m_user.Operators ??= new List<OperatorTables>
			{
				new OperatorTables() { Name = "Addition", Tables = new List<FactTable>() },
				new OperatorTables() { Name = "Subtraction", Tables = new List<FactTable>() },
				new OperatorTables() { Name = "Multiplication", Tables = new List<FactTable>() },
				new OperatorTables() { Name = "Division", Tables = new List<FactTable>() }
			};

			m_user.MergeIndex ??= new Dictionary<string, int>();
			m_user.Color ??= "White";
			m_user.Created ??= File.GetLastWriteTimeUtc(FileName).ToString("s");
			m_user.Modified ??= File.GetCreationTimeUtc(FileName).ToString("s");

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
			State = States.Busy;
			try
			{
				m_consoleSemaphore.Wait();
				EraseLog();
				while (true)
				{
					WriteLogRead("What is your friend's handle? ", out string handle);
					if (string.IsNullOrEmpty(handle))
						break;

					if (handle == Handle)
					{
						WriteLogLine($"That's your handle!");
						continue;
					}

					Friend friend = m_user.Friends.FirstOrDefault(x => x.Handle == handle);
					if (friend != null)
					{
						if (!friend.Blocked.HasValue)
							WriteLogLine($"You already asked {handle} to be your friend and we're waiting for a response.");
						else if (friend.Blocked.Value)
							WriteLogLine($"You and {handle} are blocked. Both you and {handle} must unfriend before you can become friends.");
						else
							WriteLogLine($"{handle} is already your friend!");

						break;
					}

					WriteLogRead("What is your friend's email? ", out string email);
					if (string.IsNullOrEmpty(email))
						break;

					friend = new Friend(handle, email);
					m_user.Friends.Add(friend);
					SaveUser();
					WriteLogLine($"A friend request has been sent to {handle}.");
					await MonitorFriendAsync(friend);
					break;
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			State = States.Listening;
		}

		private void ListFriends()
		{
			try
			{
				m_consoleSemaphore.Wait();
				EraseLog();
				if (m_user.Friends.Count == 0)
				{
					WriteLogLine("You have no friends.");
					return;
				}

				WriteLogLine("Friends:");
				foreach (Friend friend in m_user.Friends)
				{
					WriteLogLine($"{friend.Handle},{(friend.Name != null ? $" {friend.Name}," : "")}" +
						$"{(friend.Email != null ? $" {friend.Email}," : "")}" +
						$"{(friend.Color != null ? $" Favorite Color: {friend.Color}," : "")}");

					WriteLogLine($"  {(friend.Created != null ? $" Created: {friend.Created}" : "")}" +
						$"{(friend.Modified != null ? $" Modified: {friend.Modified}" : "")}" +
						$"{(friend.Blocked.HasValue ? (friend.Blocked.Value ? " (blocked)" : "") : " (pending)")}" +
						$"{(m_online.Any(x => x.Id == friend.Id) ? " (online)" : "")}");
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private void PrintTables()
		{
			try
			{
				m_consoleSemaphore.Wait();
				EraseLog();
				if (m_user.Operators.Any(x => x.Tables.Count > 0))
				{
					WriteLogLine("Tables json:");
					WriteLogLine(JsonSerializer.Serialize(m_user.Operators, m_serializerOptions));
					WriteLogLine("MergeIndex json:");
					WriteLogLine(JsonSerializer.Serialize(m_user.MergeIndex, m_serializerOptions));
				}
				else
				{
					WriteLogLine($"{Handle} has no tables.");
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private async Task UnfriendFriendAsync()
		{
			try
			{
				await m_consoleSemaphore.WaitAsync();
				EraseLog();
				if (m_user.Friends.Count == 0)
				{
					WriteLogLine("You have no friends.");
					return;
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			State = States.Busy;
			Friend friend = await ChooseFriendAsync($"Whom would you like to unfriend" +
				$" (number, Enter to abort): ", m_user.Friends);

			if (friend != null)
			{
				await SendCommandAsync(CommandNames.Hello, Id, friend.Id, friend.Id, m_user, false);
				await UnfriendAsync(friend);
				WriteLogLine($"{friend.Handle} has been unfriended.");
			}

			State = States.Listening;
		}

		private async Task ChatFriendAsync()
		{
			IsRaceLeader = true;
			try
			{
				await m_consoleSemaphore.WaitAsync();
				EraseLog();
				if (m_user.Friends.Count == 0)
				{
					WriteLogLine("You have no friends.");
					return;
				}

				if (m_online.Count == 0)
				{
					WriteLogLine("None of your friends is online.");
					return;
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			State = States.Busy;
			Friend friend = await ChooseFriendAsync("With whom would you like to chat?" +
				" (number, Enter to abort): ", m_online);

			if (friend != null)
			{
				State = States.Connecting;
				ActiveChatChannelName = MakeChatChannelName(friend);
				ActiveChatFriend = friend;
				await m_hubConnection.SendAsync(c_joinChannel, ActiveChatChannelName, Id);
			}
			else
			{
				State = States.Listening;
			}
		}

		private async Task MessageLoopAsync()
		{
			// don't block incoming commands while in the message loop
			if (m_commandSemaphore.CurrentCount == 0)
				_ = m_commandSemaphore.Release();

			State = States.Chatting;
			try
			{
				await m_consoleSemaphore.WaitAsync();
				m_console.SetCursorPosition(0, OutputLine);

				if (IsRaceLeader)
				{
					WriteLine($"You're the Race Leader!");
					WriteLine($"Type messages, type '{c_leaveChatCommand}' to leave the" +
						$" chat{(HaveTables ? $" or '{c_raceCommand}' to race." : ".")}");
				}
				else
				{
					WriteLine($"Type messages, type '{c_leaveChatCommand}' to leave the chat.");
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			while (true)
			{
				while (true)
				{
					if (State == States.Listening || IsStateRacing)
						return;

					if (m_console.KeyAvailable)
						break;

					await Task.Delay(100);
				}

				string message = m_console.ReadLine();
				try
				{
					await m_consoleSemaphore.WaitAsync();
					if (m_waitForEnter)
					{
						m_waitForEnter = false;
						break;
					}

					if (State != States.Chatting && State != States.FriendAway)
						return;

					if (await CheckForCommandAsync(message))
					{
						WriteLine($"You sent the command: {message}");
						continue;
					}

					if (message.ToLower() == c_leaveChatCommand)
					{
						WriteLine();
						await LeaveChatChannelAsync(sendHello: true);
						break;
					}

					if (message.ToLower() == c_raceCommand)
					{
						if (m_console.ScriptMode > 1)
							throw new Exception("Racing not supported in scripting mode.");

						if (IsRaceLeader)
						{
							if (HaveTables)
							{
								WriteLine();
								await SendCommandAsync(CommandNames.InitiateRace, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);
								return;
							}
							else
							{
								Console.CursorTop = OutputLine;
								WriteLine($"You and {ActiveChatFriend.Handle} must have tables in common in order to race.");
								continue;
							}
						}
						else
						{
							Console.CursorTop = OutputLine;
							WriteLine("Only the race leader can start a race.");
							continue;
						}
					}

					await m_hubConnection.SendAsync(c_sendMessage, Id, ActiveChatChannelName, message);
				}
				catch (Exception exception)
				{
					WriteLine($"Error sending message, exception: {exception.Message}");
					break;
				}
				finally
				{
					_ = m_consoleSemaphore.Release();
				}
			}

			Echo = null;
			EraseLogAndDisplayMenu();
			return;
		}

		private async Task<int> RaceLoopAsync()
		{
			RaceData raceData = new RaceData();
			Dictionary<string, List<int>> list = new Dictionary<string, List<int>>();
			int cardCount = 0;
			foreach (string key in MyTableList.Keys)
			{
				if (MyTableList[key].Count > 0 && OpponentTableList[key].Count > 0)
				{
					list[key] = MyTableList[key].Count < OpponentTableList[key].Count ? MyTableList[key] : OpponentTableList[key];
					cardCount += list[key].Count * list[key].Count;
				}
			}

			for (raceData.QuizCount = 1; raceData.QuizCount <= c_roundsPerQuiz; raceData.QuizCount++)
			{
				int index = m_random.Next(cardCount);
				Debug.WriteLine($"{raceData.QuizCount} index is {index}");
				string key = null;
				foreach (string test in list.Keys)
				{
					int cards = list[test].Count * list[test].Count;
					if (cards > index)
					{
						key = test;
						break;
					}

					index -= cards;
				}

				int cardBase = index / list[key].Count + 1;
				Card card = m_user.Operators.FirstOrDefault(x =>
					x.Name == key).Tables.FirstOrDefault(x =>
					x.Base == cardBase).Cards[index % list[key].Count];

				raceData.Operator = (int) Enum.Parse(typeof(FactOperator), key);
				raceData.Base = cardBase;
				raceData.First = card.Fact.First;
				raceData.Second = card.Fact.Second;
				raceData.Time = 0;
				raceData.Try = 1;
				raceData.Score = 0;
				raceData.Busy = true;
				int result;
				if ((result = await ProcessRaceCardCommandAsync(raceData)) < -1)
					return result;

				State = States.RacingWaiting;
				while (OpponentRaceData.Busy && IsStateRacing)
				{
					try
					{
						await m_loopSemaphore.WaitAsync();
						if (State == States.Resetting)
						{
							State = States.Racing;
							return 0;
						}

						await Task.Delay(100);
					}
					finally
					{
						_ = m_loopSemaphore.Release();
					}
				}

				if (!IsStateRacing)
					return 0;

				State = States.Racing;
				ComputeRaceScore();
				await Task.Delay(c_resultDisplayTime);
			}

			await SendCommandAsync(CommandNames.RaceCard, Id, ActiveChatFriend.Id, ActiveChatFriend.Id, raceData: null);
			ReportScore();
			return 0;
		}

		private void ComputeRaceScore()
		{
			MyRaceScore += ComputeRaceScore(MyRaceData, OpponentRaceData);
			OpponentRaceScore += ComputeRaceScore(OpponentRaceData, MyRaceData);
		}

		private int ComputeRaceScore(RaceData raceData1, RaceData raceData2)
		{
			switch (raceData1.Score)
			{
				case 2:
					return raceData2.Score == 2 ? raceData1.Time < raceData2.Time ? 2 : 1 : 2;
				case 1:
					return raceData2.Score == 1 ? raceData1.Time < raceData2.Time ? 1 : 0 : 1;
				case 0:
					return 0;
				default:
					Debug.WriteLine($"Error in ComputeScore, unexpected score {raceData1.Score} (expected 0, 1, or 2)");
					return 0;
			}
		}

		private void ReportScore()
		{
			WaitWriteLine();
			if (MyRaceScore > OpponentRaceScore)
				WaitWriteLine($"Congratulations, you won: {MyRaceScore} to {OpponentRaceScore}!");
			else if (MyRaceData.Score < OpponentRaceScore)
				WaitWriteLine($"Sorry, you lost: {MyRaceScore} to {OpponentRaceScore}.");
			else
				WaitWriteLine($"It's a tie: {MyRaceScore} to {OpponentRaceScore}!");

			WaitWriteLine();
		}

		private async Task<bool> CheckForCommandAsync(string message)
		{
			if (!Regex.Match(message, c_connectionJsonCommand).Success)
				return false;

			ConnectionCommand command = await DeserializeAndSendCommandAsync(message, Id, ActiveChatChannelName,
				ActiveChatFriend.Id);

			if (command?.CommandName == CommandNames.Echo)
			{
				Action<string> echo = Echo;
				Echo = command.Flag == true ? (x) => WaitWriteLogLine($"{Handle}: {x}") : (Action<string>) null;

				// echo the Echo command
				if (echo == null && Echo != null)
					Echo.Invoke(command.Json);
			}

			return command != null;
		}

		private void EraseLogAndDisplayMenu()
		{
			try
			{
				m_consoleSemaphore.Wait();
				if (Echo != null)
				{
					int line = m_console.CursorTop;
					State = States.Listening;
					m_console.CursorTop = line;
				}
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			DisplayMenu();
		}

		private async Task LeaveChatChannelAsync(bool sendHello)
		{
			if (sendHello)
				await SendCommandAsync(CommandNames.Hello, Id, ActiveChatChannelName, ActiveChatFriend.Id, m_user, false);

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

		private async Task<Friend> ChooseFriendAsync(string prompt, List<Friend> friends)
		{
			Friend friend = null;
			try
			{
				await m_consoleSemaphore.WaitAsync();
				WriteLogLine("Friends:");
				int index;
				for (index = 0; index < friends.Count; index++)
					WriteLogLine($"{string.Format("{0:X}", index)}: {friends[index].Handle}");

				Tuple<Point, ConsoleKeyInfo> result = await WriteLogReadKeyAsync(prompt);
				while (true)
				{
					if (result.Item2.Key == ConsoleKey.Enter)
						break;

					friend = int.TryParse(result.Item2.KeyChar.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
						out index) && index >= 0 && index < friends.Count ? friends[index] : null;

					if (friend != null)
						break;

					result = await WriteLogReadKeyAsync($"{result.Item2.KeyChar} not valid, enter a number between 0 and" +
						$" {Math.Min(friends.Count, 9) - 1}, please try again: ");
				}

				Console.SetCursorPosition(result.Item1.X, result.Item1.Y);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			return friend;
		}

		private async Task ProcessHelloCommandAsync(string from, ConnectionCommand command)
		{
			if (State == States.Connecting)
			{
				if (command.Flag == true)
				{
					await SendCommandAsync(CommandNames.TableList, Id, from, from, MyTableList);
				}
				else
				{
					await m_hubConnection.SendAsync(c_leaveChannel, ActiveChatChannelName, Id);
					await WaitWriteLogLineAsync($"{command.Racer.Handle} can't chat at the moment.");
					State = States.Listening;
				}
			}
			else if (State == States.Chatting && command.Flag == false)
			{
				if (m_console.CursorLeft > 0)
				{
					await WaitWriteLineAsync($" {ActiveChatFriend.Handle} has left the chat, hit Enter...");
					m_waitForEnter = true;
				}
				else
				{
					EraseLogAndDisplayMenu();
				}

				await LeaveChatChannelAsync(sendHello: false);
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
						await WaitWriteLogLineAsync($"{existing.Handle} has blocked you. {existing.Handle} must" +
							$" unfriend you before you can become friends.");
					}
					else if (existing.Blocked == false)
					{
						await WaitWriteLogLineAsync($"{existing.Handle} has unfriended you.");
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

						// special output for triggering while scripting
						if (m_console.ScriptMode > 0)
							await WaitWriteLogLineAsync($"(Sent unfriend command to {pending.Handle}.)");
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
				await WaitWriteLogLineAsync($"Your {(existing.Blocked.HasValue ? "" : "(pending) ")}friend {existing.Handle} is online.");
				m_online.Add(existing);
				if (existing.Blocked == false)
					await SendCommandAsync(CommandNames.Hello, Id, existing.Id, existing.Id, m_user, true);
			}
		}

		private async Task ProcessVerifyCommandAsync(string from, ConnectionCommand verify)
		{
			Tuple<Friend, Friend> update = UpdateFriendData(verify.Racer);
			Friend existing = update.Item1;
			Friend pending = update.Item2;
			if (!verify.Flag.HasValue)
			{
				// add a Friends record to get notifications for him
				if (existing == null)
					m_user.Friends.Add(pending);

				if (!m_online.Contains(pending))
					m_online.Add(pending);

				PreviousState = State;
				State = States.Busy;
				Tuple<Point, ConsoleKeyInfo> confirm = await WriteLogReadKeyAsync($"Accept friend request from" +
					$" {pending.Handle}, email address {pending.Email}? [y/n] ");

				State = PreviousState;
				pending.Blocked = confirm.Item2.Key != ConsoleKey.Y;
				_ = m_user.Friends.Remove(existing);
				_ = m_user.Friends.Remove(pending);
				m_user.Friends.Add(pending);
				SaveUser();

				if (m_online.Contains(pending))
					await SendCommandAsync(CommandNames.Verify, Id, HandleChannelName, from, m_user, !pending.Blocked);

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
				await WaitWriteLogLineAsync($"You and {existing.Handle} are {(existing.Blocked.Value ? "not" : "now")} friends!");
		}

		private async Task UnfriendAsync(Friend friend)
		{
			await m_hubConnection.SendAsync(c_leaveChannel, friend.Id ?? MakeHandleChannelName(friend), Id);
			_ = m_online.Remove(friend);
			_ = m_user.Friends.Remove(friend);
			SaveUser();
		}

		private async Task ProcessTableListCommandAsync(string from, Dictionary<string, List<int>> tables)
		{
			OpponentTableList = tables;
			bool connecting;
			do
			{
				try
				{
					await m_consoleSemaphore.WaitAsync();
					if (State == States.Racing)
					{
						// opponent popped from Quiz page
						EraseLog();
						connecting = true;
						break;
					}

					connecting = State == States.Connecting || State == States.Listening;
					if (connecting)
					{
						EraseLog();
					}
					else if (State == States.FriendAway)
					{
						// opponent popped from Tables page
						EraseLog();
						WriteLogLine($"{ActiveChatFriend.Handle} has returned!");
						State = States.Chatting;
					}

					if (State == States.Listening || State == States.RaceInitializing && !IsRaceLeader)
						await SendCommandAsync(CommandNames.TableList, Id, from, from, MyTableList);

					if (State != States.RaceInitializing)
					{
						if (connecting)
							WriteLogLine(string.Empty);

						_ = IntersectTables();
					}
					else if (IsRaceLeader)
					{
						await SendCommandAsync(CommandNames.StartRace, Id, from, from);
					}
				}
				finally
				{
					_ = m_consoleSemaphore.Release();
				}
			}
			while (false);

			if (connecting)
				await MessageLoopAsync();
		}

		private void ProcessAwayCommand()
		{
			try
			{
				m_consoleSemaphore.Wait();
				EraseLog();
				WaitWriteLogLine($"{ActiveChatFriend.Handle} is away...");
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			State = States.FriendAway;
		}

		private async Task ProcessInitiateRaceCommandAsync()
		{
			if (State == States.Chatting)
			{
				InitializeScoreboard();
				State = States.RaceInitializing;
			}

			if (IsRaceLeader)
				await SendCommandAsync(CommandNames.TableList, Id, ActiveChatFriend.Id, ActiveChatFriend.Id, MyTableList);
			else
				await SendCommandAsync(CommandNames.InitiateRace, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);
		}

		private async Task ProcessStartRaceCommandAsync()
		{
			// don't block incoming commands while in the race loop (opponent stats, etc.)
			if (m_commandSemaphore.CurrentCount == 0)
				_ = m_commandSemaphore.Release();

			State = States.Racing;
			if (!IsRaceLeader)
				await SendCommandAsync(CommandNames.StartRace, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);

			if (IsRaceLeader)
			{
				int result = await RaceLoopAsync();
				if (result ==0)
				{
					Console.SetCursorPosition(0, OutputLine);
					await MessageLoopAsync();
				}
				else if (result == -4)
				{
					// we quit
					await ProcessNavigateBackCommandAsync(true);
				}
			}
		}

		private async Task<int> ProcessRaceCardCommandAsync(RaceData raceData)
		{
			// don't block incoming commands while reading data
			if (!IsRaceLeader && m_commandSemaphore.CurrentCount == 0)
				_ = m_commandSemaphore.Release();

			if (raceData == null)
			{
				ReportScore();
				await MessageLoopAsync();
				return 0;
			}

			raceData.Correct = MyRaceData.Correct;
			raceData.Wrong = MyRaceData.Wrong;
			OpponentRaceData.SetCard(raceData);
			MyRaceData = raceData;
			if (IsRaceLeader)
				await SendCommandAsync(CommandNames.RaceCard, Id, ActiveChatFriend.Id, ActiveChatFriend.Id, OpponentRaceData);

			if (ShowCountdown)
				await CountdownAsync();

			Fact fact = new Fact(raceData.First, raceData.Second, Arithmetic.Operator[(FactOperator) raceData.Operator]);
			DateTime startTime = DateTime.Now;
			ExpireTime = startTime + c_countdownFrom;
			TimeLeft = (int) c_countdownFrom.TotalSeconds;
			using (new Timer(UpdateQuestion, null, 1000, 1000))
			{
				int guess = await ReadAnswerAsync($"{MyRaceData.QuizCount} of {c_roundsPerQuiz}, {TimeLeft}: What is ",
					fact.Render, fact.Answer, "Correct!", raceData);

				if (guess < 0)
					return guess;

				if (guess == fact.Answer)
				{
					raceData.Time = (int) (DateTime.Now - startTime).TotalMilliseconds;
					raceData.Score = 2;
					raceData.Correct++;
					MyRaceData = raceData;
					await SendRaceResultAsync();

					// leader expects (at least) two CardResults, first with Busy true, second with Busy false (after results have been shown locally)
					raceData.Busy = false;
					MyRaceData = raceData;
					await SendRaceResultAsync();
					return 0;
				}

				raceData.Time = (int) (DateTime.Now - startTime).TotalMilliseconds;
				raceData.Try = 2;
				MyRaceData = raceData;
				await SendRaceResultAsync();
				if ((guess = await ReadAnswerAsync($"Not quite... {TimeLeft}: what is ", fact.Render, fact.Answer, "You are right!", raceData)) < 0)
					return guess;

				raceData.Time = (int) (DateTime.Now - startTime).TotalMilliseconds;
				raceData.Busy = false;
				if (guess == fact.Answer)
				{
					raceData.Score = 1;
					raceData.Correct++;
					MyRaceData = raceData;
					await SendRaceResultAsync();
					return 0;
				}

				try
				{
					await m_consoleSemaphore.WaitAsync();
					WriteLine($"No! {fact.Render} = {fact.Answer}");
					raceData.Score = 0;
					raceData.Wrong++;
					MyRaceData = raceData;
					await SendRaceResultAsync();
					return 0;
				}
				finally
				{
					_ = m_consoleSemaphore.Release();
				}
			}
		}

		private void ProcessCardResultCommand(RaceData raceData)
		{
			OpponentRaceData = raceData;
			if (!IsRaceLeader && !MyRaceData.Busy && !OpponentRaceData.Busy)
				ComputeRaceScore();
		}

		private async Task ProcessResetCommandAsync()
		{
			ResetRace($"{ActiveChatFriend.Handle} is resetting the race...");
			if (State == States.RacingWaiting)
			{
				State = States.Resetting;
				await m_loopSemaphore.WaitAsync();
				_ = m_loopSemaphore?.Release();
			}

			if (IsRaceLeader)
				await ProcessStartRaceCommandAsync();
			else
				await SendCommandAsync(CommandNames.StartRace, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);
		}

		private async Task ProcessNavigateBackCommandAsync(bool weQuit = false)
		{
			if (IsStateRacing)
			{
				try
				{
					await m_consoleSemaphore.WaitAsync();
					WriteLine();
					WriteLine();
					if (weQuit)
						WriteLine("Ending the race...");
					else
						WriteLine($"{ActiveChatFriend.Handle} is ending the race...");
				}
				finally
				{
					_ = m_consoleSemaphore.Release();
				}

				await MessageLoopAsync();
			}
		}

		private void InitializeScoreboard()
		{
			try
			{
				m_consoleSemaphore.Wait();
				EraseLog();
				WriteLine();
				ScoreboardLine = OutputLine;
				MyRaceScore = OpponentRaceScore = 0;
				MyRaceData = new RaceData();
				OpponentRaceData = new RaceData();
				ShowCountdown = true;
				OutputLine += 3;
				LogTop += 3;
				LogBottom += 3;
				_ = CheckScrollWindow(OutputLine);
				Console.SetCursorPosition(0, OutputLine);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private async Task CountdownAsync()
		{
			ShowCountdown = false;
			await WaitWriteLineAsync();
			await WaitWriteLineAsync("Racing in   ", printEndline: false);
			for (int countdown = 3; countdown > 0; countdown--)
			{
				try
				{
					await m_consoleSemaphore.WaitAsync();
					Console.CursorLeft -= 2;
					Console.Write($"{countdown}!");
				}
				finally
				{
					_ = m_consoleSemaphore.Release();
				}

				await Task.Delay(1000);
			}

			try
			{
				await m_consoleSemaphore.WaitAsync();
				Console.CursorLeft = 0;
				Console.Write("            ");
				Console.SetCursorPosition(0, OutputLine);
				WriteLine("Racing, type answers or type quit or reset:");
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private async Task<int> ReadAnswerAsync(string prompt, string fact, int rawAnswer,
			string correctMessage, RaceData raceData)
		{
			AnswerPrompt = $"{prompt}{fact}? ";
			await WaitWriteLineAsync(AnswerPrompt, printEndline: false);
			string guess = string.Empty;
			string answer = rawAnswer.ToString();
			while (true)
			{
				State = States.RacingWaiting;
				while (true)
				{
					try
					{
						await m_loopSemaphore.WaitAsync();
						if (State == States.Resetting)
						{
							State = States.Racing;
							WriteLine();
							return -2;
						}

						if (!IsStateRacing)
						{
							WriteLine();
							return -3;
						}

						if (m_console.KeyAvailable)
							break;

						await Task.Delay(100);
						if (DateTime.Now > ExpireTime)
						{
							WriteLine();
							WriteLine("Time.");
							raceData.Wrong++;
							raceData.Time = (int) c_countdownFrom.TotalMilliseconds;
							MyRaceData = raceData;
							await SendRaceResultAsync();
							if (raceData.Busy)
							{
								raceData.Busy = false;
								await SendRaceResultAsync();
							}

							return -1;
						}
					}
					finally
					{
						_ = m_loopSemaphore.Release();
					}
				}

				State = States.Racing;
				try
				{
					await m_consoleSemaphore.WaitAsync();
					ConsoleKeyInfo keyInfo = Console.ReadKey(true);
					if (CommandMatch(ref guess, keyInfo, "quit", out bool complete))
					{
						if (complete)
						{
							AnswerPrompt = null;
							await SendCommandAsync(CommandNames.NavigateBack, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);
							await SendCommandAsync(CommandNames.NavigateBack, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);
							if (!IsRaceLeader)
							{
								_ = m_consoleSemaphore.Release();
								await ProcessNavigateBackCommandAsync(weQuit: true);
							}
							else
							{
								WriteLine();
							}

							return -4;
						}
					}
					else if (CommandMatch(ref guess, keyInfo, "reset", out complete))
					{
						if (complete)
						{
							await SendCommandAsync(CommandNames.Reset, Id, ActiveChatFriend.Id, ActiveChatFriend.Id);
							break;
						}
					}
					else if (int.TryParse(keyInfo.KeyChar.ToString(), out int digit))
					{
						Console.Write(digit);
						guess += keyInfo.KeyChar;
						if (guess != answer[0..guess.Length] || guess.Length == answer.Length)
						{
							WriteLine();
							int number = int.Parse(guess);
							if (number == rawAnswer)
								WriteLine(correctMessage);

							return number;
						}
					}
				}
				finally
				{
					if (m_consoleSemaphore.CurrentCount == 0)
						_ = m_consoleSemaphore.Release();
				}
			}

			ResetRace("Resetting the race...");
			return -5;
		}

		private async Task SendRaceResultAsync()
		{
			await SendCommandAsync(CommandNames.CardResult, Id, ActiveChatFriend.Id, ActiveChatFriend.Id, MyRaceData);
			if (!IsRaceLeader && !MyRaceData.Busy && !OpponentRaceData.Busy)
				ComputeRaceScore();
		}

		private void UpdateQuestion(object state)
		{
			try
			{
				m_consoleSemaphore.Wait();
				if (AnswerPrompt == null || TimeLeft < 1)
					return;

				Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
				Console.CursorLeft = 0;
				Console.Write(AnswerPrompt = AnswerPrompt.Replace(string.Format("{0,2:0}:", TimeLeft),
					string.Format("{0,2:0}:", --TimeLeft)));
				Console.SetCursorPosition(cursor.X, cursor.Y);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		bool CommandMatch(ref string guess, ConsoleKeyInfo key, string command, out bool complete)
		{
			complete = false;
			if (key.Key == ConsoleKey.Backspace)
			{
				if (guess.Length > 0)
				{
					guess = guess[..^1];
					Console.CursorLeft--;
					Console.Write(" ");
					Console.CursorLeft--;
				}

				return true;
			}

			if (guess.Length >= command.Length || guess + key.KeyChar != command[..(guess.Length + 1)])
				return false;

			Console.Write(key.KeyChar);
			guess += key.KeyChar;
			complete = guess == command;
			return true;
		}

		private bool IntersectTables()
		{
			foreach (string line in FormatTableList($"{ActiveChatFriend.Handle} has", OpponentTableList).Split("\n"))
				WriteLogLine(line);

			if (HaveTables)
			{
				foreach (string line in FormatTableList("You have", MyTableList).Split("\n"))
					WriteLogLine(line);

				int count = m_user.Operators.Count(x => OpponentTableList.Any(y => x.Name == y.Key &&
					x.Tables.Any(z => y.Value.Contains(z.Base))));

				HaveIntersection = count == 0;
				if (HaveIntersection)
				{
					WriteLogLine($"You and {ActiveChatFriend.Handle} have no tables in common" +
						$"{(IsRaceLeader ? " and can't race." : ".")}");
					return false;
				}
				else
				{
					WriteLogLine($"You and {ActiveChatFriend.Handle} have {count} {(count == 1 ? "set" : "sets")}" +
						$" of tables in common.");
				}
			}
			else
			{
				WriteLogLine($"You have no tables{(IsRaceLeader ? " and can't race." : ".")}");
				return false;
			}

			return true;
		}

		private string FormatTableList(string title, Dictionary<string, List<int>> tables)
		{
			StringBuilder result = new StringBuilder();
			_ = result.AppendLine(string.Format("{0} these tables:", title));
			foreach (string type in tables.Keys)
			{
				string list = string.Empty;
				if (tables[type].Count > 0)
				{
					tables[type].ForEach(x => list += $"{x}, ");
					list = list[..^2];
				}
				else
				{
					list = "(None)";
				}

				_ = result.AppendLine($"{type}: {list}");
			}

			return result.ToString();
		}

		private async Task SendMergeAsync(Friend friend)
		{
			if (!DateTime.TryParse(m_user.Created, out DateTime myCreated) ||
				!DateTime.TryParse(friend.Created, out DateTime created))
			{
				WriteLogLine($"Error in CheckSendMerge, could not parse DateTime from {m_user.Created}" +
					$" and/or {friend.Created}");
				return;
			}

			if (myCreated != created && !m_merged.Contains(friend.DeviceId))
			{
				await WaitWriteLogLineAsync($"{friend.Handle} is online on device {friend.DeviceId}, sending merge data...");
				m_merged.Add(friend.DeviceId);
				await SendCommandAsync(CommandNames.Merge, Id, friend.Id, friend.DeviceId, m_user);
			}
		}

		private void ResetRace(string message)
		{
			try
			{
				m_consoleSemaphore.Wait();
				Console.SetCursorPosition(0, OutputLine);
				WriteLine();
				WriteLine(message);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			ShowCountdown = true;
			InitializeScoreboard();
		}

		private async Task ProcessMergeCommandAsync(ConnectionCommand merge, string to)
		{
			User user = merge.Merge;
			if (!DateTime.TryParse(m_user.Created, out DateTime myCreated) ||
				!DateTime.TryParse(user.Created, out DateTime created))
			{
				WriteLogLine($"Error in MergeAccountsAsync parsing Created date '{m_user.Created}' or '{user.Created}'.");
				return;
			}

			if (myCreated == created || to != DeviceId)
				return;

			if (!DateTime.TryParse(m_user.Modified, out DateTime myModified) ||
				!DateTime.TryParse(user.Modified, out DateTime modified))
			{
				WriteLogLine($"Error in MergeAccountsAsync parsing Modified date '{m_user.Modified}' or '{user.Modified}'.");
				return;
			}

			bool updateId = m_user.Id != user.Id && myCreated > created;
			bool update = myModified < modified && (m_user.Name != user.Name || m_user.Handle != user.Handle ||
				m_user.Email != user.Email || m_user.Color != user.Color);

			if (updateId)
			{
				// don't leave the DeviceId channel since other Merge commands may be coming in on it
				await m_hubConnection.SendAsync(c_leaveChannel, ChatChannelName, Id);
				m_user.Id = user.Id;
				await m_hubConnection.SendAsync(c_joinChannel, IdChannelName, Id);
				await m_hubConnection.SendAsync(c_joinChannel, ChatChannelName, Id);
			}

			if (update)
			{
				m_user.Name = user.Name;
				m_user.Handle = user.Handle;
				m_user.Email = user.Email;
				m_user.Color = user.Color;
			}

			await WaitWriteLogLineAsync($"Merging data from {Handle}{(updateId ? $", Id {user.Id}," : "")} on device {user.DeviceId}...");
			m_merged.Add(user.DeviceId);
			update = MergeFriends(update, user);
			update = MergeTables(update, user);
			await WaitWriteLogLineAsync($"Merged data from {Handle} on device {user.DeviceId}.");
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
					m_user.Friends.Add(friend);
					save = true;
				}
				else
				{
					if (DateTime.TryParse(myFriend.Modified, out DateTime myModified) &&
						DateTime.TryParse(friend.Modified, out DateTime modified))
					{
						if (myModified < modified)
						{
							_ = m_user.Friends.Remove(myFriend);
							m_user.Friends.Add(friend);
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
							MergeCards(card, mergeIndex, null, myMergeIndex);

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
								MergeCards(card, mergeIndex, null, myMergeIndex);
								myTable.Cards.Add(card);
								save = true;
							}
							else
							{
								MergeCards(card, mergeIndex, myCard, myMergeIndex);
								myCard.BestTime = card.BestTime == 0 || myCard.BestTime == 0 ?
									Math.Max(card.BestTime, myCard.BestTime) :
									Math.Min(card.BestTime, myCard.BestTime);

								save = true;
							}
						}
					}
				}
			}

			foreach (OperatorTables math in m_user.Operators)
			{
				foreach (FactTable table in math.Tables)
				{
					foreach (Card card in table.Cards.Where(x => (x.MergeQuizzed?.Length ?? 0) <= myMergeIndex))
					{
						InitializeMergeProperties(card, myMergeIndex);
						save = true;
					}
				}
			}

			return save;
		}

		private void MergeCards(Card card, int mergeIndex, Card myCard, int myMergeIndex)
		{
			if (myCard == null)
			{
				int quizzed = card.Quizzed - (card.MergeQuizzed?.Sum() ?? 0);
				int correct = card.Correct - (card.MergeCorrect?.Sum() ?? 0);
				int time = card.TotalTime - (card.MergeTime?.Sum() ?? 0);
				InitializeMergeProperties(card, myMergeIndex, true);
				card.Quizzed = card.MergeQuizzed[myMergeIndex] = quizzed;
				card.Correct = card.MergeCorrect[myMergeIndex] = correct;
				card.TotalTime = card.MergeTime[myMergeIndex] = time;
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
			}
		}

		private void InitializeMergeProperties(Card card, int mergeIndex, bool clear = false)
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

		private int[] GrowArray(int[] mergeQuizzed)
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
					friend.DeviceId = updated.DeviceId;
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
			LogBottom = cursorPosition.Y;
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

			m_console.SetCursorPosition(0, LogBottom + 1);
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
			try
			{
				m_consoleSemaphore.Wait();
				EraseLog();
				OutputLine = m_console.CursorTop;
				LogTop = LogBottom = OutputLine + c_logWindowOffset;
				WriteLine();
				WriteLine("Pick a command: t to list tables, a to add a friend, l to list friends,");
				WriteLine("u to unfriend a friend, c to chat, or x to exit");
				PromptLine = m_console.CursorTop;
				OutputLine++;
				LogTop++;
				LogBottom++;
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

			State = States.Listening;
			IsRaceLeader = false;
		}

		private async void UpdateScoreboard()
		{
			if (ActiveChatFriend == null)
				return;

			try
			{
				await m_consoleSemaphore.WaitAsync();
				int nameDigits = Math.Max(Handle.Length, ActiveChatFriend.Handle.Length);
				int count = MyRaceData.Busy ? MyRaceData.QuizCount - 1 : MyRaceData.QuizCount;
				double myAverage = count > 0 ? Math.Round(100.0 * MyRaceData.Correct / count) : 0;
				count = OpponentRaceData.Busy ? OpponentRaceData.QuizCount - 1 : OpponentRaceData.QuizCount;
				double opponentAverage = count > 0 ? Math.Round(100.0 * OpponentRaceData.Correct / count) : 0;
				int avgDigits = Math.Max(GetDigitCount(myAverage), GetDigitCount(opponentAverage));
				double myTime = MyRaceData.Time / 1000.0;
				double opponentTime = OpponentRaceData.Time / 1000.0;
				int timeDigits = Math.Max(GetDigitCount(myTime), GetDigitCount(opponentTime)) + 2;
				int correctDigits = Math.Max(GetDigitCount(MyRaceData.Correct), GetDigitCount(OpponentRaceData.Correct));
				int wrongDigits = Math.Max(GetDigitCount(MyRaceData.Wrong), GetDigitCount(OpponentRaceData.Wrong));
				Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
				Console.SetCursorPosition(0, ScoreboardLine);
				Console.WriteLine($"Racer{Padding(nameDigits - 5)}  Try      Time{Padding(timeDigits - 2)}" +
					$" Right, Wrong  Card Total     ");
				
				Console.WriteLine($"{Handle}{Padding(nameDigits - Handle.Length)} " +
					$" {MyRaceData.Try} of 2, " +
					$" {string.Format($"{{0,{timeDigits}:0.0}}s ", myTime)}" +
					$" {string.Format($"{{0,{correctDigits}}}", MyRaceData.Correct)}," +
					$" {string.Format($"{{0,{wrongDigits}}}", MyRaceData.Wrong)}:" +
					$" {string.Format($"{{0,{avgDigits}}}% ", myAverage)}" +
					$" {Padding(3 - avgDigits)}{Padding(2 - correctDigits)}{Padding(2 - wrongDigits)}" +
					$"{MyRaceData.Score}    {MyRaceScore}    ");
				
				Console.WriteLine($"{ActiveChatFriend.Handle}{Padding(nameDigits - ActiveChatFriend.Handle.Length)} " +
					$" {OpponentRaceData.Try} of 2, " +
					$" {string.Format($"{{0,{timeDigits}:0.0}}s ", opponentTime)}" +
					$" {string.Format($"{{0,{correctDigits}}}", OpponentRaceData.Correct)}," +
					$" {string.Format($"{{0,{wrongDigits}}}", OpponentRaceData.Wrong)}:" +
					$" {string.Format($"{{0,{avgDigits}}}% ", opponentAverage)}" +
					$" {Padding(3 - avgDigits)}{Padding(2 - correctDigits)}{Padding(2 - wrongDigits)}" +
					$"{OpponentRaceData.Score}    {OpponentRaceScore}    ");
				
				Console.SetCursorPosition(cursor.X, cursor.Y);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private async Task<ConsoleKeyInfo> ReadKeyAvailableAsync(Func<bool> enable)
		{
			while (!enable() || !m_console.KeyAvailable)
				await Task.Delay(10);

			return m_console.ReadKey(intercept: true);
		}

		private static string Padding(int padding)
		{
			return padding > 0 ? new string(' ', padding) : string.Empty;
		}

		private void WaitWriteLine(string line = null, bool printEndline = true)
		{
			try
			{
				m_consoleSemaphore.Wait();
				WriteLine(line, printEndline);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

		}

		private async Task WaitWriteLineAsync(string line = null, bool printEndline = true)
		{
			try
			{
				await m_consoleSemaphore.WaitAsync();
				WriteLine(line, printEndline);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private void WriteLine(string line = null, bool printEndline = true)
		{
			_ = TrimLog();
			Console.CursorTop = CheckScrollWindow(OutputLine).Y;
			if (printEndline)
			{
				OutputLine++;
				int extraLines = m_console.CursorTop;
				m_console.WriteLine(line ?? string.Empty);
				if ((extraLines = m_console.CursorTop - extraLines - 1) > 0)
				{
					for (; extraLines > 0; extraLines--)
					{
						_ = TrimLog();
						OutputLine++;
					}
				}
			}
			else
			{
				LogTop--;
				LogBottom--;
				m_console.Write(line ?? string.Empty);
			}
		}

		private void EraseLog()
		{
			if (m_console.ScriptMode > 1 || m_log.Count == 0 || Echo != null &&
				(IsStateRacing || State == States.Chatting))
			{
				return;
			}

			Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
			Console.SetCursorPosition(0, LogTop);
			ConsoleColor color = m_console.ForegroundColor;
			try
			{
				Console.ForegroundColor = m_console.BackgroundColor;
				foreach (string line in m_log)
					Console.WriteLine(line);
			}
			finally
			{
				m_log.Clear();
				Console.ForegroundColor = color;
				Console.SetCursorPosition(cursor.X, cursor.Y);
				LogTop = LogBottom = OutputLine + c_logWindowOffset;
			}
		}

		private Point TrimLog()
		{
			Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
			if (m_console.ScriptMode > 1)
				return cursor;

			if (OutputLine == LogBottom - c_logWindowOffset)
			{
				LogTop++;
				LogBottom++;
				return cursor;
			}

			ConsoleColor color = m_console.ForegroundColor;
			try
			{
				Console.ForegroundColor = m_console.BackgroundColor;
				m_console.SetCursorPosition(0, LogTop++);
				Console.WriteLine(m_log[0]);
				m_log.RemoveAt(0);
			}
			finally
			{
				Console.ForegroundColor = color;
				Console.SetCursorPosition(cursor.X, cursor.Y);
			}

			return cursor;
		}

		private async Task WaitWriteLogLineAsync(string line)
		{
			try
			{
				await m_consoleSemaphore.WaitAsync();
				WriteLogLine(line);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}
		}

		private void WaitWriteLogLine(string line)
		{
			try
			{
				m_consoleSemaphore.Wait();
				WriteLogLine(line);
			}
			finally
			{
				_ = m_consoleSemaphore.Release();
			}

		}

		private void WriteLogLine(string line)
		{
			if (m_console.ScriptMode > 1)
			{
				m_console.WriteLine(line);
				return;
			}

			Point cursor = MoveCursorToLog();
			int extraLines = m_console.CursorTop;
			m_console.WriteLine(line);
			if ((extraLines = m_console.CursorTop - extraLines) > 1)
				LogBottom += extraLines - 1;

			m_log.Add(line);
			m_console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private void WriteLogRead(string line, out string value)
		{
			if (m_console.ScriptMode > 1)
			{
				m_console.WriteLine(line);
				value = m_console.ReadLine();
				return;
			}

			_ = MoveCursorToLog();
			m_console.Write(line);
			value = m_console.ReadLine();
			m_log.Add(line + value);
		}

		private async Task<Tuple<Point, ConsoleKeyInfo>> WriteLogReadKeyAsync(string line)
		{
			Point cursor = m_console.ScriptMode < 2 ? MoveCursorToLog() :
				new Point(m_console.CursorLeft, m_console.CursorTop);

			m_console.Write(line);
			ConsoleKeyInfo confirm = await ReadKeyAvailableAsync(() => State != States.Listening);
			if (m_console.ScriptMode < 2)
				m_log.Add(line + confirm.KeyChar);

			return new Tuple<Point, ConsoleKeyInfo>(cursor, confirm);
		}

		private Point MoveCursorToLog()
		{
			Point cursor = CheckScrollWindow(LogBottom);
			m_console.SetCursorPosition(0, LogBottom++);
			return cursor;
		}

		private Point CheckScrollWindow(int line)
		{
			// scroll the window up so our cursor movement doesn't go out of bounds
			// TODO: seems we should be able to move the window in the buffer but I couldn't get it to work, so this
			Point cursor = new Point(m_console.CursorLeft, m_console.CursorTop);
			if (m_console.ScriptMode < 2 && line >= Console.WindowHeight - 1)
			{
				Console.CursorTop = Console.WindowHeight - 1;
				while (line-- >= Console.WindowHeight - 1)
				{
					Console.WriteLine();
					PromptLine--;
					OutputLine--;
					ScoreboardLine--;
					LogTop--;
					LogBottom--;
					cursor.Y--;
				}
			}

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

		private const int c_logWindowOffset = 2;
		private const string c_connectionJsonCommand = "{ ?\"Command\":";
		private readonly TimeSpan c_resultDisplayTime = TimeSpan.FromSeconds(2.0);
		private readonly TimeSpan c_countdownFrom = TimeSpan.FromSeconds(30.0);
		private const int c_roundsPerQuiz = 10;
		private Harness m_console;
		private User m_user;
		private States m_state;
		private bool m_waitForEnter;
		private Dictionary<string, List<int>> m_myTables;
		private RaceData m_opponentRaceData;
		private RaceData m_myRaceData;
		private int m_myRaceScore;
		private int m_opponentRaceScore;
		private readonly List<User> m_users = new List<User>();
		private readonly List<Friend> m_online = new List<Friend>();
		private readonly List<string> m_log = new List<string>();
		private readonly Stack<States> m_states = new Stack<States>();
		private readonly List<string> m_merged = new List<string>();
		private readonly Random m_random = new Random();
		private readonly JsonSerializerOptions m_serializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true
		};
	}
}
