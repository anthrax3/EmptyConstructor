﻿using System.Collections.Generic;
using System.Reflection;
using Fody;
using Xunit;
#pragma warning disable 618

public class WithIncludesTests
{
    static Assembly assembly;
    static TestResult testResult;

    static WithIncludesTests()
    {
        var weavingTask = new ModuleWeaver
        {
            IncludeNamespaces = new List<string>
            {
                "MyNameSpace"
            },
        };
        testResult = weavingTask.ExecuteTestRun("AssemblyToProcess.dll",
            assemblyName: nameof(WithIncludesTests));
        assembly = testResult.Assembly;
    }

    [Fact]
    public void ClassInheritWithNonEmptyConstructor()
    {
        var type = assembly.GetType("ClassInheritWithNonEmptyConstructor", true);
        Assert.Single(type.GetConstructors());
    }

    [Fact]
    public void ClassInheritWithNonEmptyConstructorInNamespace()
    {
        testResult.GetInstance("MyNameSpace.ClassWithNoEmptyConstructorInNamespace");
    }
}