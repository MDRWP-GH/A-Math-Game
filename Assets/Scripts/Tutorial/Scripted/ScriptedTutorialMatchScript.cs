using System.Collections.Generic;

using AMath.Core;

using AMath.Gameplay.Board;

using AMath.Tutorial.Definitions;
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

    /// Predetermined opening racks and turn list for tutorial matches.

    /// Board geometry lives in <see cref="TutorialAuthoredBoard"/>.

    /// </summary>

    public sealed class ScriptedTutorialMatchScript

    {

        public const int HumanPlayerId = 0;

        public const int BotPlayerId = 1;



        private ScriptedTutorialMatchScript(

            MatchConfig config,

            IReadOnlyList<IReadOnlyList<byte>> openingRacks,

            IReadOnlyList<ScriptedTutorialTurn> turns,

            IReadOnlyList<TilePlacement> initialBoard = null,

            IReadOnlyList<byte> passLessonRack = null,

            IReadOnlyList<byte> exchangeLessonRack = null,

            IReadOnlyList<TilePlacement> lessonBoard = null)

        {

            Config = config;

            OpeningRacks = openingRacks;

            Turns = turns;

            InitialBoard = initialBoard ?? System.Array.Empty<TilePlacement>();

            PassLessonRack = passLessonRack ?? System.Array.Empty<byte>();

            ExchangeLessonRack = exchangeLessonRack ?? System.Array.Empty<byte>();

            LessonBoard = lessonBoard ?? System.Array.Empty<TilePlacement>();

        }



        /// <summary>Match header (seed, seats, no turn timer).</summary>

        public MatchConfig Config { get; }



        /// <summary>Opening rack per seat index (must match <see cref="GameRules.RackSize"/>).</summary>

        public IReadOnlyList<IReadOnlyList<byte>> OpeningRacks { get; }



        /// <summary>Turns in play order.</summary>

        public IReadOnlyList<ScriptedTutorialTurn> Turns { get; }



        /// <summary>Tiles already on the board before the lesson begins.</summary>

        public IReadOnlyList<TilePlacement> InitialBoard { get; }



        /// <summary>Rack used for the pass lesson when the player cannot form a move.</summary>

        public IReadOnlyList<byte> PassLessonRack { get; }



        /// <summary>Rack used for the exchange lesson.</summary>

        public IReadOnlyList<byte> ExchangeLessonRack { get; }



        /// <summary>Board state after the premium placement turn (pass/exchange lessons).</summary>

        public IReadOnlyList<TilePlacement> LessonBoard { get; }



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

                        IsAi = false,

                        ColorId = 1

                    },

                    new PlayerIdentity

                    {

                        PlayerId = BotPlayerId,

                        PersistentGuid = "tutorial-bot",

                        DisplayName = "Bot",

                        IsAi = true,

                        ColorId = 4

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

                new ScriptedTutorialTurn(HumanPlayerId, TutorialAuthoredBoard.Intro.HumanTurn),

                new ScriptedTutorialTurn(BotPlayerId, TutorialAuthoredBoard.Intro.BotTurn)

            };



            return new ScriptedTutorialMatchScript(config, racks, turns);

        }



        /// <summary>Chapter 2: board already has 1+2=3; player extends with +4=7.</summary>

        public static ScriptedTutorialMatchScript Connect()

        {

            var config = new MatchConfig

            {

                RandomSeed = 20260826,

                TurnSeconds = 0,

                GameVersion = "tutorial-connect",

                Players =

                {

                    new PlayerIdentity

                    {

                        PlayerId = HumanPlayerId,

                        PersistentGuid = "tutorial-human",

                        DisplayName = "You",

                        IsAi = false,

                        ColorId = 1

                    },

                    new PlayerIdentity

                    {

                        PlayerId = BotPlayerId,

                        PersistentGuid = "tutorial-bot",

                        DisplayName = "Bot",

                        IsAi = true,

                        ColorId = 4

                    }

                }

            };



            IReadOnlyList<byte>[] racks =

            {

                new byte[] { Plus, 4, EqualsSign, 7, 8, 9, 0, 6 },

                new byte[] { 1, Plus, 2, EqualsSign, 3, 8, 9, 0 }

            };



            ScriptedTutorialTurn[] turns =

            {

                new ScriptedTutorialTurn(HumanPlayerId, TutorialAuthoredBoard.Connect.HumanTurn)

            };



            return new ScriptedTutorialMatchScript(config, racks, turns, TutorialAuthoredBoard.Connect.Initial);

        }



        /// <summary>Chapter 3: premium squares, then pass and exchange with a stuck rack.</summary>

        public static ScriptedTutorialMatchScript PremiumSkills()

        {

            var config = new MatchConfig

            {

                RandomSeed = 20260827,

                TurnSeconds = 0,

                GameVersion = "tutorial-premium",

                Players =

                {

                    new PlayerIdentity

                    {

                        PlayerId = HumanPlayerId,

                        PersistentGuid = "tutorial-human",

                        DisplayName = "You",

                        IsAi = false,

                        ColorId = 1

                    },

                    new PlayerIdentity

                    {

                        PlayerId = BotPlayerId,

                        PersistentGuid = "tutorial-bot",

                        DisplayName = "Bot",

                        IsAi = true,

                        ColorId = 4

                    }

                }

            };



            IReadOnlyList<byte>[] racks =

            {

                new byte[] { Plus, 2, EqualsSign, 5, 8, 9, 0, 6 },

                new byte[] { 1, Plus, 2, EqualsSign, 3, 8, 9, 0 }

            };



            ScriptedTutorialTurn[] turns =

            {

                new ScriptedTutorialTurn(HumanPlayerId, TutorialAuthoredBoard.Premium.HumanTurn)

            };



            return new ScriptedTutorialMatchScript(

                config,

                racks,

                turns,

                TutorialAuthoredBoard.Premium.Initial,

                TutorialAuthoredBoard.Premium.PassRack,

                TutorialAuthoredBoard.Premium.ExchangeRack,

                TutorialAuthoredBoard.Premium.LessonBoard);

        }

    }

}


