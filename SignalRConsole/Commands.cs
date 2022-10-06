using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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


			public static void InitializeCommands(HubConnection hubConnection)
			{
				m_hubConnection = hubConnection;
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to, User user, bool? flag)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					From = from,
					Channel = channel,
					To = to,
					Racer = new Friend(user),
					Flag = flag
				};

				if (name != CommandNames.Hello && name != CommandNames.Verify)
				{
					Debug.WriteLine($"Error: SendCommand called with string, string, string, User and bool? is only valid for the" +
						$" {CommandNames.Hello} and {CommandNames.Verify} commands, called with: {name}");
				}
				else
				{
					await command.SendCommandAsync();
				}
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to, User user)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = CommandNames.Merge.ToString(),
					From = from,
					Channel = channel,
					To = to,
					Merge = user,
				};

				if (name != CommandNames.Merge)
				{
					Debug.WriteLine($"Error: SendCommand called with string, string, string and User is only valid for the" +
						$" {CommandNames.Merge} command, called with: {name}");
				}
				else
				{
					await command.SendCommandAsync();
				}
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to,
				Dictionary<string, List<int>> tables)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					From = from,
					Channel = channel,
					To = to,
					Tables = tables
				};

				if (name != CommandNames.TableList)
				{
					Debug.WriteLine($"Error: SendCommand called with Dictionary<string, List<int>> is only valid" +
						$" for the {CommandNames.TableList} command, called with: {name}");
				}
				else
				{
					await command.SendCommandAsync();
				}
			}


			public string Command { get; set; }
			public Friend Racer { get; set; }
			public Dictionary<string, List<int>> Tables { get; set; }
			public User Merge { get; set; }
			public bool? Flag { get; set; }

			[JsonIgnore]
			public string Channel { get; set; }
			[JsonIgnore]
			public CommandNames CommandName { get; set; }
			[JsonIgnore]
			public string From { get; private set; }
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
				Verify,
				Merge,
				TableList
			}


			private async Task SendCommandAsync()
			{
				try
				{
					await m_hubConnection.SendAsync(ConsoleChat.c_sendCommand, From, Channel, To, JsonSerializer.Serialize(this));
				}
				catch (Exception exception)
				{
					Console.WriteLine($"Error in SendCommandAsync, exception: {exception.Message}");
				}
			}
		}


		private static HubConnection m_hubConnection;
	}
}
