using System;
using System.Collections.Generic;
using System.Linq;
using GameOfLife;
using Xunit;

public class LifeEngineTests
{
    private static LifeEngine Board(Pattern pattern, int top = 5, int left = 5, int rows = 20, int cols = 20, bool wrap = true)
    {
        var engine = new LifeEngine(rows, cols) { Wrap = wrap };
        engine.Place(pattern, top, left);
        return engine;
    }

    private static HashSet<(int, int)> Alive(LifeEngine engine)
    {
        var set = new HashSet<(int, int)>();
        for (int r = 0; r < engine.Rows; r++)
            for (int c = 0; c < engine.Cols; c++)
                if (engine[r, c]) set.Add((r, c));
        return set;
    }

    private static HashSet<(int, int)> Shifted(Pattern pattern, int top, int left) =>
        pattern.Cells.Select(p => (p.Row + top, p.Col + left)).ToHashSet();

    [Fact]
    public void EmptyBoardDiesOut()
    {
        var engine = new LifeEngine(10, 10);
        Assert.Equal(SimulationState.Extinct, engine.Step());
        Assert.Equal(1, engine.Generation);
        Assert.Equal(0, engine.Population);
    }

    [Fact]
    public void LonelyCellDies()
    {
        var engine = new LifeEngine(10, 10);
        engine[4, 4] = true;
        Assert.Equal(1, engine.Population);
        Assert.Equal(SimulationState.Extinct, engine.Step());
    }

    [Fact]
    public void BlockIsStable()
    {
        var engine = Board(Pattern.Parse("Block", "OO", "OO"));
        Assert.Equal(SimulationState.Stable, engine.Step());
        Assert.Equal(1, engine.Period);
        Assert.Equal(4, engine.Population);
    }

    [Fact]
    public void BlinkerHasPeriodTwo()
    {
        var engine = Board(Patterns.Blinker);
        Assert.Equal(SimulationState.Running, engine.Step());
        Assert.True(engine[4, 6] && engine[5, 6] && engine[6, 6]);
        Assert.Equal(SimulationState.Oscillating, engine.Step());
        Assert.Equal(2, engine.Period);
    }

    [Theory]
    [InlineData("Pulsar", 3)]
    [InlineData("Pentadecathlon", 15)]
    public void OscillatorsAreDetected(string name, int period)
    {
        var pattern = Patterns.All.Single(p => p.Name == name);
        var engine = new LifeEngine(40, 40);
        engine.PlaceCentered(pattern);

        SimulationState state = SimulationState.Running;
        for (int i = 0; i < period; i++) state = engine.Step();

        Assert.Equal(SimulationState.Oscillating, state);
        Assert.Equal(period, engine.Period);
    }

    [Fact]
    public void GliderMovesDiagonally()
    {
        var engine = Board(Patterns.Glider, 2, 2);
        for (int i = 0; i < 4; i++) Assert.Equal(SimulationState.Running, engine.Step());
        Assert.Equal(Shifted(Patterns.Glider, 3, 3), Alive(engine));
    }

    [Fact]
    public void LightweightSpaceshipMovesSideways()
    {
        var engine = Board(Patterns.LightweightSpaceship, 8, 4);
        for (int i = 0; i < 4; i++) engine.Step();
        Assert.Equal(Shifted(Patterns.LightweightSpaceship, 8, 2), Alive(engine));
    }

    [Fact]
    public void GliderWrapsAroundTheTorus()
    {
        // 4 generations move a glider by one cell, so after 4 * size it is back where it started.
        var engine = Board(Patterns.Glider, 0, 0, rows: 10, cols: 10);
        var start = Alive(engine);
        SimulationState state = SimulationState.Running;
        for (int i = 0; i < 40; i++) state = engine.Step();
        Assert.Equal(start, Alive(engine));
        Assert.Equal(SimulationState.Oscillating, state);
        Assert.Equal(40, engine.Period);
    }

    [Fact]
    public void GliderBecomesBlockWithoutWrap()
    {
        var engine = Board(Patterns.Glider, 0, 0, rows: 10, cols: 10, wrap: false);
        SimulationState state = SimulationState.Running;
        for (int i = 0; i < 60 && state == SimulationState.Running; i++) state = engine.Step();
        Assert.Equal(SimulationState.Stable, state);
        Assert.Equal(4, engine.Population);
    }

    [Fact]
    public void BlinkerAcrossTheEdgeNeedsWrap()
    {
        var wrapped = new LifeEngine(10, 10) { Wrap = true };
        wrapped[5, 9] = wrapped[5, 0] = wrapped[5, 1] = true;
        wrapped.Step();
        Assert.True(wrapped[4, 0] && wrapped[5, 0] && wrapped[6, 0]);
        Assert.Equal(3, wrapped.Population);

        var bounded = new LifeEngine(10, 10) { Wrap = false };
        bounded[5, 9] = bounded[5, 0] = bounded[5, 1] = true;
        bounded.Step();
        Assert.Equal(0, bounded.Population);
    }

    [Fact]
    public void GliderGunKeepsGrowing()
    {
        var engine = new LifeEngine(60, 80) { Wrap = false };
        engine.Place(Patterns.GosperGliderGun, 2, 2);
        int start = engine.Population;
        for (int i = 0; i < 120; i++) Assert.NotEqual(SimulationState.Extinct, engine.Step());
        Assert.True(engine.Population > start + 15, $"population {engine.Population}");
    }

    [Fact]
    public void DiehardDiesAfter130Generations()
    {
        var engine = new LifeEngine(60, 60) { Wrap = false };
        engine.PlaceCentered(Patterns.Diehard);
        for (int i = 0; i < 129; i++) Assert.NotEqual(SimulationState.Extinct, engine.Step());
        Assert.Equal(SimulationState.Extinct, engine.Step());
        Assert.Equal(130, engine.Generation);
    }

    [Fact]
    public void EditingResetsCycleDetection()
    {
        var engine = Board(Pattern.Parse("Block", "OO", "OO"));
        Assert.Equal(SimulationState.Stable, engine.Step());

        engine[15, 15] = true;
        Assert.Equal(SimulationState.Running, engine.State);
        Assert.Equal(0, engine.Period);
        Assert.Equal(5, engine.Population);
    }

    [Fact]
    public void ClearAndRandomizeResetGeneration()
    {
        var engine = new LifeEngine(30, 30);
        engine.Randomize(new Random(1), 0.5);
        Assert.InRange(engine.Population, 300, 600);
        engine.Step();
        engine.Step();
        Assert.Equal(2, engine.Generation);

        engine.Randomize(new Random(2));
        Assert.Equal(0, engine.Generation);
        Assert.Equal(Alive(engine).Count, engine.Population);

        engine.Clear();
        Assert.Equal(0, engine.Generation);
        Assert.Equal(0, engine.Population);
        Assert.Empty(Alive(engine));
    }

    [Fact]
    public void PlaceClipsWithoutWrap()
    {
        var engine = new LifeEngine(10, 10) { Wrap = false };
        engine.Place(Patterns.Glider, 8, 8);
        Assert.Equal(new HashSet<(int, int)> { (8, 9) }, Alive(engine));
        Assert.Equal(1, engine.Population);

        var wrapped = new LifeEngine(10, 10) { Wrap = true };
        wrapped.Place(Patterns.Glider, 8, 8);
        Assert.Equal(new HashSet<(int, int)> { (8, 9), (9, 0), (0, 8), (0, 9), (0, 0) }, Alive(wrapped));
    }

    [Fact]
    public void RejectsTinyBoardsAndBadCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LifeEngine(2, 10));
        var engine = new LifeEngine(5, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine[5, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine[0, -1] = true);
    }

    [Fact]
    public void PatternsParseCorrectly()
    {
        Assert.Equal(5, Patterns.Glider.Cells.Count);
        Assert.Equal(36, Patterns.GosperGliderGun.Cells.Count);
        Assert.Equal((9, 36), (Patterns.GosperGliderGun.Height, Patterns.GosperGliderGun.Width));
        Assert.Equal(48, Patterns.Pulsar.Cells.Count);
        Assert.All(Patterns.All, p => Assert.NotEmpty(p.Cells));
    }
}
