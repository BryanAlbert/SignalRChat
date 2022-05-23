using System; 
using System.Collections.Generic;
using System.IO;

namespace SignalRConsole
{
	public class Harness
	{
		public Harness(string[] args, string workingDirectory = null)
		{
			if (workingDirectory != null)
			{
				WorkingDirectory = workingDirectory.Length > Environment.CurrentDirectory.Length &&
					Environment.CurrentDirectory.StartsWith(Environment.CurrentDirectory) ?
						workingDirectory[(Environment.CurrentDirectory.Length + 1)..] :
						workingDirectory;
				Console.WriteLine($"Harness: working directory is: {WorkingDirectory}");
			}

			m_args = new List<string>(args);
			if (m_args.Count > 0 && File.Exists(m_args[0]))
			{
				// command line can contain the script file name, the logging file name, and the tag for console output
				m_inputStreamFilename = NextArg();
				if (WorkingDirectory == ".")
					WorkingDirectory = Directory.GetParent(m_inputStreamFilename).FullName;
				else
					m_inputStreamFilename = Path.Combine(WorkingDirectory, m_inputStreamFilename);

				Console.WriteLine($"Harness: input is provided by {m_inputStreamFilename}");
				m_inputStream = File.ReadLines(m_inputStreamFilename).GetEnumerator();
				GetNextInputLine();

				if (m_args.Count > 0)
				{
					m_outputStreamFilename = NextArg();
					if (WorkingDirectory != ".")
						m_outputStreamFilename = Path.Combine(WorkingDirectory, m_outputStreamFilename); 

					Console.WriteLine($"Harness: output is written to {m_outputStreamFilename}");
					m_outputStream = new StreamWriter(m_outputStreamFilename) { AutoFlush = true };
				}

				if (m_args.Count > 0)
				{
					m_tag = NextArg();
					Console.WriteLine($"Harness: User is {m_tag}");
				}
			}
		}


		public bool ScriptMode => m_inputStreamFilename != null;
		public string WorkingDirectory { get; set; } = ".";
		public int CursorLeft { get => Console.CursorLeft; set { Console.CursorLeft = value; } }
		public int CursorTop { get => Console.CursorTop; set { Console.CursorTop = value; } }
		public ConsoleColor ForegroundColor { get => Console.ForegroundColor; set { Console.ForegroundColor = value; } }
		public ConsoleColor BackgroundColor { get => Console.BackgroundColor; set { Console.BackgroundColor = value; } }
		public string CurrentOutputLine { get; private set; }
		public string CurrentInputLine { get; private set; }
		public bool KeyAvailable
		{
			get
			{
				if (!ScriptMode)
					return Console.KeyAvailable;

				if (CurrentInputLine.StartsWith(c_blockCommand))
				{
					GetNextInputLine();
					m_menuBlocked = true;
				}
				else if (CurrentInputLine.StartsWith(c_resumeCommand))
				{
					GetNextInputLine();
					m_menuBlocked = false;
				}
				else if (CurrentInputLine.StartsWith(c_waitForCommand))
				{
					m_menuBlocked = CurrentInputLine[c_waitForCommand.Length..] != CurrentOutputLine;
					if (!m_menuBlocked)
						GetNextInputLine();
				}

				return !m_menuBlocked;
			}
		}


		public string NextArg()
		{
			if (m_args.Count == 0)
				return null;

			string arg = m_args[0];
			m_args.RemoveAt(0);
			return arg;
		}

		public ConsoleKeyInfo ReadKey(bool intercept = false)
		{
			if (ScriptMode)
			{
				LogWriteLine(CurrentInputLine);
				Console.WriteLine(m_tag != null ? $"{m_tag} {CurrentInputLine}" : CurrentInputLine);

				if (Enum.TryParse(CurrentInputLine, out ConsoleKey info))
				{
					char key = CurrentInputLine[0];
					GetNextInputLine();
					return new ConsoleKeyInfo(key, info, false, false, false);
				}
				else
				{
					Console.WriteLine($"{(m_tag != null ? $"{m_tag}" : "")}Error: Failed to parse a ConsoleKey from" +
						$" '{CurrentInputLine}', manual input is requried.");
					m_inputStream = null;
				}
			}

			return Console.ReadKey(intercept);
		}

		public string ReadLine()
		{
			if (ScriptMode)
			{
				string line = CurrentInputLine;
				Console.WriteLine(m_tag != null ? $"{m_tag} {CurrentInputLine}" : CurrentInputLine);
				LogWriteLine(line);
				GetNextInputLine();
				return line;
			}

			return Console.ReadLine();
		}

		public void WriteLine(string value)
		{
			Console.WriteLine(m_tag != null ? $"{m_tag} {value}" : value);
			LogWriteLine(value);
			CurrentOutputLine = value;
		}

		public void Write(char value)
		{
			Console.Write(value);
			LogWrite(value.ToString());
			CurrentOutputLine = value.ToString();
		}

		public void Write(string value)
		{
			Console.Write(m_tag != null ? $"{m_tag} {value}" : value);
			LogWrite(value);
			CurrentOutputLine = value;
		}

		public void SetCursorPosition(int left, int top)
		{
			if (!ScriptMode)
				Console.SetCursorPosition(left, top);
		}


		private void GetNextInputLine()
		{
			do
			{
				if (m_eof)
				{
					Console.WriteLine($"Error: The file {m_inputStreamFilename} has run out of input, manual input is now required.");
					m_inputStream = null;
					break;
				}

				m_eof = !m_inputStream.MoveNext();
				CurrentInputLine = m_inputStream.Current;
			}
			while (!m_eof && CurrentInputLine?.StartsWith("#") == true);
		}

		private void LogWriteLine(string line)
		{
			m_outputStream?.WriteLine(line);
		}

		private void LogWrite(string line)
		{
			m_outputStream?.Write(line);
		}


		private const string c_blockCommand = ">menu-block";
		private const string c_resumeCommand = ">menu-resume";
		private const string c_waitForCommand = ">wait-for: ";
		private readonly List<string> m_args;
		private readonly string m_inputStreamFilename;
		private IEnumerator<string> m_inputStream;
		private bool m_eof;
		private string m_tag;
		private readonly string m_outputStreamFilename;
		private readonly StreamWriter m_outputStream;
		private bool m_menuBlocked;
	}
}
