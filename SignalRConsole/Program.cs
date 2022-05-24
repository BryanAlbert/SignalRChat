using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static SignalRConsole.ConsoleChat;

namespace SignalRConsole
{
	public class Program
	{
		private static async Task<int> Main(string[] args)
		{
			do
			{
				if (args.Length > 0 && File.Exists(args[0]))
				{
					string[] lines = File.ReadAllLines(args[0]);
					if (lines[0].Split(" ")[0] != c_harness)
						break;

					string workingDirectory = Directory.GetParent(args[0]).FullName;
					List<Task<int>> tasks = new List<Task<int>>();
					foreach (string line in lines)
					{
						switch (line.Split(" ")[0])
						{
							case c_harness:
								Console.WriteLine($"Processing test harness file {args[0]}: {line[(c_harness.Length + 1)..]}");
								break;
							case c_start:
								string commandLine = line[(c_start.Length + 1)..];
								Console.WriteLine($"\nLaunching with command line: {commandLine}");
								ConsoleChat consoleChat = new ConsoleChat();
								tasks.Add(Task.Run(async () => await consoleChat.RunAsync(new Harness(commandLine.Split(" "),
									workingDirectory))));
								while (consoleChat.State != States.Listening && consoleChat.State != States.Broken)
									await Task.Delay(10);
								break;
							case c_startWait:
								commandLine = line[(c_startWait.Length + 1)..];
								Console.WriteLine($"\nLaunching and waiting with command line: {commandLine}");
								await new ConsoleChat().RunAsync(new Harness(commandLine.Split(" "), workingDirectory));
								break;
							default:
								Console.WriteLine($"Error: unknown command '{line.Split(" ")[0]}' in file {args[0]}");
								return -1;
						}
					}

					Task.WaitAll(tasks.ToArray());
					return 0;
				}
			}
			while (false);

			return await new ConsoleChat().RunAsync(new Harness(args));
		}


		private const string c_harness = "test-harness";
		private const string c_start = "start";
		private const string c_startWait = "start-wait";
	}
}
