using ViewAppxPackage;

namespace TestProject1;

public class UITestMethodAttribute : TestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        TestResult[] result = null!;

        MyThreading.RunOnUIAsync(() =>
        {
            result = base.Execute(testMethod);
        }).Wait();

        return result!;
    }
}


