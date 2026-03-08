namespace APIPSI16.Services
{
    /// <summary>
    /// Shared helpers for calculating opportunity-to-user match scores.
    /// Match is based on job role preferences (70% weight) and location (30% weight).
    /// </summary>
    public static class MatchScoreHelper
    {
        public const double RoleWeight = 0.7;
        public const double LocationWeight = 0.3;

        /// <summary>
        /// Country codes that are geographically compact. Within these countries,
        /// a different city/location still yields a partial location score.
        /// For large countries (US, CA, etc.) a different location → 0 score.
        /// </summary>
        private static readonly HashSet<string> SmallCountries = new(StringComparer.OrdinalIgnoreCase)
        {
            "PT", "NL", "BE", "LU", "AT", "CH", "IE", "DK", "HR", "SI", "SK", "CZ", "HU"
        };

        /// <summary>
        /// Computes a 0-100 location score based on structured location data.
        /// <list type="bullet">
        ///   <item>Same LocationId → 100 (exact match)</item>
        ///   <item>Same region, small country → 65</item>
        ///   <item>Different region, small country → 35</item>
        ///   <item>Same region, large country → 50</item>
        ///   <item>Different region, large country → 0 (e.g. US state change)</item>
        ///   <item>Different country → 0</item>
        ///   <item>Missing data → -1 (caller should treat as "no location data")</item>
        /// </list>
        /// </summary>
        public static int ComputeLocationScore(
            int? userLocationId, int? oppLocationId,
            string? userRegion, string? oppRegion,
            string? userCountryCode, string? oppCountryCode)
        {
            if (!userLocationId.HasValue || !oppLocationId.HasValue)
                return -1; // no structured location data available

            if (userLocationId.Value == oppLocationId.Value)
                return 100;

            // Different locations – compare countries
            if (string.IsNullOrWhiteSpace(userCountryCode) || string.IsNullOrWhiteSpace(oppCountryCode))
                return 0;

            if (!string.Equals(userCountryCode, oppCountryCode, StringComparison.OrdinalIgnoreCase))
                return 0; // different country

            // Same country – proximity depends on country size
            bool sameRegion = !string.IsNullOrWhiteSpace(userRegion)
                && string.Equals(userRegion, oppRegion, StringComparison.OrdinalIgnoreCase);

            if (SmallCountries.Contains(userCountryCode))
                return sameRegion ? 65 : 35;
            else
                return sameRegion ? 50 : 0;
        }

        /// <summary>
        /// Legacy string-based location match (kept for backward compatibility).
        /// Prefer <see cref="ComputeLocationScore"/> when structured IDs are available.
        /// </summary>
        public static bool LocationsMatch(string? userLocation, string? opportunityLocation)
        {
            if (string.IsNullOrWhiteSpace(userLocation) || string.IsNullOrWhiteSpace(opportunityLocation))
                return false;

            var ul = userLocation.Trim();
            var ol = opportunityLocation.Trim();
            return ol.Contains(ul, StringComparison.OrdinalIgnoreCase)
                || ul.Contains(ol, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// /// Computes a 0-100 role score using the F1-score (harmonic mean of precision and recall).
        /// Precision = fraction of the user's preferences that the opportunity requires.
        /// Recall    = fraction of the opportunity's requirements that the user has.
        /// Using only recall would give 100% whenever the user's preferences are a superset
        /// of the required roles, even if the opportunity is very different from what the user wants.
        /// </summary>
        /// <param name="matchCount">Number of roles present in both user preferences and required roles.</param>
        /// <param name="userPrefCount">Total number of user job-role preferences.</param>
        /// <param name="requiredRoleCount">Total number of roles required by the opportunity.</param>
        /// <returns>0-100 role score, or 0 if there is no data.</returns>
        public static int ComputeRoleScore(int matchCount, int userPrefCount, int requiredRoleCount)
        {
            int total = userPrefCount + requiredRoleCount;
            return total > 0 ? (int)Math.Round(2.0 * matchCount / total * 100) : 0;
        }
        /// <summary>
        /// Combines a role score (0-100) and a location score (0-100 or -1 for no data)
        /// into a single weighted percentage.
        /// </summary>
        /// <param name="roleScore">0-100 score based on job-role overlap.</param>
        /// <param name="locationScore">0-100 score from <see cref="ComputeLocationScore"/>, or -1 if no data.</param>
        /// <param name="hasRoles">Whether the opportunity specifies required job roles.</param>
        /// <returns>Weighted match percentage (0-100).</returns>
        public static int ComputeWeightedScore(int roleScore, int locationScore, bool hasRoles)
        {
            bool hasLocations = locationScore >= 0;

            if (hasRoles && hasLocations)
                return (int)Math.Round(roleScore * RoleWeight + locationScore * LocationWeight);
            if (hasRoles)
                return roleScore;
            if (hasLocations)
                return locationScore;
            return 0;
        }

        /// <summary>
        /// Overload that accepts a boolean locationMatched flag for simple cases.
        /// </summary>
        public static int ComputeWeightedScore(int roleScore, bool locationMatched, bool hasRoles, bool hasLocations)
        {
            int locationScore = locationMatched ? 100 : 0;

            if (hasRoles && hasLocations)
                return (int)Math.Round(roleScore * RoleWeight + locationScore * LocationWeight);
            if (hasRoles)
                return roleScore;
            if (hasLocations)
                return locationScore;
            return 0;
        }
    }
}
