using System;
using System.Threading.Tasks;

namespace Verdict.Tests;

public class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting TestHarness tests...");
        await TestHarness.RunTests();
        await ProviderTests.RunAllTests();
        await ServiceTests.RunAllTests();
        Console.WriteLine("\nTests finished.");
    }
}
