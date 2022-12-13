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
			m_args = new List<string>(args);
			do
			{
				if (m_args.Count > 0)
				{
					if (!ProcessCommandLineSwitches())
						return ExitCode;

					if (m_args.Count > 0 && File.Exists(m_args[0]))
					{
						string[] lines = File.ReadAllLines(m_args[0]);
						if (lines[0].Split(" ")[0] != c_harness)
							break;

						string workingDirectory = Directory.GetParent(m_args[0]).FullName;
						List<Task<int>> tasks = new();
						foreach (string line in lines)
						{
							if (line[0] == '#')
								continue;

							string command = line.Split(" ")[0];
							switch (command)
							{
								case c_harness:
									Console.WriteLine($"Program: Processing test harness file {m_args[0]}: {line[(c_harness.Length + 1)..]}");
									break;
								case c_start:
									string commandLine = line[(c_start.Length + 1)..];
									Console.WriteLine($"\nProgram: Launching with command line: {commandLine}");
									ConsoleChat consoleChat = new();
									tasks.Add(Task.Run(async () => await consoleChat.RunAsync(new Harness(commandLine.Split(" "),
										workingDirectory))));
									while (consoleChat.State != States.Listening && consoleChat.State != States.Broken)
										await Task.Delay(10);
									break;
								case c_startWait:
									commandLine = line[(c_startWait.Length + 1)..];
									Console.WriteLine($"\nProgram: Launching and waiting with command line: {commandLine}");
									_ = await new ConsoleChat().RunAsync(new Harness(commandLine.Split(" "), workingDirectory));
									break;
								case c_startWaitFor:
								case c_startWaitForRegex:
									int start = line.IndexOf('"') + 1;
									string waitForOutput = line[start..line.LastIndexOf('"')];
									commandLine = line[(line.LastIndexOf('"') + 2)..];
									Console.WriteLine($"\nProgram: Launching with command line: {commandLine}");
									consoleChat = new ConsoleChat();
									Harness harness = new(commandLine.Split(' '), workingDirectory);
									tasks.Add(Task.Run(async () => await consoleChat.RunAsync(harness)));
									while (!harness.OutputMatches(waitForOutput, command == c_startWaitForRegex))
										await Task.Delay(10);
									break;
								case c_waitAll:
									Task.WaitAll(tasks.ToArray());
									break;
								default:
									Console.WriteLine($"Error in Program: unknown command '{command}' in file {m_args[0]}");
									return -1;
							}
						}

						Task.WaitAll(tasks.ToArray());
						Console.WriteLine($"\nProgram: {m_args[0]} Finished running.");
						return 0;
					}
				}
			}
			while (false);

			return await new ConsoleChat().RunAsync(new Harness(m_args.ToArray(), null, Verbose));
		}


		public static bool Verbose { get; private set; }
		public static int ExitCode { get; set; }


		private static bool ProcessCommandLineSwitches()
		{
			bool checkMerge = false;
			bool qkr = false;

			while (m_args.Count > 0 && m_args[0][0] == '-')
			{
				string @switch = NextArg()[1..];
				switch (@switch)
				{
					case "?":
					case "help":
						Usage();
						return false;
					case "verbose":
						Verbose = true;
						break;
					case "checkmerge":
						checkMerge = true;
						break;
					case "qkr":
						qkr = true;
						break;
					default:
						Console.WriteLine($"\nError: unrecognized command line switch: {@switch}");
						ExitCode = -1;
						Usage();
						return false;
				}
			}

			if (checkMerge)
			{
				CheckMerge(qkr);
				return false;
			}

			return true;
		}

		private static void Usage()
		{
			Console.WriteLine("\nUsage: -checkmerge [-qkr As QkrFolder] Folder Handle");
			Console.WriteLine("   After running a merge test, validates json files in Folder with the base name Mia." +
				"\n   For example, Test-25 Mia will analyze json files from the Test-25 folder Old\\Mia.qkr," +
				"\n   Old\\Mia.qkr.json, New\\Mia.qkr, and New\\Mia.qkr.json for consistency. Test-26 Mia will" +
				"\n   analyze Mia.qkr and Mia.qkr.json in Test-26's First, Second, and Third folders. If QKR" +
				"\n   was tested, use -qkr As QkrFolder to specify which user was run on QKR, e.g. Old, New," +
				"\n   First, etc. and the folder QKR stores its json files in.");
			Console.WriteLine("Usage: [-verbose] [Folder] [Handle]");
			Console.WriteLine("Usage: [-verbose] TestHarness");
			Console.WriteLine("Usage: ScriptFile");
			Console.WriteLine("Usage: [Folder] InputFileName OutputFileName [Tag]");
			Console.WriteLine("\n-verbose        show output used for scripting triggers");
			Console.WriteLine("As              Folder QKR was tested from: Old, New, First, etc.");
			Console.WriteLine("Folder          Run from the directory Folder: .\\Test-01\\");
			Console.WriteLine("QkrFolder       The directory containing QKR json files: C:\\User\\...\\LocalState");
			Console.WriteLine("Handle          Load user with Handle Handle: Mia");
			Console.WriteLine("TestHarness     Load the test-harness file by the path TestHarness, setting the working" +
				"\n                directory to the file's parent folder: .\\Test-01\\Test.txt");
			Console.WriteLine("ScriptFile      Load the script file specified by the path ScriptFile, setting the" +
				"\n                working directory to the file's parent folder: .\\Test-01\\BruceInput.txt" +
				"\n                (when specified in a test harness file, the working folder is that" +
				"\n                file's parent directory)");
			Console.WriteLine("InputFileName   Run the commands in the script file InputFileName, relative to Folder");
			Console.WriteLine("OutputFileName  Write output to OutputFileName, relative to Folder");
			Console.WriteLine("Tag             Prepend console output with Tag, typically a Handle or sub-directory");
			Console.WriteLine("\nReturns 0 for success, negative values for various errors.");
		}

		private static void CheckMerge(bool checkQkr)
		{
			string qkrAs = null;
			string qkrFolder = null;
			if (checkQkr)
			{
				if (m_args.Count < 4)
				{
					Console.WriteLine("Specify a child folder and the QKR folder with the -qkr switch.");
					Usage();
					return;
				}

				qkrAs = NextArg();
				qkrFolder = NextArg();
			}

			if (m_args.Count < 2)
			{
				Console.WriteLine("Specify Folder and Handle on the command line.");
				Usage();
				return;
			}

			Console.WriteLine("\nValidating merge results...");
			CheckMerge check = new(NextArg(), NextArg(), qkrAs, qkrFolder);
			ExitCode = check.RunCheck() ? 0 : -1;
			Console.WriteLine($"\nCheck {(ExitCode == 0 ? "succeeded" : "failed")}.");
		}

		private static string NextArg()
		{
			if (m_args.Count == 0)
				return null;

			string arg = m_args[0];
			m_args.RemoveAt(0);
			return arg;
		}


		private const string c_harness = "test-harness";
		private const string c_start = "start";
		private const string c_startWait = "start-wait";
		private const string c_startWaitFor = "start-wait-for:";
		private const string c_startWaitForRegex = "start-wait-for-regex:";
		private const string c_waitAll = "wait-all";
		private static List<string> m_args;
	}
}
