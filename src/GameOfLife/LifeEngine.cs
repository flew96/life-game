using System;
using System.Collections.Generic;

namespace GameOfLife
{
    /// <summary>What the last <see cref="LifeEngine.Step"/> found out about the board.</summary>
    public enum SimulationState
    {
        /// <summary>The board has not repeated itself yet.</summary>
        Running,
        /// <summary>Every cell is dead.</summary>
        Extinct,
        /// <summary>The board no longer changes (period 1).</summary>
        Stable,
        /// <summary>The board repeats a previous configuration (period 2 or more).</summary>
        Oscillating,
    }

    /// <summary>
    /// Conway's Game of Life (B3/S23) on a fixed-size grid. The engine knows nothing about
    /// the UI, so it can be unit-tested on any platform.
    /// </summary>
    public sealed class LifeEngine
    {
        /// <summary>How many past generations are remembered for cycle detection.</summary>
        public const int MaxHistory = 4096;

        private bool[] _cells;
        private bool[] _next;
        private readonly Dictionary<string, int> _seen = new Dictionary<string, int>();
        private readonly Queue<KeyValuePair<string, int>> _historyOrder = new Queue<KeyValuePair<string, int>>();
        private bool _historyDirty = true;
        private bool _wrap = true;

        public LifeEngine(int rows, int cols)
        {
            if (rows < 3) throw new ArgumentOutOfRangeException(nameof(rows), "At least 3 rows are required.");
            if (cols < 3) throw new ArgumentOutOfRangeException(nameof(cols), "At least 3 columns are required.");

            Rows = rows;
            Cols = cols;
            _cells = new bool[rows * cols];
            _next = new bool[rows * cols];
        }

        public int Rows { get; }
        public int Cols { get; }

        /// <summary>Number of generations computed since the last Clear/Randomize.</summary>
        public int Generation { get; private set; }

        /// <summary>Number of live cells.</summary>
        public int Population { get; private set; }

        /// <summary>Result of the last step.</summary>
        public SimulationState State { get; private set; }

        /// <summary>Period of the detected cycle (1 for still lifes), 0 if none was detected.</summary>
        public int Period { get; private set; }

        /// <summary>When true, the edges wrap around (the board is a torus).</summary>
        public bool Wrap
        {
            get => _wrap;
            set
            {
                if (_wrap == value) return;
                _wrap = value;
                MarkEdited();
            }
        }

        public bool this[int row, int col]
        {
            get
            {
                CheckBounds(row, col);
                return _cells[row * Cols + col];
            }
            set
            {
                CheckBounds(row, col);
                int i = row * Cols + col;
                if (_cells[i] == value) return;
                _cells[i] = value;
                Population += value ? 1 : -1;
                MarkEdited();
            }
        }

        /// <summary>Kills every cell and resets the generation counter.</summary>
        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
            Population = 0;
            Restart();
        }

        /// <summary>Fills the board randomly and resets the generation counter.</summary>
        public void Randomize(Random random, double density = 0.3)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (density < 0 || density > 1) throw new ArgumentOutOfRangeException(nameof(density));

            int population = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                bool alive = random.NextDouble() < density;
                _cells[i] = alive;
                if (alive) population++;
            }
            Population = population;
            Restart();
        }

        /// <summary>
        /// Draws a pattern with its top-left corner at (<paramref name="top"/>, <paramref name="left"/>).
        /// Cells outside the board wrap around when <see cref="Wrap"/> is on, otherwise they are dropped.
        /// </summary>
        public void Place(Pattern pattern, int top, int left)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            foreach (var cell in pattern.Cells)
            {
                int r = top + cell.Row;
                int c = left + cell.Col;
                if (Wrap)
                {
                    r = Mod(r, Rows);
                    c = Mod(c, Cols);
                }
                else if (r < 0 || r >= Rows || c < 0 || c >= Cols)
                {
                    continue;
                }
                this[r, c] = true;
            }
        }

        /// <summary>Draws a pattern centred on the board.</summary>
        public void PlaceCentered(Pattern pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            Place(pattern, (Rows - pattern.Height) / 2, (Cols - pattern.Width) / 2);
        }

        /// <summary>Computes the next generation and checks for extinction or repetition.</summary>
        public SimulationState Step()
        {
            if (_historyDirty)
            {
                _seen.Clear();
                _historyOrder.Clear();
                Remember(StateKey(), Generation);
                _historyDirty = false;
            }

            int population = 0;
            for (int r = 0; r < Rows; r++)
            {
                int up = r - 1, down = r + 1;
                if (_wrap)
                {
                    if (up < 0) up = Rows - 1;
                    if (down == Rows) down = 0;
                }

                for (int c = 0; c < Cols; c++)
                {
                    int left = c - 1, right = c + 1;
                    if (_wrap)
                    {
                        if (left < 0) left = Cols - 1;
                        if (right == Cols) right = 0;
                    }

                    int n = Alive(up, left) + Alive(up, c) + Alive(up, right)
                          + Alive(r, left) + Alive(r, right)
                          + Alive(down, left) + Alive(down, c) + Alive(down, right);

                    int i = r * Cols + c;
                    bool alive = n == 3 || (n == 2 && _cells[i]);
                    _next[i] = alive;
                    if (alive) population++;
                }
            }

            var swap = _cells;
            _cells = _next;
            _next = swap;

            Generation++;
            Population = population;

            if (population == 0)
            {
                State = SimulationState.Extinct;
                Period = 0;
                return State;
            }

            string key = StateKey();
            if (_seen.TryGetValue(key, out int seenAt))
            {
                Period = Generation - seenAt;
                State = Period == 1 ? SimulationState.Stable : SimulationState.Oscillating;
            }
            else
            {
                Period = 0;
                State = SimulationState.Running;
            }
            Remember(key, Generation);
            return State;
        }

        private int Alive(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols) return 0;
            return _cells[row * Cols + col] ? 1 : 0;
        }

        private void Restart()
        {
            Generation = 0;
            MarkEdited();
        }

        private void MarkEdited()
        {
            _historyDirty = true;
            Period = 0;
            State = Population == 0 ? SimulationState.Extinct : SimulationState.Running;
        }

        private void Remember(string key, int generation)
        {
            _seen[key] = generation;
            _historyOrder.Enqueue(new KeyValuePair<string, int>(key, generation));

            while (_historyOrder.Count > MaxHistory)
            {
                var oldest = _historyOrder.Dequeue();
                // The same board may have been seen again later; only forget the entry if it is the old one.
                if (_seen.TryGetValue(oldest.Key, out int g) && g == oldest.Value)
                    _seen.Remove(oldest.Key);
            }
        }

        /// <summary>Packs the board into a compact string (16 cells per char) for the history set.</summary>
        private string StateKey()
        {
            var chars = new char[(_cells.Length + 15) / 16];
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i]) chars[i >> 4] |= (char)(1 << (i & 15));
            }
            return new string(chars);
        }

        private void CheckBounds(int row, int col)
        {
            if (row < 0 || row >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
            if (col < 0 || col >= Cols) throw new ArgumentOutOfRangeException(nameof(col));
        }

        private static int Mod(int value, int m)
        {
            int r = value % m;
            return r < 0 ? r + m : r;
        }
    }
}
