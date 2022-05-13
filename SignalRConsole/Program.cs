using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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
					if (lines[0] != c_harness)
						break;

					List<Task<int>> tasks = new List<Task<int>>();
					foreach (string line in lines)
					{
						switch (line.Split(" ")[0])
						{
							case c_harness:
								Console.WriteLine($"Processing test harnes file {args[0]}");
								break;
							case c_start:
								string commandLine = line[(c_start.Length + 1)..];
								Console.WriteLine($"Launching and waiting with command line: {commandLine}");
								Harness harness = new Harness(commandLine.Split(" "), Directory.GetParent(args[0]).FullName);
								ConsoleChat consoleChat = new ConsoleChat();
								tasks.Add(Task.Run(async () => await consoleChat.RunAsync(harness)));
								break;
							case c_startWait:
								commandLine = line[(c_startWait.Length + 1)..];
								Console.WriteLine($"Launching and waiting with command line: {commandLine}");
								harness = new Harness(commandLine.Split(" "), Directory.GetParent(args[0]).FullName);
								await new ConsoleChat().RunAsync(harness);
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
