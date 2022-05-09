using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class User
	{
		public User()
		{
		}

		public User(string handle, string email)
		{
			Handle = handle;
			Email = email;
			Friends = new List<User>();
		}

		public User(string handle, string email, string name, string color, string id) :
			this(handle, email)
		{
			Name = name;
			Color = color;
			Id = id;
		}

		public User(string handle, string email, string name, string color, string id, string fileName) :
			this(handle, email, name, color, id)
		{
			FileName = fileName;
		}


		public void AddFriend(User user)
		{
			Friends.Add(user);
		}


		public string Handle { get; set; }
		public string Email { get; set; }
		public string Name { get; set; }
		public string Color { get; set; }
		public string Id { get; set; }
		public bool? Blocked { get; set; }
		public List<User> Friends { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }
		[JsonIgnore]
		public bool HelloInitiated { get; set; }
	}
}
