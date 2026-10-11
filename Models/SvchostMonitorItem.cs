using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace AutoCommand.Models
{
    public class SvchostMonitorItem : INotifyPropertyChanged, IResolvableHost
    {
        private long _rxPackets;
        private long _txPackets;
        private long _rxBytes;
        private long _txBytes;
        private DateTime _lastSeen;
        private string _processName;
        private string _hostname;
        private string _taskName;
        private string _taskUser;

        public int ProcessId { get; set; }
        public string RemoteIp { get; set; }
        public string Protocol { get; set; }

        /// <summary>
        /// Full executable path as logged by Sysmon (null when unknown). It
        /// identifies the row's process even after that process has exited,
        /// which is when the live name lookup would otherwise come up empty.
        /// </summary>
        public string ImagePath { get; set; }

        public string Hostname
        {
            get => _hostname;
            set
            {
                if (_hostname == value) return;
                _hostname = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsResolved));
                OnPropertyChanged(nameof(HostnameDisplay));
            }
        }

        /// <summary>True when the hostname has been resolved to a real name (not raw IP / empty).</summary>
        public bool IsResolved =>
            !string.IsNullOrEmpty(_hostname) &&
            _hostname != "Unknown" &&
            _hostname != "-" &&
            !System.Net.IPAddress.TryParse(_hostname, out _);

        /// <summary>Friendly display — shows italic '(resolving…)' when not yet resolved.</summary>
        public string HostnameDisplay =>
            IsResolved ? _hostname : string.IsNullOrEmpty(_hostname) ? "resolving…" : _hostname;

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessDisplay)); }
        }

        /// <summary>Last time a Tx packet was seen for this remote IP.</summary>
        public DateTime LastSeen
        {
            get => _lastSeen;
            set
            {
                _lastSeen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastSeenDisplay));
                OnPropertyChanged(nameof(IsStale));
            }
        }

        /// <summary>True when no traffic has been seen for over five minutes (row dims).</summary>
        public bool IsStale => _lastSeen != default && DateTime.UtcNow - _lastSeen > TimeSpan.FromMinutes(5);

        /// <summary>Friendly display: "PID (name)" or just "PID" if name is unknown.</summary>
        public string ProcessDisplay => string.IsNullOrEmpty(ProcessName)
            ? ProcessId.ToString()
            : $"{ProcessId} ({ProcessName})";

        /// <summary>Human-readable last-seen time.</summary>
        public string LastSeenDisplay
        {
            get
            {
                if (_lastSeen == default) return "—";
                var age = DateTime.UtcNow - _lastSeen;
                if (age.TotalSeconds < 5)  return "just now";
                if (age.TotalSeconds < 60) return $"{(int)age.TotalSeconds}s ago";
                if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes}m ago";
                return _lastSeen.ToLocalTime().ToString("HH:mm:ss");
            }
        }

        public long RxPackets
        {
            get => _rxPackets;
            set { _rxPackets = value; OnPropertyChanged(); }
        }

        public long TxPackets
        {
            get => _txPackets;
            set { _txPackets = value; OnPropertyChanged(); }
        }

        public long RxBytes
        {
            get => _rxBytes;
            set { _rxBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(RxBytesDisplay)); }
        }

        public long TxBytes
        {
            get => _txBytes;
            set { _txBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TxBytesDisplay)); OnPropertyChanged(nameof(IsHighTransmitter)); }
        }

        /// <summary>
        /// Transmit threshold shared by every row: above it a row counts as a
        /// high transmitter (pinned to the top of the grid, tinted). User-
        /// changeable from the monitor toolbar; default 1 KB.
        /// </summary>
        public static long TxThresholdBytes = 1024;

        /// <summary>True while this row's transmit volume is at or above the high-transmitter threshold.</summary>
        public bool IsHighTransmitter => TxBytes >= TxThresholdBytes;

        public string RxBytesDisplay => FormatBytes(RxBytes);
        public string TxBytesDisplay => FormatBytes(TxBytes);

        /// <summary>
        /// Scheduled task that launched this taskhostw instance (attribution from
        /// TaskSchedulerService), or null when the row is not a task host.
        /// </summary>
        public string TaskName
        {
            get => _taskName;
            private set { _taskName = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessToolTip)); }
        }

        /// <summary>Account the scheduled task ran as, when known.</summary>
        public string TaskUser
        {
            get => _taskUser;
            private set { _taskUser = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessToolTip)); }
        }

        /// <summary>Called by the view when TaskSchedulerService resolves the task behind a taskhostw row.</summary>
        public void SetTaskInfo(string taskName, string taskUser)
        {
            if (TaskName != null) return;
            TaskName = taskName;
            TaskUser = taskUser;
        }

        /// <summary>Tooltip for the Process column: image path plus scheduled-task attribution.</summary>
        public string ProcessToolTip
        {
            get
            {
                if (string.IsNullOrEmpty(ImagePath) && TaskName == null) return null;
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(ImagePath)) sb.AppendLine(ImagePath);
                if (TaskName != null)
                {
                    sb.Append("Task: ").Append(TaskName);
                    if (!string.IsNullOrEmpty(TaskUser)) sb.Append("   (as ").Append(TaskUser).Append(')');
                }
                return sb.ToString();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called by the UI refresh timer to re-evaluate the relative "Xs ago" text
        /// and the staleness flag without needing to mutate LastSeen itself.
        /// </summary>
        public void RefreshLastSeenDisplay()
        {
            OnPropertyChanged(nameof(LastSeenDisplay));
            OnPropertyChanged(nameof(IsStale));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{(bytes / 1024.0):F1} KB";
            return $"{(bytes / 1048576.0):F1} MB";
        }
    }
}
