using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ViewAppxPackage
{
    public class MyThreading
    {
        static Thread _uiThread;
        static DispatcherQueue _uiThreadDispatcherQueue;

        static Thread _workerThread;
        static DispatcherQueue _workerThreadDispatcher;

        static internal bool CurrentIsUiThread => Thread.CurrentThread == _uiThread;
        static internal bool CurrentIsWorkerThread => Thread.CurrentThread == _workerThread;

        static internal void SetCurrentAsUIThread()
        {
            _uiThread = Thread.CurrentThread;
            _uiThreadDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        static internal void SetWorkerThread(Thread thread)
        {
            _workerThread = thread;
        }

        /// <summary>
        /// Run a sync action on the worker thread (complete when it's done)
        /// </summary>
        async internal static Task RunOnWorkerAsync(Action syncAction)
        {
            await RunOnWorkerAsync(() =>
            {
                syncAction();
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Run an async action on the worker thread (complete when it's done)
        /// </summary>
        internal static async Task RunOnWorkerAsync(Func<Task> asyncAction)
        {
            if (MyThreading.CurrentIsWorkerThread)
            {
                await asyncAction();
                return;
            }

            var semaphore = new SemaphoreSlim(0, 1);
            PostToWorker(async () =>
            {
                await asyncAction();
                semaphore.Release();
            });

            await semaphore.WaitAsync();
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

        internal static async Task CreateWorkerThreadAsync()
        {
            // Create the worker thread (STA)
            var controller = DispatcherQueueController.CreateOnDedicatedThread();
            _workerThreadDispatcher = controller.DispatcherQueue;

            await RunOnWorkerAsync(() =>
            {
                Debug.Assert(_workerThread == null);
                _workerThread = Thread.CurrentThread;
            });
        }

        /// <summary>
        /// Queue to the UI thread
        internal static void PostToUI(
            DispatcherQueueHandler action,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            // DispatcherQueue can be null during shutdown
            _uiThreadDispatcherQueue?.TryEnqueue(priority, action);
        }

        /// <summary>
        /// Queue a sync action to the UI thread and (async) wait for it to complete
        /// </summary>
        internal static async Task RunOnUIAsync(
            Action syncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            await RunOnUIAsync(() =>
            {
                syncAction();
                return Task.CompletedTask;
            },
            priority);
        }

        /// <summary>
        /// Queue an async action to the UI thread and (async) wait for it to complete
        /// </summary>
        internal static async Task RunOnUIAsync(
            Func<Task> asyncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            // bugbug: need to handle async actions
            var semaphore = new SemaphoreSlim(0, 1);
            MyThreading.PostToUI(async () =>
            {
                await asyncAction();
                semaphore.Release();
            },
            priority);

            await semaphore.WaitAsync();
        }


        internal static void ShutdownWorkerThread()
        {
            _workerThreadDispatcher?.EnqueueEventLoopExit();
        }
    }
}
