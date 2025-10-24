// MainWindow.xaml.cs
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using IOPath = System.IO.Path;

namespace SecretSantaSorter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            PreviewKeyDown += Window_PreviewKeyDown;

            UiLog.Attach(OutputBox);
            UiLog.Write(Globals.Messages.LoggerReady, Globals.LogTags.System);

            LstNames.ItemsSource = PersonStorage.AllPersons;
            LstNames.DisplayMemberPath = "Name";

            // Start in display mode
            SwapView(selectionMode: false);
        }

        // ----------------- Pending selection state -----------------

        private enum PendingOp
        { None, AddOneWay, AddMutual, RemoveOneWay, RemoveMutual, RemovePerson, ClearAllForOne }

        private enum PendingPhase
        { None, PickPrimary, PickSecond }

        private PendingOp _pendingOp = PendingOp.None;
        private PendingPhase _phase = PendingPhase.None;

        private PersonData? _pendingPrimary;
        private Action<PersonData, PersonData>? _pendingHandlerTwo; // two-pick ops
        private Action<PersonData>? _pendingHandlerOne;             // one-pick ops

        private void Window_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ClearPending(Globals.Messages.CancelledEsc);
                e.Handled = true;
            }
        }

        private void PrepareSelectionList()
        {
            LstNames.SelectedIndex = -1;   // clear selection
            LstNames.UnselectAll();        // belt-and-braces
            LstNames.UpdateLayout();       // ensure visual tree is current
            _ = LstNames.Focus();              // keyboard/mouse focus ready
        }

        private void SwapView(bool selectionMode)
        {
            LstNames.Visibility = selectionMode ? Visibility.Visible : Visibility.Collapsed;
            LstDisplay.Visibility = selectionMode ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BeginOperationTwoPick(PendingOp op, Action<PersonData, PersonData> handler, string label)
        {
            if (PersonStorage.AllPersons.Count < 2)
            {
                UiLog.Write(Globals.Messages.NeedAtLeastTwo, Globals.LogTags.UI, LogLevel.Warn);
                return;
            }

            _pendingOp = op;
            _pendingHandlerTwo = handler;
            _pendingHandlerOne = null;
            _pendingPrimary = null;
            _phase = PendingPhase.PickPrimary;

            SwapView(selectionMode: true);
            PrepareSelectionList();
            UiLog.Write(string.Format(CultureInfo.CurrentCulture, Globals.Formats.PickFirst, label),
                        Globals.LogTags.Action);
        }

        private void BeginOperationOnePick(PendingOp op, Action<PersonData> handler, string label)
        {
            if (PersonStorage.AllPersons.Count < 1)
            {
                UiLog.Write(Globals.Messages.NoPeopleAvailable, Globals.LogTags.UI, LogLevel.Warn);
                return;
            }

            _pendingOp = op;
            _pendingHandlerOne = handler;
            _pendingHandlerTwo = null;
            _pendingPrimary = null;
            _phase = PendingPhase.PickPrimary;

            SwapView(selectionMode: true);
            PrepareSelectionList();
            UiLog.Write(string.Format(CultureInfo.CurrentCulture, "Pick the person to {0}.", label),
                        Globals.LogTags.Action);
        }

        private void ClearPending(string? reason = null)
        {
            if (_pendingOp != PendingOp.None && reason is not null)
            {
                UiLog.Write(reason, Globals.LogTags.Action, LogLevel.Debug);
            }

            _pendingPrimary = null;
            _pendingOp = PendingOp.None;
            _pendingHandlerTwo = null;
            _pendingHandlerOne = null;
            _phase = PendingPhase.None;

            SwapView(selectionMode: false);
        }

        private void LstNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_pendingOp == PendingOp.None)
            {
                return;
            }

            if (LstNames.SelectedItem is not PersonData pick)
            {
                return;
            }

            if (_phase == PendingPhase.PickPrimary)
            {
                // One-pick op: execute immediately
                if (_pendingHandlerOne is not null)
                {
                    try
                    {
                        _pendingHandlerOne(pick);
                        LstDisplay.Items.Refresh();
                    }
                    finally
                    {
                        ClearPending();
                    }
                    return;
                }

                // Two-pick op: store primary and ask for second
                _pendingPrimary = pick;
                LstNames.SelectedItem = null; // clear visual selection before second pick
                _phase = PendingPhase.PickSecond;
                UiLog.Write(string.Format(CultureInfo.CurrentCulture,
                                          Globals.Formats.PrimarySetPickSecond, _pendingPrimary.Name),
                            Globals.LogTags.Action);
                return;
            }

            if (_phase == PendingPhase.PickSecond)
            {
                if (_pendingPrimary is null || _pendingHandlerTwo is null)
                {
                    ClearPending(Globals.Messages.CancelledInvalidState);
                    return;
                }

                if (string.Equals(pick.Name, _pendingPrimary.Name, StringComparison.OrdinalIgnoreCase))
                {
                    UiLog.Write(Globals.Messages.PickDifferentSecond, Globals.LogTags.UI, LogLevel.Warn);
                    return;
                }

                try
                {
                    _pendingHandlerTwo(_pendingPrimary, pick);
                    LstDisplay.Items.Refresh();
                }
                finally
                {
                    ClearPending();
                }
            }
        }

        // ----------------- Buttons -----------------

        private void BtnAddPerson(object sender, RoutedEventArgs e)
        {
            string? name = txtName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                UiLog.Write(Globals.Messages.NameEmpty, Globals.LogTags.UI, LogLevel.Warn);
                return;
            }

            PersonData p = PersonPush.CreateOrGet(name);
            UiLog.Write($"Created/loaded person: {p.Name}", Globals.LogTags.People);

            txtName.Clear();
            _ = txtName.Focus();
            LstDisplay.Items.Refresh();
        }

        private void BtnRemovePerson(object sender, RoutedEventArgs e)
        {
            // One-pick flow: pick the person to remove
            BeginOperationOnePick(PendingOp.RemovePerson,
                person =>
                {
                    if (PersonPush.RemovePerson(person, out int scrubbed))
                    {
                        UiLog.Write(string.Format(CultureInfo.CurrentCulture,
                                                  Globals.Formats.RemovedPerson, person.Name, scrubbed),
                                    Globals.LogTags.People);
                    }
                    else
                    {
                        UiLog.Write(string.Format(CultureInfo.CurrentCulture,
                                                  Globals.Formats.FailedRemovePerson, person.Name),
                                    Globals.LogTags.People, LogLevel.Error);
                    }
                },
                "remove");
        }

        private void BtnRemoveAllPersons(object sender, RoutedEventArgs e)
        {
            if (PersonStorage.AllPersons.Count == 0)
            {
                UiLog.Write(Globals.Messages.NoPeopleToClear, Globals.LogTags.People, LogLevel.Warn);
                return;
            }

            MessageBoxResult res = System.Windows.MessageBox.Show(
                Globals.Dialog.ConfirmClearAllBody,
                Globals.Dialog.ConfirmClearAllTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes)
            {
                return;
            }

            int count = PersonStorage.AllPersons.Count;
            PersonStorage.AllPersons.Clear();
            UiLog.Write(string.Format(CultureInfo.CurrentCulture, Globals.Formats.ClearedAllData, count),
                        Globals.LogTags.People);
            ClearPending(Globals.Messages.CancelledBecauseClearedAll);
            txtName.Clear();
            LstNames.SelectedItem = null;
            LstDisplay.Items.Refresh();
        }

        private void BtnClearLog(object sender, RoutedEventArgs e)
        {
            UiLog.Clear();
        }

        // -------- Add restrictions (two-pick flows) --------

        private void BtnAddSingleRestriction(object sender, RoutedEventArgs e)
        {
            BeginOperationTwoPick(PendingOp.AddOneWay,
                (a, b) =>
                {
                    bool ok = PersonPush.AddRestriction(a, b);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture,
                                      ok ? Globals.Formats.AddedRestriction
                                         : Globals.Formats.AddedRestrictionNoChange,
                                      a.Name, b.Name),
                        Globals.LogTags.Action, ok ? LogLevel.Info : LogLevel.Debug);
                },
                "add a one-way restriction");
        }

        private void BtnAddMutualRestriction(object sender, RoutedEventArgs e)
        {
            BeginOperationTwoPick(PendingOp.AddMutual,
                (a, b) =>
                {
                    bool ok = PersonPush.AddMutualRestriction(a, b);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture,
                                      ok ? Globals.Formats.AddedMutualRestriction
                                         : Globals.Formats.AddedMutualNoChange,
                                      a.Name, b.Name),
                        Globals.LogTags.Action, ok ? LogLevel.Info : LogLevel.Debug);
                },
                "add a mutual restriction");
        }

        // -------- Remove restrictions --------

        private void BtnRemoveSingleRestriction(object sender, RoutedEventArgs e)
        {
            BeginOperationTwoPick(PendingOp.RemoveOneWay,
                (a, b) =>
                {
                    bool changed = a.RemoveRestriction(b);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture,
                                      changed ? Globals.Formats.RemovedRestriction
                                              : Globals.Formats.RemovedRestrictionNoChange,
                                      a.Name, b.Name),
                        Globals.LogTags.Action, changed ? LogLevel.Info : LogLevel.Warn);
                },
                "remove a one-way restriction");
        }

        private void BtnRemoveMutualRestriction(object sender, RoutedEventArgs e)
        {
            BeginOperationTwoPick(PendingOp.RemoveMutual,
                (a, b) =>
                {
                    bool changed = PersonPush.RemoveMutualRestriction(a, b);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture,
                                      changed ? Globals.Formats.RemovedMutual
                                              : Globals.Formats.RemovedMutualNoChange,
                                      a.Name, b.Name),
                        Globals.LogTags.Action, changed ? LogLevel.Info : LogLevel.Warn);
                },
                "remove a mutual restriction");
        }

        private void BtnRemoveAllSingleRestrictions(object sender, RoutedEventArgs e)
        {
            // One-pick: clear ONLY this person's outgoing restrictions (no incoming scrub).
            BeginOperationOnePick(PendingOp.ClearAllForOne,
                person =>
                {
                    int removed = PersonPush.ClearRestrictions(person);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture, Globals.Formats.ClearedOutgoing, removed, person.Name),
                        Globals.LogTags.People);
                    LstDisplay.Items.Refresh();
                },
                "clear this person's restrictions");
        }

        private void BtnRemoveAllMutualRestrictions(object sender, RoutedEventArgs e)
        {
            // One-pick flow: clear outgoing + incoming for that person
            BeginOperationOnePick(PendingOp.ClearAllForOne,
                person =>
                {
                    (int outCnt, int inCnt) = PersonPush.ClearAllRestrictionLinks(person);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture, Globals.Formats.ClearedAllFor, person.Name, outCnt, inCnt),
                        Globals.LogTags.People);
                    LstDisplay.Items.Refresh();
                },
                "clear all restrictions for");
        }

        // -------- Popup to show matches --------

        private void ShowTextPopup(string title, string text)
        {
            TextBlock tb = new()
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };

            ScrollViewer sv = new()
            {
                Content = tb,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            Window win = new()
            {
                Title = title,
                Owner = this,
                Content = sv,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 700,
                MinHeight = 400,
                MaxWidth = 1600,
                MaxHeight = 800,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            _ = win.ShowDialog();
        }

        private void BtnDisplayList(object sender, RoutedEventArgs e)
        {
            try
            {
                List<(PersonData Primary, PersonData Recipient)> pairs = SecretSantaSorter.SecretSantaMatcher.Match(); // may throw if impossible
                IEnumerable<string> lines = pairs.Select(t =>
                    string.Format(CultureInfo.CurrentCulture, Globals.Formats.PairLine, t.Primary.Name, t.Recipient.Name));
                ShowTextPopup(Globals.Dialog.AssignmentsPopupTitle, string.Join(Environment.NewLine, lines));
            }
            catch (InvalidOperationException ex)
            {
                _ = System.Windows.MessageBox.Show(this, ex.Message, Globals.Dialog.NoValidAssignmentTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _ = System.Windows.MessageBox.Show(this, ex.ToString(), Globals.Dialog.ErrorTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -------- Export to folder (WinForms folder picker, disambiguated) --------

        private void BtnSaveList(object sender, RoutedEventArgs e)
        {
            try
            {
                List<(PersonData Primary, PersonData Recipient)> pairs = SecretSantaSorter.SecretSantaMatcher.Match(); // may throw

                string? baseFolder = PickFolder();
                if (string.IsNullOrEmpty(baseFolder))
                {
                    return; // user cancelled
                }

                string year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
                string folderName = string.Format(CultureInfo.CurrentCulture, Globals.Formats.FolderName, year);
                string targetDir = CreateUniqueDirectory(baseFolder, folderName);

                foreach ((PersonData Primary, PersonData Recipient) in pairs)
                {
                    string fileName = SanitizeFileName($"{Primary.Name}{Globals.Files.OutputExtension}");
                    string filePath = EnsureUniquePath(IOPath.Combine(targetDir, fileName));
                    File.WriteAllText(filePath, string.Format(Globals.Files.DocumentInputString, Primary.Name, Recipient.Name) + Environment.NewLine, System.Text.Encoding.UTF8);
                }

                _ = System.Windows.MessageBox.Show(this,
                                               string.Format(CultureInfo.CurrentCulture,
                                                             Globals.Formats.ExportedCount, pairs.Count, targetDir),
                                               Globals.Dialog.ExportCompleteTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                _ = System.Windows.MessageBox.Show(this, ex.Message, Globals.Dialog.NoValidAssignmentTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _ = System.Windows.MessageBox.Show(this, ex.ToString(), Globals.Dialog.ExportErrorTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? PickFolder()
        {
            using FolderBrowserDialog dlg = new()
            {
                Description = Globals.Dialog.FolderPickerDescription,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            return dlg.ShowDialog() == Forms.DialogResult.OK ? dlg.SelectedPath : null;
        }

        private static string CreateUniqueDirectory(string parent, string baseName)
        {
            string path = IOPath.Combine(parent, baseName);
            if (!Directory.Exists(path))
            {
                _ = Directory.CreateDirectory(path);
                return path;
            }
            int i = 2;
            while (true)
            {
                string candidate = IOPath.Combine(parent, $"{baseName}_{i}");
                if (!Directory.Exists(candidate))
                {
                    _ = Directory.CreateDirectory(candidate);
                    return candidate;
                }
                i++;
            }
        }

        private static string SanitizeFileName(string name)
        {
            char[] bad = IOPath.GetInvalidFileNameChars();
            string cleaned = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? Globals.Files.DefaultFileName : cleaned;
        }

        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            string? dir = IOPath.GetDirectoryName(path);
            string name = IOPath.GetFileNameWithoutExtension(path);
            string ext = IOPath.GetExtension(path);

            int i = 2;
            while (true)
            {
                string candidate = IOPath.Combine(dir!, $"{name}_{i}{ext}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }

                i++;
            }
        }
    }
}