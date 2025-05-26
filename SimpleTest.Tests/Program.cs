namespace Qwaitumin.SimpleTest.Tests;

public static class Program
{
  static void Main()
  {
    new SimpleTestPrinter(Console.WriteLine).Run(); // This should run everything
    Console.WriteLine("****");
    new SimpleTestPrinter(Console.WriteLine).Run(["--test-class", "Tests2"]); // This should only run "Tests" class
    Console.WriteLine("****");
    new SimpleTestPrinter(Console.WriteLine).Run(["--test-class", "Tests2", "--test-method", "LambdaAssertionPass"]); // This should only run "Tests" a single method
  }
}
