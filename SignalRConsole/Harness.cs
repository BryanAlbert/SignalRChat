using System;
using System.Collections.Generic;
using System.IO;

namespace SignalRConsole
{
	public class Harness
	{
		public Harness(string[] args)
		{
			m_args = new List<string>(args);
			if (m_args.Count > 0 && File.Exists(m_args[0]))
			{
				m_inputStreamFilename = NextArg();
				Console.WriteLine($"Note: input is provided by {m_inputStreamFilename}");
				m_inputStream = File.ReadLines(m_inputStreamFilename).GetEnumerator();
			}
		}


		public int CursorLeft { get => Console.CursorLeft; set { Console.CursorLeft = value; } }
		public int CursorTop { get => Console.CursorTop; set { Console.CursorTop = value; } }
		public bool KeyAvailable => m_inputStream != null || Console.KeyAvailable;
		public ConsoleColor ForegroundColor { get => Console.ForegroundColor; set { Console.ForegroundColor = value; } }
		public ConsoleColor BackgroundColor { get => Console.BackgroundColor; set { Console.BackgroundColor = value; } }


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
			if (m_inputStream != null)
			{
				if (m_inputStream.MoveNext())
				{
					if (!intercept)
						Console.Write(m_inputStream.Current);

					if (Enum.TryParse(m_inputStream.Current, out ConsoleKey info))
						return new ConsoleKeyInfo(m_inputStream.Current[0], info, false, false, false);

					Console.WriteLine($"Error: Failed to parse a ConsoleKey from '{m_inputStream.Current}'.");
				}
				else
				{
					Console.WriteLine($"Error: The file {m_inputStreamFilename} has run out of input, manual input is required.");
				}
			}

			return Console.ReadKey(intercept);
		}

		public string ReadLine()
		{
			if (m_inputStream != null)
			{
				if (m_inputStream.MoveNext())
				{
					Console.WriteLine(m_inputStream.Current);
					return m_inputStream.Current;
				}
				else
				{
					Console.WriteLine($"Error: the file {m_inputStreamFilename} has run out of input, manual input is required.");
				}
			}

			return Console.ReadLine();
		}

		public void WriteLine(string value)
		{
			Console.WriteLine(value);
		}

		public void Write(char value)
		{
			Console.Write(value);
		}

		public void Write(string value)
		{
			Console.Write(value);
		}

		public void SetCursorPosition(int left, int top)
		{
			Console.SetCursorPosition(left, top);
		}


		private readonly List<string> m_args;
		private readonly string m_inputStreamFilename;
		private readonly IEnumerator<string> m_inputStream;
	}
}
