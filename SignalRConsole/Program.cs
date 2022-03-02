using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace SignalRConsole
{
	class Program
	{
		private static async Task Main(string[] args)
		{
			if (args is null)
				throw new ArgumentNullException(nameof(args));

			Console.WriteLine($"Connecting to URL: {c_chatHubUrl}");

			Console.Write("What is your name? ");
			string name = Console.ReadLine();

			m_hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();

			_ = m_hubConnection.On<string>("Command", async (r) => await OnCommandAsync(r));
			_ = m_hubConnection.On<string, string>("JoinGroupMessage", (g, u) =>
				Console.WriteLine($"{u} has joined the {g} chat."));
			_ = m_hubConnection.On<string, string>("ReceiveGroupMessage", (u, m) =>
				Console.WriteLine($"{u} said: {m}"));
			_ = m_hubConnection.On<string, string>("LeaveGroupMessage", (g, u) =>
				Console.WriteLine($"{u} has left the {g} chat."));
			
			string group;
			await m_hubConnection.StartAsync();
			while (true)
			{
				Console.WriteLine("\nWhat group would you like to join? Enter to quit...");
				group = Console.ReadLine();
				if (group.Length == 0)
					break;

				string message;
				await m_hubConnection.SendAsync("JoinGroupChat", group, name);
				Console.WriteLine("\nType messages, type 'goodbye' to leave the chat.");
				while (true)
				{
					message = Console.ReadLine();
					if (message == "goodbye")
					{
						await m_hubConnection.SendAsync("LeaveGroupChat", group, name);
						break;
					}

					if (message.StartsWith('{"Command}":'))
					{
						await ProcessCommand(message);
						continue;
					}

					await m_hubConnection.SendAsync("SendGroupMessage", group, name, message);
				}
			}

			await m_hubConnection.StopAsync();
		}

		private static async Task OnCommandAsync(string command)
		{
			// TODO: email this token so QKR can validate the email address
			// TODO: actually, this needs to happen on the server, the client doesn't send commands 
			string token = m_random.Next(99999).ToString();
			Console.WriteLine($"Incoming registration request for {command}, sending token: {token}");
			await m_hubConnection.SendAsync("Register", token);
		}

		private static Task ProcessCommand(string message)
		{
			throw new NotImplementedException();
		}


#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif
		private static Random m_random = new Random();
		private static HubConnection m_hubConnection;
	}
}
