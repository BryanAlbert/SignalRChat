using System;
using System.Collections.Generic;
using System.Text;
using Text.Json;
using Text.Json.Serialization;

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

			}
			catch (Exception)
			{

				throw;
			}
		}
	}
}
