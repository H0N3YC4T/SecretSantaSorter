// Globals.cs
using System;

namespace SecretSantaSorter
{
    /// <summary>
    /// Centralized constants and format strings used across the app.
    /// Grouped by purpose (log tags, UI messages, dialogs, formats, etc.)
    /// to avoid scattered “magic strings” and make future maintenance easier.
    /// </summary>
    public static class Globals
    {
        /// <summary>Canonical log tags used with <see cref="UiLog"/>.</summary>
        public static class LogTags
        {
            /// <summary>System/boot messages.</summary>
            public const string System = "system";

            /// <summary>User interaction / validation messages.</summary>
            public const string UI = "ui";

            /// <summary>Action flow messages (e.g., selection prompts).</summary>
            public const string Action = "action";

            /// <summary>Person / storage operations.</summary>
            public const string People = "people";
        }

        /// <summary>Common symbol strings used in rendered UI text.</summary>
        public static class Symbols
        {
            /// <summary>Arrow used in the names-&gt;restrictions display view.</summary>
            public const string RestrictionArrow = " ->! ";

            /// <summary>Arrow used in “giver -&gt; recipient” displays.</summary>
            public const string PairArrow = " -> ";
        }

        /// <summary>
        /// Format strings for composing messages. Use with <see cref="string.Format(IFormatProvider,string,object[])"/>.
        /// </summary>
        public static class Formats
        {
            /// <summary>Folder name pattern: {0}=year (e.g., <c>SecretSanta_2025</c>).</summary>
            public const string FolderName = "SecretSanta_{0}";

            /// <summary>One line of the final pairs list: {0}=giver, {1}=recipient.</summary>
            public const string PairLine = "{0} -> {1}";

            /// <summary>Export completion body: {0}=count, {1}=target folder.</summary>
            public const string ExportedCount = "Exported {0} assignments to:\n{1}";

            /// <summary>Log line after person removal: {0}=name, {1}=scrubbed references.</summary>
            public const string RemovedPerson = "Removed {0}; scrubbed {1} references.";

            /// <summary>Log line when person removal failed: {0}=name.</summary>
            public const string FailedRemovePerson = "Failed to remove {0}";

            /// <summary>Log line after clearing all data: {0}=number of people.</summary>
            public const string ClearedAllData = "Cleared all data ({0} people).";

            /// <summary>Log line after clearing a person's outgoing restrictions: {0}=count, {1}=name.</summary>
            public const string ClearedOutgoing = "Cleared {0} outgoing restrictions for {1}";

            /// <summary>Log line after clearing outgoing+incoming for a person.</summary>
            public const string ClearedAllFor = "Cleared restrictions for {0}: outgoing={1}, incoming={2}";

            /// <summary>Added one-way restriction: {0}=A, {1}=B.</summary>
            public const string AddedRestriction = "Added restriction: {0} → {1}";

            /// <summary>No-op adding one-way restriction: {0}=A, {1}=B.</summary>
            public const string AddedRestrictionNoChange = "No change (already exists): {0} → {1}";

            /// <summary>Added mutual restriction: {0}=A, {1}=B.</summary>
            public const string AddedMutualRestriction = "Added mutual restriction: {0} ↔ {1}";

            /// <summary>No-op adding mutual restriction: {0}=A, {1}=B.</summary>
            public const string AddedMutualNoChange = "No change (already exists): {0} ↔ {1}";

            /// <summary>Removed one-way restriction: {0}=A, {1}=B.</summary>
            public const string RemovedRestriction = "Removed restriction: {0} → {1}";

            /// <summary>No-op removing one-way restriction: {0}=A, {1}=B.</summary>
            public const string RemovedRestrictionNoChange = "No change (not found): {0} → {1}";

            /// <summary>Removed mutual restriction: {0}=A, {1}=B.</summary>
            public const string RemovedMutual = "Removed mutual restriction: {0} ↔ {1}";

            /// <summary>No-op removing mutual restriction: {0}=A, {1}=B.</summary>
            public const string RemovedMutualNoChange = "No mutual restriction to remove: {0} ↔ {1}";

            /// <summary>Prompt after selecting primary: {0}=primary name.</summary>
            public const string PrimarySetPickSecond = "Primary set: {0}. Now pick the SECOND person.";

            /// <summary>Initial selection prompt: {0}=action label (e.g., “add a one-way restriction”).</summary>
            public const string PickFirst = "Pick the FIRST person to {0}.";
        }

        /// <summary>Plain text messages used in logs and prompts.</summary>
        public static class Messages
        {
            /// <summary>Shown right after the logger is attached.</summary>
            public const string LoggerReady = "Logger ready";

            /// <summary>Input validation: name textbox was empty.</summary>
            public const string NameEmpty = "Name was empty";

            /// <summary>Action requires at least two people.</summary>
            public const string NeedAtLeastTwo = "Need at least two people to perform this action";

            /// <summary>No people available for a one-pick action.</summary>
            public const string NoPeopleAvailable = "No people available";

            /// <summary>Attempt to clear all when list is empty.</summary>
            public const string NoPeopleToClear = "No people to clear";

            /// <summary>Warn when user tries to pick the same person twice.</summary>
            public const string PickDifferentSecond = "Pick a DIFFERENT person as the second participant.";

            /// <summary>State cleared via ESC.</summary>
            public const string CancelledEsc = "Cancelled via ESC";

            /// <summary>Pending operation cancelled due to invalid state.</summary>
            public const string CancelledInvalidState = "Cancelled: invalid pending state";

            /// <summary>Pending operation cancelled because the subject person was removed.</summary>
            public const string CancelledBecauseRemoval = "Cancelled pending action due to removal";

            /// <summary>Pending operation cancelled after clearing all data.</summary>
            public const string CancelledBecauseClearedAll = "Cancelled pending action (cleared all)";
        }

        /// <summary>Dialog titles and bodies used with <c>MessageBox</c> and popups.</summary>
        public static class Dialog
        {
            /// <summary>Confirmation title for wiping all people.</summary>
            public const string ConfirmClearAllTitle = "Confirm Clear All";

            /// <summary>Body text asking to confirm wiping all people.</summary>
            public const string ConfirmClearAllBody =
                "This will remove ALL people and their restrictions. Continue?";

            /// <summary>Title shown after successful export.</summary>
            public const string ExportCompleteTitle = "Export complete";

            /// <summary>Generic export error title.</summary>
            public const string ExportErrorTitle = "Export error";

            /// <summary>Generic error title.</summary>
            public const string ErrorTitle = "Error";

            /// <summary>Title used when matching fails due to constraints.</summary>
            public const string NoValidAssignmentTitle = "No valid assignment";

            /// <summary>Popup title for the on-screen list of pairs.</summary>
            public const string AssignmentsPopupTitle = "Secret Santa Assignments";

            /// <summary>Description text for the folder selection dialog.</summary>
            public const string FolderPickerDescription = "Choose a folder to save Secret Santa outputs";
        }

        /// <summary>Error strings thrown from business logic.</summary>
        public static class Errors
        {
            /// <summary>Thrown when at least one person has no permissible recipients.</summary>
            public const string NoValidAssignmentPersonHasNoOptions =
                "No valid assignment: at least one person has no permissible recipients.";

            /// <summary>Thrown after randomized backtracking fails to find a solution.</summary>
            public const string NoValidAssignmentUnderRestrictions =
                "Failed to find a valid assignment under the given restrictions.";

            /// <summary>Used by <c>PersonStorage.Normalize</c> for invalid input names.</summary>
            public const string NameEmptyOrWhitespace = "Name cannot be empty/whitespace.";
        }

        /// <summary>File/folder related constants.</summary>
        public static class Files
        {
            /// <summary>String to place in the saved files.</summary>
            public const string DocumentInputString = "{0} is buying a present for {1}";

            /// <summary>Fallback filename when a person's name is empty after sanitization.</summary>
            public const string DefaultFileName = "Unnamed";

            /// <summary>Extension for per-giver output files.</summary>
            public const string OutputExtension = ".txt";
        }
    }
}
