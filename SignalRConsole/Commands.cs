using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalRConsole
{
	public class Commands
	{
		public class ConnectionCommand
		{
			public ConnectionCommand()
			{
				CommandName = CommandNames.Unrecognized;
				Command = CommandName.ToString();
			}


			public static void Initialize(Action<string, string, string> sendCommand)
			{
				m_sendCommand = sendCommand;
			}

			public static void SendCommand(CommandNames name, string channel, string to, string data, bool? flag)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					Channel = channel,
					To = to,
					Data = data,
					Flag = flag
				};

				if (name != CommandNames.Hello && name != CommandNames.Verify)
				{
					Debug.WriteLine($"Error: SendCommand called with string  and bool is only valid for the" +
						$" {CommandNames.Hello} and {CommandNames.Verify} commands, called with: {name}");
				}
				else
				{
					command.SendCommand();
				}
			}


			public string Command { get; set; }
			public string Data { get; set; }
			public bool? Flag { get; set; }

			[JsonIgnore]
			public string Channel { get; set; }
			[JsonIgnore]
			public CommandNames CommandName { get; set; }
			[JsonIgnore]
			public string To { get; set; }

			public static ConnectionCommand DeserializeCommand(string json)
			{
				ConnectionCommand command;
				try
				{
					command = JsonSerializer.Deserialize<ConnectionCommand>(json);
					if (Enum.TryParse(command.Command, out CommandNames name))
						command.CommandName = name;
					else
						Debug.WriteLine($"Error in ReceiveCommand, unrecognized Command: {command.Command}");
				}
				catch (Exception exception)
				{
					Debug.WriteLine($"Exception in ReceiveCommand deserializing {json}: {exception.Message}");
					command = new ConnectionCommand();
				}

				return command;
			}


			public enum CommandNames
			{
				Unrecognized,
				Hello,
				Verify
			}


			private void SendCommand()
			{
				m_sendCommand(Channel, To, JsonSerializer.Serialize(this));
			}
		}


		private static Action<string, string, string> m_sendCommand;
	}
}
