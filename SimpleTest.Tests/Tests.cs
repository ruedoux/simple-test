namespace Qwaitumin.SimpleTest.Tests;

public static class Program
{
  static void Main()
  {
    if (!new SimpleTestPrinter(Console.WriteLine).Run())
      Environment.Exit(1);
  }
}

[SimpleTestClass]
public class Tests
{
  [SimpleBeforeAll]
  public void BeforeAll()
  {
    Console.WriteLine("[SimpleBeforeAll] runs before all methods in a class!");
  }

  [SimpleBeforeEach]
  public void BeforeEachMethod()
  {
    Console.WriteLine("[SimpleBeforeEach] runs before each method in a class!");
  }

  [SimpleAfterAll]
  public void AfterAll()
  {
    Console.WriteLine("[SimpleAfterAll] runs after all methods in a class!");
  }

  [SimpleAfterEach]
  public void AfterEachMethod()
  {
    Console.WriteLine("[SimpleAfterEach] runs after each method in a class!");
  }

  [SimpleTestMethod]
  public void AssertionTestsPass()
  {
    Assertions.AssertEqual(0, 0);
    Assertions.AssertEqual("a", "a");
    Assertions.AssertNotEqual("a1", "a");
    Assertions.AssertMoreThan(1, 0);
    Assertions.AssertEqualOrMoreThan(0, 0);
    Assertions.AssertEqualOrMoreThan(1, 0);
    Assertions.AssertLessThan(0, 1);
    Assertions.AssertEqualOrLessThan(0, 0);
    Assertions.AssertEqualOrLessThan(0, 1);
    Assertions.AssertInRange(0, -1, 1);
    Assertions.AssertNotInRange(2, -1, 1);

    int? amINull = null;
    Assertions.AssertNull(amINull);
    amINull = 1;
    Assertions.AssertNotNull(amINull);
  }

  [SimpleTestMethod]
  public void AssertionTestsFail()
  {
    Assertions.AssertEqual(0, 1);
  }

  [SimpleTestMethod]
  public void FileAssertionTestsPass()
  {
    using SimpleTestDirectory testDirectory = new();
    var filePath = testDirectory.GetRelativePath("dummy-file");
    File.Create(filePath); // Test directory cleans all files created in it
    Assertions.AssertFileExists(filePath);
  }

  [SimpleTestMethod]
  public void FileAssertionTestsFail()
  {
    using SimpleTestDirectory testDirectory = new();
    var filePath = testDirectory.GetRelativePath("dummy-file");
    Assertions.AssertFileExists(filePath);
  }

  [SimpleTestMethod]
  public void LambdaAssertionPass()
  {
    Assertions.AssertThrows<DivideByZeroException>(() =>
    {
      int a = 0;
      for (int i = 0; i < 1; i++) a = 1 / i;
      Console.WriteLine($"You will never see this printed: {a}");
    });
    Assertions.AssertAwaitAtMost(1000, () => Thread.Sleep(10));
  }

  [SimpleTestMethod]
  public void LambdaAssertionPassFail1()
  {
    Assertions.AssertAwaitAtMost(50, () => throw new ArgumentException("Example endless exception."));
  }
}