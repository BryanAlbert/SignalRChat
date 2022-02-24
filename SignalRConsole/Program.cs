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
			
			_ = hubConnection.On<string>("JoinChat", (user) => Console.WriteLine($"{user} has joined the chat."));
			hubConnection.StartAsync().Wait();
			_ = hubConnection.On<string, string>("ReceiveMessage", (user, receivedMessage) =>
				Console.WriteLine($"{user} said: {receivedMessage}"));
			_ = hubConnection.On<string>("LeaveChat", (user) => Console.WriteLine($"{user} has left the chat."));
			
			await hubConnection.SendAsync("JoinChat", name);

			Console.WriteLine("\nType messages, type 'goodbye' to leave the chat.");
			string message;
			do
			{
				message = Console.ReadLine();
				hubConnection.SendAsync("SendMessage", name, message).Wait();
			}
			while (message != "goodbye");

			hubConnection.SendAsync("LeaveChat", name).Wait();
		}


#if true
		private static readonly string c_chatHubUrl = "https://localhost:5001/chathub";
#else
		private static readonly string c_chatHubUrl = "https://localhost:44398/chathub";
#endif
	}
}
