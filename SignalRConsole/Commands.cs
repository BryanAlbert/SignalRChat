using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class Commands
	{
		public static Commands DeserializeCommand(string json)
		{
			Commands commands;
			try
			{
				commands = JsonSerializer.Deserialize<Commands>(json);
				return commands;
			}
			catch (Exception)
			{

				throw;
			}
		}
	}
}
