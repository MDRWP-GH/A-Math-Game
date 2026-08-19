using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Assistance;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;

namespace AMath.Gameplay.Interaction
{
    /// <summary>
    /// Local draft for the human player's turn: rack selection, pending
    /// placements, preview validation/score, then confirm via the event bus.
    /// Never mutates authoritative match state.
    /// </summary>
    public sealed class TurnInputSession : ISelectionStateReader
    {
        private readonly IEventBus _eventBus;
        private readonly BoardManager _boardManager;
        private readonly PlayerManager _playerManager;
        private readonly ScoreCalculator _scoreCalculator = new();
        private readonly List<TilePlacement> _pending = new(GameRules.RackSize);
        private readonly List<byte> _rackScratch = new(GameRules.RackSize);

        private int? _selectedRackIndex;
        private byte? _pendingDeclaration;

        public TurnInputSession(
            IEventBus eventBus,
            BoardManager boardManager,
            PlayerManager playerManager)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _boardManager = boardManager ?? throw new ArgumentNullException(nameof(boardManager));
            _playerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
        }

        /// <inheritdoc />
        public byte? SelectedTileId
        {
            get
            {
                if (!_selectedRackIndex.HasValue) return null;
                PlayerState local = LocalPlayer;
                if (local == null) return null;
                int index = _selectedRackIndex.Value;
                return index >= 0 && index < local.Rack.Count ? local.Rack[index] : null;
            }
        }

        /// <summary>Selected index into the local rack, or null.</summary>
        public int? SelectedRackIndex => _selectedRackIndex;

        /// <summary>Draft placements not yet submitted.</summary>
        public IReadOnlyList<TilePlacement> PendingPlacements => _pending;

        /// <summary>Last preview validation (updated after each draft change).</summary>
        public PlacementValidation PreviewValidation { get; private set; } = new();

        /// <summary>Estimated score when the preview is valid; otherwise 0.</summary>
        public int PreviewScore { get; private set; }

        /// <summary>Detailed A-Math score breakdown when the draft is valid; otherwise null.</summary>
        public PlacementScoreBreakdown PreviewBreakdown { get; private set; }

        /// <summary>Declaration waiting to be applied for the next flexible tile place.</summary>
        public byte? PendingDeclaration
        {
            get => _pendingDeclaration;
            set => _pendingDeclaration = value;
        }

        /// <summary>Raised whenever draft/selection/preview changes.</summary>
        public event Action Changed;

        public void SelectFromRack(int rackIndex)
        {
            PlayerState local = LocalPlayer;
            if (local == null || rackIndex < 0 || rackIndex >= local.Rack.Count)
            {
                ClearSelection();
                return;
            }

            _selectedRackIndex = rackIndex;
            _pendingDeclaration = null;
            byte tileId = local.Rack[rackIndex];
            _eventBus.Publish(new TileDraggedEvent { TileId = tileId });
            RaiseChanged();
        }

        public void ClearSelection()
        {
            _selectedRackIndex = null;
            _pendingDeclaration = null;
            RaiseChanged();
        }

        public bool TryPlaceOnCell(int x, int y, out string error)
        {
            error = null;
            PlayerState local = LocalPlayer;
            if (local == null)
            {
                error = "Local player is not ready.";
                return false;
            }

            if (!_selectedRackIndex.HasValue)
            {
                error = "Select a tile from your rack first.";
                return false;
            }

            int rackIndex = _selectedRackIndex.Value;
            if (rackIndex < 0 || rackIndex >= local.Rack.Count)
            {
                error = "Invalid rack selection.";
                return false;
            }

            if (!BoardGrid.InBounds(x, y))
            {
                error = "Outside the board.";
                return false;
            }

            if (_boardManager.Grid.IsOccupied(x, y) || ContainsPending(x, y))
            {
                error = "That cell is already filled.";
                return false;
            }

            byte tileId = local.Rack[rackIndex];
            byte declaredAs = TilePlacement.NoDeclaration;
            if (AMathTileSet.RequiresDeclaration(tileId))
            {
                if (!_pendingDeclaration.HasValue
                    || !AMathTileSet.IsLegalDeclaration(tileId, _pendingDeclaration.Value))
                {
                    error = $"Choose what {AMathTileSet.SymbolOf(tileId)} should be before placing.";
                    return false;
                }

                declaredAs = _pendingDeclaration.Value;
            }

            // Build a virtual rack that excludes tiles already in the draft.
            BuildAvailableRack(local.Rack, _rackScratch);
            if (!_rackScratch.Remove(tileId))
            {
                error = "That tile is already used in the draft.";
                return false;
            }

            _pending.Add(new TilePlacement
            {
                TileId = tileId,
                X = (byte)x,
                Y = (byte)y,
                DeclaredAs = declaredAs
            });

            _selectedRackIndex = null;
            _pendingDeclaration = null;
            RefreshPreview(local.Rack);
            RaiseChanged();
            return true;
        }

        public void RemovePendingAt(int x, int y)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].X == x && _pending[i].Y == y)
                {
                    _pending.RemoveAt(i);
                    RefreshPreview(LocalPlayer?.Rack);
                    RaiseChanged();
                    return;
                }
            }
        }

        public void ClearDraft()
        {
            _pending.Clear();
            ClearSelection();
            PreviewValidation = new PlacementValidation();
            PreviewScore = 0;
            PreviewBreakdown = null;
            RaiseChanged();
        }

        public bool TryConfirmPlace(out string error)
        {
            error = null;
            PlayerState local = LocalPlayer;
            if (local == null)
            {
                error = "Local player is not ready.";
                return false;
            }

            if (_pending.Count == 0)
            {
                error = "Place at least one tile first.";
                return false;
            }

            RefreshPreview(local.Rack);
            if (!PreviewValidation.IsValid)
            {
                error = PreviewValidation.Error ?? "Invalid placement.";
                return false;
            }

            var command = new PlaceTilesCommand();
            command.Placements.AddRange(_pending);
            _eventBus.Publish(new LocalCommandRequestedEvent { Command = command });
            ClearDraft();
            return true;
        }

        public void RequestPass()
        {
            _eventBus.Publish(new LocalCommandRequestedEvent { Command = new PassTurnCommand() });
            ClearDraft();
        }

        public bool TryRequestExchange(IReadOnlyList<int> rackIndices, out string error)
        {
            error = null;
            PlayerState local = LocalPlayer;
            if (local == null)
            {
                error = "Local player is not ready.";
                return false;
            }

            if (rackIndices == null || rackIndices.Count == 0)
            {
                error = "Select tiles to exchange.";
                return false;
            }

            var command = new ExchangeTilesCommand();
            var used = new HashSet<int>();
            for (int i = 0; i < rackIndices.Count; i++)
            {
                int index = rackIndices[i];
                if (!used.Add(index) || index < 0 || index >= local.Rack.Count)
                {
                    error = "Invalid exchange selection.";
                    return false;
                }

                command.TileIds.Add(local.Rack[index]);
            }

            _eventBus.Publish(new LocalCommandRequestedEvent { Command = command });
            ClearDraft();
            return true;
        }

        private PlayerState LocalPlayer =>
            _playerManager.LocalPlayerId >= 0
                ? _playerManager.GetById(_playerManager.LocalPlayerId)
                : null;

        private void RefreshPreview(IReadOnlyList<byte> rack)
        {
            if (rack == null || _pending.Count == 0)
            {
                PreviewValidation = new PlacementValidation();
                PreviewScore = 0;
                PreviewBreakdown = null;
                return;
            }

            PreviewValidation = _boardManager.Validate(rack, _pending);
            if (PreviewValidation.IsValid)
            {
                PreviewBreakdown = _scoreCalculator.CalculateDetailed(PreviewValidation.Lines, _pending.Count);
                PreviewScore = PreviewBreakdown.Total;
            }
            else
            {
                PreviewBreakdown = null;
                PreviewScore = 0;
            }
        }

        private void BuildAvailableRack(IReadOnlyList<byte> rack, List<byte> into)
        {
            into.Clear();
            into.AddRange(rack);
            for (int i = 0; i < _pending.Count; i++)
                into.Remove(_pending[i].TileId);
        }

        private bool ContainsPending(int x, int y)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].X == x && _pending[i].Y == y)
                    return true;
            }

            return false;
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
