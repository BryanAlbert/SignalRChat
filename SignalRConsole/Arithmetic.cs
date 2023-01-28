using System.Collections.Generic;
using System;

namespace SignalRConsole
{
	public class Arithmetic
	{
		public Arithmetic()
		{
		}

		public Arithmetic(string handle, string email)
		{
			Handle = handle;
			Email = email;
		}

		public Arithmetic(User user) : this(user.Handle, user.Email)
		{
			Id = user.Id;
			Name = user.Name;
			Color = user.Color;
			Created = user.Created;
			Modified = user.Modified;
		}


		public static readonly Dictionary<FactOperator, Operator> Operator = new()
		{
			{
				FactOperator.Multiplication, new Operator
				{
					FactOperator = FactOperator.Multiplication,
					Name = FactOperator.Multiplication.ToString(),
					Symbol = c_arithmeticMultiplicationSymbol,
					Create = (b, c) => new Tuple<int, int>(b, c),
					Compute = (f, s) => f * s,
					Filter = (t, f) => t.Contains(f.Second)
				}
			},
			{
				FactOperator.Division, new Operator
				{
					// since Console only creates cards from the RaceCard command, they're just first, second
					FactOperator = FactOperator.Division,
					Name = FactOperator.Division.ToString(),
					Symbol = c_arithmeticDivisionSymbol,
					Create = (b, c) => new Tuple<int, int>(b, c),
					Compute = (f, s) => f / s,
					Filter = (t, f) => t.Contains(f.First / f.Second)
				}
			},
			{
				FactOperator.Addition, new Operator
				{
					FactOperator = FactOperator.Addition,
					Name = FactOperator.Addition.ToString(),
					Symbol = c_arithmeticAdditionSymbol,
					Create = (b, c) => new Tuple<int, int>(b, c),
					Compute = (f, s) => f + s,
					Filter = (t, f) => t.Contains(f.Second)
				}
			},
			{
				FactOperator.Subtraction, new Operator
				{
					// since Console only creates cards from the RaceCard command, they're just first, second
					FactOperator = FactOperator.Subtraction,
					Name = FactOperator.Subtraction.ToString(),
					Symbol = c_arithmeticSubtractionSymbol,
					Create = (b, c) => new Tuple<int, int>(b, c),
					Compute = (f, s) => f - s,
					Filter = (t, f) => t.Contains(f.First - f.Second + 1)
				}
			}
		};


		public string Id { get; set; }
		public string Email { get; set; }
		public string Name { get; set; }
		public string Handle { get; set; }
		public string Color { get; set; }
		public string Created { get; set; }
		public string Modified { get; set; }


		public const string c_arithmeticMultiplicationSymbol = "\u00D7";
		public const string c_arithmeticDivisionSymbol = "\u00F7";
		public const string c_arithmeticAdditionSymbol = "\u002B";
		public const string c_arithmeticSubtractionSymbol = "\u2212";
	}
}