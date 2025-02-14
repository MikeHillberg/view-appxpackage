using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

namespace ViewAppxPackage
{
    /// <summary>
    /// Wrap a T object and just return its value, 
    /// but assert that the current thread is the worker thread.
    /// </summary>
    internal struct WorkerThreadChecker<T>
    {
        T _value;

        internal WorkerThreadChecker(T value)
        {
            _value = value;
        }

        internal T Value
        {
            get
            {
                Debug.Assert(MainWindow.CurrentIsWorkerThread);
                return _value;
            }
            set
            {
                Debug.Assert(MainWindow.CurrentIsWorkerThread);
                _value = value;
            }
        }

        /// <summary>
        /// Return the value without the thread check assert,
        /// for the case where you want to read from the wrong thread and you know what you're doing
        /// </summary>
        internal T ValueNoThreadCheck => _value;
    }
}
