using System.Collections.Generic;
using AMath.Core;

namespace AMath.Gameplay.Board
{
    /// <summary>
    /// Validates that a line of tiles forms a mathematically correct A-Math
    /// equation. Runs only from validated placements on the host (and in
    /// replay/verification on clients).
    ///
    /// Uses exact rational arithmetic (long numerator/denominator) instead of
    /// floating point so results like 1÷3×3=1 validate exactly and every peer
    /// agrees bit-for-bit — a requirement for deterministic lockstep.
    ///
    /// Rules implemented:
    ///  - the line must contain at least one '=' and every '='-separated
    ///    segment must evaluate to the same value;
    ///  - x/÷ binds tighter than +/- (standard precedence, left-to-right);
    ///  - single-digit tiles (0-9) may combine into numbers of up to
    ///    <see cref="GameRules.MaxNumberDigits"/> digits, without leading zeros;
    ///  - double-digit tiles (10-20) always stand alone;
    ///  - a single unary minus is allowed at the start of a segment (e.g. 3-5=-2).
    /// </summary>
    public static class EquationEvaluator
    {
        #region Public API

        /// <summary>
        /// Checks a full line of *effective* tile ids (declarations already resolved).
        /// </summary>
        public static bool IsValidEquation(IReadOnlyList<byte> effectiveIds, out string error)
        {
            error = null;

            if (effectiveIds == null || effectiveIds.Count < GameRules.MinEquationLength)
            {
                error = "Equation is too short.";
                return false;
            }

            // Split into '='-separated segments.
            var segments = new List<List<byte>>(3) { new List<byte>(8) };
            for (int i = 0; i < effectiveIds.Count; i++)
            {
                byte id = effectiveIds[i];
                if (id == AMathTileSet.EqualsSign)
                    segments.Add(new List<byte>(8));
                else
                    segments[^1].Add(id);
            }

            if (segments.Count < 2)
            {
                error = "Equation must contain '='.";
                return false;
            }

            Fraction? expected = null;
            for (int s = 0; s < segments.Count; s++)
            {
                if (!TryEvaluateSegment(segments[s], out Fraction value, out error))
                    return false;

                if (expected.HasValue && !expected.Value.Equals(value))
                {
                    error = "Both sides of '=' are not equal.";
                    return false;
                }

                expected = value;
            }

            return true;
        }

        #endregion

        #region Segment evaluation

        private struct Token
        {
            public bool IsNumber;
            public long Value;   // when IsNumber
            public byte Operator; // when !IsNumber
        }

        private static bool TryEvaluateSegment(List<byte> ids, out Fraction result, out string error)
        {
            result = default;
            if (!TryTokenize(ids, out List<Token> tokens, out error))
                return false;

            if (tokens.Count == 0)
            {
                error = "Empty expression next to '='.";
                return false;
            }

            int index = 0;
            long sign = 1;

            // Optional single unary minus at segment start.
            if (!tokens[0].IsNumber)
            {
                if (tokens[0].Operator == AMathTileSet.Minus && tokens.Count > 1)
                {
                    sign = -1;
                    index = 1;
                }
                else
                {
                    error = "Expression cannot start with an operator.";
                    return false;
                }
            }

            // expr := term { (+|-) term }, term := number { (x|÷) number }
            if (!TryReadTerm(tokens, ref index, out Fraction accumulated, out error))
                return false;
            accumulated = accumulated.Multiply(sign);

            while (index < tokens.Count)
            {
                Token op = tokens[index];
                if (op.IsNumber)
                {
                    error = "Two numbers without an operator.";
                    return false;
                }

                index++;
                if (!TryReadTerm(tokens, ref index, out Fraction term, out error))
                    return false;

                if (op.Operator == AMathTileSet.Plus)
                    accumulated = accumulated.Add(term);
                else if (op.Operator == AMathTileSet.Minus)
                    accumulated = accumulated.Subtract(term);
                else
                {
                    error = "Malformed expression.";
                    return false;
                }
            }

            result = accumulated;
            return true;
        }

        private static bool TryReadTerm(List<Token> tokens, ref int index, out Fraction term, out string error)
        {
            term = default;
            error = null;

            if (index >= tokens.Count || !tokens[index].IsNumber)
            {
                error = "Operator is missing a number.";
                return false;
            }

            term = new Fraction(tokens[index].Value);
            index++;

            while (index + 1 < tokens.Count
                   && !tokens[index].IsNumber
                   && (tokens[index].Operator == AMathTileSet.Times || tokens[index].Operator == AMathTileSet.Divide))
            {
                byte op = tokens[index].Operator;
                Token right = tokens[index + 1];
                if (!right.IsNumber)
                {
                    error = "Operator is missing a number.";
                    return false;
                }

                if (op == AMathTileSet.Times)
                {
                    term = term.Multiply(right.Value);
                }
                else
                {
                    if (right.Value == 0)
                    {
                        error = "Division by zero.";
                        return false;
                    }
                    term = term.Divide(right.Value);
                }

                index += 2;
            }

            // Trailing x/÷ with nothing after it.
            if (index < tokens.Count && !tokens[index].IsNumber
                && (tokens[index].Operator == AMathTileSet.Times || tokens[index].Operator == AMathTileSet.Divide))
            {
                error = "Expression ends with an operator.";
                return false;
            }

            return true;
        }

        #endregion

        #region Tokenization

        private static bool TryTokenize(List<byte> ids, out List<Token> tokens, out string error)
        {
            tokens = new List<Token>(ids.Count);
            error = null;

            int i = 0;
            while (i < ids.Count)
            {
                byte id = ids[i];

                if (AMathTileSet.IsResolvedOperator(id))
                {
                    tokens.Add(new Token { IsNumber = false, Operator = id });
                    i++;
                    continue;
                }

                if (AMathTileSet.IsDoubleDigit(id))
                {
                    // 10..20 tiles must stand alone between operators.
                    bool prevIsNumber = i > 0 && AMathTileSet.IsNumber(ids[i - 1]);
                    bool nextIsNumber = i + 1 < ids.Count && AMathTileSet.IsNumber(ids[i + 1]);
                    if (prevIsNumber || nextIsNumber)
                    {
                        error = $"Tile {AMathTileSet.SymbolOf(id)} cannot join another number.";
                        return false;
                    }

                    tokens.Add(new Token { IsNumber = true, Value = id });
                    i++;
                    continue;
                }

                if (AMathTileSet.IsSingleDigit(id))
                {
                    // Combine a run of single digits into one number.
                    int runStart = i;
                    long value = 0;
                    while (i < ids.Count && AMathTileSet.IsSingleDigit(ids[i]))
                    {
                        value = value * 10 + ids[i];
                        i++;
                    }

                    int digits = i - runStart;
                    if (digits > GameRules.MaxNumberDigits)
                    {
                        error = $"Numbers may have at most {GameRules.MaxNumberDigits} digits.";
                        return false;
                    }

                    if (digits > 1 && ids[runStart] == 0)
                    {
                        error = "Numbers cannot have a leading zero.";
                        return false;
                    }

                    tokens.Add(new Token { IsNumber = true, Value = value });
                    continue;
                }

                // Blank / +/- / x/÷ must have been resolved via DeclaredAs before evaluation.
                error = $"Tile {AMathTileSet.SymbolOf(id)} was not declared.";
                return false;
            }

            return true;
        }

        #endregion

        #region Fraction

        /// <summary>Exact rational number used for equation evaluation.</summary>
        private readonly struct Fraction
        {
            private readonly long _numerator;
            private readonly long _denominator; // always > 0

            public Fraction(long value)
            {
                _numerator = value;
                _denominator = 1;
            }

            private Fraction(long numerator, long denominator)
            {
                if (denominator < 0)
                {
                    numerator = -numerator;
                    denominator = -denominator;
                }

                long gcd = Gcd(System.Math.Abs(numerator), denominator);
                if (gcd > 1)
                {
                    numerator /= gcd;
                    denominator /= gcd;
                }

                _numerator = numerator;
                _denominator = denominator == 0 ? 1 : denominator;
            }

            public Fraction Add(Fraction other) =>
                new(_numerator * other._denominator + other._numerator * _denominator, _denominator * other._denominator);

            public Fraction Subtract(Fraction other) =>
                new(_numerator * other._denominator - other._numerator * _denominator, _denominator * other._denominator);

            public Fraction Multiply(long value) => new(_numerator * value, _denominator);

            public Fraction Divide(long value) => new(_numerator, _denominator * value);

            public bool Equals(Fraction other) =>
                _numerator == other._numerator && _denominator == other._denominator;

            private static long Gcd(long a, long b)
            {
                while (b != 0)
                    (a, b) = (b, a % b);
                return a == 0 ? 1 : a;
            }
        }

        #endregion
    }
}
