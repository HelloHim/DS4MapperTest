using DS4MapperTest.Common;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class FlickSnappingTests
    {
        private const double Tolerance = 0.0000001;

        private static double Degrees(double radians) => radians * 180.0 / Math.PI;

        private static double Radians(double degrees) => degrees * Math.PI / 180.0;

        [TestMethod]
        public void SnappingOffLeavesAngleUnchanged()
        {
            double angle = Radians(37.0);

            double result = FlickSnapping.Apply(angle, FlickSnapAngle.Off, 1.0);

            Assert.AreEqual(angle, result, Tolerance);
        }

        [TestMethod]
        public void ZeroStrengthLeavesAngleUnchanged()
        {
            double angle = Radians(37.0);

            double result = FlickSnapping.Apply(angle, FlickSnapAngle.Ninety, 0.0);

            Assert.AreEqual(angle, result, Tolerance);
        }

        [TestMethod]
        public void FourDirectionSnapRoundsToNearestQuarterTurn()
        {
            Assert.AreEqual(90.0,
                Degrees(FlickSnapping.Apply(Radians(80.0), FlickSnapAngle.Ninety, 1.0)),
                Tolerance);
            Assert.AreEqual(0.0,
                Degrees(FlickSnapping.Apply(Radians(20.0), FlickSnapAngle.Ninety, 1.0)),
                Tolerance);
            Assert.AreEqual(180.0,
                Degrees(FlickSnapping.Apply(Radians(160.0), FlickSnapAngle.Ninety, 1.0)),
                Tolerance);
        }

        [TestMethod]
        public void FourDirectionSnapIgnoresDiagonals()
        {
            // 45 deg is a diagonal, so four-direction snapping must not land there.
            double result = Degrees(FlickSnapping.Apply(Radians(50.0),
                FlickSnapAngle.Ninety, 1.0));

            Assert.AreEqual(90.0, result, Tolerance);
        }

        [TestMethod]
        public void EightDirectionSnapRoundsToNearestEighthTurn()
        {
            Assert.AreEqual(45.0,
                Degrees(FlickSnapping.Apply(Radians(50.0), FlickSnapAngle.FortyFive, 1.0)),
                Tolerance);
            Assert.AreEqual(135.0,
                Degrees(FlickSnapping.Apply(Radians(130.0), FlickSnapAngle.FortyFive, 1.0)),
                Tolerance);
        }

        [TestMethod]
        public void NegativeAnglesSnapToNegativeDirections()
        {
            Assert.AreEqual(-90.0,
                Degrees(FlickSnapping.Apply(Radians(-80.0), FlickSnapAngle.Ninety, 1.0)),
                Tolerance);
            Assert.AreEqual(-45.0,
                Degrees(FlickSnapping.Apply(Radians(-50.0), FlickSnapAngle.FortyFive, 1.0)),
                Tolerance);
        }

        [TestMethod]
        public void PartialStrengthBiasesTowardNearestDirection()
        {
            // Halfway between the raw 80 deg flick and the snapped 90 deg one.
            double result = Degrees(FlickSnapping.Apply(Radians(80.0),
                FlickSnapAngle.Ninety, 0.5));

            Assert.AreEqual(85.0, result, Tolerance);
        }

        [TestMethod]
        public void MidpointRoundsAwayFromZeroLikeJsm()
        {
            // C++ round() sends exact midpoints away from zero. Banker's rounding,
            // the .NET default, would send 45 deg to 0 and -45 deg to 0 instead.
            Assert.AreEqual(90.0,
                Degrees(FlickSnapping.Apply(Radians(45.0), FlickSnapAngle.Ninety, 1.0)),
                Tolerance);
            Assert.AreEqual(-90.0,
                Degrees(FlickSnapping.Apply(Radians(-45.0), FlickSnapAngle.Ninety, 1.0)),
                Tolerance);
        }

        [TestMethod]
        public void StrengthIsClampedToUnitRange()
        {
            Assert.AreEqual(90.0,
                Degrees(FlickSnapping.Apply(Radians(80.0), FlickSnapAngle.Ninety, 5.0)),
                Tolerance);
            Assert.AreEqual(80.0,
                Degrees(FlickSnapping.Apply(Radians(80.0), FlickSnapAngle.Ninety, -5.0)),
                Tolerance);
        }

        [TestMethod]
        public void SnapIntervalsMatchJsmModes()
        {
            Assert.AreEqual(Math.PI / 2.0,
                FlickSnapping.SnapIntervalRadians(FlickSnapAngle.Ninety), Tolerance);
            Assert.AreEqual(Math.PI / 4.0,
                FlickSnapping.SnapIntervalRadians(FlickSnapAngle.FortyFive), Tolerance);
        }

        [TestMethod]
        public void AlreadyAlignedAngleIsUnmoved()
        {
            double angle = Radians(180.0);

            double result = FlickSnapping.Apply(angle, FlickSnapAngle.Ninety, 1.0);

            Assert.AreEqual(180.0, Degrees(result), Tolerance);
        }
    }
}
