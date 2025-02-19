﻿using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;

namespace ViewAppxPackage
{
    /// <summary>
    /// Enumerator-like wrapper for reading an event log. You can peek at the next record's time, or pop the record
    /// </summary>
    internal class EventLogEnumerator : IDisposable
    {
        EventLogReader _reader;
        EventLogWatcher _watcher;
        EventLogQuery _readerQuery;

        internal EventLogEnumerator(string logName)
        {
            _readerQuery = new(logName, PathType.LogName)
            {
                ReverseDirection = true,
                TolerateQueryErrors = true
            };
            _reader = new(_readerQuery);

            // Watch for changes to the log
            // Need a new query because it rejects ReverseDirection
            EventLogQuery watcherQuery = new(logName, PathType.LogName)
            {
                TolerateQueryErrors = true
            };

            _watcher = new(watcherQuery);
            _watcher.EventRecordWritten += (s, e) => Changed?.Invoke(this, null);
            _watcher.Enabled = true;
        }

        internal void Reset()
        {
            _next?.Dispose();
            _next = null;

            _reader?.Dispose();
            _reader = new(_readerQuery);
        }

        internal event EventHandler Changed;

        // The next record on the log
        EventRecord _next = null;

        void EnsureNext()
        {
            if (_next != null)
            {
                // Already have the next record
                return;
            }

            if (_reader == null)
            {
                // We've read the whole log
                return;
            }

            // Get the next event from the log
            _next = _reader.ReadEvent();
            if (_next == null)
            {
                // No more events
                _reader = null;
            }
        }

        /// <summary>
        /// Get the TimeCreated of the next event in the log
        /// </summary>
        internal DateTime PeekTime()
        {
            EnsureNext();
            var nextTime = _next?.TimeCreated.Value;

            // If there is no next event, then set min value so that it will never
            // be considered the next event
            if (nextTime == null)
            {
                nextTime = DateTime.MinValue;
            }

            return nextTime.Value;
        }

        /// <summary>
        /// Get the next event in the log
        /// </summary>
        /// <returns></returns>
        internal EventRecord Pop()
        {
            EnsureNext();

            // Return _next and clear it
            var next = _next;
            _next = null;
            return next;
        }

        public void Dispose()
        {
            _next?.Dispose();
            _next = null;

            _reader?.Dispose();
            _reader = null;

            _watcher?.Dispose();
            _watcher = null;
        }
    }
}
