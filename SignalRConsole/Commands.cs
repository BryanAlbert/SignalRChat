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


			public static void Initialize(Action<string> sendCommand)
			{
				m_sendCommand = sendCommand;
			}

			public static void SendCommand(CommandNames name, bool flag)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Flag = flag
				};

				if (name != CommandNames.Echo)
				{
					Debug.WriteLine($"Error: SendCommand called with bool is only valid" +
						$" for the {CommandNames.Echo} command, called with: {name}");
				}
				else
				{
					command.SendCommand();
				}
			}

			public static void SendCommand(CommandNames name, string data)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					Data = data
				};

				if (name != CommandNames.Handle && name != CommandNames.Hello)
				{
					Debug.WriteLine($"Error: SendCommand called with string is only valid for the {CommandNames.Handle}" +
						$" and {CommandNames.Hello} commands, called with: {name}");
				}
				else
				{
					command.SendCommand();
				}
			}

			public static void SendCommand(CommandNames name, string data, bool flag)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					Data = data,
					Flag = flag
				};

				if (name != CommandNames.Verify)
				{
					Debug.WriteLine($"Error: SendCommand called with string  and bool is only valid for the" +
						$" {CommandNames.Handle} command, called with: {name}");
				}
				else
				{
					command.SendCommand();
				}
			}


			public string Command { get; set; }
			public string Data { get; set; }
			public bool Flag { get; set; }

			[JsonIgnore]
			public CommandNames CommandName { get; set; }

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


			/// <summary>
			/// Unrecognized: a new command issued by a newer QKR, perhaps
			/// Handle: Data contains the opponent's Handle
			/// Away: opponent has pushed the Tables page and can't interact
			/// TableList: Tables contains a Dictionary<string, List<int>> of the opponent's tables
			/// InitiateRace: leader sends when pushing to the Tables page, follower responds with from
			///		the Tables page
			/// StartRace: Leader sends when pushing to the Race page, follower responds with from the Race page
			/// RaceCard: RaceData contains RaceData, leader sends when the next card is picked
			/// CardResult: RaceData contains quiz result, sent when a card is finished
			/// Reset: sent to restart the current race
			/// NavigateBack: sent when the current page is popped off the navigation statck
			/// Echo: Flag contains bool, when entered from the Connect page's Entry, turns echo on or off,
			///		e.g. {"Command":"Echo","Flag":true}
			/// </summary>
			public enum CommandNames
			{
				Unrecognized,
				Hello,
				Verify,
				Handle,
				Echo
			}


			private void SendCommand()
			{
				m_sendCommand(JsonSerializer.Serialize(this));
			}
		}


		private static Action<string> m_sendCommand;
	}
}
