using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Verdict.Tests;

public class TestRunner
{
    private static int _passed = 0;
    private static int _failed = 0;
    private static readonly List<string> _failures = new();

    public static async Task RunAllTests()
    {
        Console.WriteLine("=== VERDICT COMPREHENSIVE TEST HARNESS ===\n");
        
        try 
        {
            // Run all test suites and aggregate results
            var modelResults = await ModelTests.GetTestResults();
            AggregateResults(modelResults);

            var serviceResults = await ServiceTests.GetTestResults();
            AggregateResults(serviceResults);

            var providerResults = await ProviderTests.GetTestResults();
            AggregateResults(providerResults);

            var viewModelResults = await ViewModelTests.GetTestResults();
            AggregateResults(viewModelResults);

            // Print final summary
            PrintSummary();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL] Test harness crashed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void AggregateResults((int passed, int failed, List<string> failures) results)
    {
        _passed += results.passed;
        _failed += results.failed;
        _failures.AddRange(results.failures);
    }

    private static void PrintSummary()
    {
        Console.WriteLine($"\n{'=',60}");
        Console.WriteLine($"  RESULTS: {_passed} passed, {_failed} failed out of {_passed + _failed} tests");
        if (_failures.Any())
        {
            Console.WriteLine($"\n  FAILURES:");
            foreach (var failure in _failures)
                Console.WriteLine($"    ✗ {failure}");
        }
        Console.WriteLine($"{'=',60}");
        
        if (_failed == 0)
            Console.WriteLine("\n[SUCCESS] All tests completed successfully.");
        else
            Console.WriteLine($"\n[WARNING] {_failed} test(s) failed.");
    }
}