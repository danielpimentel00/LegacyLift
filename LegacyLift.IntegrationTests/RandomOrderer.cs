using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestCaseOrderer("LegacyLift.IntegrationTests.RandomOrderer", "LegacyLift.IntegrationTests")]

namespace LegacyLift.IntegrationTests;

public class RandomOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(_ => Random.Shared.Next());
}