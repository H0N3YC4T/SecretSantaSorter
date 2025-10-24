// MainWindow.xaml.cs
using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
            this.InitializeComponent();

            this.PreviewKeyDown += this.Window_PreviewKeyDown;

            UiLog.Attach(this.OutputBox);
            UiLog.Write(Globals.Messages.LoggerReady, Globals.LogTags.System);

            this.LstNames.ItemsSource = PersonStorage.AllPersons;
            this.LstNames.DisplayMemberPath = "Name";

            // Start in display mode
            this.SwapView(selectionMode: false);
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
                this.ClearPending(Globals.Messages.CancelledEsc);
                e.Handled = true;
            }
        }

        private void PrepareSelectionList()
        {
            this.LstNames.SelectedIndex = -1;   // clear selection
            this.LstNames.UnselectAll();        // belt-and-braces
            this.LstNames.UpdateLayout();       // ensure visual tree is current
            this.LstNames.Focus();              // keyboard/mouse focus ready
        }

        private void SwapView(bool selectionMode)
        {
            this.LstNames.Visibility = selectionMode ? Visibility.Visible : Visibility.Collapsed;
            this.LstDisplay.Visibility = selectionMode ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BeginOperationTwoPick(PendingOp op, Action<PersonData, PersonData> handler, string label)
        {
            if (PersonStorage.AllPersons.Count < 2)
            {
                UiLog.Write(Globals.Messages.NeedAtLeastTwo, Globals.LogTags.UI, LogLevel.Warn);
                return;
            }

            this._pendingOp = op;
            this._pendingHandlerTwo = handler;
            this._pendingHandlerOne = null;
            this._pendingPrimary = null;
            this._phase = PendingPhase.PickPrimary;

            this.SwapView(selectionMode: true);
            this.PrepareSelectionList();
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

            this._pendingOp = op;
            this._pendingHandlerOne = handler;
            this._pendingHandlerTwo = null;
            this._pendingPrimary = null;
            this._phase = PendingPhase.PickPrimary;

            this.SwapView(selectionMode: true);
            this.PrepareSelectionList();
            UiLog.Write(string.Format(CultureInfo.CurrentCulture, "Pick the person to {0}.", label),
                        Globals.LogTags.Action);
        }

        private void ClearPending(string? reason = null)
        {
            if (this._pendingOp != PendingOp.None && reason is not null)
                UiLog.Write(reason, Globals.LogTags.Action, LogLevel.Debug);

            this._pendingPrimary = null;
            this._pendingOp = PendingOp.None;
            this._pendingHandlerTwo = null;
            this._pendingHandlerOne = null;
            this._phase = PendingPhase.None;

            this.SwapView(selectionMode: false);
        }

        private void LstNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this._pendingOp == PendingOp.None) return;
            if (this.LstNames.SelectedItem is not PersonData pick) return;

            if (this._phase == PendingPhase.PickPrimary)
            {
                // One-pick op: execute immediately
                if (this._pendingHandlerOne is not null)
                {
                    try
                    {
                        this._pendingHandlerOne(pick);
                        this.LstDisplay.Items.Refresh();
                    }
                    finally
                    {
                        this.ClearPending();
                    }
                    return;
                }

                // Two-pick op: store primary and ask for second
                this._pendingPrimary = pick;
                this.LstNames.SelectedItem = null; // clear visual selection before second pick
                this._phase = PendingPhase.PickSecond;
                UiLog.Write(string.Format(CultureInfo.CurrentCulture,
                                          Globals.Formats.PrimarySetPickSecond, this._pendingPrimary.Name),
                            Globals.LogTags.Action);
                return;
            }

            if (this._phase == PendingPhase.PickSecond)
            {
                if (this._pendingPrimary is null || this._pendingHandlerTwo is null)
                {
                    this.ClearPending(Globals.Messages.CancelledInvalidState);
                    return;
                }

                if (string.Equals(pick.Name, this._pendingPrimary.Name, StringComparison.OrdinalIgnoreCase))
                {
                    UiLog.Write(Globals.Messages.PickDifferentSecond, Globals.LogTags.UI, LogLevel.Warn);
                    return;
                }

                try
                {
                    this._pendingHandlerTwo(this._pendingPrimary, pick);
                    this.LstDisplay.Items.Refresh();
                }
                finally
                {
                    this.ClearPending();
                }
            }
        }

        // ----------------- Buttons -----------------

        private void BtnAddPerson(object sender, RoutedEventArgs e)
        {
            var name = this.txtName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                UiLog.Write(Globals.Messages.NameEmpty, Globals.LogTags.UI, LogLevel.Warn);
                return;
            }

            var p = PersonPush.CreateOrGet(name);
            UiLog.Write($"Created/loaded person: {p.Name}", Globals.LogTags.People);

            this.txtName.Clear();
            this.txtName.Focus();
            this.LstDisplay.Items.Refresh();
        }

        private void BtnRemovePerson(object sender, RoutedEventArgs e)
        {
            // One-pick flow: pick the person to remove
            this.BeginOperationOnePick(PendingOp.RemovePerson,
                person =>
                {
                    if (PersonPush.RemovePerson(person, out int scrubbed))
                        UiLog.Write(string.Format(CultureInfo.CurrentCulture,
                                                  Globals.Formats.RemovedPerson, person.Name, scrubbed),
                                    Globals.LogTags.People);
                    else
                        UiLog.Write(string.Format(CultureInfo.CurrentCulture,
                                                  Globals.Formats.FailedRemovePerson, person.Name),
                                    Globals.LogTags.People, LogLevel.Error);
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

            var res = System.Windows.MessageBox.Show(
                Globals.Dialog.ConfirmClearAllBody,
                Globals.Dialog.ConfirmClearAllTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            int count = PersonStorage.AllPersons.Count;
            PersonStorage.AllPersons.Clear();
            UiLog.Write(string.Format(CultureInfo.CurrentCulture, Globals.Formats.ClearedAllData, count),
                        Globals.LogTags.People);
            this.ClearPending(Globals.Messages.CancelledBecauseClearedAll);
            this.txtName.Clear();
            this.LstNames.SelectedItem = null;
            this.LstDisplay.Items.Refresh();
        }

        private void BtnClearLog(object sender, RoutedEventArgs e)
        {
            UiLog.Clear();
        }

        // -------- Add restrictions (two-pick flows) --------

        private void BtnAddSingleRestriction(object sender, RoutedEventArgs e)
        {
            this.BeginOperationTwoPick(PendingOp.AddOneWay,
                (a, b) =>
                {
                    var ok = PersonPush.AddRestriction(a, b);
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
            this.BeginOperationTwoPick(PendingOp.AddMutual,
                (a, b) =>
                {
                    var ok = PersonPush.AddMutualRestriction(a, b);
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
            this.BeginOperationTwoPick(PendingOp.RemoveOneWay,
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
            this.BeginOperationTwoPick(PendingOp.RemoveMutual,
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
            this.BeginOperationOnePick(PendingOp.ClearAllForOne,
                person =>
                {
                    int removed = PersonPush.ClearRestrictions(person);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture, Globals.Formats.ClearedOutgoing, removed, person.Name),
                        Globals.LogTags.People);
                    this.LstDisplay.Items.Refresh();
                },
                "clear this person's restrictions");
        }

        private void BtnRemoveAllMutualRestrictions(object sender, RoutedEventArgs e)
        {
            // One-pick flow: clear outgoing + incoming for that person
            this.BeginOperationOnePick(PendingOp.ClearAllForOne,
                person =>
                {
                    var (outCnt, inCnt) = PersonPush.ClearAllRestrictionLinks(person);
                    UiLog.Write(
                        string.Format(CultureInfo.CurrentCulture, Globals.Formats.ClearedAllFor, person.Name, outCnt, inCnt),
                        Globals.LogTags.People);
                    this.LstDisplay.Items.Refresh();
                },
                "clear all restrictions for");
        }

        // -------- Popup to show matches --------

        private void ShowTextPopup(string title, string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };

            var sv = new ScrollViewer
            {
                Content = tb,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var win = new Window
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

            win.ShowDialog();
        }

        private void BtnDisplayList(object sender, RoutedEventArgs e)
        {
            try
            {
                var pairs = SecretSantaSorter.SecretSantaMatcher.Match(); // may throw if impossible
                var lines = pairs.Select(t =>
                    string.Format(CultureInfo.CurrentCulture, Globals.Formats.PairLine, t.Primary.Name, t.Recipient.Name));
                this.ShowTextPopup(Globals.Dialog.AssignmentsPopupTitle, string.Join(Environment.NewLine, lines));
            }
            catch (InvalidOperationException ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, Globals.Dialog.NoValidAssignmentTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.ToString(), Globals.Dialog.ErrorTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -------- Export to folder (WinForms folder picker, disambiguated) --------

        private void BtnSaveList(object sender, RoutedEventArgs e)
        {
            try
            {
                var pairs = SecretSantaSorter.SecretSantaMatcher.Match(); // may throw

                string? baseFolder = PickFolder();
                if (string.IsNullOrEmpty(baseFolder)) return; // user cancelled

                string year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
                string folderName = string.Format(CultureInfo.CurrentCulture, Globals.Formats.FolderName, year);
                string targetDir = CreateUniqueDirectory(baseFolder, folderName);

                foreach (var (Primary, Recipient) in pairs)
                {
                    string fileName = SanitizeFileName($"{Primary.Name}{Globals.Files.OutputExtension}");
                    string filePath = EnsureUniquePath(IOPath.Combine(targetDir, fileName));
                    File.WriteAllText(filePath, string.Format(Globals.Files.DocumentInputString, Primary.Name, Recipient.Name) + Environment.NewLine, System.Text.Encoding.UTF8);
                }

                System.Windows.MessageBox.Show(this,
                                               string.Format(CultureInfo.CurrentCulture,
                                                             Globals.Formats.ExportedCount, pairs.Count, targetDir),
                                               Globals.Dialog.ExportCompleteTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, Globals.Dialog.NoValidAssignmentTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.ToString(), Globals.Dialog.ExportErrorTitle,
                                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? PickFolder()
        {
            using var dlg = new Forms.FolderBrowserDialog
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
                Directory.CreateDirectory(path);
                return path;
            }
            int i = 2;
            while (true)
            {
                string candidate = IOPath.Combine(parent, $"{baseName}_{i}");
                if (!Directory.Exists(candidate))
                {
                    Directory.CreateDirectory(candidate);
                    return candidate;
                }
                i++;
            }
        }

        private static string SanitizeFileName(string name)
        {
            var bad = IOPath.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? Globals.Files.DefaultFileName : cleaned;
        }

        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string? dir = IOPath.GetDirectoryName(path);
            string name = IOPath.GetFileNameWithoutExtension(path);
            string ext = IOPath.GetExtension(path);

            int i = 2;
            while (true)
            {
                string candidate = IOPath.Combine(dir!, $"{name}_{i}{ext}");
                if (!File.Exists(candidate)) return candidate;
                i++;
            }
        }
    }
}
