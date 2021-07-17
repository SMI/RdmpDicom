using NUnit.Framework;
using Rdmp.Dicom.Cache.Pipeline;
using ReusableLibraryCode.Progress;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rdmp.Dicom.Tests
{
    class PressureGaugeTests
    {
        [Test]
        public void TestGauge_NotReached()
        {
            bool someFact = false;

            var g = new PressureGauge();
            g.ThresholdBeatsPerMinute = 4;
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
        }
        [Test]
        public void TestGauge_NotReached_OverTime()
        {
            bool someFact = false;

            var g = new PressureGauge();
            g.ThresholdBeatsPerMinute = 1;

            // events are 1 minute appart so does not trigger
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 02, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 03, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
        }
        [Test]
        public void TestGauge_Reached()
        {
            bool someFact = false;

            var g = new PressureGauge();
            g.ThresholdBeatsPerMinute = 4;
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsTrue(someFact);
        }

        [Test]
        public void TestGauge_Reached_OverTime()
        {
            bool someFact = false;

            var g = new PressureGauge();
            g.ThresholdBeatsPerMinute = 1;
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 30), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsTrue(someFact);
        }
        [Test]
        public void TestGauge_Reached_OverTime_Boundary()
        {
            bool someFact = false;

            var g = new PressureGauge();
            g.ThresholdBeatsPerMinute = 1;
            g.Tick(new DateTime(2001, 01, 01, 01, 01, 30), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsFalse(someFact);
            g.Tick(new DateTime(2001, 01, 01, 01, 02, 29), new ThrowImmediatelyDataLoadEventListener(), () => someFact = true);
            Assert.IsTrue(someFact);
        }
    }
}
