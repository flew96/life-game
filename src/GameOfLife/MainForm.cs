using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameOfLife
{
    internal sealed class MainForm : Form
    {
        private const int BoardRows = 80;
        private const int BoardCols = 120;

        /// <summary>Timer interval (ms) for each position of the speed slider.</summary>
        private static readonly int[] SpeedIntervals = { 1000, 600, 400, 250, 150, 100, 70, 45, 25, 10 };

        private readonly LifeEngine _engine = new LifeEngine(BoardRows, BoardCols);
        private readonly Random _random = new Random();
        private readonly System.ComponentModel.Container _components = new System.ComponentModel.Container();
        private readonly Timer _timer = new Timer();

        private GridView _grid;
        private Button _startButton;
        private Button _stepButton;
        private ComboBox _patternBox;
        private CheckBox _wrapBox;
        private CheckBox _autoStopBox;
        private TrackBar _speedBar;
        private ToolStripStatusLabel _generationLabel;
        private ToolStripStatusLabel _populationLabel;
        private ToolStripStatusLabel _messageLabel;

        public MainForm()
        {
            BuildUi();

            _timer.Tick += (s, e) => StepOnce();
            ApplySpeed();

            _engine.Randomize(_random);
            _grid.Engine = _engine;
            UpdateStatus(null);
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "Game of Life";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(1120, 740);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            Font = SystemFonts.MessageBoxFont;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* keep the default icon */ }

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 6, 6, 2),
                WrapContents = true,
            };

            _startButton = MakeButton("Start", (s, e) => ToggleRunning());
            _stepButton = MakeButton("Step", (s, e) => StepOnce());
            var randomButton = MakeButton("Random", (s, e) => RandomizeBoard());
            var clearButton = MakeButton("Clear", (s, e) => ClearBoard());

            _patternBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Margin = new Padding(12, 5, 3, 3),
            };
            foreach (var pattern in Patterns.All) _patternBox.Items.Add(pattern);
            _patternBox.SelectedIndex = 0;

            var insertButton = MakeButton("Insert", (s, e) => InsertPattern());

            _wrapBox = new CheckBox
            {
                Text = "Wrap edges",
                Checked = _engine.Wrap,
                AutoSize = true,
                Margin = new Padding(12, 7, 3, 3),
            };
            _wrapBox.CheckedChanged += (s, e) => _engine.Wrap = _wrapBox.Checked;

            _autoStopBox = new CheckBox
            {
                Text = "Stop on repeat",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(6, 7, 3, 3),
            };

            var speedLabel = new Label
            {
                Text = "Speed:",
                AutoSize = true,
                Margin = new Padding(12, 9, 0, 3),
            };

            _speedBar = new TrackBar
            {
                Minimum = 1,
                Maximum = SpeedIntervals.Length,
                Value = 6,
                TickStyle = TickStyle.None,
                Width = 140,
                AutoSize = false,
                Height = 30,
                Margin = new Padding(0, 4, 3, 0),
            };
            _speedBar.ValueChanged += (s, e) => ApplySpeed();

            toolbar.Controls.AddRange(new Control[]
            {
                _startButton, _stepButton, randomButton, clearButton,
                _patternBox, insertButton,
                _wrapBox, _autoStopBox,
                speedLabel, _speedBar,
            });

            var tips = new ToolTip(_components);
            tips.SetToolTip(_startButton, "Start / pause the simulation (Space)");
            tips.SetToolTip(_stepButton, "Compute one generation (N)");
            tips.SetToolTip(randomButton, "Fill the board randomly (R)");
            tips.SetToolTip(clearButton, "Kill every cell (C)");
            tips.SetToolTip(insertButton, "Draw the selected pattern in the middle of the board (I)");
            tips.SetToolTip(_wrapBox, "Cells on one edge see the cells on the opposite edge as neighbours");
            tips.SetToolTip(_autoStopBox, "Pause when the board dies out or repeats a previous state");

            var status = new StatusStrip { SizingGrip = true };
            _generationLabel = new ToolStripStatusLabel { AutoSize = false, Width = 140, TextAlign = ContentAlignment.MiddleLeft };
            _populationLabel = new ToolStripStatusLabel { AutoSize = false, Width = 140, TextAlign = ContentAlignment.MiddleLeft };
            _messageLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            status.Items.AddRange(new ToolStripItem[] { _generationLabel, _populationLabel, _messageLabel });

            _grid = new GridView { Dock = DockStyle.Fill };
            _grid.CellsEdited += (s, e) => UpdateStatus(null);

            // Fill-docked control must be added first so it gets the space left by the others.
            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(status);

            ResumeLayout(true);
        }

        private static Button MakeButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(80, 28),
                Margin = new Padding(3),
                UseVisualStyleBackColor = true,
            };
            button.Click += onClick;
            return button;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Space:
                    ToggleRunning();
                    return true;
                case Keys.N:
                    StepOnce();
                    return true;
                case Keys.R:
                    RandomizeBoard();
                    return true;
                case Keys.C:
                    ClearBoard();
                    return true;
                case Keys.I:
                    InsertPattern();
                    return true;
                case Keys.Escape:
                    SetRunning(false);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ToggleRunning() => SetRunning(!_timer.Enabled);

        private void SetRunning(bool running)
        {
            if (running && _engine.Population == 0)
            {
                UpdateStatus("The board is empty: draw some cells or press Random.");
                return;
            }

            _timer.Enabled = running;
            _startButton.Text = running ? "Pause" : "Start";
            _stepButton.Enabled = !running;
            if (running) UpdateStatus(null);
        }

        private void StepOnce()
        {
            var state = _engine.Step();
            _grid.Invalidate();

            string message = null;
            switch (state)
            {
                case SimulationState.Extinct:
                    message = "All cells are dead.";
                    break;
                case SimulationState.Stable:
                    message = "The board is stable: nothing changes any more.";
                    break;
                case SimulationState.Oscillating:
                    message = $"The board repeats itself every {_engine.Period} generations.";
                    break;
            }

            if (message != null && (_autoStopBox.Checked || state == SimulationState.Extinct))
                SetRunning(false);

            UpdateStatus(message);
        }

        private void RandomizeBoard()
        {
            _engine.Randomize(_random);
            _grid.Invalidate();
            UpdateStatus(null);
        }

        private void ClearBoard()
        {
            SetRunning(false);
            _engine.Clear();
            _grid.Invalidate();
            UpdateStatus("Draw cells with the left mouse button, erase with the right one.");
        }

        private void InsertPattern()
        {
            if (!(_patternBox.SelectedItem is Pattern pattern)) return;
            _engine.PlaceCentered(pattern);
            _grid.Invalidate();
            UpdateStatus($"Inserted: {pattern.Name}.");
        }

        private void ApplySpeed()
        {
            _timer.Interval = SpeedIntervals[_speedBar.Value - 1];
        }

        private void UpdateStatus(string message)
        {
            _generationLabel.Text = $"Generation: {_engine.Generation}";
            _populationLabel.Text = $"Population: {_engine.Population}";
            _messageLabel.Text = message ?? "LMB: draw · RMB: erase · Space: start/pause · N: step";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
