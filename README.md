# SimpleTest

SimpleTest is a lightweight, one-file-only test library for C#. It allows you to define and execute test classes with minimal setup.

## Features

- Lightweight and easy to integrate
- Minimal overhead with a single file
- Zero dependencies

## Installation

You can include SimpleTest into your project in two ways:

1. **Add as a Project Reference:**

   - Download or clone the SimpleTest repository.
   - Add a reference to `SimpleTest/SimpleTest.csproj` in your main project.

2. **Include the Source File:**

   - Copy the `SimpleTest/SimpleTest.cs` file into your project directory.

## Usage

To use SimpleTest, follow these steps:

1. **Define Test Classes and Methods:**

   - Create your test classes and annotate test methods. For example:

   ```csharp
   using Qwaitumin.SimpleTest;

   [SimpleTestClass]
   public class ExampleTest
   {
    [SimpleTest]
    public void TestMethod()
    {
      // Your test code here
    }
   }
   ```

2. **Run Your Tests:**

   - Instantiate and run your test classes. For example:

   ```csharp
   class Program
   {
      static void Main(string[] args)
      {
        if (!new SimpleTestPrinter(Console.WriteLine).Run())
          Environment.Exit(1);
      }
   }
   ```


You can specify which class and method to run (by default it runs everything). For example:
- `dotnet run` - will run all test classes
- `dotnet run --test-class ExampleTest` - will run only `ExampleTest` class
- `dotnet run --test-class ExampleTest --test-method TestMethod` - will run only `TestMethod` method in `ExampleTest` class
