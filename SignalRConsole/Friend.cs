﻿using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class Friend : Arithmetic
	{
		public Friend()
		{
		}

		public Friend(string handle, string email)
		{
			Handle = handle;
			Email = email;
		}

		public Friend(User user) : base(user)
		{
			DeviceId = user.DeviceId;
		}


		public bool? Blocked { get; set; }
		public string BluetoothId { get; set; }
		public string BluetoothDeviceName { get; set; }
		public string DeviceId { get; set; }

		[JsonIgnore]
		public bool HelloInitiated { get; set; }
	}
}
