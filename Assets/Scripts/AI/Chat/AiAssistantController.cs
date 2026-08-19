using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AMath.AI.Interfaces;
using AMath.AI.Modes;
using AMath.Core.Assistance;
using AMath.Core.Assistance.Context;

namespace AMath.AI.Chat
{
    /// <summary>
    /// Coordinates the optional AI chat: captures fresh safe context, routes
    /// a player question to the selected independent mode, filters the output
    /// and presents it. It is also the Core <see cref="IAiEntryPoint"/> that
    /// Tutorial UI may optionally receive; opening chat never changes tutorial
    /// or gameplay state.
    /// </summary>
    public sealed class AiAssistantController : IAiEntryPoint, IDisposable
    {
        private readonly Dictionary<string, IAiAssistantMode> _modes = new();
        private readonly IGameContextProvider _contextProvider;
        private readonly IAiRestrictionGuard _restrictionGuard;
        private readonly IAiChatView _view;
        private readonly ILocalizedTextProvider _textProvider;

        private CancellationTokenSource _requestCancellation;
        private IAiAssistantMode _selectedMode;

        /// <summary>Creates the assistant from independently testable collaborators.</summary>
        public AiAssistantController(
            IEnumerable<IAiAssistantMode> modes,
            IGameContextProvider contextProvider,
            IAiRestrictionGuard restrictionGuard,
            IAiChatView view,
            ILocalizedTextProvider textProvider)
        {
            if (modes == null) throw new ArgumentNullException(nameof(modes));
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _restrictionGuard = restrictionGuard ?? throw new ArgumentNullException(nameof(restrictionGuard));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));

            foreach (IAiAssistantMode mode in modes)
            {
                if (mode == null || string.IsNullOrWhiteSpace(mode.ModeId))
                    throw new ArgumentException("Every AI mode requires a stable id.", nameof(modes));
                if (_modes.ContainsKey(mode.ModeId))
                    throw new ArgumentException($"Duplicate AI mode id '{mode.ModeId}'.", nameof(modes));

                _modes.Add(mode.ModeId, mode);
                _selectedMode ??= mode;
            }
        }

        /// <inheritdoc />
        public bool IsAvailable { get; private set; } = true;

        /// <summary>Id of the mode that will answer the next question.</summary>
        public string SelectedModeId => _selectedMode?.ModeId;

        /// <summary>Whether a mode id was registered; the view hides affordances for the rest.</summary>
        public bool HasMode(string modeId) =>
            !string.IsNullOrWhiteSpace(modeId) && _modes.ContainsKey(modeId);

        /// <summary>Enables/disables AI availability without affecting Tutorial operation.</summary>
        public void SetAvailable(bool available)
        {
            IsAvailable = available;
            if (!available)
                CancelRequest();
        }

        /// <summary>Selects a registered assistant mode by stable id.</summary>
        public bool TrySelectMode(string modeId)
        {
            if (string.IsNullOrWhiteSpace(modeId) || !_modes.TryGetValue(modeId, out IAiAssistantMode mode))
                return false;

            _selectedMode = mode;
            return true;
        }

        /// <inheritdoc />
        public void OpenChat()
        {
            if (IsAvailable)
                _view.Open();
        }

        /// <summary>
        /// Asks the selected mode. A second question cancels the previous
        /// request so stale answers cannot appear after newer ones.
        /// </summary>
        public async Task AskAsync(string question)
        {
            if (!IsAvailable || _selectedMode == null)
            {
                _view.ShowError(_textProvider.GetText(AiLocalizationKeys.ModeUnavailable));
                return;
            }

            if (string.IsNullOrWhiteSpace(question))
            {
                _view.ShowError(_textProvider.GetText(AiLocalizationKeys.EmptyQuestion));
                return;
            }

            CancelRequest();
            _requestCancellation = new CancellationTokenSource();
            CancellationToken token = _requestCancellation.Token;
            _view.ShowUserMessage(question.Trim());
            _view.SetBusy(true);

            try
            {
                GameContextSnapshot context = _contextProvider.Capture();
                string response = await _selectedMode.RespondAsync(question, context, token);
                if (!token.IsCancellationRequested)
                {
                    if (_restrictionGuard.TryFilter(response, out string safeResponse))
                        _view.ShowAssistantMessage(safeResponse);
                    else
                        _view.ShowError(_textProvider.GetText(AiLocalizationKeys.UnsafeResponse));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when a new question replaces an in-flight request.
            }
            catch (AiModeNotReadyException)
            {
                if (!token.IsCancellationRequested)
                    _view.ShowError(_textProvider.GetText(AiLocalizationKeys.ModeNotReady));
            }
            catch (AiBackendException exception)
            {
                if (!token.IsCancellationRequested)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[AI] Backend request failed with HTTP status {exception.StatusCode}.");
                    _view.ShowError(_textProvider.GetText(AiLocalizationKeys.RequestFailed));
                }
            }
            catch (Exception)
            {
                if (!token.IsCancellationRequested)
                {
                    UnityEngine.Debug.LogWarning("[AI] Assistant request failed.");
                    _view.ShowError(_textProvider.GetText(AiLocalizationKeys.RequestFailed));
                }
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    _view.SetBusy(false);
            }
        }

        /// <summary>Cancels an in-flight backend call.</summary>
        public void CancelRequest()
        {
            _requestCancellation?.Cancel();
            _requestCancellation?.Dispose();
            _requestCancellation = null;
            _view.SetBusy(false);
        }

        /// <inheritdoc />
        public void Dispose() => CancelRequest();
    }
}
