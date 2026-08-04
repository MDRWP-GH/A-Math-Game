using System;
using System.Collections.Generic;

namespace AMath.Networking.HostMigration
{
    /// <summary>
    /// One entry in the replicated host-succession table: a client that could
    /// become the next host, with the address others would reconnect to and
    /// the quality metric used for ranking.
    /// </summary>
    [Serializable]
    public struct MigrationCandidate
    {
        /// <summary>Seat id of the candidate.</summary>
        public int PlayerId;

        /// <summary>LAN IPv4 address of the candidate (server-observed, not self-reported).</summary>
        public string Address;

        /// <summary>Round-trip time to the current host in milliseconds (lower is better).</summary>
        public double RttMs;
    }

    /// <summary>
    /// Deterministic candidate ordering: lowest ping first (best connection
    /// quality), ties broken by seat id. Every peer sorts the same replicated
    /// data with the same rule, so all survivors independently elect the same
    /// new host with zero negotiation traffic.
    /// </summary>
    public static class MigrationRanking
    {
        /// <summary>Sorts candidates in succession order (index 0 = next host).</summary>
        public static void Sort(List<MigrationCandidate> candidates)
        {
            candidates.Sort(static (a, b) =>
            {
                int byRtt = a.RttMs.CompareTo(b.RttMs);
                return byRtt != 0 ? byRtt : a.PlayerId.CompareTo(b.PlayerId);
            });
        }

        /// <summary>Index of a player in succession order; -1 when absent.</summary>
        public static int RankOf(IReadOnlyList<MigrationCandidate> sorted, int playerId)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].PlayerId == playerId)
                    return i;
            }

            return -1;
        }
    }
}
