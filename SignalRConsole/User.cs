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
			FavoriteColor = color;
			FileName = fileName;
			Id = Guid.NewGuid().ToString();
			CreationDate = DateTime.UtcNow.ToString("s");
			ModifiedDate = CreationDate;
		}

		public User(string handle, string email, string name, string color, string id, string creationDate, string modifiedDate) :
			this(handle, email)
		{
			Name = name;
			FavoriteColor = color;
			Id = id;
			CreationDate = creationDate;
			ModifiedDate = modifiedDate;
		}


		public void AddFriend(User user)
		{
			Friends.Add(user);
		}


		public string Handle { get; set; }
		public string Email { get; set; }
		public string Name { get; set; }
		public string FavoriteColor { get; set; }
		public string Id { get; set; }
		public string CreationDate { get; set; }
		public string ModifiedDate { get; set; }
		public bool? Blocked { get; set; }
		public List<User> Friends { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }
		[JsonIgnore]
		public bool HelloInitiated { get; set; }
	}
}
