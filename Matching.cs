// Matching.cs
namespace SecretSantaSorter
{
    public static class SecretSantaMatcher
    {
        /// <summary>
        /// Generates a random 1-to-1 assignment: each person gives to exactly one other person.
        /// Respects directional restrictions in PersonData.Restrictions (and disallows self matches).
        /// Returns a list of (Primary, Recipient). Throws InvalidOperationException if no solution exists.
        /// </summary>
        public static List<(PersonData Primary, PersonData Recipient)> Match(Random? random = null, int maxAttempts = 256)
        {
            random ??= new Random();

            var people = PersonStorage.AllPersons.ToList();
            if (people.Count < 2)
                return [];

            // Precompute allowed recipients per giver
            var allowed = new Dictionary<PersonData, List<PersonData>>(people.Count);
            foreach (var p in people)
            {
                var candidates = people
                    .Where(q => !ReferenceEquals(q, p) && !p.Restrictions.Contains(q))
                    .ToList();
                allowed[p] = candidates;
            }

            // Quick infeasibility check
            if (allowed.Any(kv => kv.Value.Count == 0))
                throw new InvalidOperationException("No valid assignment: at least one person has no permissible recipients.");

            // Heuristic order: fewest options first (MRV), with random tie-breaks
            List<PersonData> OrderGivers()
                => allowed.Keys
                          .OrderBy(k => allowed[k].Count)
                          .ThenBy(_ => random.Next())
                          .ToList();

            // Backtracking state
            var used = new HashSet<PersonData>();                 // recipients already taken
            var assign = new Dictionary<PersonData, PersonData>(); // giver -> recipient

            bool Solve(IReadOnlyList<PersonData> givers, int idx)
            {
                if (idx == givers.Count) return true;

                var giver = givers[idx];

                // viable candidates this step
                var candidates = allowed[giver].Where(c => !used.Contains(c)).ToList();
                ShuffleInPlace(candidates, random);

                foreach (var rec in candidates)
                {
                    assign[giver] = rec;
                    used.Add(rec);

                    if (Solve(givers, idx + 1)) return true;

                    used.Remove(rec);
                    assign.Remove(giver);
                }

                return false;
            }

            // Try a few randomized attempts
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                used.Clear();
                assign.Clear();

                var order = OrderGivers();

                // Optional: small random perturbation inside allowed lists each attempt
                foreach (var k in people) ShuffleInPlace(allowed[k], random);

                if (Solve(order, 0))
                {
                    // Return in the same order as the storage list
                    return people.Select(p => (Primary: p, Recipient: assign[p])).ToList();
                }
            }

            throw new InvalidOperationException("Failed to find a valid assignment under the given restrictions.");
        }

        // Fisher–Yates
        private static void ShuffleInPlace<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
