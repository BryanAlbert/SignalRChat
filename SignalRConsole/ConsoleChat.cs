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
	public class ConsoleChat
	{
		public async Task<int> Run(string[] args)
		{
			if (!await StartServerAsync())
				return -1;

			if (!await LoadUsersAsync(args.Length > 0 ? args[0] : null))
				return -2;

			Initialize(m_hubConnection, Id);
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
					await m_hubConnection.SendAsync(c_leaveChannel, friend.Id ?? MakeChannelName(friend), Id);

				await m_hubConnection.SendAsync(c_leaveChannel, IdChannelName, Id);
				await m_hubConnection.SendAsync(c_leaveChannel, HandleChannelName, Id);
				await m_hubConnection.SendAsync(c_leaveChannel, ChatChannelName, Id);
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


		private States State
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

		private string RegistrationToken { get; set; }
		private string Handle => m_user?.Handle;
		private string Id => m_user?.Id;
		private string Email => m_user?.Email;
		private string FileName => m_user.FileName;
		private string HandleChannelName => MakeChannelName(Handle, Email);
		private string IdChannelName => m_user.Id;
		private string ChatChannelName => MakeChatChannelName(m_user);
		private string ActiveChatChannelName { get; set; }
		private User ActiveChatFriend { get; set; }
		private int NextLine { get; set; }
		private int PromptLine { get; set; }


		private void OnRegister(string token)
		{
			ConsoleColor color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Yellow;
			ConsoleWriteLogLine($"Registration token from server: {token}");
			Console.ForegroundColor = color;
			RegistrationToken = token;
			State = States.Changing;
		}

		private async Task OnJoinedChannelAsync(string channel, string user)
		{
			if (user != Id && user != Handle)
			{
				User friend = null;
				Debug.WriteLine($"{user} has joined the {string.Join('-', ParseChannelName(channel))} channel.");
				if (channel == IdChannelName || channel == HandleChannelName)
				{
					friend = m_user.Friends.FirstOrDefault(x => x.Id == user);
				}
				else if (channel == ChatChannelName)
				{
					if (State == States.Listening)
					{
						await SendCommandAsync(CommandNames.Hello, channel, user, SerializeUserData(m_user), true);
						ActiveChatChannelName = channel;
						ActiveChatFriend = m_user.Friends.FirstOrDefault(x => x.Id == user);
						_ = await MessageLoopAsync();
					}
					else
					{
						await SendCommandAsync(CommandNames.Hello, channel, user, SerializeUserData(m_user), false);
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
						if (friend != null && user == parts[1])
						{
							// pending friend joined his Handle channel, we leave and rejoin to signal him to send Hello with null
							await m_hubConnection.SendAsync(c_leaveChannel, channel, Id);
							await MonitorUserAsync(friend);
							return;
						}
						else
						{
							// someone else joined a Handle channel we're monitoring, just ignore it
							return;
						}
					}
				}

				// since we expect two JoinedChannel messages (one for our channel, one for the friend's channel)
				// we need to lock here so that we don't add the user to the online list twice
				lock (m_lock)
				{
					if (friend != null && (!friend.Blocked ?? true) && !m_online.Contains(friend))
					{
						ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Handle} is online.");
						m_online.Add(friend);
					}
				}

				await SendCommandAsync(CommandNames.Hello, channel, user, SerializeUserData(m_user), !friend?.Blocked);
			}
		}

		private void OnSentMessage(string from, string message)
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			if (from == Id)
			{
				Console.WriteLine($"You said: {message}");
			}
			else if (cursor.X == 0)
			{
				// TODO: use the friend's Handle (track it--make ActiveChatFriend a User)
				Console.WriteLine($"{ActiveChatFriend.Handle} said: {message}");
			}
			else
			{
				Console.SetCursorPosition(0, cursor.Y + 1);
				Console.WriteLine($"{from} said: {message}");
				NextLine = Console.CursorTop;
				Console.SetCursorPosition(cursor.X, cursor.Y);
			}
		}

		private async Task OnSentCommandAsync(string from, string to, string json)
		{
			if (to == Id || to == Handle)
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
						Debug.WriteLine($"Error in OnSentCommandAsync, unrecognized command: {command.CommandName}");
						break;
				}
			}
		}

		private void OnLeftChannel(string channel, string user)
		{
			string[] parts = ParseChannelName(channel);
			Debug.WriteLine($"{user} has left the {string.Join('-', parts)} channel.");
			if (channel == IdChannelName)
			{
				User friend = m_user.Friends.FirstOrDefault(u => u.Id == user);
				if (friend != null && (!friend.Blocked ?? true))
				{
					ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Handle} is offline.");
					m_online.Remove(friend);
					}
			}
		}

		private async Task<bool> StartServerAsync()
		{
			State = States.Initializing;
			Console.WriteLine($"Initializing server and connecting to URL: {c_chatHubUrl}");
			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();

			_ = m_hubConnection.On<string>(c_register, (t) => OnRegister(t));
			_ = m_hubConnection.On(c_joinedChannel, (Action<string, string>) (async (c, u) => await OnJoinedChannelAsync(c, u)));
			_ = m_hubConnection.On(c_sentMessage, (Action<string, string>) ((f, m) => OnSentMessage(f, m)));
			_ = m_hubConnection.On(c_sentCommand, (Action<string, string, string>) (async (f, t, c) => await OnSentCommandAsync(f, t, c)));
			_ = m_hubConnection.On(c_leftChannel, (Action<string, string>) ((c, u) => OnLeftChannel(c, u)));

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

		private async Task<bool> LoadUsersAsync(string handle)
		{
			if (string.IsNullOrEmpty(handle) && !GetData("What is your handle (nickname)? ", out handle))
				return false;

			foreach (string fileName in Directory.EnumerateFiles(".").Where(x => x.EndsWith(c_fileExtension)))
			{
				User user = JsonSerializer.Deserialize<User>(File.ReadAllText(fileName));
				user.FileName = fileName;
				m_users.Add(user);
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

				m_user = new User(handle, email, name, color, Guid.NewGuid().ToString(), $"{handle}{c_fileExtension}");
				m_users.Add(m_user);
				SaveUser();
			}

			return true;
		}

		private async Task<string> RegisterAsync()
		{
			State = States.Registering;
			if (!GetData("What is your email address? ", out string email))
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

				User friend = m_user.Friends.FirstOrDefault(x => x.Handle == handle);
				if (friend != null)
				{
					if (!friend.Blocked.HasValue)
						ConsoleWriteLogLine($"You already asked {handle} to be your friend and we're waiting for a response.");
					else if (friend.Blocked.Value)
						ConsoleWriteLogLine($"You and {handle} are blocked. Both you and {handle} must unfriend to try again.");
					else
						ConsoleWriteLogLine($"{handle} is already your friend!");

					break;
				}

				_ = ConsoleWriteLogRead("What is your friend's email? ", out string email);
				if (string.IsNullOrEmpty(email))
					break;

				friend = new User(handle, email);
				m_user.AddFriend(friend);
				SaveUser();
				ConsoleWriteLogLine($"A friend request has been sent to {handle}.");
				await MonitorUserAsync(friend);
				break;
			}

			State = States.Listening;
			Console.SetCursorPosition(cursor.X, cursor.Y);
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
			foreach (User friend in m_user.Friends)
			{
				ConsoleWriteLogLine($"{friend.Handle},{(friend.Color != null ? $" {friend.Color}" : "")}" +
					$"{(friend.Blocked.HasValue ? (friend.Blocked.Value ? " (blocked)" : "") : " (pending)")}" +
					$"{(m_online.Any(x => x.Id == friend.Id) ? " (online)" : "")}");
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
			Point? cursor = default;
			User friend = ChooseFriend($"Whom would you like to unfriend (number, Enter to abort): ",
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

		private async Task RemoveUserAsync(User friend = null, Point? cursor = null)
		{
			bool delete = friend == null;
			if (delete)
			{
				EraseLog();
				State = States.Busy;
				friend = ChooseFriend($"Whom would you like to delete (number, Enter to abort): ",
					m_users, true, ref cursor);
			}

			do
			{
				if (friend != null)
				{
					if (delete)
					{
						_ = ConsoleWriteLogRead($"Are you sure you want to delete {friend.Handle}? ", out ConsoleKeyInfo confirm);
						if (confirm.Key != ConsoleKey.Y)
							break;

						File.Delete($"{friend.FileName}");
					}

					await SendCommandAsync(CommandNames.Hello, friend.Id, friend.Id, SerializeUserData(m_user), false);
					await UnfriendAsync(friend);
					if (delete)
						ConsoleWriteLogLine($"User {friend.Handle} has been deleted.");
					else
						ConsoleWriteLogLine($"Your friend {friend.Handle} has been unfriended.");
				}
			}
			while (false);

			Console.SetCursorPosition(cursor.Value.X, cursor.Value.Y);
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
			Point? cursor = default;
			User friend = ChooseFriend("Whom would you like to chat with? (number, Enter to abort): ",
				m_online, false, ref cursor);
			Console.SetCursorPosition(cursor.Value.X, cursor.Value.Y);

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

		private async Task<bool> MessageLoopAsync()
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
				if (m_waitForEnter)
				{
					m_waitForEnter = false;
					DisplayMenu();
					return true;
				}

				if (State != States.Chatting)
					return true;

				Console.CursorTop = NextLine;

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
					Console.WriteLine($"Error sending message, exception: {exception.Message}");
					success = false;
					break;
				}
			}

			DisplayMenu();
			return success;
		}

		private async Task LeaveChatChannelAsync(bool send)
		{
			if (send)
			{
				await SendCommandAsync(CommandNames.Hello, ActiveChatChannelName, ActiveChatFriend.Id,
					SerializeUserData(m_user), false);
			}

			if (ActiveChatChannelName != ChatChannelName)
				await m_hubConnection.SendAsync(c_leaveChannel, ActiveChatChannelName, Id);

			ActiveChatChannelName = null;
			ActiveChatFriend = null;
		}

		private void SaveUser()
		{
			File.WriteAllText($"{FileName}", JsonSerializer.Serialize(m_user, m_serializerOptions));
		}

		private async Task MonitorChannelsAsync()
		{
			await m_hubConnection.SendAsync(c_joinChannel, HandleChannelName, Handle);
			await m_hubConnection.SendAsync(c_joinChannel, IdChannelName, Id);
			await m_hubConnection.SendAsync(c_joinChannel, ChatChannelName, Id);
		}

		private async Task MonitorFriendsAsync()
		{
			foreach (User user in m_user.Friends.Where(u => u.Blocked != true))
				await MonitorUserAsync(user);
		}

		private async Task MonitorUserAsync(User user)
		{
			if (!user.Blocked.HasValue || user.Id == null)
				await m_hubConnection.SendAsync(c_joinChannel,  MakeChannelName(user), Handle);
			else
				await m_hubConnection.SendAsync(c_joinChannel,  user.Id, Id);
		}

		private User ChooseFriend(string prompt, List<User> users, bool delete, ref Point? cursor)
		{
			ConsoleWriteLogLine($"{(delete ? "Users:" : "Friends:")}");
			int index;
			for (index = 0; index < users.Count; index++)
			{
				User user = users[index];
				ConsoleWriteLogLine($"{string.Format("{0:X}", index)}: {user.Handle}{(delete ? $" ({user.FileName})" : "")}");
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

		private async Task HelloAsync(string from, ConnectionCommand command)
		{
			User friend = UpdateFriendData(command.Data);
			if (State == States.Connecting)
			{
				if (command.Flag == true)
				{
					_ = await MessageLoopAsync();
				}
				else
				{
					await m_hubConnection.SendAsync(c_leaveChannel, ActiveChatChannelName, Id);
					ConsoleWriteLogLine($"{friend.Handle} can't chat at the moment.");
					State = States.Listening;
				}
			}
			else if (State == States.Chatting && command.Flag == false)
			{
				if (Console.CursorLeft > 0)
				{
					Console.Write($" {ActiveChatFriend.Handle} has left the chat, hit Enter...");
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
				await CheckFriendshipAsync(from, friend, command);
			}
		}

		private async Task CheckFriendshipAsync(string from, User friend, ConnectionCommand command)
		{
			if (command.Flag == false || (!command.Flag.HasValue && friend.Blocked == false))
			{
				if (friend != null && !friend.Blocked.HasValue)
				{
					ConsoleWriteLogLine($"{friend.Handle} has blocked you. {friend.Handle} must unfriend you" +
						$" before you can become friends.");
				}

				if (friend.Blocked != true)
					await UnfriendAsync(friend);

				return;
			}
			else if (!friend.Blocked.HasValue)
			{
				if (command.Flag == true)
					await SendCommandAsync(CommandNames.Hello, friend.Id, friend.Id, SerializeUserData(m_user), false);
				else
					await SendCommandAsync(CommandNames.Verify, MakeChannelName(friend), from, SerializeUserData(m_user), null);
			}

			if (!m_online.Contains(friend))
			{
				ConsoleWriteLogLine($"Your {(friend.Blocked.HasValue ? "" : "(pending) ")}friend {friend.Handle} is online.");
				m_online.Add(friend);
			}
		}

		private async Task VerifyFriendAsync(string from, ConnectionCommand command)
		{
			User friend = UpdateFriendData(command.Data);
			User pending = m_user.Friends.FirstOrDefault(u => u.Id == friend.Id);
			if (!command.Flag.HasValue)
			{
				Point cursor = ConsoleWriteLogRead($"Accept friend request from {friend.Handle}," +
					$" email address {friend.Email}? [y/n] ", out ConsoleKeyInfo confirm);
				friend.Blocked = confirm.Key != ConsoleKey.Y;
				if (pending != null)
					m_user.Friends.Remove(pending);

				m_user.AddFriend(friend);
				SaveUser();
				await SendCommandAsync(CommandNames.Verify, HandleChannelName, from, SerializeUserData(m_user), !friend.Blocked);
				if (!friend.Blocked ?? false)
				{
					m_online.Add(friend);
					await m_hubConnection.SendAsync(c_joinChannel, command.Data);
				}

				Console.SetCursorPosition(cursor.X, cursor.Y);
				pending = friend;
			}
			else
			{
				if (pending != null)
					pending.Blocked = !command.Flag;

				SaveUser();
				if (command.Flag == false)
				{
					_ = m_online.Remove(pending);
					await m_hubConnection.SendAsync(c_leaveChannel, command.Data, Handle);
				}
			}

			if (pending != null)
				ConsoleWriteLogLine($"You and {pending.Handle} are {(pending.Blocked.Value ? "not" : "now")} friends!");
		}

		private async Task UnfriendAsync(User friend)
		{
			await m_hubConnection.SendAsync(c_leaveChannel, friend.Id ?? MakeChannelName(friend), Id);
			_ = m_online.Remove(friend);
			_ = m_user.Friends.Remove(friend);
			SaveUser();
		}

		private User UpdateFriendData(string data)
		{
			User updated = DeserializeUserData(data);
			User friend = m_user.Friends.FirstOrDefault(x => x.Id == updated.Id) ??
				m_user.Friends.FirstOrDefault(x => x.Email == updated.Email && x.Handle == updated.Handle);
			if (friend == null)
				return updated;

			if (friend.Handle != updated.Handle || friend.Name != updated.Name ||
				friend.Color != updated.Color || friend.Id != updated.Id)
			{
				friend.Handle = updated.Handle;
				friend.Name = updated.Name;
				friend.Color = updated.Color;
				friend.Id = updated.Id;
				SaveUser();
			}

			return friend;
		}

		private async Task<bool> WaitAsync(string message, int intervalms = 100, int timeouts = 10)
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

		private bool GetData(string prompt, out string data)
		{
			Console.Write(prompt);
			data = Console.ReadLine();
			return !string.IsNullOrEmpty(data);
		}

		private void DisplayMenu()
		{
			Console.WriteLine("\nPick a command: a to add a friend, l to list friends, u to unfriend a friend,");
			Console.WriteLine("d to delete a user, c to chat, or x to exit");
			PromptLine = Console.CursorTop;
			NextLine = PromptLine + 2;
			State = States.Listening;
		}

		private void EraseLog()
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

		private void ConsoleWriteLogLine(string line)
		{
			Point cursor = MoveCursorToLog();
			Console.WriteLine(line);
			m_log.Add(line);
			Console.SetCursorPosition(cursor.X, cursor.Y);
		}

		private Point ConsoleWriteLogRead(string line, out string value)
		{
			Point cursor = MoveCursorToLog();
			Console.Write(line);
			value = Console.ReadLine();
			m_log.Add(line + value);
			return cursor;
		}

		private Point ConsoleWriteLogRead(string line, out ConsoleKeyInfo confirm)
		{
			Point cursor = MoveCursorToLog();
			Console.Write(line);
			confirm = Console.ReadKey();
			m_log.Add(line + confirm.KeyChar);
			return cursor;
		}

		private Point MoveCursorToLog()
		{
			Point cursor = new Point(Console.CursorLeft, Console.CursorTop);
			Console.SetCursorPosition(0, NextLine++);
			return cursor;
		}

		private string MakeChannelName(User user)
		{
			return $"{user.Email}{c_delimiter}{user.Handle}";
		}

		private string MakeChannelName(string name, string email)
		{
			return $"{email}{c_delimiter}{name}";
		}

		private string MakeChatChannelName(User user)
		{
			return $"{user.Id}{c_delimiter}{c_chatChannelName}";
		}

		private string SerializeUserData(User user)
		{
			return $"{user.Handle}{c_delimiter}{user.Email}{c_delimiter}{user.Name}" +
				$"{c_delimiter}{user.Color}{c_delimiter}{user.Id}";
		}

		private User DeserializeUserData(string data)
		{
			string[] parts = data.Split(c_delimiter);
			return new User(parts[0], parts[1], parts[2], parts[3], parts[4]);
		}

		private string[] ParseChannelName(string channelName)
		{
			return channelName.Split(c_delimiter);
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

		private HubConnection m_hubConnection;
#if true
		private readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif

		private User m_user;
		private States m_state;
		private object m_lock = new object();
		private bool m_waitForEnter;
		private readonly List<User> m_users = new List<User>();
		private readonly List<User> m_online = new List<User>();
		private readonly List<string> m_log = new List<string>();
		private readonly JsonSerializerOptions m_serializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true
		};
	}
}
