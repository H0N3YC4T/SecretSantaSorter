// UiLog.cs
using System.Collections.ObjectModel;
using System.Windows.Threading;
using WpfDispatcher = System.Windows.Threading.Dispatcher;

// Disambiguate WPF vs WinForms
using WpfListBox = System.Windows.Controls.ListBox;
using WpfPriority = System.Windows.Threading.DispatcherPriority;

namespace SecretSantaSorter
{
    public enum LogLevel
    { Info, Warn, Error, Debug, Trace }

    public static class UiLog
    {
        private static WpfDispatcher? _dispatcher;
        private static ObservableCollection<string>? _lines;

        /// <summary>Attach once (e.g., in MainWindow) to wire the OutputBox ListBox.</summary>
        public static void Attach(WpfListBox listBox, ObservableCollection<string>? backing = null)
        {
            _dispatcher = listBox.Dispatcher;
            _lines = backing ?? [];
            listBox.ItemsSource = _lines;
        }

        /// <summary>Log a line at the top. <paramref name="tag"/> is your modifier (e.g., "Alice", "Parser").</summary>
        public static void Write(string message, string? tag = null, LogLevel level = LogLevel.Info)
        {
            if (_dispatcher is null || _lines is null)
            {
                return;
            }

            string line = $"{DateTime.Now:HH:mm:ss} [{(tag ?? level.ToString()).ToUpper()}] {message}";

            void Insert()
            {
                _lines!.Insert(0, line);
            }

            if (_dispatcher.CheckAccess())
            {
                Insert();
            }
            else
            {
                _ = _dispatcher.BeginInvoke(Insert, WpfPriority.Background);
            }
        }

        public static void Clear()
        {
            if (_dispatcher is null || _lines is null)
            {
                return;
            }

            static void DoClear()
            {
                _lines!.Clear();
            }

            if (_dispatcher.CheckAccess())
            {
                DoClear();
            }
            else
            {
                _ = _dispatcher.BeginInvoke(DoClear, DispatcherPriority.Background);
            }
        }

        /// <summary>Convenience overload with string modifier only.</summary>
        public static void WriteTag(string tag, string message)
        {
            Write(message, tag, LogLevel.Info);
        }
    }
}