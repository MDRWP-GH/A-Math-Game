namespace AMath.Tutorial.Interfaces
{
    /// <summary>
    /// Draws attention to a specific object (board cell, rack slot, button)
    /// by a stable id the tutorial data refers to — never a direct scene
    /// reference baked into step data, so highlight targets can be resolved
    /// differently per scene without touching tutorial definitions.
    /// </summary>
    public interface ITutorialHighlightService
    {
        /// <summary>Highlights the object registered under <paramref name="targetId"/>.</summary>
        void Highlight(string targetId);

        /// <summary>Clears any active highlight.</summary>
        void ClearHighlight();
    }
}
