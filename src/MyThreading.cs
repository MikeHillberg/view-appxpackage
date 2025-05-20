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

        static internal bool HasShutdownStarted = false;

        static internal void SetCurrentAsUIThread()
        {
            _uiThread = Thread.CurrentThread;
            _uiThreadDispatcherQueue = DispatcherQueue.GetForCurrentThread();

            Debug.WriteLine($"UI Thread: {Thread.CurrentThread.ManagedThreadId}");

            _uiThreadDispatcherQueue.ShutdownStarting += (s, e) =>
            {
                HasShutdownStarted = true;
            };
        }

        /// <summary>
        /// Run an async action on the worker thread (complete when it's done)
        /// </summary>
        async internal static Task RunOnWorkerAsync(Func<Task> asyncAction)
        {
            if (CurrentIsWorkerThread)
            {
                await asyncAction();
                return;
            }

            var semaphore = new SemaphoreSlim(0, 1);
            PostToWorker(async () =>
            {
                Debug.Assert(_workerThread == null || CurrentIsWorkerThread);

                await asyncAction();
                semaphore.Release();
            });

            await semaphore.WaitAsync();
        }

        /// <summary>
        /// Run a sync action on the worker thread (completes when the action is done)
        /// </summary>
        internal static async Task RunOnWorkerAsync(Action syncAction)
        {
            if (CurrentIsWorkerThread)
            {
                syncAction();
                return;
            }

            // It would be prettier to wrap this sync in an async lambda,
            // then call the async version of RunOnWorkerAsync.
            // That causes an AsyncMethodBuilder to get used, which messes with the
            // thread's SynchronizationContext, which was causing issues

            var semaphore = new SemaphoreSlim(0, 1);
            PostToWorker(() =>
            {
                Debug.Assert(_workerThread == null || CurrentIsWorkerThread);

                syncAction();
                semaphore.Release();
            });

            await semaphore.WaitAsync();
        }

        /// <summary>
        /// Queue a sync Action to the worker thread but don't wait for it to run
        /// <param name="syncAction"></param>
        internal static void PostToWorker(
            Action syncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            PostToDispatcher(
                _workerThreadDispatcher,
                () =>
                {
                    Debug.Assert(!_workerIsSet || CurrentIsWorkerThread);
                    syncAction();
                });
        }

        /// <summary>
        /// Queue an asyncAction to the worker thread but don't wait for it to run
        /// </summary>
        internal static void PostToWorker(
            Func<Task> asyncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            PostToDispatcher(
                _workerThreadDispatcher,
                () =>
                {
                    Debug.Assert(!_workerIsSet || CurrentIsWorkerThread);
                    _ = asyncAction();
                });
        }

        /// <summary>
        /// Queue a sync to a given DQ, but don't wait for it to run
        /// </summary>
        static void PostToDispatcher(
            DispatcherQueue dispatcherQueue,
            Action action,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            dispatcherQueue.TryEnqueue(() => action());
        }

        /// <summary>
        /// Queue an async action to a given DQ, but don't wait for it to run
        /// </summary>
        static void PostToDispatcher(
            DispatcherQueue dispatcherQueue,
            Func<Task> asyncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            dispatcherQueue.TryEnqueue(() => asyncAction());
        }

        /// <summary>
        /// Set up the worker thread. Everything runs on either the UI thread or the worker thread
        /// </summary>
        /// <returns></returns>
        internal static async Task EnsureWorkerThreadAsync()
        {
            if (_workerThreadDispatcher != null)
            {
                return;
            }

            // Create the worker thread (STA)
            var controller = DispatcherQueueController.CreateOnDedicatedThread();
            _workerThreadDispatcher = controller.DispatcherQueue;

            // Move over to the new worker thread and set it up
            await RunOnWorkerAsync(() =>
            {
                Debug.Assert(_workerThread == null);
                Debug.Assert(_workerThreadDispatcher == DispatcherQueue.GetForCurrentThread());

                _workerThread = Thread.CurrentThread;
                _workerIsSet = true;
                Debug.WriteLine($"Worker thread: {Thread.CurrentThread.ManagedThreadId}");

                // Run this thread in a DQ pump
                DispatcherQueueSynchronizationContext syncContext = new(_workerThreadDispatcher);
                SynchronizationContext.SetSynchronizationContext(syncContext);
            });
        }

        static bool _workerIsSet = false;

        /// <summary>
        /// Queue to the UI thread
        internal static void PostToUI(
            Action action,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            // DispatcherQueue can be null during shutdown
            if (_uiThreadDispatcherQueue == null)
            {
                return;
            }
            PostToDispatcher(_uiThreadDispatcherQueue, action);
        }

        internal static void PostToUI(
            Func<Task> asyncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            // DispatcherQueue can be null during shutdown
            if (_uiThreadDispatcherQueue == null)
            {
                return;
            }
            PostToDispatcher(_uiThreadDispatcherQueue, asyncAction);
        }

        /// <summary>
        /// Queue a sync action to the UI thread and (async) wait for it to complete
        /// </summary>
        internal static async Task RunOnUIAsync(
            Action syncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            var semaphore = new SemaphoreSlim(0, 1);
            PostToUI(() =>
            {
                syncAction();
                semaphore.Release();
            },
            priority);

            await semaphore.WaitAsync();
        }

        /// <summary>
        /// Queue an async action to the UI thread and (async) wait for it to complete
        /// </summary>
        internal static async Task RunOnUIAsync(
            Func<Task> asyncAction,
            DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            var semaphore = new SemaphoreSlim(0, 1);
            PostToUI(async () =>
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
