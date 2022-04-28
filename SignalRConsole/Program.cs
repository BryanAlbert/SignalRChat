using System.Threading.Tasks;

namespace SignalRConsole
{
	public class Program
	{
		private static async Task<int> Main(string[] args)
		{
			return await new ConsoleChat().Run(args);
		}
	}
}
