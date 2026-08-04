using System;

namespace AMath.Gameplay.Board
{
    /// <summary>
    /// One tile being placed on one cell.
    /// <see cref="DeclaredAs"/> resolves flexible tiles at placement time:
    /// blank tiles declare which tile they represent, and the "+/-" and "x/÷"
    /// tiles declare which operator they act as. 255 means "not declared".
    /// The struct is 4 bytes, keeping command payloads tiny for LAN play.
    /// </summary>
    [Serializable]
    public struct TilePlacement : IEquatable<TilePlacement>
    {
        /// <summary>No declaration (regular tile).</summary>
        public const byte NoDeclaration = 255;

        /// <summary>Tile id from <see cref="AMathTileSet"/> as it exists on the rack.</summary>
        public byte TileId;

        /// <summary>Board column, 0..14.</summary>
        public byte X;

        /// <summary>Board row, 0..14.</summary>
        public byte Y;

        /// <summary>Effective tile id for blank / dual-operator tiles; <see cref="NoDeclaration"/> otherwise.</summary>
        public byte DeclaredAs;

        /// <summary>The tile id used for equation evaluation (declaration wins when present).</summary>
        public readonly byte EffectiveTileId => DeclaredAs == NoDeclaration ? TileId : DeclaredAs;

        public readonly bool Equals(TilePlacement other) =>
            TileId == other.TileId && X == other.X && Y == other.Y && DeclaredAs == other.DeclaredAs;

        public override readonly bool Equals(object obj) => obj is TilePlacement other && Equals(other);

        public override readonly int GetHashCode() => (TileId << 24) | (DeclaredAs << 16) | (X << 8) | Y;
    }
}
