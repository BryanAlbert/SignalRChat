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

		public async Task JoinChannel(string channel, string user)
		{
			Console.WriteLine($"{user} joined the channel: {channel.Replace('\n', ' ')}");
			await Groups.AddToGroupAsync(Context.ConnectionId, channel);
			await Clients.Group(channel).SendAsync("JoinedChannel", channel, user);
		}

		public async Task LeaveChannel(string channel, string user)
		{
			Console.WriteLine($"{user} left the channel: {channel.Replace('\n', ' ')}");
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel);
			await Clients.Group(channel).SendAsync("LeftChannel", channel, user);
		}

		public async Task SendMessage(string from, string channel, string message)
		{
			Console.WriteLine($"{from} sent this message to the channel {channel.Replace('\n', ' ')}: {message}");
			await Clients.Group(channel).SendAsync("SentMessage", from, message);
		}

		public async Task SendCommand(string from, string channel, string to, string command)
		{
			Console.WriteLine($"{from} sent this command to {to} in the channel {channel.Replace('\n', ' ')}: {command}");
			await Clients.Group(channel).SendAsync("SentCommand", from, to, command);
		}


		readonly Random m_random = new Random();
	}
}
