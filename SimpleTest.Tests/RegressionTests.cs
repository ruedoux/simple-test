using System.Numerics;

namespace Qwaitumin.SimpleTest.Tests;

[TestClass]
public class RegressionTests
{
  [TestMethod]
  public void AssertEqualEnumerableComparesByContents()
  {
    Assertions.AssertEqual(new[] { 1, 2, 3 }, new List<int> { 1, 2, 3 });
  }

  [TestMethod]
  public void AssertAwaitAtMostStopsRetryingAfterTimeout()
  {
    int attemptCount = 0;

    Assertions.AssertThrows<TimeoutException>(() => Assertions.AssertAwaitAtMost(50, () =>
    {
      Interlocked.Increment(ref attemptCount);
      throw new InvalidOperationException("Still waiting.");
    }));

    Thread.Sleep(150);
    int stableCount = attemptCount;
    Thread.Sleep(100);
    Assertions.AssertEqual(stableCount, attemptCount);
  }

  [TestMethod]
  public void RunClassSupportsPrivateParameterlessConstructor()
  {
    var executor = new SimpleTestExecutor();
    var result = executor.RunClass(typeof(PrivateCtorTestClass));

    Assertions.AssertEqual(Result.Success, result.Result);
    Assertions.AssertEqual(1, result.MethodResults.Count);
    Assertions.AssertEqual(Result.Success, result.MethodResults[0].Result);
  }

  [TestMethod]
  public void HelpDoesNotPrintPassOrFailSummary()
  {
    List<string> output = [];

    bool result = new SimpleTestPrinter(output.Add).Run(["--help"]);

    Assertions.AssertTrue(result);
    Assertions.AssertFalse(output.Any(line => line.Contains("PASS") || line.Contains("FAIL")));
  }

  [TestMethod]
  public void TestMethodArgumentRequiresTestClass()
  {
    Assertions.AssertThrows<ArgumentException>(() =>
      new SimpleTestPrinter(_ => { }).Run(["--test-method", "Whatever"]));
  }

  [TestMethod]
  public void RunClassInvokesParameterizedTestMethodForEachArgument()
  {
    ParameterizedTestClass.Invocations.Clear();

    var executor = new SimpleTestExecutor();
    var result = executor.RunClass(typeof(ParameterizedTestClass));

    Assertions.AssertEqual(Result.Success, result.Result);
    Assertions.AssertEqual(3, result.MethodResults.Count);
    Assertions.AssertTrue(result.MethodResults.Select(m => m.Name).SequenceEqual(["RunsForEachValue(1)", "RunsForEachValue(2)", "RunsForEachValue(3)"]));
    Assertions.AssertTrue(ParameterizedTestClass.Invocations.SequenceEqual([1, 2, 3]));
  }

  [TestMethod]
  public void ParameterizedTestMethodRequiresArgumentsAttribute()
  {
    var executor = new SimpleTestExecutor();
    var result = executor.RunClass(typeof(InvalidParameterizedTestClass));

    Assertions.AssertEqual(Result.Error, result.Result);
    Assertions.AssertTrue(result.Messages.Any(message => message.Contains(nameof(TestMethodArguments))));
  }

  [TestClass]
  private class PrivateCtorTestClass
  {
    private PrivateCtorTestClass() { }

    [TestMethod]
    private void Runs()
    {
      Assertions.AssertTrue(true);
    }
  }

  [TestClass]
  private class ParameterizedTestClass
  {
    public static List<int> Invocations { get; } = [];

    [TestMethod]
    [TestMethodArguments(1, 2, 3)]
    private void RunsForEachValue(int value)
    {
      Invocations.Add(value);
      Assertions.AssertMoreThan(value, 0);
    }
  }

  [TestClass]
  private class InvalidParameterizedTestClass
  {
    [TestMethod]
    private void MissingArguments(int value)
    {
      Assertions.AssertMoreThan(value, 0);
    }
  }
}
