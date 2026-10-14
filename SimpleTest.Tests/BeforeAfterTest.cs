namespace SimpleTest.Tests;

[TestClass]
public class BeforeAfterTest
{
  [BeforeAll]
  public void BeforeAll()
  {
    Console.WriteLine("[SimpleBeforeAll] runs before all methods in a class!");
  }

  [BeforeEach]
  public void BeforeEachMethod()
  {
    Console.WriteLine("[SimpleBeforeEach] runs before each method in a class!");
  }

  [AfterAll]
  public void AfterAll()
  {
    Console.WriteLine("[SimpleAfterAll] runs after all methods in a class!");
  }

  [AfterEach]
  public void AfterEachMethod()
  {
    Console.WriteLine("[SimpleAfterEach] runs after each method in a class!");
  }

  [TestMethod]
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

  [TestMethod]
  public void AssertionTestsFail()
  {
    Assertions.AssertEqual(0, 1);
  }

  [TestMethod]
  public void FileAssertionTestsPass()
  {
    using SimpleTestDirectory testDirectory = new();
    var filePath = testDirectory.GetRelativePath("dummy-file");
    File.Create(filePath); // Test directory cleans all files created in it
    Assertions.AssertFileExists(filePath);
  }

  [TestMethod]
  public void FileAssertionTestsFail()
  {
    using SimpleTestDirectory testDirectory = new();
    var filePath = testDirectory.GetRelativePath("dummy-file");
    Assertions.AssertFileExists(filePath);
  }
}