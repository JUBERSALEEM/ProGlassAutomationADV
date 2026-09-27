"""ProGlass WPF → SaaS migration engine.

Phase 14 tool (saas/docs/SAAS_MIGRATION_PLAN.md is the governing
document). The WPF data root is READ-ONLY input (every SQLite file is
opened in mode=ro and never written back); each module imports inside
ONE transaction — a failed module rolls back only itself and is
reported, never half-loaded rows.

Implemented in Python (same CLI shape, module order and transform rules
the plan specifies) so the tool is executable and testable in this
environment; porting the proven logic to the .NET console worker later
is mechanical. Documented in WPF_TO_SAAS_MAPPING.md §1.13.
"""

from __future__ import annotations

import json
import sqlite3
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Any, Callable

import psycopg2
from psycopg2.extras import register_uuid

# WPF int PKs → SaaS uuid PKs everywhere: adapt uuid.UUID natively
# (process-global, same registration the verify harness uses).
register_uuid()

# UAE standard time — no DST (the plan's default tenant timezone).
UAE_OFFSET = timezone(timedelta(hours=4))


def to_utc(raw: str | None, *, source_is_utc: bool = False) -> datetime | None:
    """WPF local DateTime (Asia/Dubai, naive TEXT) → aware UTC.

    ActivityLog.TimestampUtc is already UTC (plan §3: audit's UTC values
    pass through untouched).
    """
    if not raw:
        return None
    text = str(raw).strip().replace("T", " ")
    if not text:
        return None
    naive = None
    # EF Core SQLite TEXT format: 2026-01-31 14:05:09.1234567
    for fmt in ("%Y-%m-%d %H:%M:%S.%f", "%Y-%m-%d %H:%M:%S",
                "%Y-%m-%d %H:%M", "%Y-%m-%d"):
        try:
            naive = datetime.strptime(text, fmt)
            break
        except ValueError:
            continue
    if naive is None:
        return None
    tz = timezone.utc if source_is_utc else UAE_OFFSET
    return naive.replace(tzinfo=tz).astimezone(timezone.utc)


def to_date(raw: str | None) -> str | None:
    """DateOnly-shaped value → ISO date string (the SaaS date columns)."""
    if not raw:
        return None
    text = str(raw).strip()
    if not text:
        return None
    return text.replace("T", " ").split(" ")[0] or None


def money(raw: Any) -> Decimal:
    """WPF TEXT-affinity money → exact decimal (plan §3)."""
    if raw is None:
        return Decimal("0")
    return Decimal(str(raw))


def round2(value: Decimal) -> Decimal:
    """2 dp, midpoint AWAY from zero (the ported MoneyRounding policy)."""
    return value.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP)


def insert_with_savepoint(pg, sql: str, params: tuple) -> tuple[bool, str]:
    """One row inside the module transaction, isolated by a SAVEPOINT:
    a rejected row (CHECK, UNIQUE, FK…) rolls back only itself — the
    module continues, nothing is half-loaded. Returns (ok, error text —
    the finding's diagnosis)."""
    pg.execute("SAVEPOINT row")
    try:
        pg.execute(sql, params)
    except Exception as ex:               # noqa: BLE001 — row-level
        error = f"{type(ex).__name__}: {ex}".splitlines()[0][:200]
        pg.execute("ROLLBACK TO SAVEPOINT row")
        pg.execute("RELEASE SAVEPOINT row")
        return False, error
    pg.execute("RELEASE SAVEPOINT row")
    return True, ""


@dataclass
class Finding:
    severity: str          # info | warning | error
    module: str
    message: str
    row: str = ""          # source row identity (table + pk / number)

    def as_dict(self) -> dict:
        return {"severity": self.severity, "module": self.module,
                "message": self.message, "row": self.row}


@dataclass
class ModuleResult:
    name: str
    status: str            # ok | rolled_back | skipped
    extracted: int = 0
    loaded: int = 0
    skipped: int = 0
    message: str = ""
    findings: list[Finding] = field(default_factory=list)


class Module:
    """One import unit: Extract → Transform → Validate → Load."""

    name: str = "?"
    #: source SQLite file (inside the data root)
    source_db: str = "?"
    depends_on: list[str] = []
    reason_deferred: str | None = None

    def __init__(self, source_root: Path):
        self.root = source_root
        self.findings: list[Finding] = []

    def finding(self, severity: str, message: str, row: str = "") -> None:
        self.findings.append(Finding(severity, self.name, message, row))

    def open_source(self) -> sqlite3.Connection:
        """The governing rule: the WPF data root is read-only input."""
        path = self.root / self.source_db
        if not path.exists():
            raise FileNotFoundError(
                f"{self.source_db} not found under the source root")
        # mode=ro — SQLite refuses to write; originals are never touched.
        return sqlite3.connect(f"file:{path}?mode=ro", uri=True)

    def run(self, pg: psycopg2.extensions.cursor, tenant_id: uuid.UUID,
            id_map: dict[str, Any]) -> tuple[int, int]:
        """Returns (extracted, loaded). Raises to roll the module back."""
        raise NotImplementedError


class MigrationRun:
    def __init__(self, connection: psycopg2.extensions.connection,
                 tenant_id: uuid.UUID, apply: bool):
        self.connection = connection
        self.tenant_id = tenant_id
        self.apply = apply
        #: cross-module identity maps (WPF int ids → SaaS uuids etc.)
        self.id_map: dict[str, Any] = {}
        self.results: list[ModuleResult] = []

    def run_module(self, module: Module) -> ModuleResult:
        """One transaction per module in APPLY mode (a failed module
        rolls back only itself). In DRY-RUN the whole run shares ONE
        transaction — earlier modules' rows stay visible so later
        modules' foreign keys resolve exactly as an apply would — and
        each module is isolated by a savepoint; finalize() rolls
        everything back at the end."""
        result = ModuleResult(name=module.name, status="ok")
        result.findings = module.findings
        # Modules may need the mode (the attachments module must not
        # write bytes to disk in a dry-run; rows ride the rollback).
        module._apply = self.apply
        with self.connection.cursor() as cursor:
            cursor.execute("SAVEPOINT module_sp")
            try:
                # The RLS session GUC is transaction-scoped.
                cursor.execute(
                    "SELECT set_config('app.tenant_id', %s, true)",
                    (str(self.tenant_id),))
                extracted, loaded = module.run(
                    cursor, self.tenant_id, self.id_map)
                if self.apply:
                    cursor.execute("RELEASE SAVEPOINT module_sp")
                    self.connection.commit()
                else:
                    # Keep the rows in the open transaction (later
                    # modules' FKs need them); finalize() undoes all.
                    pass
                result.extracted, result.loaded = extracted, loaded
            except Exception as ex:                # noqa: BLE001 — reported
                cursor.execute("ROLLBACK TO SAVEPOINT module_sp")
                cursor.execute("RELEASE SAVEPOINT module_sp")
                if self.apply:
                    self.connection.commit()   # nothing loaded: safe
                result.status = "rolled_back"
                result.message = f"{type(ex).__name__}: {ex}".splitlines()[0]
        self.results.append(result)
        return result

    def finalize(self) -> None:
        """Dry-run: the single transaction is discarded — a dry run
        loads EXACTLY what an apply would, then leaves nothing behind."""
        if not self.apply:
            self.connection.rollback()

    def report(self) -> dict:
        return {
            "tenant": str(self.tenant_id),
            "mode": "apply" if self.apply else "dry-run",
            "modules": [
                {"name": r.name, "status": r.status,
                 "extracted": r.extracted, "loaded": r.loaded,
                 "skipped": r.skipped, "message": r.message,
                 "findings": [f.as_dict() for f in r.findings]}
                for r in self.results
            ],
        }


def render_html(report: dict, generated_at: datetime) -> str:
    """The plan §4 validation report: HTML + JSON."""
    rows = []
    for module in report["modules"]:
        badge = {"ok": "✅", "rolled_back": "❌", "skipped": "⬜"}.get(
            module["status"], "?")
        findings = module.get("findings") or []
        errors = sum(1 for f in findings if f["severity"] == "error")
        warnings = sum(1 for f in findings if f["severity"] == "warning")
        detail = "".join(
            f"<li><b>{f['severity']}</b> {f['message']}"
            + (f" <code>{f['row']}</code>" if f["row"] else "") + "</li>"
            for f in findings)
        rows.append(
            f"<tr><td>{badge} {module['name']}</td>"
            f"<td>{module['status']}</td>"
            f"<td>{module['extracted']}</td><td>{module['loaded']}</td>"
            f"<td>{module.get('skipped', 0)}</td>"
            f"<td>{errors} / {warnings}</td></tr>"
            + (f'<tr><td colspan="6"><ul>{detail}</ul></td></tr>' if detail else ""))
    return f"""<!doctype html><html><head><meta charset="utf-8">
<title>ProGlass migration report</title>
<style>body{{font-family:system-ui;margin:2rem}}
table{{border-collapse:collapse}}td,th{{border:1px solid #ccc;padding:6px 12px;text-align:left}}
code{{background:#f4f4f4;padding:1px 4px}}ul{{margin:4px 0}}</style></head><body>
<h1>ProGlass WPF → SaaS migration report</h1>
<p>Tenant <code>{report['tenant']}</code> · mode <b>{report['mode']}</b> ·
generated {generated_at.isoformat()}</p>
<table><tr><th>Module</th><th>Status</th><th>Extracted</th>
<th>Loaded</th><th>Skipped rows</th><th>Errors / warnings</th></tr>
{''.join(rows)}
</table></body></html>"""


def write_report(report: dict, out_dir: Path, generated_at: datetime) -> list[Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    json_path = out_dir / "migration_report.json"
    html_path = out_dir / "migration_report.html"
    json_path.write_text(json.dumps(report, indent=2))
    html_path.write_text(render_html(report, generated_at))
    return [json_path, html_path]
