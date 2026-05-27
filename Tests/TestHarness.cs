using System;
using System.Threading.Tasks;

namespace Verdict.Tests;

public static class TestHarness
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== VERDICT TEST HARNESS ===\n");

        // Run deliberation simulations
        await SimulationTests.RunDeliberationSimulations();

        // Run JurorOpinionTests (xUnit-based, called directly as static methods)
        Console.WriteLine("\n=== JUROR OPINION TESTS ===");
        var jurorTests = new JurorOpinionTests();
        jurorTests.TestJurorOpinionFormation();
        jurorTests.TestBiasInfluenceOnOpinion();
        jurorTests.TestOpinionStrengthDecay();
        jurorTests.TestJurorOpinionDescriptionGeneration();
        Console.WriteLine("  ✓ All JurorOpinionTests passed.\n");

        // Run comprehensive test suites via TestRunner
        await TestRunner.RunAllTests();
    }
}

