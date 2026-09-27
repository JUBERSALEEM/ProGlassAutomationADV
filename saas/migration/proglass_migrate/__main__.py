"""`python -m proglass_migrate …` — the documented CLI entry point."""

import sys

from .cli import main

if __name__ == "__main__":
    sys.exit(main())
