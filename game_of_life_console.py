#!/usr/bin/env python3
"""Conway's Game of Life in the terminal.

Run without arguments for an interactive setup, or pass options, e.g.:

    python game_of_life_console.py --rows 20 --cols 40 --speed fast
    python game_of_life_console.py --manual --speed step --no-wrap
"""

import argparse
import random
import sys
import time

ALIVE = "██"  # two full blocks: cells look square in most terminals
DEAD = " ·"

SPEEDS = {"fast": 0.1, "normal": 0.5, "slow": 1.0, "step": None}

Board = list  # list[list[bool]]


def create_board(rows, cols):
    """Return an empty board."""
    return [[False] * cols for _ in range(rows)]


def random_board(rows, cols, density=0.3, rng=random):
    """Return a board where each cell is alive with the given probability."""
    return [[rng.random() < density for _ in range(cols)] for _ in range(rows)]


def count_neighbors(board, row, col, wrap=True):
    """Count live neighbours of a cell; with wrap the board is a torus."""
    rows, cols = len(board), len(board[0])
    count = 0
    for dr in (-1, 0, 1):
        for dc in (-1, 0, 1):
            if dr == 0 and dc == 0:
                continue
            r, c = row + dr, col + dc
            if wrap:
                r %= rows
                c %= cols
            elif not (0 <= r < rows and 0 <= c < cols):
                continue
            count += board[r][c]
    return count


def next_generation(board, wrap=True):
    """Compute the next generation (rule B3/S23)."""
    new_board = create_board(len(board), len(board[0]))
    for row, line in enumerate(board):
        for col, alive in enumerate(line):
            n = count_neighbors(board, row, col, wrap)
            new_board[row][col] = n == 3 or (n == 2 and alive)
    return new_board


def population(board):
    return sum(map(sum, board))


def board_key(board):
    """Hashable snapshot of a board, used to detect repeating states."""
    return tuple(tuple(line) for line in board)


def render(board, generation):
    """Draw the board in place (ANSI escape codes, no flicker from `cls`)."""
    lines = ["".join(ALIVE if cell else DEAD for cell in line) for line in board]
    status = f"Generation: {generation}   Population: {population(board)}   (Ctrl+C to quit)"
    sys.stdout.write("\x1b[H\x1b[2J" + "\n".join(lines) + "\n" + status + "\n")
    sys.stdout.flush()


def ask_int(prompt, default, minimum=3, maximum=200):
    while True:
        raw = input(f"{prompt} [{default}]: ").strip()
        if not raw:
            return default
        try:
            value = int(raw)
        except ValueError:
            print("Please enter a whole number.")
            continue
        if minimum <= value <= maximum:
            return value
        print(f"Please enter a number from {minimum} to {maximum}.")


def ask_choice(prompt, choices, default):
    options = "/".join(choices)
    while True:
        raw = input(f"{prompt} ({options}) [{default}]: ").strip().lower()
        if not raw:
            return default
        if raw in choices:
            return raw
        print(f"Please choose one of: {options}.")


def read_manual_board(rows, cols):
    """Ask the user for live cells, 1-based 'row col' pairs."""
    board = create_board(rows, cols)
    print(f"Enter live cells as 'row col' (1..{rows} and 1..{cols}), one per line; empty line or 'done' to finish.")
    while True:
        entry = input("> ").strip().lower()
        if entry in ("", "done"):
            return board
        parts = entry.replace(",", " ").split()
        if len(parts) != 2 or not all(p.lstrip("-").isdigit() for p in parts):
            print("Use two whole numbers separated by a space, e.g. '3 5'.")
            continue
        r, c = (int(p) for p in parts)
        if 1 <= r <= rows and 1 <= c <= cols:
            board[r - 1][c - 1] = not board[r - 1][c - 1]
        else:
            print("Coordinates are out of bounds.")


def run(board, speed="normal", wrap=True, max_generations=None):
    """Run the simulation until it dies out, repeats, or is interrupted."""
    seen = {}
    generation = 0
    delay = SPEEDS[speed]
    try:
        while True:
            render(board, generation)
            key = board_key(board)
            if population(board) == 0:
                print("All cells are dead.")
                return
            if key in seen:
                period = generation - seen[key]
                if period == 1:
                    print("The board is stable: nothing changes any more.")
                else:
                    print(f"The board repeats itself every {period} generations.")
                return
            if max_generations is not None and generation >= max_generations:
                print(f"Stopped after {generation} generations.")
                return
            seen[key] = generation

            if delay is None:
                input("Press Enter for the next generation...")
            else:
                time.sleep(delay)
            board = next_generation(board, wrap)
            generation += 1
    except (KeyboardInterrupt, EOFError):
        print("\nInterrupted.")


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description="Conway's Game of Life in the terminal.")
    parser.add_argument("--rows", type=int, help="board height (3-200)")
    parser.add_argument("--cols", type=int, help="board width (3-200)")
    parser.add_argument("--speed", choices=SPEEDS, help="delay between generations; 'step' waits for Enter")
    parser.add_argument("--manual", action="store_true", help="enter live cells by hand instead of a random board")
    parser.add_argument("--density", type=float, default=0.3, help="share of live cells on a random board (0-1)")
    parser.add_argument("--no-wrap", action="store_true", help="treat cells beyond the edges as dead")
    parser.add_argument("--generations", type=int, help="stop after this many generations")
    parser.add_argument("--seed", type=int, help="random seed, for reproducible boards")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    for name in ("rows", "cols"):
        value = getattr(args, name)
        if value is not None and not 3 <= value <= 200:
            sys.exit(f"--{name} must be between 3 and 200")
    if not 0 <= args.density <= 1:
        sys.exit("--density must be between 0 and 1")

    interactive = args.rows is None and args.cols is None and args.speed is None and not args.manual
    try:
        rows = args.rows or ask_int("Rows", 20)
        cols = args.cols or ask_int("Columns", 40)
        speed = args.speed or ask_choice("Speed", list(SPEEDS), "normal")
        manual = args.manual or (interactive and ask_choice("Start board", ["random", "manual"], "random") == "manual")
        board = read_manual_board(rows, cols) if manual else random_board(rows, cols, args.density, random.Random(args.seed))
    except (KeyboardInterrupt, EOFError):
        print("\nBye.")
        return

    run(board, speed, wrap=not args.no_wrap, max_generations=args.generations)


if __name__ == "__main__":
    main()
