using System;
using System.Collections.Generic;
using AMath.Core;
using AMath.Core.Events;
using AMath.Networking;
using AMath.Networking.Room;
using AMath.Managers;
using Mirror;
using UnityEngine;

namespace AMath.UI
{
    /// <summary>Presenter for the post-match results screen.</summary>
    public sealed class MatchResultPresenter : MonoBehaviour
    {
        public event Action<MatchResult> ResultChanged;

        private IEventBus _eventBus;
        private RoomManager _roomManager;
        private MatchResult _result;

        public MatchResult Result => _result;
        public bool CanRematch => NetworkServer.active;

        private void Start()
        {
            if (NetworkContext.Services == null) return;
            _eventBus = NetworkContext.Services.Resolve<IEventBus>();
            _roomManager = NetworkContext.Services.Resolve<RoomManager>();
            _eventBus.Subscribe<MatchFinishedEvent>(OnFinished);
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<MatchFinishedEvent>(OnFinished);
        }

        private void OnFinished(MatchFinishedEvent evt)
        {
            _result = evt.Result;
            ResultChanged?.Invoke(_result);
        }

        public void Rematch()
        {
            if (_roomManager == null)
                return;

            MatchFormat format = NetworkContext.Services?.Resolve<RoomSession>()?.SelectedFormat
                                 ?? MatchFormat.Individual;
            _roomManager.StartMatch(format);
        }

        public void Leave()
        {
            _roomManager?.LeaveRoom();
        }
    }
}
