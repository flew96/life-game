using System;
using System.Collections.Generic;

namespace GameOfLife
{
    public struct CellOffset
    {
        public CellOffset(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public int Row { get; }
        public int Col { get; }
    }

    /// <summary>A named set of live cells, described as text: 'O' is alive, anything else is dead.</summary>
    public sealed class Pattern
    {
        private Pattern(string name, IReadOnlyList<CellOffset> cells, int height, int width)
        {
            Name = name;
            Cells = cells;
            Height = height;
            Width = width;
        }

        public string Name { get; }
        public IReadOnlyList<CellOffset> Cells { get; }
        public int Height { get; }
        public int Width { get; }

        public static Pattern Parse(string name, params string[] lines)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name is required.", nameof(name));
            if (lines == null || lines.Length == 0) throw new ArgumentException("Pattern is empty.", nameof(lines));

            var cells = new List<CellOffset>();
            int width = 0;
            for (int r = 0; r < lines.Length; r++)
            {
                width = Math.Max(width, lines[r].Length);
                for (int c = 0; c < lines[r].Length; c++)
                {
                    if (lines[r][c] == 'O') cells.Add(new CellOffset(r, c));
                }
            }
            return new Pattern(name, cells.AsReadOnly(), lines.Length, width);
        }

        public override string ToString() => Name;
    }

    /// <summary>Well-known patterns offered in the UI.</summary>
    public static class Patterns
    {
        public static readonly Pattern Glider = Pattern.Parse("Glider",
            ".O.",
            "..O",
            "OOO");

        public static readonly Pattern LightweightSpaceship = Pattern.Parse("Lightweight spaceship",
            ".O..O",
            "O....",
            "O...O",
            "OOOO.");

        public static readonly Pattern Blinker = Pattern.Parse("Blinker",
            "OOO");

        public static readonly Pattern Pulsar = Pattern.Parse("Pulsar",
            "..OOO...OOO..",
            ".............",
            "O....O.O....O",
            "O....O.O....O",
            "O....O.O....O",
            "..OOO...OOO..",
            ".............",
            "..OOO...OOO..",
            "O....O.O....O",
            "O....O.O....O",
            "O....O.O....O",
            ".............",
            "..OOO...OOO..");

        public static readonly Pattern Pentadecathlon = Pattern.Parse("Pentadecathlon",
            "..O....O..",
            "OO.OOOO.OO",
            "..O....O..");

        public static readonly Pattern GosperGliderGun = Pattern.Parse("Gosper glider gun",
            "........................O...........",
            "......................O.O...........",
            "............OO......OO............OO",
            "...........O...O....OO............OO",
            "OO........O.....O...OO..............",
            "OO........O...O.OO....O.O...........",
            "..........O.....O.......O...........",
            "...........O...O....................",
            "............OO......................");

        public static readonly Pattern RPentomino = Pattern.Parse("R-pentomino",
            ".OO",
            "OO.",
            ".O.");

        public static readonly Pattern Acorn = Pattern.Parse("Acorn",
            ".O.....",
            "...O...",
            "OO..OOO");

        public static readonly Pattern Diehard = Pattern.Parse("Diehard",
            "......O.",
            "OO......",
            ".O...OOO");

        public static IReadOnlyList<Pattern> All { get; } = new[]
        {
            Glider,
            LightweightSpaceship,
            Blinker,
            Pulsar,
            Pentadecathlon,
            GosperGliderGun,
            RPentomino,
            Acorn,
            Diehard,
        };
    }
}
