using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("AMath.Tests.EditMode")]

namespace AMath.UI
{
    /// <summary>
    /// Shared drag-and-drop state for rack-to-board tile placement.
    /// Click placement stays on the button handlers; this only adds drag UX.
    /// </summary>
    internal static class TileDragState
    {
        public static bool IsDragging { get; private set; }
        public static int RackIndex { get; private set; } = -1;
        public static bool DropSucceeded { get; private set; }

        public static void Begin(int rackIndex)
        {
            IsDragging = true;
            RackIndex = rackIndex;
            DropSucceeded = false;
        }

        public static void MarkDropSucceeded() => DropSucceeded = true;

        public static void End()
        {
            IsDragging = false;
            RackIndex = -1;
            DropSucceeded = false;
        }
    }

    /// <summary>
    /// Wires rack buttons and board cells for drag-and-drop tile placement.
    /// </summary>
    internal sealed class TilePlacementInput
    {
        private readonly MatchBoardView _boardView;
        private readonly Func<bool> _canInteract;
        private readonly Func<int?> _selectedRackIndex;
        private readonly Action<int, int, int> _placeFromRack;
        private readonly Action _refreshBoard;

        public TilePlacementInput(
            MatchBoardView boardView,
            Func<bool> canInteract,
            Func<int?> selectedRackIndex,
            Action<int, int, int> placeFromRack,
            Action refreshBoard)
        {
            _boardView = boardView;
            _canInteract = canInteract;
            _selectedRackIndex = selectedRackIndex;
            _placeFromRack = placeFromRack;
            _refreshBoard = refreshBoard;
        }

        public void AttachRackButton(
            Button button,
            int rackIndex,
            Func<Sprite> spriteProvider,
            Action<int> onBeginDrag = null)
        {
            var source = button.gameObject.GetComponent<RackTileDragSource>();
            if (source == null)
                source = button.gameObject.AddComponent<RackTileDragSource>();

            source.Configure(
                rackIndex,
                _canInteract,
                spriteProvider,
                onBeginDrag,
                _boardView.ClearHover,
                _refreshBoard);
        }

        public void AttachBoardCell(Button button, int x, int y)
        {
            var target = button.gameObject.GetComponent<BoardCellDropTarget>();
            if (target == null)
                target = button.gameObject.AddComponent<BoardCellDropTarget>();

            target.Configure(
                x,
                y,
                _canInteract,
                _selectedRackIndex,
                _boardView.SetHoverCell,
                _boardView.ClearHover,
                _placeFromRack,
                _refreshBoard);
        }
    }

    internal sealed class RackTileDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private int _rackIndex;
        private Func<bool> _canInteract;
        private Func<Sprite> _spriteProvider;
        private Action<int> _onBeginDrag;
        private Action _clearHover;
        private Action _refreshBoard;
        private RectTransform _ghost;
        private Canvas _rootCanvas;
        private bool _dragActive;

        public void Configure(
            int rackIndex,
            Func<bool> canInteract,
            Func<Sprite> spriteProvider,
            Action<int> onBeginDrag,
            Action clearHover,
            Action refreshBoard)
        {
            _rackIndex = rackIndex;
            _canInteract = canInteract;
            _spriteProvider = spriteProvider;
            _onBeginDrag = onBeginDrag;
            _clearHover = clearHover;
            _refreshBoard = refreshBoard;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canInteract != null && !_canInteract())
                return;

            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_rootCanvas == null)
                return;

            _onBeginDrag?.Invoke(_rackIndex);
            Sprite sprite = _spriteProvider?.Invoke();
            TileDragState.Begin(_rackIndex);
            _dragActive = true;
            CreateGhost(sprite, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!TileDragState.IsDragging || _ghost == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);
            _ghost.localPosition = localPoint;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragActive)
                return;

            DestroyGhost();
            _clearHover?.Invoke();
            _refreshBoard?.Invoke();
            TileDragState.End();
            _dragActive = false;
        }

        private void OnDisable()
        {
            if (!_dragActive)
                return;

            DestroyGhost();
            TileDragState.End();
            _dragActive = false;
        }

        private void CreateGhost(Sprite sprite, PointerEventData eventData)
        {
            var ghostObject = new GameObject(
                "TileDragGhost",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            ghostObject.transform.SetParent(_rootCanvas.transform, false);

            _ghost = ghostObject.GetComponent<RectTransform>();
            _ghost.sizeDelta = new Vector2(64f, 64f);

            var image = ghostObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = sprite != null;
            image.raycastTarget = false;
            image.color = sprite != null
                ? new Color(1f, 1f, 1f, 0.85f)
                : new Color(UiPalette.CellOccupied.r, UiPalette.CellOccupied.g, UiPalette.CellOccupied.b, 0.85f);

            OnDrag(eventData);
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                if (Application.isPlaying)
                    Destroy(_ghost.gameObject);
                else
                    DestroyImmediate(_ghost.gameObject);
            }
            _ghost = null;
        }
    }

    internal sealed class BoardCellDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private int _x;
        private int _y;
        private Func<bool> _canInteract;
        private Func<int?> _selectedRackIndex;
        private Action<int, int> _setHoverCell;
        private Action _clearHover;
        private Action<int, int, int> _placeFromRack;
        private Action _refreshBoard;

        public void Configure(
            int x,
            int y,
            Func<bool> canInteract,
            Func<int?> selectedRackIndex,
            Action<int, int> setHoverCell,
            Action clearHover,
            Action<int, int, int> placeFromRack,
            Action refreshBoard)
        {
            _x = x;
            _y = y;
            _canInteract = canInteract;
            _selectedRackIndex = selectedRackIndex;
            _setHoverCell = setHoverCell;
            _clearHover = clearHover;
            _placeFromRack = placeFromRack;
            _refreshBoard = refreshBoard;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!TileDragState.IsDragging || TileDragState.RackIndex < 0)
                return;
            if (_canInteract != null && !_canInteract())
                return;

            _placeFromRack?.Invoke(TileDragState.RackIndex, _x, _y);
            TileDragState.MarkDropSucceeded();
            _clearHover?.Invoke();
            _refreshBoard?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!ShouldHighlight())
                return;

            _setHoverCell?.Invoke(_x, _y);
            _refreshBoard?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!ShouldHighlight())
                return;

            _clearHover?.Invoke();
            _refreshBoard?.Invoke();
        }

        private bool ShouldHighlight()
        {
            if (_canInteract != null && !_canInteract())
                return false;

            if (TileDragState.IsDragging)
                return true;

            return _selectedRackIndex?.Invoke().HasValue ?? false;
        }
    }
}
