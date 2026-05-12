# AGENTS.md

## Cursor Cloud specific instructions

### Overview

PixivUtil2 is a Python CLI tool for downloading artwork from Pixiv and Pixiv FANBOX. It uses an embedded SQLite database and requires Pixiv authentication (cookies) for most operations.

### Running Tests

Tests require `PYTHONPATH=.` to resolve root-level module imports (`common`, `model`, etc.):

```bash
PYTHONPATH=. uv run pytest -v ./test/
```

Some tests in `test_PixivModel_fanbox.py` and `test_PixivDBManager.py` have pre-existing failures unrelated to environment setup.

### Running the Application

```bash
uv run python PixivUtil2.py --help
```

The app requires Pixiv authentication (cookie in `config.ini`) for any download operation. Without credentials it exits with code 100.

### Linting

No formal linter is configured in the project. You can use `ruff check .` for basic Python linting (pre-existing warnings exist).

### Key Gotchas

- The project does **not** use `__init__.py` files; all imports rely on `PYTHONPATH` including the workspace root.
- `uv sync` installs dependencies into `.venv/`. Use `uv run` to execute commands within that venv.
- Running `PixivUtil2.py` for the first time creates `config.ini` and `db.sqlite` in the working directory; remember to clean these up if they shouldn't be committed.
- FFmpeg is optional (only needed for ugoira video conversion features).
