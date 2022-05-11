using System.Threading.Tasks;

namespace SignalRConsole
{
	public class Program
	{
		private static async Task<int> Main(string[] args)
		{
			Harness harness = new Harness();
			return await new ConsoleChat().Run(args, harness);
		}
	}
}
