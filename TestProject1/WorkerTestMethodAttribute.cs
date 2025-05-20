using Microsoft.UI.Dispatching;
using ViewAppxPackage;

namespace TestProject1;

public class WorkerTestMethodAttribute : TestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        TestResult[] result = null!;

        Semaphore sem = new(0, 1);
        _ = MyThreading.RunOnWorkerAsync(() =>
        {
            result = base.Execute(testMethod);
            sem.Release();
        });
        sem.WaitOne();

        return result!;
    }
}
