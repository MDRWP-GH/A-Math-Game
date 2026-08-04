using System.Collections.Generic;
using AMath.Gameplay.Board;
using NUnit.Framework;
using static AMath.Gameplay.Board.AMathTileSet;

namespace AMath.Tests
{
    public sealed class EquationEvaluatorTests
    {
        private static bool Check(params byte[] ids) =>
            EquationEvaluator.IsValidEquation(new List<byte>(ids), out _);

        [Test]
        public void SimpleAddition_IsValid() =>
            Assert.IsTrue(Check(1, Plus, 2, EqualsSign, 3));

        [Test]
        public void WrongResult_IsInvalid() =>
            Assert.IsFalse(Check(1, Plus, 2, EqualsSign, 4));

        [Test]
        public void MultiDigitNumbers_Combine() =>
            Assert.IsTrue(Check(1, 2, EqualsSign, 1, 2)); // 12 = 12

        [Test]
        public void LeadingZero_IsInvalid() =>
            Assert.IsFalse(Check(0, 5, EqualsSign, 5)); // 05 = 5

        [Test]
        public void MissingEquals_IsInvalid() =>
            Assert.IsFalse(Check(1, Plus, 2, Plus, 3));

        [Test]
        public void OperatorPrecedence_MultiplicationFirst() =>
            Assert.IsTrue(Check(1, Plus, 2, Times, 3, EqualsSign, 7)); // 1+2x3 = 7

        [Test]
        public void ExactFractions_EvaluateCorrectly() =>
            Assert.IsTrue(Check(1, Divide, 3, Times, 3, EqualsSign, 1)); // (1÷3)x3 = 1

        [Test]
        public void DivisionByZero_IsInvalid() =>
            Assert.IsFalse(Check(5, Divide, 0, EqualsSign, 5));

        [Test]
        public void UnaryMinusAfterEquals_IsValid() =>
            Assert.IsTrue(Check(3, Minus, 5, EqualsSign, Minus, 2)); // 3-5 = -2

        [Test]
        public void DoubleDigitTile_CannotJoinAnotherNumber() =>
            Assert.IsFalse(Check(12, 2, EqualsSign, 12, 2)); // "12" tile next to "2" tile

        [Test]
        public void DoubleDigitTile_StandsAloneCorrectly() =>
            Assert.IsTrue(Check(12, EqualsSign, 3, Times, 4)); // 12 = 3x4

        [Test]
        public void ThreeSegments_AllMustMatch()
        {
            Assert.IsTrue(Check(6, EqualsSign, 2, Times, 3, EqualsSign, 6));
            Assert.IsFalse(Check(6, EqualsSign, 2, Times, 3, EqualsSign, 7));
        }

        [Test]
        public void UndeclaredFlexibleTile_IsInvalid() =>
            Assert.IsFalse(Check(1, PlusOrMinus, 2, EqualsSign, 3));

        [Test]
        public void FourDigitNumber_IsInvalid() =>
            Assert.IsFalse(Check(1, 2, 3, 4, EqualsSign, 1, 2, 3, 4));
    }
}
