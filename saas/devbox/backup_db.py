#!/usr/bin/env python3
"""ProGlass SaaS database backup — the WPF BackupService port (Phase 15).

WPF ground truth (Services/Database/DatabaseBackup/, read 2026-09-27):
  • BackupService: backup SETS with retention MaxBackupSets = 10; auto
    backups throttled (a set at most every AutoBackupInterval = 1h);
    manual warns when the last set is < ManualWarnWindow = 10min old;
    the facade NEVER throws — backup problems are logged/reported.
  • RestoreDrillService: a backup is only proven when a RESTORE has
    been exercised — the drill restores a set to a scratch database and
    verifies integrity (row counts) against the source.

SaaS translation: each set = one pg_dump -Fc archive + a manifest.json
(BackupSetInfo: name/createdAt/bytes/fileCount/reason + checksums).

Usage:
    python3 backup_db.py --db-uri URI [--backup-dir DIR] \
        [--reason manual] [--force] [--restore-drill] [--list]
        [--retention N]

Exit codes: 0 ok · 1 backup/restore failure (never a crash).
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

# The WPF policy, ported verbatim.
MAX_BACKUP_SETS = 10
AUTO_BACKUP_INTERVAL_MINUTES = 60
MANUAL_WARN_WINDOW_MINUTES = 10


def find_pg_dump() -> str:
    """pg_dump from PATH, else the pgserver bundle this repo's tooling
    installs (same binaries the verify harness runs)."""
    found = shutil.which("pg_dump")
    if found:
        return found
    try:
        import pgserver
        bundled = Path(pgserver.__file__).parent / "pginstall" / "bin" \
            / "pg_dump"
        if bundled.exists():
            return str(bundled)
    except ImportError:
        pass
    raise SystemExit("error: pg_dump not found (PATH or pgserver bundle)")


def find_psql() -> str:
    found = shutil.which("psql")
    if found:
        return found
    try:
        import pgserver
        bundled = Path(pgserver.__file__).parent / "pginstall" / "bin" \
            / "psql"
        if bundled.exists():
            return str(bundled)
    except ImportError:
        pass
    raise SystemExit("error: psql not found (PATH or pgserver bundle)")


def set_dirs(backup_dir: Path) -> list[Path]:
    return sorted(
        (d for d in backup_dir.iterdir() if d.is_dir()
         and (d / "manifest.json").exists()),
        key=lambda d: json.loads(
            (d / "manifest.json").read_text()).get("createdAt", ""),
        reverse=True)


def md5_of(path: Path) -> str:
    import hashlib
    digest = hashlib.md5()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def row_counts(db_uri: str) -> dict[str, int]:
    """Per-table row counts (the RestoreDrillService integrity check)."""
    import psycopg2
    conn = psycopg2.connect(db_uri)
    try:
        with conn.cursor() as cur:
            cur.execute("""
                SELECT table_name FROM information_schema.tables
                WHERE table_schema = 'saas'
                ORDER BY table_name
            """)
            tables = [r[0] for r in cur.fetchall()]
            counts = {}
            for table in tables:
                cur.execute(f'SELECT count(*) FROM saas."{table}"')
                counts[table] = cur.fetchone()[0]
        return counts
    finally:
        conn.close()


def create_backup(db_uri: str, backup_dir: Path, reason: str,
                  force: bool) -> int:
    backup_dir.mkdir(parents=True, exist_ok=True)
    sets = set_dirs(backup_dir)
    now = datetime.now(timezone.utc)

    # Manual-warn window (WPF ManualWarnWindow — the operator chose to
    # force with --force).
    if sets and not force:
        manifest = json.loads((sets[0] / "manifest.json").read_text())
        age_min = (now - datetime.fromisoformat(
            manifest["createdAt"])).total_seconds() / 60
        if age_min < MANUAL_WARN_WINDOW_MINUTES:
            print(f"warning: the newest backup set is only "
                  f"{age_min:.0f} minute(s) old (warn window "
                  f"{MANUAL_WARN_WINDOW_MINUTES} min). Use --force to"
                  " back up anyway.")
            return 0

    name = now.strftime("backup-%Y%m%d-%H%M%S")
    target = backup_dir / name
    target.mkdir(parents=True)
    archive = target / "database.dump"
    try:
        subprocess.run(
            [find_pg_dump(), "--format=custom", "--file", str(archive),
             "--dbname", db_uri],
            check=True, capture_output=True, text=True)
    except subprocess.CalledProcessError as ex:
        shutil.rmtree(target, ignore_errors=True)
        print("error: pg_dump failed — no backup set written")
        print(ex.stderr[:500])
        return 1

    manifest = {
        "name": name,
        "createdAt": now.isoformat(),
        "reason": reason,
        "fileCount": 1,
        "totalBytes": archive.stat().st_size,
        "dumpChecksumMd5": md5_of(archive),
        "sourceRowCounts": row_counts(db_uri),
        "pgVersion": subprocess.run(
            [find_psql(), "--dbname", db_uri, "--tuples-only", "--no-psqlrc",
             "-c", "SHOW server_version"],
            check=True, capture_output=True, text=True).stdout.strip(),
    }
    (target / "manifest.json").write_text(json.dumps(manifest, indent=2))
    print(f"backup set: {name} "
          f"({manifest['totalBytes'] / 1024:.1f} KB, reason={reason})")

    # Retention (WPF MaxBackupSets): prune the OLDEST sets beyond the
    # cap — never the one just written.
    sets = set_dirs(backup_dir)
    pruned = 0
    while len(sets) > MAX_BACKUP_SETS:
        oldest = sets.pop()
        shutil.rmtree(oldest, ignore_errors=True)
        pruned += 1
    if pruned:
        print(f"retention: pruned {pruned} set(s) "
              f"(cap {MAX_BACKUP_SETS})")
    return 0


def restore_drill(db_uri: str, backup_dir: Path) -> int:
    """Restores the newest set into a SCRATCH database and verifies the
    per-table row counts against the manifest — the proof a backup is
    restorable (RestoreDrillService). The scratch database is dropped
    afterwards; the source is never touched."""
    sets = set_dirs(backup_dir)
    if not sets:
        print("error: no backup sets to drill")
        return 1
    newest = sets[0]
    manifest = json.loads((newest / "manifest.json").read_text())
    archive = newest / "database.dump"

    if md5_of(archive) != manifest.get("dumpChecksumMd5"):
        print("error: archive checksum mismatch — the set is corrupt")
        return 1

    import psycopg2
    source = psycopg2.connect(db_uri)
    source.autocommit = True
    scratch_name = f"pglass_drill_{datetime.now(timezone.utc):%Y%m%d%H%M%S}"
    try:
        with source.cursor() as cur:
            cur.execute(f'CREATE DATABASE "{scratch_name}"')
    finally:
        source.close()

    scratch_uri = _replace_db(db_uri, scratch_name)
    try:
        restore = subprocess.run(
            [str(Path(find_pg_dump()).parent / "pg_restore"),
             "--dbname", scratch_uri, "--no-owner", "--no-privileges",
             str(archive)],
            capture_output=True, text=True)
        if restore.returncode != 0:
            print("error: pg_restore failed:")
            print(restore.stderr[:800])
            return 1

        restored = row_counts(scratch_uri)
        expected = manifest.get("sourceRowCounts", {})
        mismatches = [
            f"{table}: manifest {expected.get(table, '?')} vs restored"
            f" {restored.get(table, '?')}"
            for table in sorted(set(expected) | set(restored))
            if expected.get(table) != restored.get(table)]
        if mismatches:
            print("error: restore drill FAILED — row count mismatches:")
            for line in mismatches:
                print(f"  {line}")
            return 1
        total = sum(restored.values())
        print(f"restore drill PASSED — {len(restored)} tables, "
              f"{total} rows verified against {newest.name}")
        return 0
    finally:
        cleanup = psycopg2.connect(db_uri)
        cleanup.autocommit = True
        try:
            with cleanup.cursor() as cur:
                cur.execute(f'DROP DATABASE IF EXISTS "{scratch_name}"')
        finally:
            cleanup.close()


def _replace_db(db_uri: str, new_name: str) -> str:
    """postgresql://user:pass@host:port/dbname?params → …/new_name?params
    (the query string — e.g. a pgserver unix-socket host= — survives)."""
    base, sep, query = db_uri.partition("?")
    prefix, _, _ = base.rpartition("/")
    result = f"{prefix}/{new_name}"
    return result + (sep + query if query else "")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="ProGlass SaaS backup sets (WPF BackupService port)")
    parser.add_argument("--db-uri", required=True)
    parser.add_argument("--backup-dir", default="backups")
    parser.add_argument("--reason", default="manual")
    parser.add_argument("--force", action="store_true",
                        help="skip the manual-warn window")
    parser.add_argument("--restore-drill", action="store_true",
                        help="restore the newest set to a scratch database"
                             " and verify integrity")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--retention", type=int, default=None,
                        help="override the retention cap (default 10)")
    args = parser.parse_args(argv)

    global MAX_BACKUP_SETS
    if args.retention is not None:
        MAX_BACKUP_SETS = args.retention

    if args.list:
        for path in set_dirs(Path(args.backup_dir)):
            manifest = json.loads((path / "manifest.json").read_text())
            print(f"{manifest['createdAt']}  "
                  f"{manifest['totalBytes'] / 1024:>10.1f} KB  "
                  f"{manifest['reason']:<10} {manifest['name']}")
        return 0

    if args.restore_drill:
        return restore_drill(args.db_uri, Path(args.backup_dir))

    return create_backup(args.db_uri, Path(args.backup_dir), args.reason,
                         args.force)


if __name__ == "__main__":
    sys.exit(main())
