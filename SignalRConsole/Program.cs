using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static SignalRConsole.Commands;
using static SignalRConsole.Commands.ConnectionCommand;

namespace SignalRConsole
{
	public class Program
	{
		private static async Task<int> Main(string[] args)
		{
			if (!await StartServerAsync())
				return -1;

			if (!await LoadUsersAsync(args.Length > 0 ? args[0] : null))
				return -2;

			await MonitorChannelsAsync();
			await MonitorFriendsAsync();

			DisplayMenu();

			while (true)
			{
				Console.CursorLeft = 0;
				Console.Write($"{State}> ");
				while (!Console.KeyAvailable || State != States.Listening)
					await Task.Delay(10);

				ConsoleKeyInfo menu = Console.ReadKey(intercept: true);
				try
				{
					switch (menu.Key)
					{
						case ConsoleKey.A:
							await AddFriendAsync();
							continue;
						case ConsoleKey.L:
							ListFriends();
							continue;
						case ConsoleKey.U:
							await UnfriendFriendAsync();
							continue;
						case ConsoleKey.D:
							await RemoveUserAsync();
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
					Console.WriteLine("Disconnected from the server, reconnecting...");
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
				_ = MoveCursorToLog();
				foreach (User friend in m_user.Friends.Where(x => x.Blocked != true))
					await m_hubConnection.SendAsync(c_leaveGroupChat, MakeGroupName(friend), Name);

				await m_hubConnection.SendAsync(c_leaveGroupChat, GroupName, Name);
				await m_hubConnection.SendAsync(c_leaveGroupChat, ChatGroupName, Name);
				await m_hubConnection.StopAsync();
			}
			catch (Exception exception)
			{
				Console.WriteLine($"Exception shutting down: {exception.Message}");
			}
			finally
			{
				Console.WriteLine("\nFinished.");
			}

			return 0;
		}


		public const string c_register = "Register";
		public const string c_sendCommand = "SendCommand";
		public const string c_receiveCommand = "ReceiveCommand";
		public const string c_joinGroupChat = "JoinGroupChat";
		public const string c_joinGroupMessage = "JoinGroupMessage";
		public const string c_sendGroupMessage = "SendGroupMessage";
		public const string c_receiveGroupMessage = "ReceiveGroupMessage";
		public const string c_sendGroupCommandTo = "SendGroupCommandTo";
		public const string c_receiveGroupCommandTo = "ReceiveGroupCommandTo";
		public const string c_receiveGroupCommand = "ReceiveGroupCommand";
		public const string c_leaveGroupChat = "LeaveGroupChat";
		public const string c_leaveGroupMessage = "LeaveGroupMessage";
		public const string c_chatGroupName = "Chat";
		public const string c_fileExtension = ".qkr.json";
		public const string c_leaveChatCommand = "goodbye";


		private static States State
		{
			get => m_state;
			set
			{
				if (value != States.Initializing)
				{
					Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
					Console.SetCursorPosition(0, PromptLine);
					int padding = m_state == States.Initializing ? 0 : (m_state.ToString().Length) - value.ToString().Length + 2;
					if (value == States.Listening)
					{
						Console.Write(padding > 0 ? $"{value}> {(padding > 0 ? new string(' ', padding) : "")}" : $"{value}> ");
						while (padding-- > 0)
							Console.CursorLeft--;
					}
					else
					{
						Console.Write($"{value}...{(padding > 0 ? new string(' ', padding) : "")}");
						Console.SetCursorPosition(cursor.X, cursor.Y);
					}
				}

				m_state = value;
			}
		}

		private static string RegistrationToken { get; set; }
		private static string Name => m_user?.Name;
		private static string Email => m_user?.InternetId;
		private static string FileName => m_user.FileName;
		private static string GroupName => MakeGroupName(Name, Email);
		private static string ChatGroupName => MakeChatGroupName(m_user);
		private static string ActiveChatGroupName { get; set; }
		private static int NextLine { get; set; }
		private static int PromptLine { get; set; }


		private static void OnRegister(string token)
		{
			ConsoleColor color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Yellow;
			ConsoleWriteLogLine($"Registration token from server: {token}");
			Console.ForegroundColor = color;
			RegistrationToken = token;
			State = States.Changing;
		}

		private static async Task OnGroupJoinAsync(string group, string user)
		{
			if (user != Name)
			{
				Debug.WriteLine($"{user} has joined the {string.Join('-', ParseGroupName(group))} group.");
				if (group == GroupName)
				{
					User friend = m_user.Friends.FirstOrDefault(x => x.Name == user);
					SendCommand(CommandNames.Hello, group, user, null, !friend?.Blocked);
					if (friend != null && (!friend.Blocked ?? true) && !m_online.Contains(friend))
					{
						ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Name} is online.");
						m_online.Add(friend);
					}
				}
				else if (group == ChatGroupName)
				{
					if (State == States.Listening)
					{
						SendCommand(CommandNames.Hello, group, user, null, true);
						ActiveChatGroupName = group;
						_ = await MessageLoopAsync();
					}
					else
					{
						SendCommand(CommandNames.Hello, group, user, null, false);
					}
				}
				else
				{
					User friend = m_user.Friends.FirstOrDefault(x => x.Name == user && x.Blocked == null);
					if (friend != null)
					{
						await m_hubConnection.SendAsync(c_leaveGroupChat, group, Name);
						await MonitorUserAsync(friend);
					}
				}
			}
		}

		private static void OnGroupMessage(string from, string message)
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			if (from == Name)
			{
				Console.WriteLine($"You said: {message}");
			}
			else if (cursor.X == 0)
			{
				Console.WriteLine($"{from} said: {message}");
			}
			else
			{
				Console.SetCursorPosition(0, cursor.Y + 1);
				Console.WriteLine($"{from} said: {message}");
				NextLine = Console.CursorTop;
				Console.SetCursorPosition(cursor.X, cursor.Y);
			}
		}

		private static async Task OnGroupCommandToAsync(string from, string to, string json)
		{
			if (to == Name)
				await OnGroupCommandAsync(from, json);
		}

		private static async Task OnGroupCommandAsync(string from, string json)
		{
			ConnectionCommand command = DeserializeCommand(json);
			switch (command.CommandName)
			{
				case CommandNames.Hello:
					await HelloAsync(from, command);
					break;
				case CommandNames.Verify:
					await VerifyFriendAsync(from, command);
					break;
				case CommandNames.Unrecognized:
				default:
					Debug.WriteLine($"Error in OnGroupCommandAsync, unrecognized command: {command.CommandName}");
					break;
			}
		}

		private static async Task OnGroupLeaveAsync(string group, string user)
		{
			string[] parts = ParseGroupName(group);
			Debug.WriteLine($"{user} has left the {string.Join('-', parts)} chat.");
			if (group == GroupName)
			{
				User friend = m_user.Friends.FirstOrDefault(u => u.Name == user);
				if (friend != null && (!friend.Blocked ?? true))
				{
					ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {user} is offline.");
					m_online.Remove(friend);
				}
			}
			else if (group == ActiveChatGroupName)
			{
				Console.Write($"{(Console.CursorLeft > 0 ? "\n" : "")}{user} has left the chat. (Hit Enter)");
				if (ActiveChatGroupName != ChatGroupName)
					await m_hubConnection.SendAsync(c_leaveGroupChat, ActiveChatGroupName, Name);

				ActiveChatGroupName = null;
				DisplayMenu();
			}
		}

		private static async Task<bool> StartServerAsync()
		{
			State = States.Initializing;
			Console.WriteLine($"Initializing server and connecting to URL: {c_chatHubUrl}");
			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();
			Initialize(async (g, t, j) => await m_hubConnection.SendAsync(c_sendGroupCommandTo, Name, g, t, j));

			_ = m_hubConnection.On<string>(c_register, (t) => OnRegister(t));
			_ = m_hubConnection.On(c_joinGroupMessage, (Action<string, string>) (async (g, u) => await OnGroupJoinAsync(g, u)));
			_ = m_hubConnection.On(c_receiveGroupMessage, (Action<string, string>) ((f, m) => OnGroupMessage(f, m)));
			_ = m_hubConnection.On(c_receiveGroupCommand, (Action<string, string>) (async (f, c) => await OnGroupCommandAsync(f, c)));
			_ = m_hubConnection.On(c_receiveGroupCommandTo, (Action<string, string, string>) (async (f, t, c) => await OnGroupCommandToAsync(f, t, c)));
			_ = m_hubConnection.On(c_leaveGroupMessage, (Action<string, string>) (async (g, u) => await OnGroupLeaveAsync(g, u)));

			try
			{
				await m_hubConnection.StartAsync();
			}
			catch (Exception exception)
			{
				Console.WriteLine($"Failed to connect to server (is the server running?), exception:" +
					$" {exception.Message}");
				return false;
			}

			return true;
		}

		private static async Task<bool> LoadUsersAsync(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				Console.Write("What is your name? ");
				name = Console.ReadLine();
				if (string.IsNullOrEmpty(name))
					return false;
			}

			foreach (string fileName in Directory.EnumerateFiles(".").Where(x => x.EndsWith(c_fileExtension)))
			{
				User user = JsonSerializer.Deserialize<User>(File.ReadAllText(fileName));
				user.FileName = fileName;
				m_users.Add(user);
			}

			m_user = m_users.FirstOrDefault(u => u.Name == name);
			if (m_user == null)
			{
				string email = await RegisterAsync();
				if (email == null)
					return false;

				m_user = new User(name, email, $"{name}{c_fileExtension}");
				m_users.Add(m_user);
				SaveUser();
			}

			return true;
		}

		private static async Task<string> RegisterAsync()
		{
			State = States.Registering;
			Console.Write("What is your email address? ");
			string email = Console.ReadLine();
			if (string.IsNullOrEmpty(email))
				return null;

			await m_hubConnection.SendAsync(c_register, email);
			if (!await WaitAsync("Waiting for the server"))
			{
				Console.WriteLine("Timeout waiting for a response from the server, aborting.");
				return null;
			}

			State = States.Initializing;
			Console.WriteLine("Enter the token returned from the server, Enter to abort: ");
			string token;
			while (true)
			{
				token = Console.ReadLine();
				if (token == RegistrationToken)
					return email;

				if (token == string.Empty)
					return null;

				Console.WriteLine("Tokens do not match, please try again, Enter to abort: ");
			}
		}

		private static async Task AddFriendAsync()
		{
			EraseLog();
			State = States.Busy;
			Point cursor = Point.Empty;
			while (true)
			{
				Point temp = ConsoleWriteLogRead("What is your friend's name? ", out string name);
				if (cursor.IsEmpty)
					cursor = temp;

				if (string.IsNullOrEmpty(name))
					break;

				if (name == Name)
				{
					ConsoleWriteLogLine($"That's your name!");
					continue;
				}

				User friend = m_user.Friends.FirstOrDefault(x => x.Name == name);
				if (friend != null)
				{
					if (!friend.Blocked.HasValue)
						ConsoleWriteLogLine($"You already asked {name} to be your friend and we're waiting for a response.");
					else if (friend.Blocked.Value)
						ConsoleWriteLogLine($"You and {name} are blocked. Both you and {name} must unfriend to try again.");
					else
						ConsoleWriteLogLine($"{name} is already your friend!");

					break;
				}

				_ = ConsoleWriteLogRead("What is your friend's email? ", out string email);
				if (string.IsNullOrEmpty(email))
					break;

				friend = new User(name, email);
				m_user.AddFriend(friend);
				SaveUser();
				ConsoleWriteLogLine($"A friend request has been sent to {name}.");
				await MonitorUserAsync(friend);
				break;
			}

			State = States.Listening;
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private static void ListFriends()
		{
			EraseLog();
			if (m_user.Friends.Count == 0)
			{
				ConsoleWriteLogLine("You have no friends.");
				return;
			}

			ConsoleWriteLogLine("Friends:");
			foreach (User friend in m_user.Friends)
			{
				ConsoleWriteLogLine($"{friend.Name}" +
					$"{(friend.Blocked.HasValue ? (friend.Blocked.Value ? " (blocked)" : "") : " (pending)")}" +
					$"{(m_online.Any(x => x.Name == friend.Name) ? " (online)" : "")}");
			}
		}

		private static async Task UnfriendFriendAsync()
		{
			EraseLog();
			if (m_user.Friends.Count == 0)
			{
				ConsoleWriteLogLine("You have no friends.");
				return;
			}

			State = States.Busy;
			Point? cursor = default;
			User friend = ChooseFriend($"Which friend would you like to unfriend (number, Enter to abort): ",
				m_user.Friends, false, ref cursor);
			if (friend != null)
			{
				await RemoveUserAsync(friend, cursor);
			}
			else
			{
				Console.SetCursorPosition(cursor.Value.X, cursor.Value.Y);
				State = States.Listening;
			}
		}

		private static async Task RemoveUserAsync(User friend = null, Point? cursor = null)
		{
			bool delete = friend == null;
			if (delete)
			{
				EraseLog();
				State = States.Busy;
				friend = ChooseFriend($"Which user would you like to delete (number, Enter to abort): ",
					m_users, true, ref cursor);
			}

			do
			{
				if (friend != null)
				{
					if (delete)
					{
						_ = ConsoleWriteLogRead($"Are you sure you want to delete {friend.Name}? ", out ConsoleKeyInfo confirm);
						if (confirm.Key != ConsoleKey.Y)
							break;

						File.Delete($"{friend.FileName}");
					}

					await UnfriendAsync(friend);
					if (delete)
						ConsoleWriteLogLine($"User {friend.Name} has been deleted.");
					else
						ConsoleWriteLogLine($"Your friend {friend.Name} has been unfriended.");
				}
			}
			while (false);

			Console.SetCursorPosition(cursor.Value.X, cursor.Value.Y);
			State = States.Listening;
		}

		private static async Task ChatFriendAsync()
		{
			EraseLog();
			if (m_online.Count == 0)
			{
				ConsoleWriteLogLine("None of your friends is online.");
				return;
			}

			State = States.Busy;
			Point? cursor = default;
			User friend = ChooseFriend("Which friend would you like to chat with? (number, Enter to abort): ",
				m_online, false, ref cursor);
			Console.SetCursorPosition(cursor.Value.X, cursor.Value.Y);

			if (friend != null)
			{
				State = States.Connecting;
				ActiveChatGroupName = MakeChatGroupName(friend);
				await m_hubConnection.SendAsync(c_joinGroupChat, ActiveChatGroupName, Name);
			}
			else
			{
				State = States.Listening;
			}
		}

		private static async Task<bool> MessageLoopAsync()
		{
			EraseLog();
			State = States.Chatting;
			Console.SetCursorPosition(0, NextLine);
			Console.WriteLine($"Type messages, type '{c_leaveChatCommand}' to leave the chat.");
			bool success = true;
			while (true)
			{
				while (true)
				{
					if (State == States.Listening)
						return true;

					if (Console.KeyAvailable)
						break;

					await Task.Delay(100);
				}

				NextLine = Console.CursorTop;
				string message = Console.ReadLine();
				if (State != States.Chatting)
					return true;

				Console.CursorTop = NextLine;

				try
				{
					if (message == c_leaveChatCommand)
					{
						await m_hubConnection.SendAsync(c_leaveGroupChat, ActiveChatGroupName, Name);
						if (ActiveChatGroupName == ChatGroupName)
							await m_hubConnection.SendAsync(c_joinGroupChat, ChatGroupName, Name);

						ActiveChatGroupName = null;
						break;
					}

					await m_hubConnection.SendAsync(c_sendGroupMessage, Name, ActiveChatGroupName, message);
				}
				catch (Exception exception)
				{
					Console.WriteLine($"Error sending message, exception: {exception.Message}");
					success = false;
					break;
				}
			}

			DisplayMenu();
			return success;
		}

		private static void SaveUser()
		{
			File.WriteAllText($"{FileName}", JsonSerializer.Serialize(m_user, m_serializerOptions));
		}

		private static async Task MonitorChannelsAsync()
		{
			await m_hubConnection.SendAsync(c_joinGroupChat, GroupName, Name);
			await m_hubConnection.SendAsync(c_joinGroupChat, ChatGroupName, Name);
		}

		private static async Task MonitorFriendsAsync()
		{
			foreach (User user in m_user.Friends.Where(u => u.Blocked != true))
				await MonitorUserAsync(user);
		}

		private static async Task MonitorUserAsync(User user)
		{
			await m_hubConnection.SendAsync(c_joinGroupChat, MakeGroupName(user), Name);
		}

		private static User ChooseFriend(string prompt, List<User> users, bool delete, ref Point? cursor)
		{
			ConsoleWriteLogLine($"{(delete ? "Users:" : "Friends:")}");
			int index;
			for (index = 0; index < users.Count; index++)
			{
				User user = users[index];
				ConsoleWriteLogLine($"{string.Format("{0:X}", index)}: {user.Name}{(delete ? $" ({user.FileName})" : "")}");
			}

			cursor = ConsoleWriteLogRead(prompt, out ConsoleKeyInfo selection);

			while (true)
			{
				if (selection.Key == ConsoleKey.Enter)
					return null;

				User user = int.TryParse(selection.KeyChar.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
					out index) && index >= 0 && index < users.Count ? users[index] : null;

				if (user != null)
					return user;

				cursor = ConsoleWriteLogRead($"{selection.KeyChar} not valid, enter a number between 1 and" +
					$" {Math.Min(users.Count, 9)}, please try again: ", out selection);
			}
		}

		private static async Task HelloAsync(string from, ConnectionCommand command)
		{
			if (State == States.Connecting)
			{
				if (command.Flag == true)
				{
					_ = await MessageLoopAsync();
				}
				else
				{
					await m_hubConnection.SendAsync(c_leaveGroupChat, ActiveChatGroupName, Name);
					ConsoleWriteLogLine($"Your friend can't chat at the moment.");
					State = States.Listening;
				}
			}
			else
			{
				await CheckFriendshipAsync(from, command);
			}
		}

		private static async Task CheckFriendshipAsync(string from, ConnectionCommand command)
		{
			User friend = m_user.Friends.FirstOrDefault(x => x.Name == from);
			if (command.Flag == false || friend == null || (!command.Flag.HasValue && friend.Blocked == false))
			{
				if (friend != null && !friend.Blocked.HasValue)
				{
					ConsoleWriteLogLine($"{friend.Name} has blocked you. {friend.Name} must unfriend you" +
						$" before you can become friends.");
				}

				await UnfriendAsync(friend);
				return;
			}
			else if (friend == null)
			{
				// TODO: delete this, this case is already handled above!
				SendCommand(CommandNames.Verify, GroupName, from, GroupName, null);
			}
			else if (!friend.Blocked.HasValue)
			{
				// TODO: what is this case? It may not even be necessary? A comment would be good
				SendCommand(CommandNames.Verify, MakeGroupName(friend), from, GroupName, null);
			}

			if (!m_online.Contains(friend))
			{
				ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Name} is online.");
				m_online.Add(friend);
			}
		}

		private static async Task VerifyFriendAsync(string from, ConnectionCommand command)
		{
			User friend = GetUserFromGroupName(command.Data);
			User user = m_user.Friends.FirstOrDefault(u => u.Name == friend.Name);
			if (!command.Flag.HasValue)
			{
				Point cursor = ConsoleWriteLogRead($"Accept friend request from {friend.Name}," +
					$" email address {friend.InternetId}? [y/n] ", out ConsoleKeyInfo confirm);
				friend.Blocked = confirm.Key != ConsoleKey.Y;
				if (user != null)
					m_user.Friends.Remove(user);

				m_user.AddFriend(friend);
				SaveUser();
				SendCommand(CommandNames.Verify, GroupName, from, GroupName, !friend.Blocked);
				if (!friend.Blocked ?? false)
				{
					m_online.Add(friend);
					await m_hubConnection.SendAsync(c_joinGroupChat, command.Data);
				}

				Console.SetCursorPosition(cursor.X, cursor.Y);
				user = friend;
			}
			else
			{
				user.Blocked = !command.Flag;
				SaveUser();
				if (command.Flag == false)
				{
					await m_hubConnection.SendAsync(c_leaveGroupChat, command.Data, Name);
					_ = m_online.Remove(user);
				}
			}

			ConsoleWriteLogLine($"You and {user.Name} are {(user.Blocked.Value ? "not" : "now")} friends!");
		}

		private static async Task UnfriendAsync(User friend)
		{
			SendCommand(CommandNames.Hello, GroupName, friend.Name, null, false);
			if (!friend.Blocked ?? true)
				await m_hubConnection.SendAsync(c_leaveGroupChat, MakeGroupName(friend), Name);

			_ = m_online.Remove(friend);
			_ = m_user.Friends.Remove(friend);
			SaveUser();
		}

		private static async Task<bool> WaitAsync(string message, int intervalms = 100, int timeouts = 10)
		{
			TimeSpan interval = TimeSpan.FromMilliseconds(intervalms);
			DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(timeouts);
			Console.Write(message);
			Point cursorPosition = new Point(Console.CursorLeft, Console.CursorTop);
			NextLine = cursorPosition.Y;
			char bullet = '.';
			for (int x = cursorPosition.X; State != States.Changing && DateTime.Now < timeout; x++)
			{
				if (x >= cursorPosition.X + 5)
				{
					bullet = bullet == '.' ? ' ' : '.';
					x = cursorPosition.X;
					Console.SetCursorPosition(cursorPosition.X, cursorPosition.Y);
				}

				Console.SetCursorPosition(x, cursorPosition.Y);
				Console.Write(bullet);

				await Task.Delay(interval);
			}

			Console.SetCursorPosition(0, NextLine + 1);
			return DateTime.Now < timeout;
		}

		private static void DisplayMenu()
		{
			Console.WriteLine("\nPick a command: a to add a friend, l to list friends, u to unfriend a friend,");
			Console.WriteLine("d to delete a user, c to chat, or x to exit");
			PromptLine = Console.CursorTop;
			NextLine = PromptLine + 2;
			State = States.Listening;
		}

		private static void EraseLog()
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			NextLine = PromptLine + 2;
			Console.SetCursorPosition(0, NextLine);
			ConsoleColor color = Console.ForegroundColor;
			Console.ForegroundColor = Console.BackgroundColor;
			foreach (string line in m_log)
				Console.WriteLine(line);

			m_log.Clear();
			Console.ForegroundColor = color;
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private static void ConsoleWriteLogLine(string line)
		{
			Point cursor = MoveCursorToLog();
			Console.WriteLine(line);
			m_log.Add(line);
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private static Point ConsoleWriteLog(string line)
		{
			Point cursor = MoveCursorToLog();
			Console.Write(line);
			m_log.Add(line);
			return cursor;
		}

		private static Point ConsoleWriteLogRead(string line, out string value)
		{
			Point cursor = MoveCursorToLog();
			Console.Write(line);
			value = Console.ReadLine();
			m_log.Add(line + value);
			return cursor;
		}

		private static Point ConsoleWriteLogRead(string line, out ConsoleKeyInfo confirm)
		{
			Point cursor = MoveCursorToLog();
			Console.Write(line);
			confirm = Console.ReadKey();
			m_log.Add(line + confirm.KeyChar);
			return cursor;
		}

		private static Point MoveCursorToLog()
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			Console.SetCursorPosition(0, NextLine++);
			return cursor;
		}

		private static string MakeGroupName(User user)
		{
			return $"{user.InternetId}\n{user.Name}";
		}

		private static string MakeGroupName(string name, string email)
		{
			return $"{email}\n{name}";
		}

		private static string MakeChatGroupName(User user)
		{
			return $"{MakeGroupName(user)}\n{c_chatGroupName}";
		}

		private static User GetUserFromGroupName(string groupName)
		{
			string[] parts = ParseGroupName(groupName);
			return new User(parts[1], parts[0]);
		}

		private static string[] ParseGroupName(string groupName)
		{
			return groupName.Split('\n');
		}


		private enum States
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

		private static HubConnection m_hubConnection;
#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif

		private static User m_user;
		private static States m_state;
		private static readonly List<User> m_users = new List<User>();
		private static readonly List<User> m_online = new List<User>();
		private static readonly List<string> m_log = new List<string>();
		private static readonly JsonSerializerOptions m_serializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true
		};
	}
}
