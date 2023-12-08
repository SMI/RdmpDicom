using NUnit.Framework;
using Rdmp.Dicom.Cache.Pipeline;
using Rdmp.Core.ReusableLibraryCode.Progress;
using System;

namespace Rdmp.Dicom.Tests;

class PressureGaugeTests
{
    [Test]
    public void TestGauge_NotReached()
    {
        var someFact = false;

        var g = new PressureGauge
        {
            ThresholdBeatsPerMinute = 4
        };
        g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
    }
    [Test]
    public void TestGauge_NotReached_OverTime()
    {
        var someFact = false;

        var g = new PressureGauge
        {
            ThresholdBeatsPerMinute = 1
        };

        // events are 1 minute appart so does not trigger
        g.Tick(new(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 02, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 03, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
    }
    [Test]
    public void TestGauge_Reached()
    {
        var someFact = false;

        var g = new PressureGauge
        {
            ThresholdBeatsPerMinute = 4
        };
        g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact);
    }

    [Test]
    public void TestGauge_Reached_OverTime()
    {
        var someFact = false;

        var g = new PressureGauge
        {
            ThresholdBeatsPerMinute = 1
        };
        g.Tick(new DateTime(2001, 01, 01, 01, 01, 01), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 01, 30), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact);
    }
    [Test]
    public void TestGauge_Reached_OverTime_Boundary()
    {
        var someFact = false;

        var g = new PressureGauge
        {
            ThresholdBeatsPerMinute = 1
        };
        g.Tick(new DateTime(2001, 01, 01, 01, 01, 30), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact, Is.False);
        g.Tick(new(2001, 01, 01, 01, 02, 29), ThrowImmediatelyDataLoadEventListener.Quiet, () => someFact = true);
        Assert.That(someFact);
    }
}