﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Machine.Specifications.Explorers;
using Machine.Specifications.Model;
using TestDriven.Framework;

namespace Machine.Specifications.TDNetRunner
{
  public class SpecificationRunner : ITestRunner
  {
    private AssemblyExplorer explorer;
    private ResultFormatterFactory resultFormatterFactory;
    public SpecificationRunner()
    {
      explorer = new AssemblyExplorer();
      resultFormatterFactory = new ResultFormatterFactory();
    }

    public TestRunState RunAssembly(ITestListener testListener, Assembly assembly)
    {
      var descriptions = explorer.FindDescriptionsIn(assembly);

      return RunDescriptions(descriptions, testListener);
    }

    private TestRunState RunDescriptions(IEnumerable<Description> descriptions, ITestListener testListener)
    {
      if (descriptions.Count() == 0) return TestRunState.NoTests;

      var testResults = new List<TestResult>();

      foreach (var description in descriptions)
      {
        if (description.Specifications.Count() == 0) continue;
        testListener.WriteLine(description.Name, Category.Output);
        description.RunContextBeforeAll();
        string lastWhenPrinted = "";

        foreach (var specification in description.Specifications)
        {
          if (specification.HasWhenClause && specification.WhenClause != lastWhenPrinted)
          {
            lastWhenPrinted = specification.WhenClause;
            testListener.WriteLine(String.Format("\n  When {0}", specification.WhenClause), Category.Output);
          }

          TestResult testResult = GetTestResult(testListener, description, specification);
          testResults.Add(testResult);
        }

        description.RunContextAfterAll();
        testListener.WriteLine("", Category.Output);
      }

      if (testResults.Count == 0)
      {
        return TestRunState.NoTests;
      }

      bool failure = false;

      foreach (var testResult in testResults)
      {
        testListener.TestFinished(testResult);
        failure |= testResult.State == TestState.Failed;
      }

      return failure ? TestRunState.Failure : TestRunState.Success;
    }

    private TestResult GetTestResult(ITestListener testListener, Description description, Specification specification)
    {
      var result = description.VerifySpecification(specification);
      var formatter = resultFormatterFactory.GetResultFormatterFor(result);

      testListener.WriteLine(formatter.FormatResult(specification), Category.Output);

      TestResult testResult = new TestResult();
      testResult.Name = specification.Name;
      if (result.Passed)
      {
        testResult.State = TestState.Passed;
      }
      else
      {
        testResult.State = TestState.Failed;
        if (result.Exception != null)
        {
          testResult.StackTrace = result.Exception.ToString();
        }
        //testListener.WriteLine(result.Exception.ToString().Split(new [] {'\r', '\n'}).Last(), Category.Output);
      }
      return testResult;
    }

    public TestRunState RunNamespace(ITestListener testListener, Assembly assembly, string targetNamespace)
    {
      var descriptions = explorer.FindDescriptionsIn(assembly, targetNamespace);

      return RunDescriptions(descriptions, testListener);
    }

    public TestRunState RunMember(ITestListener testListener, Assembly assembly, MemberInfo member)
    {
      if (member.MemberType == MemberTypes.TypeInfo)
      {
        Type type = (Type)member;
        var description = explorer.FindDescription(type);

        if (description == null)
        {
          return TestRunState.NoTests;
        }

        return RunDescriptions(new[] {description}, testListener);
      }
      else if (member.MemberType == MemberTypes.Field)
      {
        FieldInfo fieldInfo = (FieldInfo)member;
        var description = explorer.FindDescription(fieldInfo);

        return RunDescriptions(new[] {description}, testListener);
      }
      else
      {
        return TestRunState.NoTests;
      }
    }
  }
}