namespace Qwaitumin.SimpleTest.Tests;

[SimpleTestClass]
public class Tests2
{
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