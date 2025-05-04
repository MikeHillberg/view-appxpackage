using ViewAppxPackage;

namespace TestProject1;

public class WorkerTestMethodAttribute : TestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        TestResult[] result = null!;

        MyThreading.RunOnWorkerAsync(() =>
        {
            result = base.Execute(testMethod);
        }).Wait();

        return result!;
    }
}


