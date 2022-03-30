using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class User
	{
		public User()
		{
		}

		public User(string name, string email, bool? blocked)
		{
			Name = name;
			InternetId = email;
			Blocked = blocked;
			Friends = new List<User>();
		}

		public User(string name, string email) : this(name, email, null)
		{
		}

		public User(User user, bool? blocked) : this(user.Name, user.InternetId, blocked)
		{
		}

		public User(string groupName, bool? blocked)
		{
		}


		public void AddFriend(User user)
		{
			Friends.Add(user);
		}


		public static User DeserializeCommand(string json)
		{
			User commands;
			try
			{
				commands = JsonSerializer.Deserialize<User>(json);
				return commands;
			}
			catch (Exception)
			{

				throw;
			}
		}


		public string Name { get; set; }
		public string InternetId { get; set; }
		public bool? Blocked { get; set; }
		public List<User> Friends { get; set; }
		[JsonIgnore]
		public string FileName => $"{Name}{Program.c_fileExtension}";
	}
}
