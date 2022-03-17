﻿using Microsoft.AspNetCore.SignalR.Client;
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

			await MonitorChannels();
			await MonitorFriendsAsync();

			DisplayMenu();

			while (true)
			{
				PromptLine = Console.CursorTop;
				Console.CursorLeft = 0;
				Console.Write($"{State}> ");
				while (!Console.KeyAvailable || State != States.Listening)
					await Task.Delay(10);

				ConsoleKeyInfo menu = Console.ReadKey(intercept: true);
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
						await DeleteFriendAsync();
						continue;
					case ConsoleKey.C:
						await ConnectFriendAsync();
						continue;
					case ConsoleKey.X:
						break;
					default:
						continue;
				}

				break;
			}

			foreach (User friend in m_user.Friends)
				await m_hubConnection.SendAsync(c_leaveGroupChat, MakeGroupName(friend), Name);

			await m_hubConnection.SendAsync(c_leaveGroupChat, GroupName, Name);
			await m_hubConnection.StopAsync();
			_ = MoveCursorToLog();
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
		public const string c_chatGroupName = "Chat";


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
		private static string GroupName => MakeGroupName(Name, Email);
		public static string ChatGroupName => MakeChatGroupName(m_user);
		public static string ActiveChatGroupName { get; set; }
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
				string[] parts = ParseGroupName(group);
				Debug.WriteLine($"{user} has joined the {string.Join('-', parts)} group.");
				if (group == GroupName)
				{
					User friend = m_user.Friends.FirstOrDefault(x => x.Name == user);
					SendCommand(CommandNames.Hello, group, user, friend?.Verified);
					if (friend != null && !m_online.Contains(friend))
						m_online.Add(friend);
				}
				else if (group == ChatGroupName)
				{
					SendCommand(CommandNames.Hello, group, user, State == States.Listening);
					ActiveChatGroupName = group;
					if (State == States.Listening)
						_ = await MessageLoopAsync();
				}
				else
				{
					CheckFriendshipPending(user);
				}
			}
		}

		private static void OnGroupMessage(string user, string message)
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			if (user == Name)
			{
				Console.WriteLine($"You said: {message}");
			}
			else if (cursor.X == 0)
			{
				Console.WriteLine($"{user} said: {message}");
			}
			else
			{
				Console.SetCursorPosition(0, cursor.Y + 1);
				Console.WriteLine($"{user} said: {message}");
				NextLine = Console.CursorTop;
				Console.SetCursorPosition(cursor.X, cursor.Y);
			}
		}

		private static async Task OnGroupCommandAsync(string sender, string json)
		{
			if (sender == Name)
				return;

			Debug.WriteLine($"Command from {sender}: {json}");
			ConnectionCommand command = DeserializeCommand(json);
			switch (command.CommandName)
			{
				case CommandNames.Hello:
					await HelloAsync(sender, command);
					break;
				case CommandNames.Verify:
					VerifyFriend(GetUserFromGroupName(command.Data), command.Flag);
					break;
				case CommandNames.Disonnect:
					await m_hubConnection.SendAsync(c_leaveGroupChat, ActiveChatGroupName, Name);
					DisplayMenu();
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
			string[] parts = ParseGroupName(group);
			Debug.WriteLine($"{user} has left the {string.Join('-', parts)} chat.");
			if (group == ActiveChatGroupName)
			{
				Console.Write($"{(Console.CursorLeft > 0 ? "\n" : "")}{user} has left the chat. (Hit Enter)");
				DisplayMenu();
			}
			else if (group == GroupName)
			{
				User friend = m_user.Friends.FirstOrDefault(u => u.Name == user);
				ConsoleWriteLogLine($"Your {(friend.Verified.HasValue ? "(pending) " : " ")}friend {user} is offline.");
				m_online.Remove(friend);
			}
		}

		private static async Task<bool> StartServer()
		{
			State = States.Initializing;
			Console.WriteLine($"Initializing server and connecting to URL: {c_chatHubUrl}");
			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();
			Initialize(async (g, j) => await m_hubConnection.SendAsync(c_sendGroupCommand, g, Name, j));

			_ = m_hubConnection.On<string>(c_register, (t) => OnRegister(t));
			_ = m_hubConnection.On(c_joinGroupMessage, (Action<string, string>) (async (g, u) => await OnGroupJoinAsync(g, u)));
			_ = m_hubConnection.On(c_receiveGroupMessage, (Action<string, string>) ((u, m) => OnGroupMessage(u, m)));
			_ = m_hubConnection.On(c_receiveGroupCommand, (Action<string, string>) (async (u, c) => await OnGroupCommandAsync(u, c)));
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
			if (string.IsNullOrEmpty(name))
				return false;

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
				Console.WriteLine("'Timeout waiting for a response from the server, aborting.");
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
			State = States.Busy;
			Point cursor;
			do
			{
				cursor = ConsoleWriteLog("What is your friend's name? ");
				string name = Console.ReadLine();
				if (string.IsNullOrEmpty(name))
					break;

				User friend = m_user.Friends.FirstOrDefault(x => x.Name == name);
				if (friend != null)
				{
					if (!friend.Verified.HasValue)
						Console.WriteLine($"You already asked {name} to be your friend and we're waiting for a response.");
					else if (!friend.Verified.Value)
						Console.WriteLine($"{name} has denied your friend request.");
					else
						Console.WriteLine($"{name} is already your friend!");

					break;
				}

				ConsoleWriteLog("What is your friend's email? ");
				string email = Console.ReadLine();
				if (string.IsNullOrEmpty(email))
					break;

				friend = new User(name, email);
				m_user.AddFriend(friend);
				SaveUser();
				ConsoleWriteLogLine($"A friend request has been sent to {name}.");
				await MonitorUserAsync(friend);
			}
			while (false);

			State = States.Listening;
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private static void ListFriends()
		{
			ConsoleWriteLogLine("Friends:");
			foreach (User friend in m_user.Friends)
			{
				string verified = friend.Verified.HasValue ? (friend.Verified.Value ? "" : " (blocked)") : " (pending)";
				string online = m_online.Any(x => x.Name == friend.Name) ? " (online)" : "";
				ConsoleWriteLogLine($"{friend.Name}{verified}{online}");
			}
		}

		private static async Task UnfriendFriendAsync()
		{
			await DeleteFriendAsync(false);
		}

		private static async Task DeleteFriendAsync(bool delete = true)
		{
			State = States.Busy;
			Point cursor = default;
			User friend = ChooseFriend($"Which friend would you like to {(delete ? "delete" : "unfriend")}" +
				$" (number, Enter to abort): ", m_user.Friends, ref cursor);
		
			if (friend != null)
			{
				if (delete)
				{
					_ = ConsoleWriteLog($"Are you sure you want to delete {friend.Name}? ");
					if (Console.ReadKey(intercept: true).Key != ConsoleKey.Y)
						return;

					_ = m_users.Remove(friend);
					File.Delete($"{friend.Name}.qkr.json");
				}

				await m_hubConnection.SendAsync(c_leaveGroupChat, MakeGroupName(friend), Name);
				_ = m_user.Friends.Remove(friend);
				SaveUser();
				ConsoleWriteLogLine($"User {friend.Name} has been {(delete ? "deleted." : "unfriended")}");
			}

			Console.SetCursorPosition(cursor.X, cursor.Y);
			State = States.Listening;
		}

		private static async Task ConnectFriendAsync()
		{
			if (m_online.Count == 0)
			{
				ConsoleWriteLogLine("None of your friends is online.");
				return;
			}

			State = States.Busy;
			Point cursor = default;
			User friend = ChooseFriend("Which friend would you like to chat with? (number, Enter to abort): ", m_online, ref cursor);
			Console.SetCursorPosition(cursor.X, cursor.Y);
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
			State = States.Chatting;
			Console.SetCursorPosition(0, NextLine);
			Console.WriteLine("\nType messages, type 'goodbye' to leave the chat.");
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
					if (message == "goodbye")
					{
						if (ActiveChatGroupName == ChatGroupName)
							SendCommand(CommandNames.Disonnect, ActiveChatGroupName);
						else
							await m_hubConnection.SendAsync(c_leaveGroupChat, ActiveChatGroupName, Name);

						break;
					}

					await m_hubConnection.SendAsync(c_sendGroupMessage, ActiveChatGroupName, Name, message);
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
			File.WriteAllText($"{Name}.qkr.json", JsonSerializer.Serialize(m_user, m_serializerOptions));
		}

		private static async Task MonitorChannels()
		{
			await m_hubConnection.SendAsync(c_joinGroupChat, GroupName, Name);
			await m_hubConnection.SendAsync(c_joinGroupChat, ChatGroupName, Name);
		}

		private static async Task MonitorFriendsAsync()
		{
			foreach (User user in m_user.Friends.Where(u => u.Verified.Value == true))
				await MonitorUserAsync(user);
		}

		private static async Task MonitorUserAsync(User user)
		{
			await m_hubConnection.SendAsync(c_joinGroupChat, MakeGroupName(user), Name);
		}

		private static User ChooseFriend(string prompt, List<User> friends, ref Point cursor)
		{
			ConsoleWriteLogLine("Friends:");
			int index;
			for (index = 0; index < friends.Count; index++)
			{
				User friend = friends[index];
				ConsoleWriteLogLine($"{index + 1}: {friend.Name}");
			}

			_ = ConsoleWriteLog(prompt);

			while (true)
			{
				ConsoleKeyInfo selection = Console.ReadKey();
				if (selection.Key == ConsoleKey.Enter)
					return null;

				User friend = int.TryParse(selection.KeyChar.ToString(), out index) &&
					index > 0 && index <= friends.Count ? friends[index - 1] : null;

				if (friend != null)
					return friend;

				cursor = ConsoleWriteLog($"\n{selection.KeyChar} not valid, enter a number between 1 and" +
					$" {Math.Min(friends.Count, 9)}, please try again: ");
			}
		}

		private static async Task HelloAsync(string sender, ConnectionCommand command)
		{
			if (command.Data == Name)
			{
				if (State == States.Connecting)
				{
					if (command.Flag == true)
					{
						_ = await MessageLoopAsync();
					}
					else
					{
						ConsoleWriteLogLine($"Your friend can't chat at the moment.");
						State = States.Listening;
					}
				}
				else
				{
					CheckFriendshipPending(sender);
				}
			}
		}

		private static void CheckFriendshipPending(string sender)
		{
			User friend = m_user.Friends.FirstOrDefault(x => x.Name == sender);
			if (friend == null)
			{
				return;
			}
			else if (!friend.Verified.HasValue)
			{
				ConsoleWriteLogLine($"Your pending friend {sender} is online.");
				SendCommand(CommandNames.Verify, MakeGroupName(friend), GroupName, true);
			}
			else if (friend.Verified.Value == true)
			{
				ConsoleWriteLogLine($"Your friend {sender} is online.");
			}

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
				m_online.Add(friend);
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
			Console.WriteLine("d to delete a friend, c to chat, or x to exit");
			PromptLine = Console.CursorTop;
			NextLine = Console.CursorTop;
			State = States.Listening;
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
			Console.SetCursorPosition(0, ++NextLine);
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
			Chatting
		}

		private static HubConnection m_hubConnection;
		private static readonly object m_lock = new object();
#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif

		private static User m_user;
		private static States m_state;
		private static readonly List<User> m_users = new List<User>();
		private static readonly List<User> m_online = new List<User>();
		private static readonly JsonSerializerOptions m_serializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true
		};
	}
}
