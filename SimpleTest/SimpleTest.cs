using System.Reflection;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Qwaitumin.SimpleTest;

public enum Result { SUCCESS, FAIL }

[AttributeUsage(AttributeTargets.Class)]
public class SimpleTestClass : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class SimpleTestMethod : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class SimpleBeforeEach : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class SimpleAfterEach : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class SimpleBeforeAll : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class SimpleAfterAll : Attribute { }


public record SimpleTestMethodResult(string Name, Result Result, string[] Messages);

public record SimpleTestClassResult(
  string Name,
  Result Result,
  List<SimpleTestMethodResult> MethodResults,
  long TookMiliseconds,
  string[] Messages);

// This really doesnt matter since tests should not be included in the release build,
// or it can simply be marked to not trim test classes.
#pragma warning disable IL2067
#pragma warning disable IL2026
#pragma warning disable IL2070
public class SimpleTestRunner(
  Action<Type>? beginClassPrinter = null,
  Action<SimpleTestMethodResult>? methodResultPrinter = null,
  Action<SimpleTestClassResult>? endClassPerinter = null)
{
  public List<SimpleTestClassResult> ClassResults = [];

  private readonly Action<Type>? beginClassPrinter = beginClassPrinter;
  private readonly Action<SimpleTestMethodResult>? methodResultPrinter = methodResultPrinter;
  private readonly Action<SimpleTestClassResult>? endClassPerinter = endClassPerinter;

  public void RunAll()
  {
    var testTypes = GetTestAllClassTypes();

    foreach (var testType in testTypes)
      RunForType(testType);
  }

  public SimpleTestClassResult RunForType(Type testClassType)
  {
    if (!testClassType.IsDefined(typeof(SimpleTestClass), true))
      throw new ArgumentException($"Class '{testClassType.Name}' does not have the '{nameof(SimpleTestClass)}' attribute.");

    beginClassPrinter?.Invoke(testClassType);

    var testObject = Activator.CreateInstance(testClassType);
    var beforeAllMethod = GetMethodWithAttribute<SimpleBeforeAll>(testClassType);
    var afterAllMethod = GetMethodWithAttribute<SimpleAfterAll>(testClassType);
    var beforeEachMethod = GetMethodWithAttribute<SimpleBeforeEach>(testClassType);
    var afterEachMethod = GetMethodWithAttribute<SimpleAfterEach>(testClassType);
    var testMethods = GetMethodsWithAttribute<SimpleTestMethod>(testClassType);

    List<SimpleTestMethodResult> methodResults = [];
    Stopwatch classStopwatch = Stopwatch.StartNew();

    Result classResult = Result.SUCCESS;
    string[] classMessages = [];
    try
    {
      beforeAllMethod?.Invoke(testObject, null);

      foreach (var testMethod in testMethods)
      {
        beforeEachMethod?.Invoke(testObject, null);

        Result methodResult = Result.SUCCESS;
        string[] methodMessages = [];
        try
        {
          testMethod.Invoke(testObject, null);
        }
        catch (Exception ex)
        {
          methodResult = Result.FAIL;
          ex = ex.InnerException ?? ex; // Need only inner exception (for clarity)
          methodMessages = ConvertExceptionToStringArray(ex);
        }

        SimpleTestMethodResult testMethodResult = new(
          testMethod.Name, methodResult, methodMessages);
        methodResults.Add(testMethodResult);
        methodResultPrinter?.Invoke(testMethodResult);

        afterEachMethod?.Invoke(testObject, null);
      }

      afterAllMethod?.Invoke(testObject, null);
    }
    catch (Exception ex)
    {
      classResult = Result.FAIL;
      classMessages = ConvertExceptionToStringArray(ex);
    }
    classStopwatch.Stop();

    classResult = methodResults.Exists(mr => mr.Result == Result.FAIL) ? Result.FAIL : Result.SUCCESS;
    classMessages = classMessages.Length != 0 ? classMessages : ["At least one of the methods has failed"];
    var simpleTestClassResult = new SimpleTestClassResult(testClassType.Name, classResult, methodResults, classStopwatch.ElapsedMilliseconds, classMessages);

    ClassResults.Add(simpleTestClassResult);
    endClassPerinter?.Invoke(simpleTestClassResult);

    return simpleTestClassResult;
  }

  public static Type[] GetTestAllClassTypes()
  {
    var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
    List<Type> types = [];
    foreach (var assembly in allAssemblies)
      types.AddRange(assembly.GetTypes()
        .Where(t => t.GetCustomAttributes(typeof(SimpleTestClass), true).Length > 0));

    return [.. types];
  }

  private static MethodInfo? GetMethodWithAttribute<T>(Type type) where T : Attribute
    => Array.Find(type.GetMethods(), m => m.GetCustomAttributes(typeof(T), true).Length > 0);

  private static MethodInfo[] GetMethodsWithAttribute<T>(Type type) where T : Attribute
    => type.GetMethods()
      .Where(m => m.GetCustomAttributes(typeof(T), true).Length > 0)
      .ToArray();

  private static string[] ConvertExceptionToStringArray(Exception ex)
    => ex.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
}
#pragma warning restore IL2067
#pragma warning restore IL2026
#pragma warning restore IL2070

// Example test runner implementation
public partial class SimpleTestPrinter(
  Action<string> printFunction,
  bool parseException = true)
{
  static class Ansi
  {
    public static readonly string NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";
    public static readonly string RED = Console.IsOutputRedirected ? "" : "\x1b[91m";
    public static readonly string GREEN = Console.IsOutputRedirected ? "" : "\x1b[92m";
    public static readonly string BLUE = Console.IsOutputRedirected ? "" : "\x1b[94m";
    public static readonly string GREY = Console.IsOutputRedirected ? "" : "\x1b[97m";
  }

  static partial class RegexHandler
  {
    [GeneratedRegex(@"(/.+/)|(\d+_\d+)|(<)")]
    public static partial Regex ExceptionRegexClear();

    [GeneratedRegex(@"(\.<>c\.)|(\.<>c__)")]
    public static partial Regex ExceptionDot();

    [GeneratedRegex(@"(\(.+\))")]
    public static partial Regex ExceptionRegexParentheses();

    [GeneratedRegex(@"(>([^\n>]*?)\()")]
    public static partial Regex ExceptionRegexParenthesesOpen();
  }

  static readonly string PREFIX_RUN = "[RUN]";
  static readonly string PREFIX_OK = "[OK ]";
  static readonly string PREFIX_ERROR = "[ERR]";

  public bool Run()
  {
    SimpleTestRunner simpleTestRunner = new(LogClassBegin, LogMethodResult, LogClassResult);
    Stopwatch stopwatch = Stopwatch.StartNew();
    simpleTestRunner.RunAll();
    stopwatch.Stop();

    int classesPassed = 0, classesTotal = 0, methodsPassed = 0, methodsTotal = 0;
    foreach (var classResult in simpleTestRunner.ClassResults)
    {
      classesTotal++;
      if (classResult.Result == Result.SUCCESS) classesPassed++;
      foreach (var methodResult in classResult.MethodResults)
      {
        methodsTotal++;
        if (methodResult.Result == Result.SUCCESS) methodsPassed++;
      }
    }

    string resultText = classesTotal == classesPassed ? AddColorToString("PASS", Ansi.GREEN) : AddColorToString("FAIL", Ansi.RED);
    printFunction($"""
    ----------------------
    {resultText} Classes: {classesPassed}/{classesTotal} Methods: {methodsPassed}/{methodsTotal}
    Took {FormatTime(stopwatch.ElapsedMilliseconds)}
    ----------------------
    """);
    return classesTotal == classesPassed;
  }

  private void LogClassBegin(Type type)
    => printFunction($"{AddColorToString(PREFIX_RUN, Ansi.BLUE)} {type.Name}");

  private void LogClassResult(SimpleTestClassResult classResult)
  {
    if (classResult.Result == Result.SUCCESS)
      printFunction($"{AddColorToString(PREFIX_OK, Ansi.GREEN)} {classResult.Name} {AddColorToString(FormatTime(classResult.TookMiliseconds), Ansi.GREY)}");
    if (classResult.Result == Result.FAIL)
    {
      printFunction($"{AddColorToString(PREFIX_ERROR, Ansi.RED)} {classResult.Name} {AddColorToString(FormatTime(classResult.TookMiliseconds), Ansi.GREY)}");
      printFunction(string.Join('\n', classResult.Messages));
    }
  }

  private void LogMethodResult(SimpleTestMethodResult methodResult)
  {
    if (methodResult.Result == Result.SUCCESS)
      printFunction($"-> {AddColorToString(PREFIX_OK, Ansi.GREEN)} {methodResult.Name}");
    if (methodResult.Result == Result.FAIL)
    {
      printFunction($"-> {AddColorToString(PREFIX_ERROR, Ansi.RED)} {methodResult.Name}");
      string exceptionMessage = parseException ?
        ParseException(string.Join('\n', methodResult.Messages)) :
        string.Join('\n', methodResult.Messages);
      printFunction(exceptionMessage);
    }
  }

  private static string FormatTime(long miliseconds)
    => $"{miliseconds / 1000}.{miliseconds % 1000}s";

  private static string AddColorToString(string msg, string color)
    => $"{color}{msg}{Ansi.NORMAL}";

  private static string ParseException(string exceptionText)
  {
    exceptionText = RegexHandler.ExceptionDot().Replace(exceptionText, ".");
    exceptionText = RegexHandler.ExceptionRegexParentheses().Replace(exceptionText, "()");
    exceptionText = RegexHandler.ExceptionRegexParenthesesOpen().Replace(exceptionText, "(");
    exceptionText = RegexHandler.ExceptionRegexClear().Replace(exceptionText, string.Empty);

    return exceptionText;
  }
}

public static class SimpleEqualsVerifier
{
  public static void Verify<T>(
    T obj, T objToEqual, T objToNotEqual)
    where T : notnull
  {
    Assertions.AssertEqual(obj, objToEqual);
    Assertions.AssertNotEqual(obj, objToNotEqual);
    Assertions.AssertEqual(obj.GetHashCode(), objToEqual.GetHashCode());
    Assertions.AssertNotEqual(obj.GetHashCode(), objToNotEqual.GetHashCode());
  }
}

public sealed class SimpleTestDirectory : IDisposable
{
  public readonly string AbsolutePath;

  public SimpleTestDirectory(string path = "./TEMPORARY_TEST_FOLDER")
  {
    AbsolutePath = Path.GetFullPath(path);
    Create();
  }

  public string GetRelativePath(string path)
    => $"{AbsolutePath}/{path}";

  public void Clean()
  {
    Delete();
    Create();
  }

  public void Delete()
    => Directory.Delete(AbsolutePath, recursive: true);

  private void Create()
    => Directory.CreateDirectory(AbsolutePath);

  public void Dispose()
  {
    Delete();
  }
}

public static class Assertions
{
  public static void AssertFileExists(string filePath)
  {
    if (!File.Exists(filePath))
      throw new FileNotFoundException($"File doesnt exist: {filePath}");
  }

  public static void AssertDirectoryExists(string directoryPath)
  {
    if (!Directory.Exists(directoryPath))
      throw new DirectoryNotFoundException($"Directory doesnt exist: {directoryPath}");
  }

  public static void AssertNotNull<T>([NotNull] T? obj, string additionalMessage = "")
  {
    if (obj is null)
      throw new ArgumentNullException(
        nameof(obj), $"Argument cannot be null. {additionalMessage}");
  }

  public static void AssertNull<T>(T? obj, string additionalMessage = "")
  {
    if (obj is not null)
      throw new ArgumentNullException(
        nameof(obj), $"Argument should be null. {additionalMessage}");
  }

  public static void AssertTrue(bool shoudlBeTrue, string additionalMessage = "")
  {
    if (!shoudlBeTrue)
    {
      throw new ValidationException(
        $"Value is false, but expected true. {additionalMessage}");
    }
  }

  public static void AssertFalse(bool shoudlBeFalse, string additionalMessage = "")
  {
    if (shoudlBeFalse)
    {
      throw new ValidationException(
        $"Value is true, but expected false. {additionalMessage}");
    }
  }

  public static void AssertEqual<T>(
    T shouldBe, T isNow, string additionalMessage = "")
  {
    if (!Equals(shouldBe, isNow))
    {
      throw new ValidationException(
        $"Value is not equal, is: '{isNow}', but should be: '{shouldBe}'. {additionalMessage}");
    }
  }

  public static void AssertEqual<T>(
    IEnumerable<T> shouldBe, IEnumerable<T> isNow, string additionalMessage = "")
  {
    if (!Equals(shouldBe, isNow))
    {
      throw new ValidationException(
        $"Value is not equal, is: '{isNow}', but should be: '{shouldBe}'. {additionalMessage}");
    }
  }

  public static void AssertNotEqual<T>(
    T shouldNotBe, T isNow, string additionalMessage = "")
  {
    if (Equals(shouldNotBe, isNow))
    {
      throw new ValidationException(
        $"Value is equal to: '{shouldNotBe}'. {additionalMessage}");
    }
  }

  public static void AssertLessThan<T>(
    T value, T maxValue, string additionalMessage = "")
        where T : IComparable<T>
  {
    if (value.CompareTo(maxValue) >= 0)
    {
      throw new ValidationException(
          $"Value '{value}' is not less than '{maxValue}'. {additionalMessage}");
    }
  }

  public static void AssertMoreThan<T>(
    T value, T minValue, string additionalMessage = "")
      where T : IComparable<T>
  {
    if (value.CompareTo(minValue) <= 0)
    {
      throw new ValidationException(
          $"Value '{value}' is not larger than '{minValue}'. {additionalMessage}");
    }
  }

  public static void AssertEqualOrLessThan<T>(
    T value, T maxValue, string additionalMessage = "")
        where T : IComparable<T>
  {
    if (value.CompareTo(maxValue) > 0)
    {
      throw new ValidationException(
          $"Value '{value}' is greater than '{maxValue}'. {additionalMessage}");
    }
  }

  public static void AssertEqualOrMoreThan<T>(
    T value, T minValue, string additionalMessage = "")
      where T : IComparable<T>
  {
    if (value.CompareTo(minValue) < 0)
    {
      throw new ValidationException(
          $"Value '{value}' is less than '{minValue}'. {additionalMessage}");
    }
  }

  public static void AssertNotInRange<T>(
    T value, T minValue, T maxValue, string additionalMessage = "")
      where T : IComparable<T>
  {
    if (value.CompareTo(minValue) >= 0 && value.CompareTo(maxValue) <= 0)
    {
      throw new ValidationException(
          $"Value '{value}' is in range: [{minValue}-{maxValue}]. {additionalMessage}");
    }
  }

  public static void AssertInRange<T>(
    T value, T minValue, T maxValue, string additionalMessage = "")
      where T : IComparable<T>
  {
    if (value.CompareTo(minValue) < 0 || value.CompareTo(maxValue) > 0)
    {
      throw new ValidationException(
          $"Value '{value}' is not in range: [{minValue}-{maxValue}]. {additionalMessage}");
    }
  }

  public static void AssertAwaitAtMost(long timeoutMs, Action action)
  {
    Exception trackedException = new("No exception, timed out.");
    var actionTask = Task.Run(() =>
    {
      while (true)
      {
        try
        {
          action();
          break;
        }
        catch (Exception ex)
        {
          trackedException = ex;
        }
        Thread.Sleep(10);
      }
    });

    var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(timeoutMs));

    if (Task.WhenAny(actionTask, timeoutTask).Result == timeoutTask)
      throw new TimeoutException(
        $"Assertion was not passed in time: {timeoutMs}ms. Reason: {trackedException.Message}\n" +
        $"{trackedException.StackTrace}");

    if (actionTask.IsFaulted && actionTask.Exception != null)
      throw actionTask.Exception;
  }

  public static void AssertThrows<T>(
    Action action, string additionalMessage = "") where T : Exception
  {
    try
    {
      action();
    }
    catch (T)
    {
      return;
    }
    catch (Exception ex)
    {
      throw new ValidationException(
        $"Expected exception of type '{typeof(T)}', but got '{ex.GetType()}' instead. {additionalMessage}");
    }

    throw new ValidationException($"Expected exception of type '{typeof(T)}' was not thrown. {additionalMessage}");
  }
}