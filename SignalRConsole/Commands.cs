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


			public static Action<string> Echo { get; set; }


			public static void InitializeCommands(HubConnection hubConnection)
			{
				m_hubConnection = hubConnection;
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					From = from,
					Channel = channel,
					To = to
				};

				if (name != CommandNames.Away && name != CommandNames.InitiateRace &&
					name != CommandNames.StartRace && name != CommandNames.Reset &&
					name != CommandNames.NavigateBack)
				{
					Debug.WriteLine($"Error: SendCommand called with string, string, and string is only valid for the" +
						$" {CommandNames.Away}, {CommandNames.InitiateRace}, {CommandNames.StartRace}, and" +
						$" {CommandNames.Reset} and {CommandNames.NavigateBack} commands, called with: {name}");

					return;
				}

				await command.SendCommandAsync();
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to,
				User user, bool? flag)
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

					return;
				}

				await command.SendCommandAsync();
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to,
				User user)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = CommandNames.Merge.ToString(),
					From = from,
					Channel = channel,
					To = to,
					Merge = user
				};

				if (name != CommandNames.Merge)
				{
					Debug.WriteLine($"Error: SendCommand called with string, string, string and User is only valid for the" +
						$" {CommandNames.Merge} command, called with: {name}");

					return;
				}

				await command.SendCommandAsync();
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
					Debug.WriteLine($"Error: SendCommand called with string, string, string and Dictionary<string, List<int>>" +
						$" is only valid for the {CommandNames.TableList} command, called with: {name}");

					return;
				}

				await command.SendCommandAsync();
			}

			public static async Task SendCommandAsync(CommandNames name, string from, string channel, string to,
				RaceData raceData)
			{
				ConnectionCommand command = new ConnectionCommand()
				{
					Command = name.ToString(),
					From = from,
					Channel = channel,
					To = to,
					RaceData = raceData
				};

				if (name != CommandNames.RaceCard && name != CommandNames.CardResult)
				{
					Debug.WriteLine($"Error: SendCommand called with string, string, string and RaceData is only valid for the" +
						$" {CommandNames.RaceCard} and {CommandNames.CardResult} commands, called with: {name}");

					return;
				}

				await command.SendCommandAsync();
			}


			public string Command { get; set; }
			public Friend Racer { get; set; }
			public Dictionary<string, List<int>> Tables { get; set; }
			public RaceData RaceData { get; set; }
			public User Merge { get; set; }
			public bool? Flag { get; set; }

			[JsonIgnore]
			public CommandNames CommandName { get; set; }
			[JsonIgnore]
			public string Channel { get; set; }
			[JsonIgnore]
			public string From { get; set; }
			[JsonIgnore]
			public string To { get; set; }
			[JsonIgnore]
			public string Json
			{
				get
				{
					m_json ??= JsonSerializer.Serialize(this);
					return m_json;
				}
			}


			public static ConnectionCommand DeserializeCommand(string json)
			{
				ConnectionCommand command;
				try
				{
					command = JsonSerializer.Deserialize<ConnectionCommand>(json);
					if (!Enum.TryParse(command.Command, out CommandNames name))
					{
						Debug.WriteLine($"Error in ReceiveCommand, unrecognized Command: {command.Command}");
						return null;
					}

					command.CommandName = name;
				}
				catch (Exception exception)
				{
					Debug.WriteLine($"Exception in ReceiveCommand deserializing {json}: {exception.Message}");
					Echo?.Invoke($"Failed to deserialize {json}: {exception.Message}");

					command = new ConnectionCommand();
				}

				return command;
			}

			public static async Task<ConnectionCommand> DeserializeAndSendCommandAsync(string json, string from = null,
				string channel = null, string to = null)
			{
				ConnectionCommand command = DeserializeCommand(json);
				command.From = from;
				command.Channel = channel;
				command.To = to;
				await command.SendCommandAsync();
				return command;
			}

			public enum CommandNames
			{
				Away,
				TableList,
				InitiateRace,
				StartRace,
				RaceCard,
				CardResult,
				Reset,
				NavigateBack,
				Hello,
				Verify,
				Merge,
				Echo,
				Unrecognized
			}


			private async Task SendCommandAsync()
			{
				try
				{
					Echo?.Invoke(Json);

					await m_hubConnection.SendAsync(ConsoleChat.c_sendCommand, From, Channel, To, Json);
				}
				catch (Exception exception)
				{
					Console.WriteLine($"Error in SendCommandAsync, exception: {exception.Message}");
				}
			}


			private string m_json;
		}

		private static HubConnection m_hubConnection;
	}
}
