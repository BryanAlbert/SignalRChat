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

		public User(string handle, string email, string name, string color, string fileName) : base(handle, email)
		{
			Handle = handle;
			Email = email;
			Name = name;
			Color = color;
			FileName = fileName;
			Id = Guid.NewGuid().ToString();
			Created = DateTime.UtcNow.ToString("s");
			Modified = Created;
			Friends = new List<Friend>();
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
