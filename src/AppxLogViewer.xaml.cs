using Microsoft.UI.Xaml.Documents;
using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppxLogViewer : Window, INotifyPropertyChanged
    {
        EventLogEnumerator _packagingEvents = null;
        EventLogEnumerator _deploymentEvents = null;
        CancellationTokenSource _highlightingCTS = null;

        // EventRecords are heavy (have a Dispose), so just copy out what we need
        struct MyEventRecord
        {
            internal string Header;
            internal Guid? ActivityId;
            internal string Description;
            internal string LevelDisplayName;
        }
        List<MyEventRecord> _eventRecords = null;

        /// <summary>
        /// Window that displays the event log content (like get-appxlog)
        /// </summary>
        public AppxLogViewer()
        {
            this.InitializeComponent();

            MainWindow.SetWindowIcon(this);

            this.Closed += (s, e) =>
            {
                _packagingEvents?.Dispose();
                _deploymentEvents?.Dispose();
                _newEventRefreshDelayTimer?.Stop();
            };

            // If the main window is shutting down, close this window too
            DispatcherQueue.ShutdownStarting += (s, e) =>
            {
                Close();
            };

            // WIP:
            //_rtb.AddHandler(
            //    UIElement.PointerReleasedEvent, 
            //    new PointerEventHandler(RtbPointerReleased), 
            //    true);
        }

        /// <summary>
        /// Create and activate the window to show the event log
        /// </summary>
        static public Window Show()
        {
            var window = new AppxLogViewer();
            window.ShowImpl();
            return window;
        }

        /// <summary>
        /// Instance helper for Show()
        /// </summary>
        void ShowImpl()
        {
            // Open the two event logs and register for change updates
            _packagingEvents = new("Microsoft-Windows-AppxPackaging/Operational");
            _packagingEvents.Changed += (_, _) => OnNewEvents();

            _deploymentEvents = new("Microsoft-Windows-AppXDeploymentServer/Operational");
            _deploymentEvents.Changed += (_, _) => OnNewEvents();

            ReadLogAndUpdateView();
            Activate();
        }

        /// <summary>
        /// Copy Text property to the UI
        /// </summary>
        void UpdateView()
        {
            _rtb.Blocks.Clear();
            IsEmpty = true;

            // Create a paragraph for each event record
            foreach (var record in _eventRecords)
            {
                IsEmpty = false;

                Paragraph p = new();
                p.Margin = new Thickness(0, 0, 0, 10);

                // Add the record.Header in bold as the first line
                Run header = new();
                header.Text = record.Header;
                header.FontWeight = FontWeights.Bold;
                p.Inlines.Add(header);
                p.Inlines.Add(new LineBreak());

                // Add the description as the second line
                // (or lines, since the description can have embedded line breaks)
                p.Inlines.Add(new Run() { Text = record.Description });
                _rtb.Blocks.Add(p);
            }
        }

        /// <summary>
        /// True if there are no records (all filtered out)
        /// </summary>
        bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                _isEmpty = value;
                RaisePropertyChanged();
            }
        }
        bool _isEmpty = true;

        /// <summary>
        /// Read from the event log and populate the view RichTextBlock
        /// </summary>
        void ReadLogAndUpdateView()
        {
            // If called from off thread (in an event notification)
            // forward to the UI thread
            if (!MyThreading.CurrentIsUiThread)
            {
                MyThreading.PostToUI(() => ReadLogAndUpdateView());
                return;
            }

            // Clear any existing highlighting and stop any in-progress highlighting
            CancelHighlighting();
            _rtb.TextHighlighters.Clear();

            _eventRecords = new();
            OldestRecord = null;

            // Get the most recent entries from the event logs
            // Note that this won't be 50/50 from the two logs, as we're just getting the most recent,
            // and they're not evenly split between the two
            for (int i = 0; i < 500; i++) // bugbug: add a "More" button rather than a random limit
            {
                // Get the next event, which could be from either log
                using var record = GetNextEvent();
                if (record == null)
                {
                    // Both logs have been fully read
                    break;
                }

                // Ignore if filter out by level (higher numbers are more verbose)
                if (record.Level > _logLevel)
                {
                    continue;
                }

                var timeCreated = record.TimeCreated?.ToString("MM/dd/yyyy HH:mm:ss");

                // Keep track of the oldest record we retrieve, to be used in a message
                OldestRecord = Utils.FormatDate((DateTimeOffset)record.TimeCreated!);

                var activityId = record.ActivityId.ToString();
                if (!string.IsNullOrEmpty(activityId))
                {
                    activityId = $", ActivityID = {activityId}";
                }

                // Save what we need from the event record, which is going to get disposed
                MyEventRecord myRecord = new()
                {
                    ActivityId = record.ActivityId,
                    Description = record.FormatDescription(),
                    Header = $"    {timeCreated}, {record.LevelDisplayName}{activityId}",
                    LevelDisplayName = record.LevelDisplayName,
                };

                _eventRecords.Add(myRecord);
            }

            // These properties are calculated by the above loop
            RaisePropertyChanged(nameof(RecordCount));
            RaisePropertyChanged(nameof(OldestRecord));

            // Populate the RichTextBlock
            UpdateView();
        }

        /// <summary>
        /// Get the next record from one of the two logs, whichever has the most recent entry
        /// </summary>
        EventRecord GetNextEvent()
        {
            var packagingTime = _packagingEvents.PeekTime();
            var deploymentTime = _deploymentEvents.PeekTime();

            if (packagingTime > deploymentTime)
            {
                return _packagingEvents.Pop();
            }
            else
            {
                return _deploymentEvents.Pop();
            }
        }

        /// <summary>
        /// When there's selected text, highlight any matches
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DocumentSelectionChanged(object sender, RoutedEventArgs e)
        {
            var selectedText = _rtb.SelectedText;
            var start = _rtb.SelectionStart.Offset;
            var pos = _rtb.Blocks[2].ContentStart.Offset;
            Debug.WriteLine($"Selection: '{selectedText}', {start}, {pos}");

            _rtb.TextHighlighters.Clear();

            if (string.IsNullOrEmpty(selectedText))
            {
                // The selection has been cleared
                _isSelectionActive = false;

                // Stop any in-process highlighting
                CancelHighlighting();

                // If some new events came in while the selection was active,
                // reload from the event log
                if (_newEventsPending)
                {
                    _newEventsPending = false;
                    Reload(null, null);
                }
            }
            else
            {
                // There's a selection going on
                _isSelectionActive = true;

                // If the selection isn't trivially short, start highlighting matches
                if (selectedText.Length > 3)
                {
                    _highlightingCTS = new();
                    HighlightAllMatchesAsync(selectedText, _highlightingCTS.Token);
                }
            }
        }


        void CancelHighlighting()
        {
            _highlightingCTS?.Cancel();
            _highlightingCTS = null;

        }

        /// <summary>
        /// Highlight in the RichTextBlock all matches of the input search string
        /// </summary>
        void HighlightAllMatchesAsync(
            string search, CancellationToken
            cancellationToken,
            int index = 0,
            int matchCount = 0)
        {
            if (index == 0)
            {
                // Root call into this method (not a recursive call)
                DebugLog.Append($"Highlighting {search}");
            }

            // Loop through all the event records looking for matches.
            // We could loop through the paragraphs instead, but this is faster
            // This is recursive, running for a while then posting back a continuation.
            // The index parameter is for the continuation.
            // bugbug: start highlighting from the viewport rather than from the beginning
            for (int i = index; i < _eventRecords.Count; i++)
            {
                // See if we should abort (because a new selection started)
                if (cancellationToken.IsCancellationRequested)
                {
                    DebugLog.Append($"Highlighting of '{search}' canceled");
                    return;
                }

                // Highlight the text if there's a match.
                // The negative offset is calcuated by trial/error
                var record = _eventRecords[i];
                matchCount += HighlightInline(record.Header, search, i, 0, -2);
                matchCount += HighlightInline(record.Description, search, i, 2, -5);

                // Periodically yield the UI thread by posting at low priority to continue
                if (i % 100 == 0)
                {
                    MyThreading.PostToUI(
                        () => HighlightAllMatchesAsync(search, cancellationToken, i + 1, matchCount),
                        Microsoft.UI.Dispatching.DispatcherQueuePriority.Low);
                    return;
                }
            }

            // We only get here on the call that completes, not each iteration of the multi-step process
            DebugLog.Append($"Highlighted {matchCount} matches");
        }

        /// <summary>
        /// Highlight an Inline if it has a match for the search string
        /// </summary>
        private int HighlightInline(
            string inlineText, // What's in a Run.Text
            string searchText,
            int paragraphIndex,
            int inlineIndex,
            int offset)
        {
            int matchCount = 0;

            // Abort if there's nothing to do
            if (!inlineText.Contains(searchText))
            {
                return matchCount;
            }
            matchCount = 1;

            var p = _rtb.Blocks[paragraphIndex] as Paragraph;
            var run = p.Inlines[inlineIndex] as Run;

            // bugbug: could be multiple matches per line
            var index = inlineText.IndexOf(searchText);

            // Index is the offset into the Run.Text of the match
            // We need to convert that value into a TextRange
            // We can get the TextPointer offset of the start of the run,
            // but TextPointer offsets and TextHighlighter offsets are in two different
            // number spaces unfortunately. (TextPointer likely takes into account zero-width
            // control characters and TextHighlighter doesn't.)

            // By trial and error I've figured out a conversion that works for this specific use case
            // Take the start of the Run, add the index, subtract a little for each preceding paragraph,
            // and subtract a little more (depends on if this is the first or second run in the paragraph)
            index += run.ContentStart.Offset;
            index -= paragraphIndex * 7;
            index += offset;

            // Now create the highlighter and add it to the RTB
            TextHighlighter highlighter = new()
            {
                Ranges =
                {
                    new TextRange()
                    {
                        StartIndex = index,
                        Length = searchText.Length
                    }
                }
            };

            _rtb.TextHighlighters.Add(highlighter);
            return matchCount;
        }

        // This is true when the event log has updated but we didn't fetch them because
        // there's an active selection
        bool _newEventsPending = false;

        bool _isSelectionActive = false;

        /// <summary>
        /// Called when the event log has been updated, updates the UI
        /// </summary>
        void OnNewEvents()
        {
            // Forward to the UI thread if necessary
            if (!MyThreading.CurrentIsUiThread)
            {
                MyThreading.PostToUI(() => OnNewEvents());
                return;
            }

            // Don't do anything if the view has been paused
            // (you can still click Refresh though)
            if (!IsPlaying)
            {
                return;
            }

            // Also don't do anything if there's a selection going on
            // Keep track that we have something though for when the selection ends
            if (_isSelectionActive)
            {
                _newEventsPending = true;
                return;
            }

            // To weather a storm of new events, only update the UI every 10 seconds
            if (_newEventRefreshDelayTimer?.IsEnabled == true)
            {
                // Don't update the UI until the timer goes off
                return;
            }

            // Create a timer if we don't already have one, and hook up the event handler
            if (_newEventRefreshDelayTimer == null)
            {
                _newEventRefreshDelayTimer = new DispatcherTimer();
                _newEventRefreshDelayTimer.Interval = TimeSpan.FromSeconds(10);
                _newEventRefreshDelayTimer.Tick += (_, _) =>
                {
                    _newEventRefreshDelayTimer.Stop();
                    if (!_isPlaying)
                    {
                        // The user paused the view
                        return;
                    }

                    Reload(null, null);
                };
            }

            // Timer's created but not yet running, so Start
            _newEventRefreshDelayTimer.Start();
        }
        DispatcherTimer _newEventRefreshDelayTimer = null;

        /// <summary>
        /// Play/Pause updating the view.
        /// The source-of-truth is the UI, so no need to raise change notifications
        /// </summary>
        bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;

                // The UI was paused, bring it up to date
                // If it's now paused, the timer might be running, but we'll ignore when it raises
                if (_isPlaying)
                {
                    Reload(null, null);
                }
            }
        }
        bool _isPlaying = true;

        /// <summary>
        /// Reload the UI. This is used both from the UI Refresh button and from event log change notifications
        /// </summary>
        private void Reload(object sender, RoutedEventArgs e)
        {
            Debug.Assert(MyThreading.CurrentIsUiThread);

            _packagingEvents.Reset();
            _deploymentEvents.Reset();
            ReadLogAndUpdateView();
        }

        /// <summary>
        /// Minimum severity level to display (lower number is more severe)
        /// </summary>
        int LogLevel
        {
            get => _logLevel;
            set
            {
                _logLevel = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(LogLevelString));
            }
        }
        int _logLevel = 5; // Verbose

        /// <summary>
        /// LogLevel property but a word rather than a number
        /// </summary>
        string LogLevelString
        {
            get
            {
                return LogLevel switch
                {
                    1 => "Critical",
                    2 => "Error",
                    3 => "Warning",
                    4 => "Information",
                    5 => "Verbose",
                    _ => "Unknown"
                };
            }
        }

        /// <summary>
        /// Helper for the toggle menu item to figure out if it's checked
        /// </summary>
        bool IsLogLevelChecked(int level, int logLevel)
        {
            return level == logLevel;
        }

        /// <summary>
        /// Called by all of the Filter menu items. Tag property tells which one
        /// </summary>
        private void SeverityFilter_Click(object sender, RoutedEventArgs e)
        {
            LogLevel = int.Parse((string)(sender as MenuFlyoutItem).Tag);
            Reload(null,null);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void RaisePropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        int RecordCount => _eventRecords == null ? 0 : _eventRecords.Count;
        string OldestRecord;

        /// <summary>
        /// Called by a hyperlink to open the Filter menu
        /// </summary>
        private void ShowFilter_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            _filterButton.Flyout.ShowAt(_filterButton);
        }
    }
}
