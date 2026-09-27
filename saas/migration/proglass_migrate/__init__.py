"""proglass_migrate — the ProGlass WPF → SaaS migration tool (Phase 14).

python -m proglass_migrate <import|validate|list-modules> — see cli.py.
"""

__all__ = ["main"]
__version__ = "1.0.0"


def main(argv: list[str] | None = None) -> int:
    from .cli import main as _main
    return _main(argv)
