using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
		private static async Task Main(string[] args)
		{
			if (args is null)
				throw new ArgumentNullException(nameof(args));

			if (!await StartServer())
				return;

			if (!await LoadUsersAsync())
				return;

			Console.WriteLine("Pick a command: a to add a friend, l to list friends, c to connect, x to exit");
			NextLine = Console.CursorTop;
			await MonitorUserAsync(m_user);
			await MonitorFriendsAsync();
			while (true)
			{
				ConsoleKeyInfo menu = Console.ReadKey(intercept: true);
				switch (menu.Key)
				{
					case ConsoleKey.A:
						await AddFriendAsync();
						continue;
					case ConsoleKey.L:
						ListFriends();
						continue;
					case ConsoleKey.C:
						await ConnectAsync();
						continue;
					case ConsoleKey.X:
						break;
					default:
						continue;
				}

				break;
			}

			while (false)
			{
				Console.WriteLine("\nTo connect to a friend, enter your friend's email,");
				Console.Write("or Enter to listen for a connection, or exit to quit: ");
				string friendEmail = Console.ReadLine();
				if (friendEmail == "exit")
					return;

				while (true)
				{
					if (string.IsNullOrEmpty(friendEmail) ? await ListenAsync() : await ConnectAsync())
						break;

					Console.WriteLine("Is your friend connected? Enter your friend's email again or");
					Console.Write("Enter to listen for a connection or exit to quit: ");
					friendEmail = Console.ReadLine();
					if (friendEmail == "exit")
						return;
				}

				// TODO: recover from error?
				if (!await MessageLoopAsync())
					break;
			}

			foreach (User friend in m_user.Friends)
				await m_hubConnection.SendAsync(c_leaveGroupChat, MakeGroupName(friend), Name);
			
			await m_hubConnection.SendAsync(c_leaveGroupChat, GroupName, Name);
			await m_hubConnection.StopAsync();
			MoveCursorToLog();
			Console.WriteLine("\nFinished.");
		 }


		public const string c_register = "Register";
		public const string c_joinGroupChat = "JoinGroupChat";
		public const string c_joinGroupMessage = "JoinGroupMessage";
		public const string c_sendGroupMessage = "SendGroupMessage";
		public const string c_receiveGroupMessage = "ReceiveGroupMessage";
		public const string c_sendGroupCommand = "SendGroupCommand";
		public const string c_receiveGroupCommand = "ReceiveGroupCommand";
		public const string c_leaveGroupChat = "LeaveGroupChat";
		public const string c_leaveGroupMessage = "LeaveGroupMessage";
		public const string c_handshake = "Hello QKR.";
		public const string c_chatGroupName = "Chat";

		private static States State { get; set; }
		private static string RegistrationToken { get; set; }
		private static string Name => m_user?.Name;
		private static string Email => m_user?.InternetId;
		private static string GroupName => MakeGroupName(Name, Email);
		public static string ChatGroupName { get; private set; }
		private static int? NextLine { get; set; }

		private static void OnRegister(string token)
		{
			lock (m_lock)
			{
				ConsoleColor color = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Yellow;
				ConsoleWriteLogLine($"Registration token from server: {token}");
				Console.ForegroundColor = color;
				RegistrationToken = token;
				State = States.Changing;
			}
		}

		private static void OnGroupJoin(string group, string user)
		{
			if (user != Name)
			{
				if (group == GroupName)
				{
					User friend = m_user.Friends.FirstOrDefault(x => x.Name == user);
					ConsoleWriteLogLine($"{user} has joined your {friend.InternetId}-{friend.Name} group.");
					SendCommand(CommandNames.Hello, group, user, friend.Verified);
					if (!m_online.Contains(friend))
						m_online.Add(friend);
				}
				else if (group == ChatGroupName)
				{
				}
				else
				{
					CheckFriendshipPending(user);
				}

				lock (m_lock)
					State = States.Changing;
			}
		}

		private static void OnGroupMessage(string user, string message)
		{
			lock (m_lock)
			{
				if (State == States.Connecting)
					State = States.Changing;
				else if (message == c_handshake)
					return;
				else if (user == Name)
					Console.WriteLine($"You said: {message}");
				else
					Console.WriteLine($"{user} said: {message}");
			}
		}

		private static void OnGroupCommand(string sender, string json)
		{
			if (sender == Name)
				return;

			Debug.WriteLine($"Command from {sender}: {json}");
			ConnectionCommand command = DeserializeCommand(json);
			switch (command.CommandName)
			{
				case CommandNames.Hello:
					Hello(sender, command);
					break;
				case CommandNames.Verify:
					VerifyFriend(GetUserFromGroupName(command.Data), command.Flag);
					break;
				case CommandNames.Handle:
				case CommandNames.Echo:
					Debug.WriteLine($"OnGroupCommandAsync not processing command: {command.CommandName}");
					break;
				case CommandNames.Unrecognized:
				default:
					Debug.WriteLine($"Error in OnGroupCommandAsync, unrecognized command: {command.CommandName}");
					break;
			}
		}

		private static void OnGroupLeave(string group, string user)
		{
			State = States.Leaving;
			User friend = m_user.Friends.FirstOrDefault(u => u.Name == user);
			lock (m_lock)
				ConsoleWriteLogLine($"{user} has left the {group.Replace('\n', ' ')} chat.");
			m_online.Remove(friend);
		}

		private static async Task<bool> StartServer()
		{
			State = States.Initializing;
			Console.WriteLine($"Initializing server and connecting to URL: {c_chatHubUrl}");
			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();
			Initialize(async (g, j) => await m_hubConnection.SendAsync(c_sendGroupCommand, g, Name, j));

			_ = m_hubConnection.On<string>(c_register, (t) => OnRegister(t));
			_ = m_hubConnection.On(c_joinGroupMessage, (Action<string, string>) ((g, u) => OnGroupJoin(g, u)));
			_ = m_hubConnection.On(c_receiveGroupMessage, (Action<string, string>) ((u, m) => OnGroupMessage(u, m)));
			_ = m_hubConnection.On(c_receiveGroupCommand, (Action<string, string>) ((u, c) => OnGroupCommand(u, c)));
			_ = m_hubConnection.On(c_leaveGroupMessage, (Action<string, string>) ((g, u) => OnGroupLeave(g, u)));

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

		private static async Task<bool> LoadUsersAsync()
		{
			Console.Write("What is your name? ");
			string name = Console.ReadLine();

			foreach (string fileName in Directory.EnumerateFiles(".").Where(x => x.EndsWith(".qkr.json")))
				m_users.Add(JsonSerializer.Deserialize<User>(File.ReadAllText(fileName)));
			
			m_user = m_users.FirstOrDefault(u => u.Name == name);
			if (m_user == null)
			{
				string email = await RegisterAsync();
				if (email == null)
					return false;

				m_user = new User(name, email, true);
				m_users.Add(m_user);
				SaveUser();
			}

			return true;
		}

		private static async Task AddFriendAsync()
		{
			Point cursor = ConsoleWriteLog("What is your friend's name? ");
			string name = Console.ReadLine();
			ConsoleWriteLog("What is your friend's email? ");
			string email = Console.ReadLine();
			Console.SetCursorPosition(cursor.X, cursor.Y);

			User friend = new User(name, email);
			m_user.AddFriend(friend);
			SaveUser();
			await MonitorUserAsync(friend);
		}

		private static async Task MonitorFriendsAsync()
		{
			foreach (User user in m_user.Friends)
				await MonitorUserAsync(user);
		}

		private static async Task<string> RegisterAsync()
		{
			State = States.Registering;
			Console.Write("What is your email address? ");
			string email = Console.ReadLine();

			await m_hubConnection.SendAsync(c_register, email);
			if (!await WaitAsync("Waiting for the server"))
			{
				Console.WriteLine("'Timeout waiting for a response from the server, aborting.");
				return null;
			}

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

		private static async Task<bool> ListenAsync()
		{
			State = States.Listening;
			ChatGroupName = MakeGroupName(GroupName, c_chatGroupName);
			await m_hubConnection.SendAsync(c_joinGroupChat, ChatGroupName, Name);
			if (await WaitAsync("Listening for a connection", 100, 600))
			{
				await m_hubConnection.SendAsync(c_sendGroupMessage, ChatGroupName, Name, c_handshake);
				return true;
			}

			return false;
		}

		private static void ListFriends()
		{
			ConsoleWriteLogLine("Friends:");
			foreach (User friend in m_user.Friends)
				ConsoleWriteLogLine($"{friend.Name}");
		}

		private static async Task<bool> ConnectAsync()
		{
			State = States.Connecting;
			if (m_online.Count == 0)
			{
				ConsoleWriteLogLine("None of your friends is online.");
				return false;
			}

			ConsoleWriteLogLine("Friends:");
			int index = 1;
			foreach (User friend in m_online)
				ConsoleWriteLogLine($"{index}: {friend.Name}");

			while (true)
			{
				ConsoleWriteLogLine("Which friend would you like to connect with? (Enter to abort) ");
				string name = Console.ReadLine();
				if (string.IsNullOrEmpty(name))
					return false;

				User friend = int.TryParse(name, out index) && index > 0 && index <= m_online.Count ?
					m_online[index - 1] : m_user.Friends.FirstOrDefault(x => x.Name == name);
				if (friend == null)
				{
					ConsoleWriteLogLine($"Friend {name} not found, pease try again.");
					continue;
				}

				ChatGroupName = MakeChatGroupName(friend);
				await m_hubConnection.SendAsync(c_joinGroupChat, ChatGroupName, Name);
				return await WaitAsync($"Connecting to {name}");
			}
		}

		private static async Task<bool> MessageLoopAsync()
		{
			State = States.Chatting;
			Console.WriteLine("\nType messages, type 'goodbye' to leave the chat.");
			while (true)
			{
				string message = Console.ReadLine();
				if (State != States.Chatting)
					return true;

				Console.CursorTop--;
				
				try
				{
					if (message == "goodbye")
					{
						await m_hubConnection.SendAsync(c_leaveGroupChat, GroupName, Name);
						return true;
					}

					await m_hubConnection.SendAsync(c_sendGroupMessage, GroupName, Name, message);
				}
				catch (Exception exception)
				{
					Console.WriteLine($"Error sending message, exception: {exception.Message}");
					return false;
				}
			}
		}

		private static void SaveUser()
		{
			File.WriteAllText($"{Name}.qkr.json", JsonSerializer.Serialize(m_user, m_serializerOptions));
		}

		private static async Task MonitorUserAsync(User user)
		{
			await m_hubConnection.SendAsync(c_joinGroupChat, MakeGroupName(user), Name);
		}

		private static void Hello(string sender, ConnectionCommand command)
		{
			if (command.Data == Name)
				CheckFriendshipPending(sender);
		}

		private static void CheckFriendshipPending(string sender)
		{
			User friend = m_user.Friends.FirstOrDefault(x => x.Name == sender);
			ConsoleWriteLogLine($"{sender} is online, is a verified friend:" +
				$" {(friend.Verified.HasValue ? (friend.Verified.Value ? "yes" : "no") : "pending")}.");
			if (!friend.Verified.HasValue)
				SendCommand(CommandNames.Verify, MakeGroupName(friend), GroupName, true);
			if (!m_online.Contains(friend))
				m_online.Add(friend);
		}

		private static void VerifyFriend(User friend, bool? verified)
		{
			User user = m_user.Friends.FirstOrDefault(u => u.Name == friend.Name);
			if (user == null)
			{
				Point cursor = ConsoleWriteLog($"Accept friend request from {friend.Name}," +
					$" email address {friend.InternetId}? [y/n] ");
				ConsoleKeyInfo confirm = Console.ReadKey(intercept: true);
				friend.Verified = confirm.Key == ConsoleKey.Y;
				m_user.AddFriend(friend);
				SaveUser();
				SendCommand(CommandNames.Verify, GroupName, GroupName, friend.Verified);
				Console.SetCursorPosition(cursor.X, cursor.Y);
				user = friend;
			}
			else
			{
				user.Verified = verified;
				SaveUser();
			}
			
			ConsoleWriteLogLine($"You and {user.Name} are {(user.Verified.Value ? "now" : "not")} friends!");
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
				lock (m_lock)
				{
					if (x >= cursorPosition.X + 5)
					{
						bullet = bullet == '.' ? ' ' : '.';
						x = cursorPosition.X;
						Console.SetCursorPosition(cursorPosition.X, cursorPosition.Y);
					}

					Console.SetCursorPosition(x, cursorPosition.Y);
					Console.Write(bullet);
				}

				await Task.Delay(interval);
			}

			lock (m_lock)
			{
				Console.SetCursorPosition(0, NextLine.Value + 1);
				NextLine = null;
			}

			return DateTime.Now < timeout;
		}

		private static void ConsoleWriteLogLine(string line)
		{
			Point cursor = MoveCursorToLog();
			Console.WriteLine(line);
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private static Point ConsoleWriteLog(string line)
		{
			Point cursor = MoveCursorToLog();
			Console.Write(line);
			return cursor;
		}

		private static Point MoveCursorToLog()
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			if (NextLine.HasValue)
			{
				NextLine = NextLine.Value + 1;
				Console.SetCursorPosition(0, NextLine.Value);
			}

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
			string[] parts = groupName.Split('\n'); 
			return new User(parts[1], parts[0]);
		}


		private enum States
		{
			Initializing,
			Changing,
			Registering,
			Listening,
			Connecting,
			Chatting,
			Leaving
		}

		private static HubConnection m_hubConnection;
		private static readonly object m_lock = new object();
#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif

		private static User m_user;
		private static readonly List<User> m_users = new List<User>();
		private static readonly List<User> m_online = new List<User>();
		private static readonly JsonSerializerOptions m_serializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true
		};
	}
}
