using System;
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

		public User(string handle, string email, string name, string color, string fileName) : this(handle, email)
		{
			Handle = handle;
			Email = email;
			Name = name;
			Color = color;
			FileName = fileName;
			Id = Guid.NewGuid().ToString();
			Created = DateTime.UtcNow.ToString("s");
			Modified = Created;
		}

		public User(string handle, string email, string name, string color, string id, string created, string modified) :
			this(handle, email)
		{
			Name = name;
			Color = color;
			Id = id;
			Created = created;
			Modified = modified;
		}


		public void AddFriend(User user)
		{
			Friends.Add(user);
		}


		public double Version => c_dataVersion;
		public string Id { get; set; }
		public string Email { get; set; }
		public string Name { get; set; }
		public string Handle { get; set; }
		public string Color { get; set; }
		public string Created { get; set; }
		public string Modified { get; set; }
		public bool? Blocked { get; set; }
		public List<User> Friends { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }
		[JsonIgnore]
		public bool HelloInitiated { get; set; }


		private const double c_dataVersion = 1.0;
	}
}
