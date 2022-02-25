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

			HubConnection hubConnection = new HubConnectionBuilder().WithUrl(c_chatHubUrl).Build();
			
			_ = hubConnection.On<string, string>("JoinGroupMessage", (g, u) =>
				Console.WriteLine($"{u} has joined the {g} chat."));
			_ = hubConnection.On<string, string>("ReceiveGroupMessage", (u, m) =>
				Console.WriteLine($"{u} said: {m}"));
			_ = hubConnection.On<string, string>("LeaveGroupMessage", (g, u) =>
				Console.WriteLine($"{u} has left the {g} chat."));
			
			string group;
			await hubConnection.StartAsync();
			while (true)
			{
				Console.WriteLine("\nWhat group would you like to join? Enter to quit...");
				group = Console.ReadLine();
				if (group.Length == 0)
					break;

				string message;
				await hubConnection.SendAsync("JoinGroupChat", group, name);
				Console.WriteLine("\nType messages, type 'goodbye' to leave the chat.");
				while (true)
				{
					message = Console.ReadLine();
					if (message == "goodbye")
					{
						await hubConnection.SendAsync("LeaveGroupChat", group, name);
						break;
					}

					await hubConnection.SendAsync("SendGroupMessage", group, name, message);
				}
			}
		}


#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif
	}
}
