using System.Collections.Generic;
using AMath.Core;
using AMath.Gameplay.Board;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tutorial.Scripted
{
    /// <summary>
    /// One authored turn in a tutorial match. Both the human and the bot
    /// seats play from this list — the bot never searches for a move.
    /// </summary>
    public sealed class ScriptedTutorialTurn
    {
        public ScriptedTutorialTurn(int playerId, IReadOnlyList<TilePlacement> placements)
        {
            PlayerId = playerId;
            Placements = placements ?? System.Array.Empty<TilePlacement>();
        }

        /// <summary>Seat that must play this turn.</summary>
        public int PlayerId { get; }

        /// <summary>Exact tiles and cells for this turn, in any place order.</summary>
        public IReadOnlyList<TilePlacement> Placements { get; }
    }

    /// <summary>
    /// Predetermined opening racks and turn list for the intro tutorial match.
    /// </summary>
    public sealed class ScriptedTutorialMatchScript
    {
        public const int HumanPlayerId = 0;
        public const int BotPlayerId = 1;

        private ScriptedTutorialMatchScript(
            MatchConfig config,
            IReadOnlyList<IReadOnlyList<byte>> openingRacks,
            IReadOnlyList<ScriptedTutorialTurn> turns)
        {
            Config = config;
            OpeningRacks = openingRacks;
            Turns = turns;
        }

        /// <summary>Match header (seed, seats, no turn timer).</summary>
        public MatchConfig Config { get; }

        /// <summary>Opening rack per seat index (must match <see cref="GameRules.RackSize"/>).</summary>
        public IReadOnlyList<IReadOnlyList<byte>> OpeningRacks { get; }

        /// <summary>Turns in play order.</summary>
        public IReadOnlyList<ScriptedTutorialTurn> Turns { get; }

        /// <summary>Intro lesson: player places 1+2=3 through center, bot answers with 2+2=4.</summary>
        public static ScriptedTutorialMatchScript Intro()
        {
            var config = new MatchConfig
            {
                RandomSeed = 20260825,
                TurnSeconds = 0,
                GameVersion = "tutorial",
                Players =
                {
                    new PlayerIdentity
                    {
                        PlayerId = HumanPlayerId,
                        PersistentGuid = "tutorial-human",
                        DisplayName = "You",
                        IsAi = false
                    },
                    new PlayerIdentity
                    {
                        PlayerId = BotPlayerId,
                        PersistentGuid = "tutorial-bot",
                        DisplayName = "Bot",
                        IsAi = true
                    }
                }
            };

            IReadOnlyList<byte>[] racks =
            {
                new byte[] { 1, Plus, 2, EqualsSign, 3, 8, 9, 0 },
                new byte[] { Plus, 2, EqualsSign, 4, 7, 8, 9, 0 }
            };

            ScriptedTutorialTurn[] turns =
            {
                new ScriptedTutorialTurn(HumanPlayerId, new[]
                {
                    P(1, 5, 7),
                    P(Plus, 6, 7),
                    P(2, 7, 7),
                    P(EqualsSign, 8, 7),
                    P(3, 9, 7)
                }),
                new ScriptedTutorialTurn(BotPlayerId, new[]
                {
                    P(Plus, 7, 8),
                    P(2, 7, 9),
                    P(EqualsSign, 7, 10),
                    P(4, 7, 11)
                })
            };

            return new ScriptedTutorialMatchScript(config, racks, turns);
        }

        private static TilePlacement P(byte tileId, int x, int y) => new()
        {
            TileId = tileId,
            X = (byte)x,
            Y = (byte)y,
            DeclaredAs = TilePlacement.NoDeclaration
        };
    }
}
