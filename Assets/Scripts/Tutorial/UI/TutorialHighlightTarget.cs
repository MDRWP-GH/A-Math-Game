using System;
using UnityEngine;

namespace AMath.Tutorial.UI
{
    /// <summary>
    /// Scene binding between a stable tutorial target id and the visual
    /// overlay used to highlight it. The overlay can be an outline, arrow,
    /// pulse animation or any other authored GameObject.
    /// </summary>
    [Serializable]
    public sealed class TutorialHighlightTarget
    {
        /// <summary>Stable id referenced by <c>TutorialHighlight</c> actions.</summary>
        [SerializeField] private string _targetId;

        /// <summary>Visual GameObject to show while this target is highlighted.</summary>
        [SerializeField] private GameObject _highlightVisual;

        /// <summary>Stable target id.</summary>
        public string TargetId => _targetId;

        /// <summary>Visual overlay associated with this target.</summary>
        public GameObject HighlightVisual => _highlightVisual;
    }
}
