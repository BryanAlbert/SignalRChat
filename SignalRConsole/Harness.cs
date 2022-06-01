using System; 
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
			if (m_args.Count > 0 && Directory.Exists(args[0]))
			{
				WorkingDirectory = NextArg();
				Console.WriteLine($"Harness: working directory is: {WorkingDirectory}");
			}
			else if (m_args.Count > 0 && (File.Exists(args[0]) || WorkingDirectory == null ? File.Exists(args[0]) :
				File.Exists(Path.Combine(WorkingDirectory, args[0]))))
			{
				// command line can contain the script file name, the logging file name, and the tag for console output
				m_inputStreamFilename = NextArg();
				if (WorkingDirectory == ".")
					WorkingDirectory = Directory.GetParent(m_inputStreamFilename).FullName;
				else
					m_inputStreamFilename = Path.Combine(WorkingDirectory, m_inputStreamFilename);

				Console.WriteLine($"Harness: input is provided by {m_inputStreamFilename}");
				m_inputStream = File.ReadLines(m_inputStreamFilename).GetEnumerator();
				CueNextInputLine();

				if (m_args.Count > 0)
				{
					m_outputStreamFilename = NextArg();
					if (workingDirectory != null)
						m_outputStreamFilename = Path.Combine(WorkingDirectory, m_outputStreamFilename); 

					Console.WriteLine($"Harness: output is written to {m_outputStreamFilename}");
					m_outputStream = new StreamWriter(m_outputStreamFilename, true) { AutoFlush = true };
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
		public string NextInputLine { get; private set; }
		public string CurrentOutputLine
		{
			set => m_output.Add(value);
			get
			{
				string output = m_output[0];
				m_output.RemoveAt(0);
				return output;
			}
		}

		public bool KeyAvailable
		{
			get
			{
				if (!ScriptMode)
					return Console.KeyAvailable;

				if (NextInputLine.StartsWith(c_menuBlockCommand))
				{
					CueNextInputLine();
					m_menuBlocked = true;
				}
				else if (NextInputLine.StartsWith(c_menuResumeCommand))
				{
					CueNextInputLine();
					m_menuBlocked = false;
				}
				
				if (NextInputLine.StartsWith(c_waitForCommand))
				{
					if (OutputMatches(NextInputLine[c_waitForCommand.Length..], false))
						CueNextInputLine();

					return !(NextInputLine.StartsWith(c_waitForCommand[0]) || NextInputLine.StartsWith(c_waitForRegExCommand[0]));
				}
				else if (NextInputLine.StartsWith(c_waitForRegExCommand))
				{
					if (OutputMatches(NextInputLine[c_waitForRegExCommand.Length..], true))
						CueNextInputLine();

					return !(NextInputLine.StartsWith(c_waitForCommand[0]) || NextInputLine.StartsWith(c_waitForRegExCommand[0]));
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
				LogWriteLine(NextInputLine);
				Console.WriteLine(m_tag != null ? $"{m_tag}: {NextInputLine}" : NextInputLine);

				if (Enum.TryParse(NextInputLine, out ConsoleKey info))
				{
					char key = NextInputLine[0];
					CueNextInputLine();
					return new ConsoleKeyInfo(key, info, false, false, false);
				}
				else
				{
					Console.WriteLine($"{(m_tag != null ? $"{m_tag}: " : "")}Error: Failed to parse a ConsoleKey from" +
						$" '{NextInputLine}', manual input is requried.");
					m_inputStream = null;
				}
			}

			return Console.ReadKey(intercept);
		}

		public string ReadLine()
		{
			if (ScriptMode)
			{
				string line = NextInputLine;
				Console.WriteLine(m_tag != null ? $"{m_tag}: {NextInputLine}" : NextInputLine);
				LogWriteLine(line);
				CueNextInputLine();
				return line;
			}

			return Console.ReadLine();
		}

		public void WriteLine(string value)
		{
			if (ScriptMode)
				value = value.Trim('\n');

			Console.WriteLine(m_tag != null ? $"{m_tag}: {value}" : value);
			LogWriteLine(value);
			CurrentOutputLine = value;
		}

		public void Write(char value)
		{
			if (ScriptMode)
				Console.WriteLine(value);
			else
				Console.Write(value);

			m_outputStream?.Write(value.ToString());
			CurrentOutputLine = value.ToString();
		}

		public void Write(string value)
		{
			if (ScriptMode)
				Console.WriteLine(m_tag != null ? $"{m_tag}: {value}" : value);
			else
				Console.Write(m_tag != null ? $"{m_tag}: {value}" : value);

			m_outputStream?.Write(value);
			CurrentOutputLine = value;
		}

		public void SetCursorPosition(int left, int top)
		{
			if (!ScriptMode)
				Console.SetCursorPosition(left, top);
		}

		public void Close()
		{
			m_outputStream?.Close();
			m_outputStream = null;
		}

		public bool OutputMatches(string line, bool useRegEx)
		{
			while(m_output.Count > 0)
			{
				bool match = useRegEx ? Regex.Match(m_output[0], line).Success : m_output[0].Trim() == line;
				m_output.RemoveAt(0);
				if (match)
					return true;
			}

			return false;
		}


		private void CueNextInputLine()
		{
			do
			{
				if (m_eof)
				{
					Console.WriteLine($"{(m_tag != null ? $"{m_tag} " : "")}Error: The file {m_inputStreamFilename}" +
						$" has run out of input, manual input is now required.");
					m_inputStream = null;
					break;
				}

				m_eof = !m_inputStream.MoveNext();
				NextInputLine = m_inputStream.Current;
			}
			while (!m_eof && NextInputLine?.StartsWith("#") == true);
		}

		private void LogWriteLine(string line)
		{
			m_outputStream?.WriteLine(line);
		}


		private const string c_menuBlockCommand = ">menu-block";
		private const string c_menuResumeCommand = ">menu-resume";
		private const string c_waitForCommand = ">wait-for: ";
		private const string c_waitForRegExCommand = ">wait-for-regex: ";
		private readonly List<string> m_args;
		private readonly string m_inputStreamFilename;
		private readonly string m_tag;
		private readonly string m_outputStreamFilename;
		private readonly List<string> m_output = new List<string>();
		private IEnumerator<string> m_inputStream;
		private bool m_eof;
		private StreamWriter m_outputStream;
		private bool m_menuBlocked;
	}
}
