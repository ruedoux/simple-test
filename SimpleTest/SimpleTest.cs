using System.Reflection;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace SimpleTest;

public enum Result { Success, Fail, Error }

[AttributeUsage(AttributeTargets.Class)]
public class TestClass : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class TestMethod : Attribute { }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestMethodArguments : Attribute
{
  public object?[] Values { get; }

  public TestMethodArguments(params bool[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params byte[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params short[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params int[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params long[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params float[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params double[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params decimal[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params char[] values)
    => Values = [.. values.Cast<object>()];

  public TestMethodArguments(params string[] values)
    => Values = [.. values.Cast<object?>()];
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class BeforeEach : Attribute { }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AfterEach : Attribute { }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class BeforeAll : Attribute { }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AfterAll : Attribute { }

public record MethodResult(
  string Name, Result Result, string[] Messages, long TookMilliseconds);
public record TestClassResult(
  string Name, Result Result, List<MethodResult> MethodResults, long TookMilliseconds, string[] Messages);

// TODO: for now not intended in release AOT, maybe create code generator instead
#pragma warning disable IL2067
#pragma warning disable IL2026
#pragma warning disable IL2070
public class SimpleTestExecutor(
  Action<Type>? beginClassPrinter = null,
  Action<MethodResult>? methodResultPrinter = null,
  Action<TestClassResult>? endClassPrinter = null)
{
  const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
  const BindingFlags CONSTRUCTOR_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

  private readonly Action<Type>? beginClassPrinter = beginClassPrinter;
  private readonly Action<MethodResult>? methodResultPrinter = methodResultPrinter;
  private readonly Action<TestClassResult>? endClassPrinter = endClassPrinter;

  public TestClassResult[] RunAll()
  {
    var testTypes = GetAllTestClassTypes();
    List<TestClassResult> testClassResults = [];

    foreach (var testType in testTypes)
      testClassResults.Add(RunClass(testType));

    return [.. testClassResults];
  }

  public TestClassResult RunClass(string name)
  {
    var type = GetTestClassTypeWithName(name)
      ?? throw new ArgumentException($"There is no defined test class: {name}");
    return RunClass(type);
  }

  public TestClassResult RunClass(Type testClassType, MethodInfo[]? methodsOverwrite = null)
  {
    if (!testClassType.IsDefined(typeof(TestClass), true))
      throw new ArgumentException($"Class '{testClassType.Name}' does not have the '{nameof(TestClass)}' attribute.");

    Stopwatch classStopwatch = Stopwatch.StartNew();
    beginClassPrinter?.Invoke(testClassType);

    List<MethodResult> methodResults = [];
    List<string> messages = [];
    Result result = Result.Success;

    try
    {
      var beforeAllMethod = GetSingleMethodWithAttribute<BeforeAll>(testClassType);
      var afterAllMethod = GetSingleMethodWithAttribute<AfterAll>(testClassType);
      var beforeEachMethod = GetSingleMethodWithAttribute<BeforeEach>(testClassType);
      var afterEachMethod = GetSingleMethodWithAttribute<AfterEach>(testClassType);

      var testMethods = methodsOverwrite is null
        ? GetMethodsWithAttribute<TestMethod>(testClassType)
        : methodsOverwrite;

      var requiresInstance =
        !(beforeAllMethod?.IsStatic ?? true) ||
        !(afterAllMethod?.IsStatic ?? true) ||
        !(beforeEachMethod?.IsStatic ?? true) ||
        !(afterEachMethod?.IsStatic ?? true) ||
        testMethods.Any(m => !m.IsStatic);

      if (requiresInstance && testClassType.GetConstructor(CONSTRUCTOR_FLAGS, null, Type.EmptyTypes, null) is null)
        throw new InvalidOperationException(
          $"Test class '{testClassType.Name}' requires an instance but has no parameterless constructor.");

      object? testClass = null;
      if (requiresInstance)
      {
        testClass = Activator.CreateInstance(testClassType, nonPublic: true)
          ?? throw new Exception($"Activated class is null: '{testClassType.Name}'");
      }

      // Just stop the test if this fails
      beforeAllMethod?.Invoke(GetInvocationTarget(testClass, beforeAllMethod), null);

      foreach (var testMethod in testMethods)
        methodResults.AddRange(RunTestMethodInvocations(
          testClass: testClass,
          testMethod: testMethod,
          beforeEachMethod: beforeEachMethod,
          afterEachMethod: afterEachMethod));

      try
      {
        afterAllMethod?.Invoke(GetInvocationTarget(testClass, afterAllMethod), null);
      }
      catch (Exception ex)
      {
        result = Result.Error;
        messages.AddRange(ConvertExceptionToStringArray(UnwrapException(ex)));
      }

      if (result != Result.Error)
      {
        if (methodResults.Any(m => m.Result == Result.Error))
        {
          result = Result.Error;
          messages.Add("At least one of the methods had an error");
        }
        else if (methodResults.Any(m => m.Result == Result.Fail))
        {
          result = Result.Fail;
          messages.Add("At least one of the methods has failed");
        }
      }
    }
    catch (Exception ex)
    {
      result = Result.Error;
      messages.AddRange(ConvertExceptionToStringArray(UnwrapException(ex)));
    }

    classStopwatch.Stop();
    var classResult = new TestClassResult(
        testClassType.Name,
        result,
        methodResults,
        classStopwatch.ElapsedMilliseconds,
        [.. messages]);
    endClassPrinter?.Invoke(classResult);
    return classResult;
  }

  public MethodResult[] RunTestMethodInvocations(
    object? testClass,
    MethodInfo testMethod,
    MethodInfo? beforeEachMethod = null,
    MethodInfo? afterEachMethod = null)
  {
    if (!testMethod.IsDefined(typeof(TestMethod), true))
      throw new ArgumentException($"Method '{testMethod.Name}' is not marked with '{nameof(TestMethod)}'.");
    ValidateTestMethodSignature(testMethod);

    var invocations = GetMethodInvocations(testMethod);
    List<MethodResult> results = [];

    foreach (var invocation in invocations)
      results.Add(RunSingleMethod(
        testClass: testClass,
        testMethod: testMethod,
        displayName: invocation.DisplayName,
        testMethodArguments: invocation.Arguments,
        beforeEachMethod: beforeEachMethod,
        afterEachMethod: afterEachMethod));

    return [.. results];
  }

  public MethodResult RunSingleMethod(
    object? testClass,
    MethodInfo testMethod,
    string? displayName = null,
    object?[]? testMethodArguments = null,
    MethodInfo? beforeEachMethod = null,
    MethodInfo? afterEachMethod = null)
  {
    if (!testMethod.IsDefined(typeof(TestMethod), true))
      throw new ArgumentException($"Method '{testMethod.Name}' is not marked with '{nameof(TestMethod)}'.");
    ValidateTestMethodSignature(testMethod);

    Result result = Result.Success;
    List<string> methodMessages = [];
    Stopwatch methodStopwatch = Stopwatch.StartNew();
    bool beforeEachFailed = false;

    try
    {
      beforeEachMethod?.Invoke(GetInvocationTarget(testClass, beforeEachMethod), null);
    }
    catch (Exception ex)
    {
      result = Result.Error;
      methodMessages.AddRange(ConvertExceptionToStringArray(UnwrapException(ex)));
      beforeEachFailed = true;
    }

    try
    {
      if (!beforeEachFailed)
        testMethod.Invoke(GetInvocationTarget(testClass, testMethod), testMethodArguments);
    }
    catch (Exception ex)
    {
      result = Result.Fail;
      methodMessages.AddRange(ConvertExceptionToStringArray(UnwrapException(ex)));
    }

    try
    {
      afterEachMethod?.Invoke(GetInvocationTarget(testClass, afterEachMethod), null);
    }
    catch (Exception ex)
    {
      result = Result.Error;
      methodMessages.AddRange(ConvertExceptionToStringArray(UnwrapException(ex)));
    }

    methodStopwatch.Stop();
    MethodResult methodResult = new(
      displayName ?? testMethod.Name, result, [.. methodMessages], methodStopwatch.ElapsedMilliseconds);
    methodResultPrinter?.Invoke(methodResult);
    return methodResult;
  }

  public TestClassResult RunSingleMethod(string className, string methodName)
  {
    var type = GetTestClassTypeWithName(className)
      ?? throw new ArgumentException($"There is no defined test class: {className}");
    var methodInfo = GetMethodWithName(type, methodName);
    return RunClass(type, [methodInfo]);
  }

  public static Type[] GetAllTestClassTypes()
  {
    var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
    List<Type> types = [];
    foreach (var assembly in allAssemblies)
    {
      if (assembly.IsDynamic) continue;

      Type[] assemblyTypes;
      try
      {
        assemblyTypes = assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException ex)
      {
        assemblyTypes = [.. ex.Types.Where(t => t is not null).Cast<Type>()];
      }

      types.AddRange(assemblyTypes.Where(t =>
        t.IsDefined(typeof(TestClass), true) &&
        t.IsClass &&
        !t.IsAbstract &&
        !t.ContainsGenericParameters));
    }
    return [.. types];
  }

  private static object? GetInvocationTarget(object? testClass, MethodInfo method)
    => method.IsStatic ? null : testClass;

  public static Type? GetTestClassTypeWithName(string name)
  {
    var matches = GetAllTestClassTypes()
      .Where(t => t.FullName == name || t.Name == name)
      .ToArray();

    return matches.Length switch
    {
      0 => null,
      1 => matches[0],
      _ => throw new ArgumentException($"Multiple test classes match '{name}'. Use full name.")
    };
  }

  private static MethodInfo? GetSingleMethodWithAttribute<T>(Type type) where T : Attribute
  {
    var methods = type.GetMethods(FLAGS)
      .OrderBy(m => m.MetadataToken)
      .Where(m => m.IsDefined(typeof(T), true))
      .ToArray();
    if (methods.Length > 1)
      throw new InvalidOperationException($"Type '{type.Name}' contains multiple methods with attribute '{typeof(T).Name}'.");
    if (methods.Length == 0)
      return null;

    ValidateMethodSignature(methods[0], typeof(T).Name);
    return methods[0];
  }

  private static MethodInfo GetMethodWithName(Type type, string methodName)
  {
    string className = type.Name;
    var methods = type.GetMethods(FLAGS)
      .Where(m => m.Name == methodName && m.IsDefined(typeof(TestMethod), true))
      .OrderBy(m => m.MetadataToken)
      .ToArray();

    if (methods.Length > 1)
      throw new ArgumentException($"Multiple test methods named '{methodName}' in class '{className}'.");
    if (methods.Length == 0)
      throw new ArgumentException($"There is no defined test method: '{methodName}', in class: '{className}'");

    ValidateTestMethodSignature(methods[0]);
    return methods[0];
  }

  private static MethodInfo[] GetMethodsWithAttribute<T>(Type type) where T : Attribute
  {
    var methods = type.GetMethods(FLAGS)
        .Where(m => m.IsDefined(typeof(T), true))
        .OrderBy(m => m.MetadataToken)
        .ToArray();
    foreach (var method in methods)
      if (typeof(T) == typeof(TestMethod))
        ValidateTestMethodSignature(method);
      else
        ValidateMethodSignature(method, typeof(T).Name);
    return methods;
  }

  private static string[] ConvertExceptionToStringArray(Exception ex)
    => ex.ToString().Split([Environment.NewLine], StringSplitOptions.None);

  private static (string DisplayName, object?[] Arguments)[] GetMethodInvocations(MethodInfo method)
  {
    var parameters = method.GetParameters();
    if (parameters.Length == 0)
      return [(method.Name, [])];

    var argumentsAttribute = method.GetCustomAttribute<TestMethodArguments>(true)
      ?? throw new InvalidOperationException(
        $"Method '{method.Name}' marked with '{nameof(TestMethod)}' must define '{nameof(TestMethodArguments)}' when using parameters.");
    if (argumentsAttribute.Values.Length == 0)
      throw new InvalidOperationException(
        $"Method '{method.Name}' marked with '{nameof(TestMethodArguments)}' must define at least one value.");

    var parameterType = parameters[0].ParameterType;
    List<(string DisplayName, object?[] Arguments)> invocations = [];

    foreach (var rawValue in argumentsAttribute.Values)
    {
      var convertedValue = ConvertArgumentValue(method.Name, parameterType, rawValue);
      invocations.Add(($"{method.Name}({FormatArgumentValue(convertedValue)})", [convertedValue]));
    }

    return [.. invocations];
  }

  private static object? ConvertArgumentValue(string methodName, Type parameterType, object? rawValue)
  {
    var targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

    if (rawValue is null)
    {
      if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null)
        throw new InvalidOperationException(
          $"Method '{methodName}' cannot receive null for parameter type '{parameterType.Name}'.");
      return null;
    }

    if (targetType.IsInstanceOfType(rawValue))
      return rawValue;

    if (targetType.IsEnum)
    {
      if (rawValue is string enumName)
        return Enum.Parse(targetType, enumName, ignoreCase: false);
      return Enum.ToObject(targetType, rawValue);
    }

    try
    {
      return Convert.ChangeType(rawValue, targetType);
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException(
        $"Method '{methodName}' cannot convert argument value '{rawValue}' to parameter type '{parameterType.Name}'.", ex);
    }
  }

  private static string FormatArgumentValue(object? value)
    => value switch
    {
      null => "null",
      string text => $"\"{text}\"",
      char character => $"'{character}'",
      _ => value.ToString() ?? "null"
    };

  private static void ValidateMethodSignature(MethodInfo method, string attributeName)
  {
    if (method.GetParameters().Length != 0)
      throw new InvalidOperationException($"Method '{method.Name}' marked with '{attributeName}' must have no parameters.");
    ValidateMethodSignatureCore(method, attributeName);
  }

  private static void ValidateTestMethodSignature(MethodInfo method)
  {
    var parameterCount = method.GetParameters().Length;
    var hasArgumentsAttribute = method.IsDefined(typeof(TestMethodArguments), true);

    if (parameterCount > 1)
      throw new InvalidOperationException(
        $"Method '{method.Name}' marked with '{nameof(TestMethod)}' can have at most one parameter.");
    if (parameterCount == 0 && hasArgumentsAttribute)
      throw new InvalidOperationException(
        $"Method '{method.Name}' marked with '{nameof(TestMethodArguments)}' must declare exactly one parameter.");

    ValidateMethodSignatureCore(method, nameof(TestMethod));
  }

  private static void ValidateMethodSignatureCore(MethodInfo method, string attributeName)
  {
    if (method.ReturnType != typeof(void))
      throw new InvalidOperationException($"Method '{method.Name}' marked with '{attributeName}' must return void.");
    if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
      throw new InvalidOperationException($"Method '{method.Name}' marked with '{attributeName}' must not be generic.");
    if (method.IsAbstract)
      throw new InvalidOperationException($"Method '{method.Name}' marked with '{attributeName}' must not be abstract.");
    if (method.IsSpecialName)
      throw new InvalidOperationException($"Method '{method.Name}' marked with '{attributeName}' must be a normal method.");
    if (method.IsDefined(typeof(AsyncStateMachineAttribute), false))
      throw new InvalidOperationException($"Method '{method.Name}' marked with '{attributeName}' must not be async void.");
  }

  private static Exception UnwrapException(Exception ex)
  {
    while (ex is TargetInvocationException && ex.InnerException is not null)
      ex = ex.InnerException;
    return ex;
  }
}
#pragma warning restore IL2067
#pragma warning restore IL2026
#pragma warning restore IL2070

public class ArgumentParser
{
  record ArgumentDefinition(
    string Argument, string AlternativeArgument, string Description);

  public Action<string> Writer = Console.WriteLine;
  private readonly Dictionary<string, string?> parsedArguments = [];
  private readonly Dictionary<string, ArgumentDefinition> allowedArguments = [];
  private readonly Dictionary<string, ArgumentDefinition> alternativeAllowedArguments = [];

  public void ShowHelp()
  {
    Writer("Usage:");
    foreach (var def in allowedArguments.Values)
    {
      var left = def.Argument;
      if (!string.IsNullOrEmpty(def.AlternativeArgument))
        left += ", " + def.AlternativeArgument;
      Writer($"  {left,-20}  {def.Description}");
    }
  }

  public void AddAllowedArgument(
    string argument,
    string alternativeArgument = "",
    string description = "")
  {
    ArgumentDefinition argumentDefinition = new(
      Argument: argument,
      AlternativeArgument: alternativeArgument,
      Description: description);

    allowedArguments[argument] = argumentDefinition;
    if (!string.IsNullOrWhiteSpace(alternativeArgument))
      alternativeAllowedArguments[alternativeArgument] = argumentDefinition;
  }

  public void ParseArguments(string[] args)
  {
    for (int i = 0; i < args.Length; i++)
    {
      var token = args[i];
      if (!(token.StartsWith("--") || token.StartsWith('-')))
        throw new ArgumentException($"Unexpected value without argument name: {token}");
      if (!allowedArguments.TryGetValue(token, out var def) &&
          !alternativeAllowedArguments.TryGetValue(token, out def))
        throw new ArgumentException($"Unknown argument name: {token}");

      var key = def.Argument;
      if (i + 1 < args.Length && !args[i + 1].StartsWith("--") && !args[i + 1].StartsWith('-'))
        parsedArguments[key] = args[++i];
      else
        parsedArguments[key] = null;
    }
  }

  public string? GetArgument(string argument, string altArgument = "")
  {
    if (parsedArguments.TryGetValue(argument, out var value))
      return value;
    if (!string.IsNullOrEmpty(altArgument) && parsedArguments.TryGetValue(altArgument, out value))
      return value;
    if (allowedArguments.TryGetValue(argument, out var def))
    {
      if (!string.IsNullOrEmpty(def.AlternativeArgument) &&
          parsedArguments.TryGetValue(def.AlternativeArgument, out value))
        return value;
    }
    return null;
  }

  public string[] GetParsedArguments()
    => [.. parsedArguments.Keys];
}

public class SimpleTestRunner
{
  private readonly ArgumentParser argumentParser = new();
  private readonly SimpleTestExecutor simpleTestExecutor;

  private readonly List<TestClassResult> classResults = [];
  public TestClassResult[] ClassResults => [.. classResults];

  public bool DidExecuteTests { get; private set; }

  public SimpleTestRunner(SimpleTestExecutor simpleTestExecutor, string[]? args = null)
  {
    this.simpleTestExecutor = simpleTestExecutor;
    if (args is null) return;

    argumentParser.AddAllowedArgument(
      argument: "--help",
      alternativeArgument: "-h",
      description: "Show help");
    argumentParser.AddAllowedArgument(
      argument: "--test-class",
      alternativeArgument: "-c",
      description: "Name of the class to test");
    argumentParser.AddAllowedArgument(
      argument: "--test-method",
      alternativeArgument: "-m",
      description: "Name of the method to test");
    argumentParser.ParseArguments(args);
  }

  public void Run()
  {
    classResults.Clear();
    DidExecuteTests = false;
    var testClassName = argumentParser.GetArgument("--test-class");
    var testMethodName = argumentParser.GetArgument("--test-method");

    if (argumentParser.GetArgument("--help") is not null || argumentParser.GetParsedArguments().Contains("--help"))
    {
      argumentParser.ShowHelp();
    }
    else if (argumentParser.GetParsedArguments().Length == 0)
    {
      DidExecuteTests = true;
      classResults.AddRange(simpleTestExecutor.RunAll());
    }
    else if (testClassName is not null && testMethodName is null)
    {
      DidExecuteTests = true;
      classResults.Add(simpleTestExecutor.RunClass(testClassName));
    }
    else if (testClassName is not null && testMethodName is not null)
    {
      DidExecuteTests = true;
      classResults.Add(simpleTestExecutor.RunSingleMethod(testClassName, testMethodName));
    }
    else
      throw new ArgumentException("Argument '--test-method' requires '--test-class'.");
  }
}

// Example test runner implementation
public partial class SimpleTestPrinter
{
  static class Ansi
  {
    public static readonly string NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";
    public static readonly string RED = Console.IsOutputRedirected ? "" : "\x1b[91m";
    public static readonly string GREEN = Console.IsOutputRedirected ? "" : "\x1b[92m";
    public static readonly string BLUE = Console.IsOutputRedirected ? "" : "\x1b[94m";
    public static readonly string GREY = Console.IsOutputRedirected ? "" : "\x1b[97m";
    public static readonly string YELLOW = Console.IsOutputRedirected ? "" : "\x1b[33m";
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

  private readonly SimpleTestExecutor simpleTestExecutor;
  private readonly Action<string> printFunction;
  private readonly bool parseException;


  public SimpleTestPrinter(Action<string> printFunction, bool parseException = true)
  {
    this.printFunction = printFunction;
    this.parseException = parseException;
    simpleTestExecutor = new(LogClassBegin, LogMethodResult, LogClassResult);
  }

  public bool Run(string[]? args = null)
  {
    SimpleTestRunner simpleTestRunner = new(simpleTestExecutor, args);
    Stopwatch stopwatch = Stopwatch.StartNew();
    simpleTestRunner.Run();
    stopwatch.Stop();
    if (!simpleTestRunner.DidExecuteTests)
      return true;
    return SummarizeResults(simpleTestRunner, stopwatch);
  }

  private bool SummarizeResults(SimpleTestRunner simpleTestRunner, Stopwatch stopwatch)
  {
    int classesPassed = 0, classesTotal = 0, methodsPassed = 0, methodsTotal = 0;
    bool hasFailures = false;
    foreach (var classResult in simpleTestRunner.ClassResults)
    {
      classesTotal++;
      if (classResult.Result == Result.Success) classesPassed++;
      if (IsFailingResult(classResult.Result)) hasFailures = true;
      foreach (var methodResult in classResult.MethodResults)
      {
        methodsTotal++;
        if (methodResult.Result == Result.Success) methodsPassed++;
        if (IsFailingResult(methodResult.Result)) hasFailures = true;
      }
    }

    string resultText = !hasFailures ? AddColorToString("PASS", Ansi.GREEN) : AddColorToString("FAIL", Ansi.RED);
    printFunction($"""
    ----------------------
    {resultText} Classes: {classesPassed}/{classesTotal} Methods: {methodsPassed}/{methodsTotal}
    Took {FormatTime(stopwatch.ElapsedMilliseconds)}
    ----------------------
    """);
    return !hasFailures;
  }

  private void LogClassBegin(Type type)
    => printFunction($"{AddColorToString(PREFIX_RUN, Ansi.BLUE)} {type.Name}");

  private void LogClassResult(TestClassResult classResult)
  {
    if (classResult.Result == Result.Success)
      printFunction($"{AddColorToString(PREFIX_OK, Ansi.GREEN)} {classResult.Name} {AddColorToString(FormatTime(classResult.TookMilliseconds), Ansi.GREY)}");
    if (classResult.Result == Result.Fail || classResult.Result == Result.Error)
    {
      printFunction($"{AddColorToString(PREFIX_ERROR, Ansi.RED)} {classResult.Name} {AddColorToString(FormatTime(classResult.TookMilliseconds), Ansi.GREY)}");
      printFunction(string.Join('\n', classResult.Messages));
    }
  }

  private void LogMethodResult(MethodResult methodResult)
  {
    if (methodResult.Result == Result.Success)
      printFunction($"-> {AddColorToString(PREFIX_OK, Ansi.GREEN)} {methodResult.Name}");
    if (methodResult.Result == Result.Fail || methodResult.Result == Result.Error)
    {
      printFunction($"-> {AddColorToString(PREFIX_ERROR, Ansi.RED)} {methodResult.Name}");

      string exceptionMessage = "<empty message>";
      if (methodResult.Messages.Length > 0)
      {
        exceptionMessage = parseException ?
        ParseException(string.Join('\n', methodResult.Messages.Skip(1))) :
        string.Join('\n', methodResult.Messages);
        exceptionMessage = methodResult.Messages[0] + '\n' + exceptionMessage;
      }

      printFunction(exceptionMessage);
    }
  }

  private static bool IsFailingResult(Result result)
    => result == Result.Fail || result == Result.Error;

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
    => Delete();
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
    var expected = shouldBe.ToArray();
    var actual = isNow.ToArray();
    if (!expected.SequenceEqual(actual))
    {
      throw new ValidationException(
        $"Value is not equal, is: '[{string.Join(", ", actual)}]', but should be: '[{string.Join(", ", expected)}]'. {additionalMessage}");
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
    using CancellationTokenSource cancellationTokenSource = new();
    var actionTask = Task.Run(() =>
    {
      while (!cancellationTokenSource.Token.IsCancellationRequested)
      {
        try { action(); break; }
        catch (Exception ex) { trackedException = ex; }
        Thread.Sleep(millisecondsTimeout: 10);
      }
      cancellationTokenSource.Token.ThrowIfCancellationRequested();
    }, cancellationTokenSource.Token);

    var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(timeoutMs));
    if (Task.WhenAny(actionTask, timeoutTask).Result == timeoutTask)
    {
      cancellationTokenSource.Cancel();
      throw new TimeoutException(
        $"Assertion was not passed in time: {timeoutMs}ms. Reason: {trackedException.Message}\n" +
        $"{trackedException.StackTrace}");
    }

    if (actionTask.IsCanceled)
      return;

    if (actionTask.IsFaulted && actionTask.Exception != null)
      throw actionTask.Exception;
  }

  public static void AssertThrows<T>(
    Action action, string additionalMessage = "") where T : Exception
  {
    try { action(); }
    catch (T) { return; }
    catch (Exception ex)
    {
      throw new ValidationException(
        $"Expected exception of type '{typeof(T)}', but got '{ex.GetType()}' instead. {additionalMessage}");
    }

    throw new ValidationException($"Expected exception of type '{typeof(T)}' was not thrown. {additionalMessage}");
  }
}
