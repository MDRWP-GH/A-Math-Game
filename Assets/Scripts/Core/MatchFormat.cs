namespace AMath.Core
{
    /// <summary>How players are grouped for scoring and results.</summary>
    public enum MatchFormat : byte
    {
        /// <summary>Every player competes alone; highest individual score wins.</summary>
        Individual = 0,

        /// <summary>Players are split into two teams; combined team score decides the winner.</summary>
        Team = 1
    }
}
