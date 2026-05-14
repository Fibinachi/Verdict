using System;
using System.Threading.Tasks;

namespace Verdict.Tests;

public class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        await TestHarness.RunTests();
        
        Console.WriteLine("\nTests finished.");
    }
}
