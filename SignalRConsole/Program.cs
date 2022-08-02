using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static SignalRConsole.ConsoleChat;

namespace SignalRConsole
{
	public class Program
	{
		/// <summary>
		/// Entry point for the SignalRChat application.
		/// </summary>
		/// <param name="args">The following arguments are supported:</param>
		/// (none): Ask for user Handle, load json from the current directory.
		/// Handle: Load json files and open the user whose Handle matches Handle, else
		///		create a new user with Handle Handle: Mia
		/// Folder: Run from the directory Folder: .\Test-01\
		/// Folder Handle: Load user with Handle (see above) in directory Folder:
		///		.\Test-01\ Mia
		/// (Test harness file path): Load the test-harness file specified by the path, setting 
		///		the working directory to the file's parent folder: .\Test-01\Test.txt
		/// (Script file path): Load the script file specified by the path, setting the working
		///		directory to the file's parent folder: MiaInput.txt (when specified in a test harnes
		///		file, the working folder is that file's parent directory)
		/// Folder InputFileName OutputFileName Tag: Run the commands in the script file 
		///		InputFileName, writes output to OutputFileName, both relative to Folder, console
		///		output is prepended with Tag: Old MiaInput.txt MiaOutput.txt Old. If this command
		///		line is specified in a test harness file, Folder is relative to that file's parent directory
		/// <returns>0 for success, negative values for various errors.</returns>
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
						if (line[0] == '#')
							continue;

						string command = line.Split(" ")[0];
						switch (command)
						{
							case c_harness:
								Console.WriteLine($"Program: Processing test harness file {args[0]}: {line[(c_harness.Length + 1)..]}");
								break;
							case c_start:
								string commandLine = line[(c_start.Length + 1)..];
								Console.WriteLine($"\nProgram: Launching with command line: {commandLine}");
								ConsoleChat consoleChat = new ConsoleChat();
								tasks.Add(Task.Run(async () => await consoleChat.RunAsync(new Harness(commandLine.Split(" "),
									workingDirectory))));
								while (consoleChat.State != States.Listening && consoleChat.State != States.Broken)
									await Task.Delay(10);
								break;
							case c_startWait:
								commandLine = line[(c_startWait.Length + 1)..];
								Console.WriteLine($"\nProgram: Launching and waiting with command line: {commandLine}");
								await new ConsoleChat().RunAsync(new Harness(commandLine.Split(" "), workingDirectory));
								break;
							case c_startWaitFor:
							case c_startWaitForRegex:
								int start = line.IndexOf('"') + 1;
								string waitForOutput = line[start..line.LastIndexOf('"')];
								commandLine = line[(line.LastIndexOf('"') + 2)..];
								Console.WriteLine($"\nProgram: Launching with command line: {commandLine}");
								consoleChat = new ConsoleChat();
								Harness harness = new Harness(commandLine.Split(' '), workingDirectory);
								tasks.Add(Task.Run(async () => await consoleChat.RunAsync(harness)));
								while (!harness.OutputMatches(waitForOutput, command == c_startWaitForRegex))
									await Task.Delay(10);
								break;
							case c_waitAll:
								Task.WaitAll(tasks.ToArray());
								break;
							default:
								Console.WriteLine($"Error in Program: unknown command '{command}' in file {args[0]}");
								return -1;
						}
					}

					Task.WaitAll(tasks.ToArray());
					Console.WriteLine($"\nProgram: {args[0]} Finished running.");
					return 0;
				}
			}
			while (false);

			return await new ConsoleChat().RunAsync(new Harness(args));
		}


		private const string c_harness = "test-harness";
		private const string c_start = "start";
		private const string c_startWait = "start-wait";
		private const string c_startWaitFor = "start-wait-for:";
		private const string c_startWaitForRegex = "start-wait-for-regex:";
		private const string c_waitAll = "wait-all";
	}
}
