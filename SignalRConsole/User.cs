﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class User : Arithmetic
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
			Version = c_dataVersion;
			Id = DeviceId = Guid.NewGuid().ToString();
			Created = DateTime.UtcNow.ToString("s");
			Modified = Created;
			FileName = fileName;
			Friends = new List<Friend>();
			MergeIndex = new Dictionary<string, int>();
			Operators = new List<OperatorTables>();
			Operators.Add(new OperatorTables() { Name = "Addition", Tables = new List<FactTable>() });
			Operators.Add(new OperatorTables() { Name = "Subtraction", Tables = new List<FactTable>() });
			Operators.Add(new OperatorTables() { Name = "Multiplication", Tables = new List<FactTable>() });
			Operators.Add(new OperatorTables() { Name = "Division", Tables = new List<FactTable>() });
		}


		public double Version { get; set; }
		public string DeviceId { get; set; }
		public List<Friend> Friends { get; set; }
		public List<OperatorTables> Operators { get; set; }
		public Dictionary<string, int> MergeIndex { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }


		public void AddFriend(Friend user)
		{
			Friends.Add(user);
		}


		public const double c_dataVersion = 1.0;
	}
}
