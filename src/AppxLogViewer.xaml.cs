using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;

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

        void Initialize()
        {
            _packagingEvents = new("Microsoft-Windows-AppxPackaging/Operational");
            _deploymentEvents = new("Microsoft-Windows-AppXDeploymentServer/Operational");

            // Get the most recent 100 entries from the event logs
            // Note that this won't be 50/50 from the two logs, as we're just getting the most recent 100
            // bugbug: add a "More" button to get more than 100

            StringBuilder sb = new();
            for (int i = 0; i < 100; i++)
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

    }
}
