using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace SignalRChat.Hubs
{
	public class ChatHub : Hub
	{
		public async Task Register(string email)
		{
			string token = m_random.Next(10000, 99999).ToString();
			Console.WriteLine($"Incoming registration request for {email}, sending token: {token}");

			await Clients.Caller.SendAsync("Register", token);
		}

		public async Task JoinChat(string user)
		{
			await Clients.All.SendAsync("JoinChat", user);
			Console.WriteLine($"{user} has joined the chat.");
		}

		public async Task LeaveChat(string user)
		{
			await Clients.All.SendAsync("LeaveChat", user);
			Console.WriteLine($"{user} has left the chat.");
		}

		public async Task SendMessage(string user, string message)
		{
			await Clients.All.SendAsync("ReceiveMessage", user, message);
			Console.WriteLine($"{user} sent this message to the chat: {message}");
		}

		public async Task SendCommand(string user, string command)
		{
			await Clients.All.SendAsync("ReceiveCommand", user, command);
			Console.WriteLine($"{user} sent this commnd to the chat: {command}");
		}

		public async Task JoinGroupChat(string group, string user)
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, group);
			await Clients.Group(group).SendAsync("JoinGroupMessage", group, user);
			Console.WriteLine($"{user} joined the group: {group.Replace('\n', ' ')}");
		}

		public async Task LeaveGroupChat(string group, string user)
		{
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
			await Clients.Group(group).SendAsync("LeaveGroupMessage", group, user);
			Console.WriteLine($"{user} left the group: {group.Replace('\n', ' ')}");
		}

		public async Task SendGroupMessage(string group, string user, string message)
		{
			await Clients.Group(group).SendAsync("ReceiveGroupMessage", user, message);
			Console.WriteLine($"{user} sent this message to the group {group.Replace('\n', ' ')}: {message}");
		}

		public async Task SendGroupCommand(string group, string user, string command)
		{
			await Clients.Group(group).SendAsync("ReceiveGroupCommand", user, command);
			Console.WriteLine($"{user} sent this command to the group {group.Replace('\n', ' ')}: {command}");
		}

		readonly Random m_random = new Random();
	}
}
