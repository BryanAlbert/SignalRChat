using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace SignalRChat.Hubs
{
	public class ChatHub : Hub
	{
		public async Task JoinChat(string user)
		{
			await Clients.All.SendAsync("JoinChat", user);
		}

		public async Task LeaveChat(string user)
		{
			await Clients.All.SendAsync("LeaveChat", user);
		}

		public async Task SendMessage(string user, string message)
		{
			await Clients.All.SendAsync("ReceiveMessage", user, message);
		}

		public async Task JoinGroupChat(string group, string user)
		{
			await Groups.AddToGroupAsync(Context.ConnectionId, group);
			await Clients.Group(group).SendAsync("JoinGroupMessage", group, user);
		}

		public async Task JoinGroupMessage(string group, string user)
		{
			await Clients.Group(group).SendAsync("JoinGroupMessage", user);
		}

		public async Task LeaveGroupChat(string group, string user)
		{
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
			await Clients.Group(group).SendAsync("LeaveGroupMessage", group, user);
		}

		public async Task LeaveGroupMessage(string group, string user)
		{
			await Clients.Group(group).SendAsync("LeaveGroupMessage", user);
		}

		public async Task SendGroupMessage(string group, string user, string message)
		{
			await Clients.Group(group).SendAsync("ReceiveGroupMessage", user, message);
		}
	}
}
