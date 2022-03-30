using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class User
	{
		public User()
		{
		}

		public User(string name, string email)
		{
			Name = name;
			InternetId = email;
			Friends = new List<User>();
		}

		public User(string name, string email, string fileName) : this(name, email)
		{
			FileName = fileName;
		}


		public void AddFriend(User user)
		{
			Friends.Add(user);
		}


		public string Name { get; set; }
		public string InternetId { get; set; }
		public bool? Blocked { get; set; }
		public List<User> Friends { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }
	}
}
