using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class User : Friend
	{
		public User()
		{
		}

		public User(string handle, string email, string name, string color) : base(handle, email)
		{
			Name = name;
			Color = color;
		}

		public User(string handle, string email, string name, string color, string fileName) :
			this(handle, email, name, color)
		{
			Id = Guid.NewGuid().ToString();
			Created = DateTime.UtcNow.ToString("s");
			Modified = Created;
			FileName = fileName;
			Friends = new List<Friend>();
		}

		// returns a Friend (no Friends list)
		public User(User user) : this(user.Handle, user.Email, user.Name, user.Color)
		{
			Id = user.Id;
			Created = user.Created;
			Modified = user.Modified;
			Blocked = user.Blocked;
		}


		public double Version => c_dataVersion;
		public List<Friend> Friends { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }


		public void AddFriend(Friend user)
		{
			Friends.Add(user);
		}


		private const double c_dataVersion = 1.0;
	}
}
