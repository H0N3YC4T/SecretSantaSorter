// People.cs
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace SecretSantaSorter
{
    /// <summary>Stores all <see cref="PersonData"/> objects (single source of truth).</summary>
    public static class PersonStorage
    {
        /// <summary>
        /// All registered people. Observable so WPF controls (e.g., ListBox) update automatically
        /// when items are added/removed: LstNames.ItemsSource = PersonStorage.AllPersons.
        /// </summary>
        public static ObservableCollection<PersonData> AllPersons { get; } = new();

        internal static string Normalize(string name)
        {
            ArgumentNullException.ThrowIfNull(name);   // <-- CA1510 fix

            var t = name.Trim();
            if (t.Length == 0) throw new ArgumentException("Name cannot be empty/whitespace.", nameof(name));
            return t;
        }
    }

    /// <summary>Query/read-only helpers for <see cref="PersonData"/>.</summary>
    public static class PersonPull
    {
        /// <summary>Returns <see langword="true"/> if any persons are registered.</summary>
        public static bool AnyPersonsRegistered() => PersonStorage.AllPersons.Count > 0;

        /// <summary>Total person count.</summary>
        public static int GetTotalPersons() => PersonStorage.AllPersons.Count;

        /// <summary>Find by name (case-insensitive); returns null if absent.</summary>
        public static PersonData? GetPersonByName(string name)
        {
            var norm = PersonStorage.Normalize(name);
            return PersonStorage.AllPersons
                .FirstOrDefault(p => string.Equals(p.Name, norm, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True if a name exists (case-insensitive).</summary>
        public static bool ValidatePersonByName(string name) => GetPersonByName(name) is not null;

        /// <summary>
        /// Retrieves a person's property value (as string) by name; returns null if person or property not found.
        /// </summary>
        public static string? GetDataByName(string name, string propertyName)
        {
            var p = GetPersonByName(name);
            return p?.GetValue(propertyName)?.ToString();
        }

        /// <summary>Live view of a person’s restrictions (instances).</summary>
        public static IReadOnlyCollection<PersonData> GetRestrictions(PersonData person) => person.Restrictions;

        /// <summary>Copy of a person’s restrictions (instances).</summary>
        public static List<PersonData> GetRestrictionsCopy(PersonData person)
            => [.. person.Restrictions];

        /// <summary>Restriction names for a person (sorted, case-insensitive).</summary>
        public static IReadOnlyList<string> GetRestrictionNames(PersonData person)
            => [.. person.Restrictions.Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];

        /// <summary>All person names, distinct + sorted (case-insensitive).</summary>
        public static IReadOnlyList<string> GetAllNames()
            => [.. PersonStorage.AllPersons.Select(p => p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Mutation helpers for <see cref="PersonData"/>.</summary>
    public static class PersonPush
    {
        /// <summary>Create a new person or return existing (case-insensitive by name).</summary>
        public static PersonData CreateOrGet(string name)
        {
            var norm = PersonStorage.Normalize(name);
            var existing = PersonPull.GetPersonByName(norm);
            if (existing is not null) return existing;

            var p = new PersonData { Name = norm };
            PersonStorage.AllPersons.Add(p);
            return p;
        }

        /// <summary>Add a one-way restriction by instances.</summary>
        public static bool AddRestriction(PersonData person, PersonData restrictedWith)
            => person.TryAddRestriction(restrictedWith);

        /// <summary>Add a one-way restriction by names (creates missing people).</summary>
        public static bool AddRestriction(string who, string restrictedWith)
        {
            var a = CreateOrGet(who);
            var b = CreateOrGet(restrictedWith);
            return AddRestriction(a, b);
        }

        /// <summary>Add mutual restriction (both directions); rolls back on partial failure.</summary>
        public static bool AddMutualRestriction(PersonData a, PersonData b)
        {
            if (!a.TryAddRestriction(b)) return false;
            if (!b.TryAddRestriction(a)) { a.RemoveRestriction(b); return false; }
            return true;
        }

        /// <summary>Add mutual restriction by names (creates missing people).</summary>
        public static bool AddMutualRestriction(string nameA, string nameB)
        {
            var a = CreateOrGet(nameA);
            var b = CreateOrGet(nameB);
            return AddMutualRestriction(a, b);
        }

        /// <summary>
        /// Removes a person from storage and from all other people's Restrictions.
        /// Returns true if the person existed and was removed.
        /// </summary>
        public static bool RemovePerson(PersonData person, out int removedReferences)
        {
            removedReferences = 0;
            if (person is null) return false;

            // First, scrub references from all other people
            foreach (var p in PersonStorage.AllPersons)
            {
                if (ReferenceEquals(p, person)) continue;
                if (p.RemoveRestriction(person)) removedReferences++;
            }

            // Then remove from the master list (ObservableCollection notifies UI)
            return PersonStorage.AllPersons.Remove(person);
        }

        /// <summary>
        /// Removes a person by name (case-insensitive) and scrubs all references.
        /// Returns true if removed. Outputs how many references were cleaned.
        /// </summary>
        public static bool RemovePerson(string name, out int removedReferences)
        {
            removedReferences = 0;
            var target = PersonPull.GetPersonByName(name);
            if (target is null) return false;

            return RemovePerson(target, out removedReferences);
        }

        /// <summary>
        /// Removes a mutual restriction between two people (by names). Returns true if at least one side changed.
        /// </summary>
        public static bool RemoveMutualRestriction(string nameA, string nameB)
        {
            var a = PersonPull.GetPersonByName(nameA);
            var b = PersonPull.GetPersonByName(nameB);
            if (a is null || b is null) return false;

            bool changed = false;
            changed |= a.RemoveRestriction(b);
            changed |= b.RemoveRestriction(a);
            return changed;
        }

        /// <summary>
        /// Removes a mutual restriction between two people (by instances). Returns true if at least one side changed.
        /// </summary>
        public static bool RemoveMutualRestriction(PersonData personA, PersonData personB)
        {
            if (personA is null || personB is null) return false;

            bool changed = false;
            changed |= personA.RemoveRestriction(personB);
            changed |= personB.RemoveRestriction(personA);
            return changed;
        }

        /// <summary>Clears all outgoing restrictions from a person. Returns how many were removed.</summary>
        public static int ClearRestrictions(PersonData person)
        {
            if (person is null) return 0;
            int count = person.Restrictions.Count;
            person.Restrictions.Clear();
            return count;
        }

        /// <summary>Clears all outgoing restrictions by name. Returns how many were removed.</summary>
        public static int ClearRestrictions(string name)
        {
            var p = PersonPull.GetPersonByName(name);
            return p is null ? 0 : ClearRestrictions(p);
        }

        /// <summary>Scrubs all incoming references to this person (others who restrict this person). Returns how many sets changed.</summary>
        public static int ScrubIncomingRestrictions(PersonData target)
        {
            if (target is null) return 0;
            int removed = 0;
            foreach (var p in PersonStorage.AllPersons)
            {
                if (ReferenceEquals(p, target)) continue;
                if (p.RemoveRestriction(target)) removed++; // HashSet ensures at most 1 per person
            }
            return removed;
        }

        /// <summary>Scrubs all incoming references to the named person. Returns how many sets changed.</summary>
        public static int ScrubIncomingRestrictions(string name)
        {
            var p = PersonPull.GetPersonByName(name);
            return p is null ? 0 : ScrubIncomingRestrictions(p);
        }

        /// <summary>Clears outgoing + scrubs incoming for a person. Returns (outgoingRemoved, incomingRemoved).</summary>
        public static (int outgoingRemoved, int incomingRemoved) ClearAllRestrictionLinks(PersonData person)
        {
            int outCnt = ClearRestrictions(person);
            int inCnt = ScrubIncomingRestrictions(person);
            return (outCnt, inCnt);
        }
    }

    /// <summary>Person identified uniquely by <see cref="Name"/> (case-insensitive).</summary>
    public sealed class PersonData : IEquatable<PersonData>
    {
        /// <summary>Unique display name; kept writable for constructors but protected by SetValue.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>People this person may not be matched with (name-based equality).</summary>
        public HashSet<PersonData> Restrictions { get; } = new();

        /// <summary>Get a property by name (case-insensitive) via reflection.</summary>
        public object? GetValue(string propertyName)
        {
            return this.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?.GetValue(this);
        }

        /// <summary>
        /// Set a property by name via reflection. Disallows changing <see cref="Name"/>.
        /// Returns <see langword="true"/> on success.
        /// </summary>
        public bool SetValue<T>(string propertyName, T newValue)
        {
            var prop = this.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is null || !prop.CanWrite) return false;
            if (string.Equals(prop.Name, nameof(this.Name), StringComparison.Ordinal)) return false; // protect identity

            try
            {
                var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                object? converted = (newValue is null)
                    ? null
                    : (target.IsInstanceOfType(newValue) ? newValue : Convert.ChangeType(newValue, target));
                prop.SetValue(this, converted);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Add a restriction; guards null/self/duplicate (by name).</summary>
        public bool TryAddRestriction(PersonData? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return false;
            return this.Restrictions.Add(other); // HashSet + name-based equality
        }

        /// <summary>Remove a restriction if present.</summary>
        public bool RemoveRestriction(PersonData? other) => other is not null && this.Restrictions.Remove(other);

        // Equality by Name (case-insensitive)
        public bool Equals(PersonData? other) =>
            other is not null && string.Equals(this.Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is PersonData p && this.Equals(p);

        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(this.Name ?? string.Empty);

        public override string ToString() => this.Name;
    }

    /// <summary>Converts a collection of restrictions to a comma-separated string of names.</summary>
    public class RestrictionsToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var names = (value as IEnumerable<PersonData>)
                ?.Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (names is null || names.Length == 0) return string.Empty;
            return string.Join(", ", names);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}