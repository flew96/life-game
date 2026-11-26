using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GameOfLife
{
    /// <summary>
    /// Draws a <see cref="LifeEngine"/> board scaled to the control size and lets the user
    /// paint cells: left mouse button draws, right mouse button erases.
    /// </summary>
    internal sealed class GridView : Control
    {
        private LifeEngine _engine;
        private bool _painting;
        private bool _paintValue;
        private Point _lastCell;

        private readonly SolidBrush _aliveBrush = new SolidBrush(Color.FromArgb(46, 160, 67));
        private readonly SolidBrush _deadBrush = new SolidBrush(Color.FromArgb(250, 250, 250));
        private readonly Pen _gridPen = new Pen(Color.FromArgb(225, 228, 232));

        public GridView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(36, 41, 47);
        }

        /// <summary>Raised after the user changes cells with the mouse.</summary>
        public event EventHandler CellsEdited;

        public LifeEngine Engine
        {
            get => _engine;
            set
            {
                _engine = value;
                Invalidate();
            }
        }

        /// <summary>Draw thin lines between cells when they are big enough.</summary>
        public bool ShowGridLines { get; set; } = true;

        private int CellSize =>
            _engine == null ? 1 : Math.Max(1, Math.Min(ClientSize.Width / _engine.Cols, ClientSize.Height / _engine.Rows));

        private Rectangle BoardBounds
        {
            get
            {
                if (_engine == null) return Rectangle.Empty;
                int size = CellSize;
                int w = size * _engine.Cols;
                int h = size * _engine.Rows;
                return new Rectangle((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (_engine == null) return;

            var board = BoardBounds;
            int size = CellSize;
            g.FillRectangle(_deadBrush, board);

            bool lines = ShowGridLines && size >= 5;
            int inset = lines ? 1 : 0;

            var alive = new List<Rectangle>(_engine.Population);
            for (int r = 0; r < _engine.Rows; r++)
            {
                for (int c = 0; c < _engine.Cols; c++)
                {
                    if (_engine[r, c])
                        alive.Add(new Rectangle(board.X + c * size + inset, board.Y + r * size + inset, size - inset, size - inset));
                }
            }

            if (lines)
            {
                for (int c = 0; c <= _engine.Cols; c++)
                    g.DrawLine(_gridPen, board.X + c * size, board.Y, board.X + c * size, board.Bottom);
                for (int r = 0; r <= _engine.Rows; r++)
                    g.DrawLine(_gridPen, board.X, board.Y + r * size, board.Right, board.Y + r * size);
            }

            if (alive.Count > 0)
                g.FillRectangles(_aliveBrush, alive.ToArray());
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_engine == null || (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right)) return;

            if (!TryGetCell(e.Location, out var cell)) return;

            Focus();
            _painting = true;
            _paintValue = e.Button == MouseButtons.Left;
            _lastCell = cell;
            SetCell(cell);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_painting || _engine == null) return;

            var board = BoardBounds;
            int size = CellSize;
            // Clamp so that dragging past the edge still paints the border cells.
            int col = Clamp((e.X - board.X) / size, 0, _engine.Cols - 1);
            int row = Clamp((e.Y - board.Y) / size, 0, _engine.Rows - 1);
            var cell = new Point(col, row);
            if (cell == _lastCell) return;

            // Fill the gap between two mouse events so fast strokes stay continuous.
            foreach (var p in Line(_lastCell, cell))
                SetCell(p);
            _lastCell = cell;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _painting = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) _painting = false;
        }

        private void SetCell(Point cell)
        {
            if (_engine[cell.Y, cell.X] == _paintValue) return;
            _engine[cell.Y, cell.X] = _paintValue;

            int size = CellSize;
            var board = BoardBounds;
            Invalidate(new Rectangle(board.X + cell.X * size, board.Y + cell.Y * size, size + 1, size + 1));
            CellsEdited?.Invoke(this, EventArgs.Empty);
        }

        private bool TryGetCell(Point location, out Point cell)
        {
            var board = BoardBounds;
            cell = Point.Empty;
            if (!board.Contains(location)) return false;

            int size = CellSize;
            cell = new Point((location.X - board.X) / size, (location.Y - board.Y) / size);
            return true;
        }

        /// <summary>Bresenham line between two cells, inclusive.</summary>
        private static IEnumerable<Point> Line(Point from, Point to)
        {
            int x = from.X, y = from.Y;
            int dx = Math.Abs(to.X - x), sx = x < to.X ? 1 : -1;
            int dy = -Math.Abs(to.Y - y), sy = y < to.Y ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                yield return new Point(x, y);
                if (x == to.X && y == to.Y) yield break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _aliveBrush.Dispose();
                _deadBrush.Dispose();
                _gridPen.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
