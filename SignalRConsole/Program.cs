using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace SignalRConsole
{
	class Program
	{
		private static async Task Main(string[] args)
		{
			if (args is null)
				throw new ArgumentNullException(nameof(args));

			if (!await StartServer())
				return;

			if (!await RegisterAsync())
				return;

			Console.Write("What is your name? ");
			Name = Console.ReadLine();

			while (true)
			{
				Console.WriteLine("To connect to a friend, enter your friend's email,");
				Console.Write("or just Enter to listen for a connection: ");
				string friendEmail = Console.ReadLine();
				while (true)
				{
					if (string.IsNullOrEmpty(friendEmail) ? await ListenAsync() : await ConnectAsync(friendEmail))
						break;

					Console.WriteLine("Is your friend connected? Enter your friend's email again or");
					Console.Write("Enter to listen for a connection or exit to quit: ");
					friendEmail = Console.ReadLine();
					if (friendEmail == "exit")
						return;
				}

				if (await MessageLoopAsync())
					break;
			}

			await m_hubConnection.StopAsync();
			Console.WriteLine("Finished.");
		 }


		private static States State { get; set; }
		private static string RegistrationToken { get; set; }
		private static string Email { get; set; }
		private static string Name { get; set; }
		private static string GroupName { get; set; }
		private static int? NextLine { get; set; }

		private static void OnRegister(string token)
		{
			lock (m_lock)
			{
				ConsoleWriteWhileWaiting($"Registration token from server: {token}");
				RegistrationToken = token;
				State = States.Changing;
			}
		}

		private static void OnGroupJoin(string group, string user)
		{
			lock (m_lock)
			{
				ConsoleWriteWhileWaiting($"{user} has joined the {group.Replace('\n', ' ')} chat.");
				if (user != Name)
					State = States.Changing;
			}
		}

		private static void OnGroupMessage(string user, string message)
		{
			lock (m_lock)
			{
				if (State == States.Connecting)
					State = States.Changing;
				else
					Console.WriteLine($"{user} said: {message}");
			}
		}

		private static void OnGroupCommand(string user, string command)
		{
			lock (m_lock)
				Console.WriteLine($"Command from {user}: {command}");
		}

		private static void OnGroupLeave(string group, string user)
		{
			lock (m_lock)
				Console.WriteLine($"{user} has left the {group.Replace('\n', ' ')} chat.");
		}

		private static async Task<bool> StartServer()
		{
			State = States.Initializing;
			Console.WriteLine($"Initializing server and connecting to URL: {c_chatHubUrl}");
			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();

			_ = m_hubConnection.On<string>(c_register, (e) => OnRegister(e));
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

		private static async Task<bool> RegisterAsync()
		{
			State = States.Registering;
			Console.Write("Enter your email address to register with the server: ");
			Email = Console.ReadLine();
			await m_hubConnection.SendAsync(c_register, Email);
			if (!await WaitAsync("Waiting for the server"))
			{
				Console.WriteLine("'Timeout waiting for a response from the server, aborting.");
				return false;
			}

			Console.WriteLine("Enter the token returned from the server, Enter to abort: ");
			string token;
			while (true)
			{
				token = Console.ReadLine();
				if (token == RegistrationToken)
					return true;
				if (token == string.Empty)
					return false;

				Console.WriteLine("Tokens do not match, please try again, Enter to abort: ");
			}
		}

		private static async Task<bool> ListenAsync()
		{
			State = States.Listening;
			GroupName = $"{Email}\n{Name}";
			await m_hubConnection.SendAsync(c_joinGroupChat, GroupName, Name);
			if (await WaitAsync("Listening for a connection", 100, 600))
			{
				await m_hubConnection.SendAsync(c_sendGroupMessage, GroupName, Name, c_handshake);
				return true;
			}

			return false;
		}

		private static async Task<bool> ConnectAsync(string email)
		{
			State = States.Connecting;
			Console.Write("Enter your friend's name: ");
			string name = Console.ReadLine();
			GroupName = $"{email}\n{name}";
			await m_hubConnection.SendAsync(c_joinGroupChat, GroupName, Name);
			return await WaitAsync($"Connecting to {name}");
		}

		private static async Task<bool> MessageLoopAsync()
		{
			State = States.Chatting;
			Console.WriteLine("\nType messages, type 'goodbye' to leave the chat.");
			while (true)
			{
				string message = Console.ReadLine();
				
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

		private static void ConsoleWriteWhileWaiting(string line)
		{
			if (NextLine.HasValue)
			{
				NextLine = NextLine.Value + 1;
				Console.SetCursorPosition(0, NextLine.Value);
			}

			Console.WriteLine(line);
		}


		private enum States
		{
			Initializing,
			Changing,
			Registering,
			Listening,
			Connecting,
			Chatting
		}

		private const string c_register = "Register";
		private const string c_joinGroupChat = "JoinGroupChat";
		private const string c_joinGroupMessage = "JoinGroupMessage";
		private const string c_sendGroupMessage = "SendGroupMessage";
		private const string c_receiveGroupMessage = "ReceiveGroupMessage";
		private const string c_receiveGroupCommand = "ReceiveGroupCommand";
		private const string c_leaveGroupChat = "LeaveGroupChat";
		private const string c_leaveGroupMessage = "LeaveGroupMessage";
		private const string c_handshake = "Hello QKR.";
		private static HubConnection m_hubConnection;
		private static readonly object m_lock = new object();
#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif
	}
}
