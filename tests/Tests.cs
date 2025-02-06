using Qwaitumin.SimpleTest;

namespace Qwaitumin.SimpleTestTest;

[SimpleTestClass]
public class ExampleTest
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
  public void TestThatShouldPass1()
  {
    Assertions.AssertEqual(0, 0);
    Assertions.AssertEqual("a", "a");
    Assertions.AssertNotEqual("a1", "a");
    Assertions.AssertFileExists("resources/dummy");
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
  public void TestThatShouldPass2()
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
  public void TestThatShouldFail1()
  {
    Assertions.AssertAwaitAtMost(50, () => throw new ArgumentException("Example endless exception."));
  }

  [SimpleTestMethod]
  public void TestThatShouldFail2()
  {
    Assertions.AssertEqual(0, 1);
  }

  [SimpleTestMethod]
  public void TestThatShouldFail3()
  {
    Assertions.AssertInRange(0, 1, 2);
  }
}