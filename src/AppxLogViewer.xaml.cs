using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ViewAppxPackage
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppxLogViewer : Window
    {
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
            };

            // WIP:
            //_rtb.AddHandler(
            //    UIElement.PointerReleasedEvent, 
            //    new PointerEventHandler(RtbPointerReleased), 
            //    true);
        }
        string Text { get; set; }

        static public void Show()
        {
            var window = new AppxLogViewer();
            window.Initialize();
            window.Activate();
        }

        EventLogEnumerator _packagingEvents = null;
        EventLogEnumerator _deploymentEvents = null;

        void UpdateContent()
        {
            _rtb.Blocks.Clear();
            Paragraph paragraph = new();
            _rtb.Blocks.Add(paragraph);

            StringBuilder sb = new();
            TextReader reader = new StringReader(Text);
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                paragraph.Inlines.Add(new Run() { Text = $"{line}\n" });

                sb.AppendLine(line);
            }


            //_rtb.IsReadOnly = false;
            //_rtb.Document.SetText(TextSetOptions.None, sb.ToString());
            //_rtb.IsReadOnly = true;
        }

        void Initialize()
        {
            _packagingEvents = new("Microsoft-Windows-AppxPackaging/Operational");
            _deploymentEvents = new("Microsoft-Windows-AppXDeploymentServer/Operational");

            // Get the most recent 100 entries from the event logs
            // Note that this won't be 50/50 from the two logs, as we're just getting the most recent 100

            StringBuilder sb = new();
            for (int i = 0; i < 200; i++) // bugbug: add a "More" button rather than a random number
            {
                using var record = GetNextEvent();
                if (record == null)
                {
                    // Both logs have been fully read
                    break;
                }

                // Whitespace
                sb.AppendLine();

                // Header line
                var timeCreated = record.TimeCreated?.ToString("MM/dd/yyyy HH:mm:ss");
                var activityId = record.ActivityId.ToString();
                if(!string.IsNullOrEmpty(activityId))
                {
                    activityId = $", ActivityID={activityId}";
                }
                sb.AppendLine($"    {timeCreated}{activityId}, {record.LevelDisplayName}");

                // Description
                sb.AppendLine(record.FormatDescription());
            }

            Text = sb.ToString();

            UpdateContent();
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

        private void DocumentSelectionChanged(object sender, RoutedEventArgs e)
        {
            //var text = _rtb.SelectedText;
            //var start = _rtb.SelectionStart.Offset;
            // Debug.WriteLine($"Selection: '{text}', {start}");
        }

        //private void RtbPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        //{
        //    Test(sender as FrameworkElement, e);

        //    Debug.WriteLine("");
        //    var pointerPoint = e.GetCurrentPoint(sender as FrameworkElement);
        //    var textPointer = _rtb.GetPositionFromPoint(pointerPoint.Position);
        //    Debug.WriteLine($"TextPointer Offset: {textPointer.Offset}");

        //    var saveStartTextPointer = _rtb.SelectionStart;
        //    var saveEndTextPointer = _rtb.SelectionEnd;
        //    var start = textPointer.GetPositionAtOffset(-5, LogicalDirection.Backward);
        //    var end = textPointer.GetPositionAtOffset(5, LogicalDirection.Forward);
        //    _rtb.Select(start, end);
        //    Debug.WriteLine($"Selected: '{_rtb.SelectedText}'");
        //    _rtb.Select(saveStartTextPointer, saveEndTextPointer);


        //    TextRange range = new()
        //    {
        //        StartIndex = textPointer.Offset,
        //        Length = 10
        //    };

        //    TextHighlighter highlighter = new()
        //    {
        //        Ranges = { range }
        //    };

        //    _rtb.TextHighlighters.Add(highlighter);

        //    Debug.WriteLine($"TextPointer Offset into original string: {Text.Substring(textPointer.Offset, 10)}");

        //    //var sub = Text.Substring(position.Offset, 10);
        //    //Debug.WriteLine($"Substring: '{sub}'");

        //    //            e.Handled = true;
        //}

        void Test(FrameworkElement sender, PointerRoutedEventArgs e)
        {
            //var textPointer = _rtb.ContentStart;

            //while(true)
            //{
            //    var pointerNext = textPointer.GetPositionAtOffset(1, LogicalDirection.Forward);
            //    _rtb.Select(textPointer, pointerNext);
            //    Debug.WriteLine($"pointerOffset={textPointer.Offset}, selection='{_rtb.SelectedText}'");
            //    textPointer = pointerNext;
            //}

            ////var pointerPoint = e.GetCurrentPoint(sender as FrameworkElement);
            ////var textPointer = _rtb.GetPositionFromPoint(pointerPoint.Position);
            ////Debug.WriteLine($"TextPointer Offset: {textPointer.Offset}");

            ////var saveStartTextPointer = _rtb.SelectionStart;
            ////var saveEndTextPointer = _rtb.SelectionEnd;
            ////var start = textPointer.GetPositionAtOffset(-5, LogicalDirection.Backward);
            ////var end = textPointer.GetPositionAtOffset(5, LogicalDirection.Forward);
            ////_rtb.Select(start, end);
            ////Debug.WriteLine($"Selected: '{_rtb.SelectedText}'");
            ////_rtb.Select(saveStartTextPointer, saveEndTextPointer);
        }
    }
}
