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
			Console.WriteLine($"{user} has joined the chat.");
			await Clients.All.SendAsync("JoinChat", user);
		}

		public async Task LeaveChat(string user)
		{
			Console.WriteLine($"{user} has left the chat.");
			await Clients.All.SendAsync("LeaveChat", user);
		}

		public async Task SendMessage(string user, string message)
		{
			Console.WriteLine($"{user} sent this message to the chat: {message}");
			await Clients.All.SendAsync("ReceiveMessage", user, message);
		}

		public async Task BroadcastCommand(string user, string command)
		{
			// TODO: rename ReceiveCommand and add a handler?
			// Otherwise we can't tell if it's a private message or a broadcast
			Console.WriteLine($"{user} sent this commnd to the chat: {command}");
			await Clients.All.SendAsync("ReceiveCommand", user, command);
		}

		public async Task JoinGroupChat(string group, string user)
		{
			Console.WriteLine($"{user} joined the group: {group.Replace('\n', ' ')}");
			await Groups.AddToGroupAsync(Context.ConnectionId, group);
			await Clients.Group(group).SendAsync("JoinGroupMessage", group, user);
		}

		public async Task LeaveGroupChat(string group, string user)
		{
			Console.WriteLine($"{user} left the group: {group.Replace('\n', ' ')}");
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
			await Clients.Group(group).SendAsync("LeaveGroupMessage", group, user);
		}

		public async Task SendGroupMessage(string from, string group, string message)
		{
			Console.WriteLine($"{from} sent this message to the group {group.Replace('\n', ' ')}: {message}");
			await Clients.Group(group).SendAsync("ReceiveGroupMessage", from, message);
		}

		public async Task SendGroupCommand(string from, string group, string command)
		{
			Console.WriteLine($"{from} sent this command to the group {group.Replace('\n', ' ')}: {command}");
			await Clients.Group(group).SendAsync("ReceiveGroupCommand", from, command);
		}

		public async Task SendGroupCommandTo(string from, string group, string to, string command)
		{
			Console.WriteLine($"{from} sent this command to {to} in the group {group.Replace('\n', ' ')}: {command}");
			await Clients.Group(group).SendAsync("ReceiveGroupCommandTo", from, to, command);
		}

		readonly Random m_random = new Random();
	}
}
