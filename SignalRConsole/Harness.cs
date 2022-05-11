using System;

namespace SignalRConsole
{
	public class Harness
	{
		public Harness()
		{
		}


		public int CursorLeft { get => Console.CursorLeft; set { Console.CursorLeft = value; } }
		public int CursorTop { get => Console.CursorTop; set { Console.CursorTop = value; } }
		public bool KeyAvailable => Console.KeyAvailable;
		public ConsoleColor ForegroundColor { get => Console.ForegroundColor; set { Console.ForegroundColor = value; } }
		public ConsoleColor BackgroundColor { get => Console.BackgroundColor; set { Console.BackgroundColor = value; } }


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

		public ConsoleKeyInfo ReadKey(bool intercept)
		{
			return Console.ReadKey(intercept);
		}

		public ConsoleKeyInfo ReadKey()
		{
			return Console.ReadKey();
		}

		public void SetCursorPosition(int left, int top)
		{
			Console.SetCursorPosition(left, top);
		}

		public string ReadLine()
		{
			return Console.ReadLine();
		}
	}
}
