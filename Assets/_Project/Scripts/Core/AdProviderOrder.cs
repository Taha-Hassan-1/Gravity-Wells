using System;
using System.Collections.Generic;

namespace GravityWells.Core
{
    // Turns the configured "admob, applovin" string into a clean priority list. Pure so the
    // parsing rules (case, spaces, blanks, repeats) are tested without any ad SDK.
    public static class AdProviderOrder
    {
        public static string[] Parse(string csv)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return result.ToArray();
            }

            foreach (string part in csv.Split(','))
            {
                string name = part.Trim().ToLowerInvariant();
                // A repeated name keeps its first (highest) position.
                if (name.Length > 0 && !result.Contains(name))
                {
                    result.Add(name);
                }
            }
            return result.ToArray();
        }
    }
}
