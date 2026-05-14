using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Verdict.Tests;

public abstract class BaseTestClass
{
    protected static int _passed = 0;
    protected static int _failed = 0;
    protected static readonly List<string> _failures = new();

    protected static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  ✓ {testName}");
        }
        else
        {
            _failed++;
            _failures.Add(testName);
            Console.WriteLine($"  ✗ {testName} - FAILED");
        }
    }

    protected static void ResetCounters()
    {
        _passed = 0;
        _failed = 0;
        _failures.Clear();
    }

    protected static (int passed, int failed, List<string> failures) GetResults()
    {
        return (_passed, _failed, _failures);
    }
}