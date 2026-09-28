using System;
using System.IO;
using AMath.Accounts;
using AMath.Core;
using AMath.Core.Commands;
using AMath.Core.Events;
using AMath.Core.StateMachines;
using AMath.Gameplay.Board;
using AMath.Gameplay.Players;
using AMath.Managers;
using AMath.Replay;
using AMath.Save;
using NUnit.Framework;

namespace AMath.Tests
{
    /// <summary>
    /// Reproducible gaps from the project-scope test-case supplement. All data is
    /// isolated under a temporary root; no installed player's save is touched.
    /// </summary>
    public sealed class ResearchScopeRegressionTests
    {
        private string _root;
        private LocalAccountService _accountService;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "amath-research-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            _accountService?.Logout();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void Research_ACC05_LogoutInvalidatesAutoLoginButPreservesAccount()
        {
            var service = new LocalAccountService(_root);
            _accountService = service;
            Assert.IsTrue(service.TryRegister("tc_account_a", "Testpass42", out AccountError registerError), registerError.ToString());
            string profileRoot = AccountSession.ProfileRoot;
            string accountFile = Path.Combine(profileRoot, "account.json");
            Assert.IsTrue(File.Exists(accountFile));

            service.Logout();

            Assert.IsFalse(AccountSession.IsAuthenticated);
            Assert.IsTrue(File.Exists(accountFile));
            var restarted = new LocalAccountService(_root);
            Assert.IsFalse(restarted.TryAutoLogin(out AccountError autoError));
            Assert.AreEqual(AccountError.None, autoError);
            Assert.IsTrue(restarted.TryLogin("tc_account_a", "Testpass42", out AccountError loginError), loginError.ToString());
            Assert.AreEqual(profileRoot, AccountSession.ProfileRoot);
            restarted.Logout();
        }

        [Test]
        public void Research_TIME01_HostTimeoutOccursAtBoundaryAndRecordsOnePass()
        {
            const int seed = 4242;
            const int turnSeconds = 60;
            var bus = new EventBus();
            var stateMachine = new GameStateMachine(bus);
            var players = new PlayerManager(bus);
            var turns = new TurnManager(bus);
            var game = new GameManager(bus, stateMachine, new BoardManager(), players, turns)
            {
                IsAuthority = true
            };
            using var replay = new ReplayManager(bus);
            var config = new MatchConfig
            {
                RandomSeed = seed,
                TurnSeconds = turnSeconds,
                GameVersion = "research-test",
                Players =
                {
                    new PlayerIdentity { PlayerId = 0, PersistentGuid = "research-a", DisplayName = "A" },
                    new PlayerIdentity { PlayerId = 1, PersistentGuid = "research-b", DisplayName = "B" }
                }
            };

            game.StartMatch(config);
            game.Tick(59f);
            Assert.AreEqual(1, turns.TurnNumber);
            Assert.AreEqual(1f, turns.RemainingSeconds);
            Assert.AreEqual(0, replay.Log.Events.Count);

            game.Tick(1f);
            Assert.AreEqual(2, turns.TurnNumber);
            Assert.AreEqual(1, turns.CurrentPlayerId);
            Assert.AreEqual(1, replay.Log.Events.Count);
            var record = replay.Log.Events[0].ToRecord();
            var timeoutPass = CommandSerializer.Deserialize((CommandType)record.CommandType, record.CommandPayload) as PassTurnCommand;
            Assert.IsNotNull(timeoutPass);
            Assert.IsTrue(timeoutPass.WasTimeout);

            game.Tick(0f);
            Assert.AreEqual(1, replay.Log.Events.Count);
        }

        [Test]
        public void Research_HIST01_NewProfileHasAnEmptyReadableHistory()
        {
            string historyRoot = Path.Combine(_root, "History");
            var store = new MatchHistoryStore(historyRoot);

            Assert.IsTrue(store.TryListEntries(out var entries, out MatchHistoryReadStatus status, out string error), error);
            Assert.AreEqual(MatchHistoryReadStatus.Ok, status);
            Assert.IsEmpty(entries);
            Assert.IsTrue(Directory.Exists(historyRoot));
            Assert.IsFalse(File.Exists(Path.Combine(historyRoot, "history_index.json")));
        }
    }
}
