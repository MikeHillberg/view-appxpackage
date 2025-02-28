using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ViewAppxPackage
{
    public class MyThreading
    {
        static Thread _uiThread;

        static Thread _workerThread;
        static DispatcherQueue _workerThreadDispatcher;

        static internal bool CurrentIsUiThread => Thread.CurrentThread == _uiThread;
        static internal bool CurrentIsWorkerThread => Thread.CurrentThread == _workerThread;

        static internal void SetUIThread(Thread thread)
        {
            _uiThread = thread;
        }

        static internal void SetWorkerThread(Thread thread)
        {
            _workerThread = thread;
        }

        /// <summary>
        /// Post to worker thread, or run sync if already there
        /// </summary>
        async internal static Task RunOnWorkerAsync(Action action)
        {
            if (MyThreading.CurrentIsWorkerThread)
            {
                action();
                return;
            }

            var semaphore = new SemaphoreSlim(0, 1);
            PostToWorker(() =>
            {
                action();
                semaphore.Release();
            });

            await Task.Run(() => semaphore.WaitAsync());
        }

        /// <summary>
        /// Queue to the worker thread
        /// </summary>
        /// <param name="action"></param>
        internal static void PostToWorker(
            Action action,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            _workerThreadDispatcher.TryEnqueue(() => action());
        }

        internal static void CreateWorkerThread()
        {
            // Create the worker thread (STA)
            var controller = Microsoft.UI.Dispatching.DispatcherQueueController.CreateOnDedicatedThread();
            _workerThreadDispatcher = controller.DispatcherQueue;
        }

        /// <summary>
        /// Queue to the UI thread
        internal static void RunOnUI(
            DispatcherQueueHandler action,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            // DispatcherQueue can be null during shutdown
            _ = MainWindow.Instance.DispatcherQueue?.TryEnqueue(priority, action);
        }

    }
}
