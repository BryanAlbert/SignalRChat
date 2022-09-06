using System;
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
			DataVersion = c_dataVersion;
			Id = DeviceId = Guid.NewGuid().ToString();
			Created = DateTime.UtcNow.ToString("s");
			Modified = Created;
			FileName = fileName;
			Friends = new List<Friend>();
			MergeIndex = new Dictionary<string, int>();
			Operators = new List<OperatorTables>
			{
				new OperatorTables() { Name = "Addition", Tables = new List<FactTable>() },
				new OperatorTables() { Name = "Subtraction", Tables = new List<FactTable>() },
				new OperatorTables() { Name = "Multiplication", Tables = new List<FactTable>() },
				new OperatorTables() { Name = "Division", Tables = new List<FactTable>() }
			};
		}


		public static Version c_dataVersion = new Version(1, 1);


		public Version DataVersion { get; set; }
		public Dictionary<string, int> MergeIndex { get; set; }
		public List<Friend> Friends { get; set; }
		public List<OperatorTables> Operators { get; set; }
		public string DeviceId { get; set; }

		[JsonIgnore]
		public string FileName { get; set; }
	}
}
