using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class Friend
	{
		public Friend()
		{
		}

		public Friend(string handle, string email)
		{
			Handle = handle;
			Email = email;
		}


		public string Id { get; set; }
		public string Email { get; set; }
		public string Name { get; set; }
		public string Handle { get; set; }
		public string Color { get; set; }
		public string Created { get; set; }
		public string Modified { get; set; }
		public bool? Blocked { get; set; }

		[JsonIgnore]
		public bool HelloInitiated { get; set; }
	}
}
