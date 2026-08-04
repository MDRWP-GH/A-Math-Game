using UnityEngine;

namespace AMath.AI.Context
{
    /// <summary>
    /// Authored system instructions for one assistant mode. Instructions are
    /// data rather than hardcoded controller strings, allowing prompt changes,
    /// localization and backend evaluation without rebuilding mode logic.
    /// </summary>
    [CreateAssetMenu(fileName = "AiPromptProfile", menuName = "A-Math/AI/Prompt Profile")]
    public sealed class AiPromptProfile : ScriptableObject
    {
        [SerializeField] private string _modeId;
        [SerializeField, TextArea(8, 24)] private string _systemInstruction;

        /// <summary>Stable mode identifier.</summary>
        public string ModeId => _modeId;

        /// <summary>Mode-specific system instruction sent with every request.</summary>
        public string SystemInstruction => _systemInstruction;

        /// <summary>Checks that the profile can safely build prompts.</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(_modeId) &&
            !string.IsNullOrWhiteSpace(_systemInstruction);
    }
}
