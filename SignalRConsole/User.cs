using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SignalRConsole
{
	public class User
	{
		public User()
		{
		}

		public User(string name, string email, bool? verified)
		{
			Name = name;
			InternetId = email;
			Verified = verified;
			Friends = new List<User>();
		}

		public User(string name, string email) : this(name, email, null)
		{
		}

		public User(User user, bool? verified) : this(user.Name, user.InternetId, verified)
		{
		}

		public User(string groupName, bool? verified)
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
		public bool? Verified { get; set; }
		public List<User> Friends { get; set; }
	}
}
