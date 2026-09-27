#!/usr/bin/env python3
"""
Generates the EF Core entity classes and IEntityTypeConfiguration classes
for the ProGlass SaaS backend FROM the verified SQL DDL in
saas/database/migrations (the single source of truth for the schema).

Output (regenerate after changing any migration DDL — never edit by hand):
  saas/backend/src/ProGlass.Saas.Domain/Entities/<Group>.cs
  saas/backend/src/ProGlass.Saas.Infrastructure/Persistence/Configuration/<Group>Configuration.cs

The DDL is applied and verified against live PostgreSQL by
saas/database/verify/apply_and_test.py before this runs, so the C# model is
generated from a schema that is known-good.

Usage: python3 saas/devbox/generate_ef_model.py
"""
from __future__ import annotations

import re
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent          # ProGlassAutomationADV
MIGRATIONS = REPO / "saas" / "database" / "migrations"
DOMAIN_ENTITIES = REPO / "saas" / "backend" / "src" / "ProGlass.Saas.Domain" / "Entities"
INFRA_CONFIG = REPO / "saas" / "backend" / "src" / "ProGlass.Saas.Infrastructure" / "Persistence" / "Configuration"

GENERATED_BANNER = """// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════
"""

# Which tables go into which generated file (grouping only).
GROUPS = {
    "Platform": ["tenants", "plans", "features", "plan_features",
                 "subscriptions", "tenant_features"],
    "Identity": ["users", "refresh_tokens", "audit_logs"],
    "MasterData": ["companies", "clients", "projects", "salesmen"],
    "Stock": ["glass_sheets", "inventory_items", "stock_transactions"],
    "Documents": ["inquiries", "attachments", "specifications",
                  "tenant_pricing_config", "tenant_vat_config",
                  "proforma_invoices", "specification_usages",
                  "spec_groups", "spec_group_items",
                  "other_charges", "wastage_rows", "custom_charges",
                  "job_orders",
                  "production_pieces", "production_events",
                  "material_reservations", "delivery_notes",
                  "delivery_note_items"],
    "Fiscal": ["tax_invoices", "credit_notes", "statements", "statement_lines"],
    "Optimization": ["optimization_sessions", "optimization_sheets",
                     "optimization_placements", "optimization_cuts",
                     "optimization_leftovers"],
}

TYPE_MAP = {
    "uuid": ("Guid", "uuid"),
    "text": ("string", "text"),
    "boolean": ("bool", "boolean"),
    "integer": ("int", "integer"),
    "bigint": ("long", "bigint"),
    "date": ("DateOnly", "date"),
    "timestamptz": ("DateTime", "timestamptz"),
    "jsonb": ("string", "jsonb"),
}


def pascal(name: str) -> str:
    return "".join(part[:1].upper() + part[1:] for part in name.split("_"))


def singular(table: str) -> str:
    if table == "salesmen":
        return "Salesman"
    if table.endswith("ies"):
        return pascal(table[:-3]) + "y"
    if table.endswith("s") and not table.endswith("ss"):
        return pascal(table[:-1])
    return pascal(table)


def split_top_level(body: str) -> list[str]:
    """Split a CREATE TABLE body on top-level commas (parens aware)."""
    parts, depth, current = [], 0, []
    for ch in body:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current).strip())
            current = []
        else:
            current.append(ch)
    tail = "".join(current).strip()
    if tail:
        parts.append(tail)
    return [p for p in parts if p]


def parse_type(sql_type: str) -> tuple[str, str]:
    m = re.fullmatch(r"numeric\((\d+),(\d+)\)", sql_type)
    if m:
        return "decimal", f"numeric({m.group(1)},{m.group(2)})"
    m = re.fullmatch(r"char\((\d+)\)", sql_type)
    if m:
        return "string", f"char({m.group(1)})"
    if sql_type in TYPE_MAP:
        return TYPE_MAP[sql_type]
    raise ValueError(f"Unknown SQL type: {sql_type}")


def csharp_string_literal(sql_literal: str) -> str:
    """'Clear' (SQL) → "Clear" (C#) with escaping."""
    inner = sql_literal[1:-1].replace("\\", "\\\\").replace('"', '\\"')
    return f'"{inner}"'


def default_literal(value: str, csharp_type: str) -> str | None:
    """Returns C# segment (no leading dot) for a DEFAULT clause, or None."""
    v = value.strip()
    u = v.upper()
    if u == "NOW()":
        return 'HasDefaultValueSql("now()")'
    if u == "GEN_RANDOM_UUID()":
        return 'HasDefaultValueSql("gen_random_uuid()")'
    if u == "CURRENT_DATE":
        return 'HasDefaultValueSql("CURRENT_DATE")'
    if re.fullmatch(r"'[^']*'", v):
        return f"HasDefaultValue({csharp_string_literal(v)})"
    if re.fullmatch(r"-?\d+(\.\d+)?", v):
        if csharp_type == "decimal":
            return f"HasDefaultValue({v}m)"
        return f"HasDefaultValue({v})"
    if v in ("true", "false"):
        return f"HasDefaultValue({v})"
    # Generator-internal marker for ALTER-TABLE expression defaults
    # (see parse_ddl) — plain SQL text, never parsed as a C# literal.
    if v.startswith("SQLDEFAULT:"):
        return f'HasDefaultValueSql("{v[len("SQLDEFAULT:"):]}")'
    # jsonb/array defaults and other SQL literals
    if v.startswith("'") and v.endswith("'"):
        return f"HasDefaultValueSql({csharp_string_literal(v)})"
    raise ValueError(f"Unhandled DEFAULT: {v}")


class Column:
    def __init__(self, name, sql_type, not_null, default):
        self.name = name
        self.sql_type = sql_type
        self.not_null = not_null
        self.default = default
        self.csharp_type, self.ef_type = parse_type(sql_type)

    @property
    def prop(self):
        return pascal(self.name)

    @property
    def nullable(self):
        return not self.not_null

    def csharp_decl(self):
        t = self.csharp_type + ("?" if self.nullable else "")
        init = " = null!;" if (not self.nullable and self.csharp_type == "string") else ""
        return f"public {t} {self.prop} {{ get; set; }}{init}"


class Table:
    def __init__(self, name):
        self.name = name
        self.columns: list[Column] = []
        self.pk: list[str] = []
        self.uniques: list[tuple[str, list[str]]] = []      # (constraint, cols)
        self.fks: list[dict] = []                            # col → target table, on_delete
        self.indexes: list[tuple[str, list[str], bool]] = [] # (name, cols, unique)

    @property
    def entity(self):
        return singular(self.name)

    def col(self, name):
        for c in self.columns:
            if c.name == name:
                return c
        raise KeyError(name)

    def has(self, name):
        return any(c.name == name for c in self.columns)


def parse_ddl() -> dict[str, Table]:
    tables: dict[str, Table] = {}
    ddl = "\n".join(p.read_text() for p in sorted(MIGRATIONS.glob("*.sql")))

    for m in re.finditer(r"CREATE TABLE saas\.(\w+)\s*\((.*?)\n\);", ddl, re.S):
        t = Table(m.group(1))
        body = m.group(2)
        for stmt in split_top_level(body):
            stmt = " ".join(stmt.split())
            if stmt.upper().startswith("CONSTRAINT"):
                cm = re.match(
                    r"CONSTRAINT (\w+) FOREIGN KEY \((\w+)\) REFERENCES saas\.(\w+) \((\w+)\)( ON DELETE (CASCADE|SET NULL))?",
                    stmt, re.I)
                if cm:
                    t.fks.append({
                        "col": cm.group(2), "target": cm.group(3),
                        "on_delete": (cm.group(6) or "NO ACTION").upper()})
                    continue
                um = re.match(r"CONSTRAINT (\w+) UNIQUE \(([^)]+)\)", stmt, re.I)
                if um:
                    t.uniques.append((um.group(1), [c.strip() for c in um.group(2).split(",")]))
                    continue
                pm = re.match(r"CONSTRAINT (\w+) PRIMARY KEY \(([^)]+)\)", stmt, re.I)
                if pm:
                    t.pk = [c.strip() for c in pm.group(2).split(",")]
                    continue
                continue  # CHECK constraints live in the DB only
            cm = re.match(
                r"(\w+) (\w+(?:\(\d+(?:,\d+)?\))?)( NOT NULL)?( DEFAULT (.+))?$", stmt)
            if not cm:
                raise ValueError(f"Cannot parse column line: {stmt!r} (table {t.name})")
            t.columns.append(Column(
                cm.group(1), cm.group(2),
                bool(cm.group(3)), cm.group(5)))

        for im in re.finditer(
                r"CREATE (UNIQUE )?INDEX (\w+) ON saas\.%s \(([^)]+)\)" % t.name, ddl):
            t.indexes.append((im.group(2),
                              [c.strip() for c in im.group(3).split(",")],
                              bool(im.group(1))))
        tables[t.name] = t

    # Columns added after the fact (e.g. 0006 ALTER TABLE ... ADD COLUMN)
    # merge into the table model so later migrations evolve the model the
    # same way CREATE TABLE did. Matches the exact forms used in
    # saas/database/migrations — new forms must be added here, by hand.
    for m in re.finditer(
            r"ALTER TABLE saas\.(\w+)\s+ADD\s+COLUMN\s+(\w+)\s+(\w+(?:\(\d+(?:,\d+)?\))?)"
            r"(\s+NOT\s+NULL)?(?:\s+DEFAULT\s+(.+?))?\s*;", ddl, re.I):
        t = tables.get(m.group(1))
        if t is None:
            raise ValueError(f"ALTER TABLE on unknown table saas.{m.group(1)}")
        if t.has(m.group(2)):
            continue  # already part of CREATE TABLE — nothing to merge
        default = m.group(5)
        if default is not None and not _is_simple_default(default):
            # SQL expression (e.g. "now() + interval '7 days'") — emitted
            # as HasDefaultValueSql, never parsed as a C# literal.
            default = _sql_default_literal(default)
        # NOT NULL may be inline (simple defaults, 0007) or arrive later
        # via "ALTER COLUMN ... SET NOT NULL" (0006) — both are handled.
        t.columns.append(Column(m.group(2), m.group(3), bool(m.group(4)), default))

    # Late constraints on existing columns: "ALTER TABLE ... ALTER COLUMN
    # x SET NOT NULL" flips a merged column to non-nullable.
    for m in re.finditer(
            r"ALTER TABLE saas\.(\w+)\s+ALTER\s+COLUMN\s+(\w+)\s+SET\s+NOT\s+NULL\s*;",
            ddl, re.I):
        t = tables.get(m.group(1))
        if t is None or not t.has(m.group(2)):
            raise ValueError(
                f"SET NOT NULL on unknown column saas.{m.group(1)}.{m.group(2)}")
        for c in t.columns:
            if c.name == m.group(2):
                c.not_null = True

    # Late default changes: "ALTER TABLE ... ALTER COLUMN x SET DEFAULT v"
    # replaces the CREATE-time default in the model config.
    for m in re.finditer(
            r"ALTER\s+TABLE\s+saas\.(\w+)\s+ALTER\s+COLUMN\s+(\w+)\s+SET\s+DEFAULT\s+(.+?)\s*;",
            ddl, re.I):
        t = tables.get(m.group(1))
        if t is None or not t.has(m.group(2)):
            raise ValueError(
                f"SET DEFAULT on unknown column saas.{m.group(1)}.{m.group(2)}")
        default = m.group(3)
        if _is_simple_default(default):
            for c in t.columns:
                if c.name == m.group(2):
                    c.default = default

    # Late type changes: "ALTER TABLE ... ALTER COLUMN x TYPE t" —
    # replaces the CREATE-time type in the model (e.g. a Phase-3 precision
    # fix widening numeric(18,2) to numeric(18,6)).
    for m in re.finditer(
            r"ALTER\s+TABLE\s+saas\.(\w+)\s+ALTER\s+COLUMN\s+(\w+)\s+TYPE\s+"
            r"(\w+(?:\(\d+(?:,\d+)?\))?)\s*;",
            ddl, re.I):
        t = tables.get(m.group(1))
        if t is None or not t.has(m.group(2)):
            raise ValueError(
                f"TYPE change on unknown column saas.{m.group(1)}.{m.group(2)}")
        for c in t.columns:
            if c.name == m.group(2):
                c.sql_type = m.group(3)
                c.csharp_type, c.ef_type = parse_type(c.sql_type)

    # Columns dropped after the fact: "ALTER TABLE ... DROP COLUMN x;".
    # Runs after the ADD pass; a drop of a never-created column is a typo
    # and must fail loudly rather than silently keep a dead column.
    for m in re.finditer(
            r"ALTER\s+TABLE\s+saas\.(\w+)\s+DROP\s+COLUMN\s+(\w+)\s*;",
            ddl, re.I):
        t = tables.get(m.group(1))
        if t is None:
            raise ValueError(f"DROP COLUMN on unknown table saas.{m.group(1)}")
        if not t.has(m.group(2)):
            raise ValueError(
                f"DROP COLUMN on unknown column saas.{m.group(1)}.{m.group(2)}")
        t.columns = [c for c in t.columns if c.name != m.group(2)]
        t.fks = [f for f in t.fks if f["col"] != m.group(2)]
        t.indexes = [(n, cols, u) for (n, cols, u) in t.indexes
                     if m.group(2) not in cols]

    # Constraints dropped after the fact (e.g. a Phase-3 FK guess removed
    # by a parity migration): removes the FK navigation / unique index
    # that CREATE TABLE originally declared.
    for m in re.finditer(
            r"ALTER\s+TABLE\s+saas\.(\w+)\s+DROP\s+CONSTRAINT\s+(\w+)\s*;",
            ddl, re.I):
        t = tables.get(m.group(1))
        if t is None:
            raise ValueError(f"DROP CONSTRAINT on unknown table saas.{m.group(1)}")
        t.fks = [f for f in t.fks if f["col"] != m.group(2)]
        t.uniques = [u for u in t.uniques if u[0] != m.group(2)]
        if t.pk and m.group(2).startswith("pk_"):
            t.pk = []

    # Indexes dropped after the fact ("DROP INDEX ... ON saas.x").
    for m in re.finditer(
            r"DROP\s+INDEX\s+(?:IF\s+EXISTS\s+)?saas\.(\w+)\s*;",
            ddl, re.I):
        for t in tables.values():
            t.indexes = [(n, cols, u) for (n, cols, u) in t.indexes
                         if n != m.group(1)]

    return tables



def _is_simple_default(value: str) -> bool:
    v = value.strip()
    return (v.upper() in ("NOW()", "GEN_RANDOM_UUID()", "CURRENT_DATE")
            or re.fullmatch(r"'[^']*'", v) is not None
            or re.fullmatch(r"-?\d+(\.\d+)?", v) is not None
            or v in ("true", "false"))


def _sql_default_literal(value: str) -> str:
    inner = value.strip().replace("\\", "\\\\").replace('"', '\\"')
    return f"SQLDEFAULT:{inner}"


def nav_name(fk_col: str) -> str:
    base = fk_col[:-3] if fk_col.endswith("_id") else fk_col
    return pascal(base)


ON_DELETE_MAP = {"CASCADE": "Cascade", "SET NULL": "SetNull", "NO ACTION": "Restrict"}


def emit_entities(tables: dict[str, Table]):
    DOMAIN_ENTITIES.mkdir(parents=True, exist_ok=True)
    for group, names in GROUPS.items():
        lines = [GENERATED_BANNER.rstrip(), "",
                 "using ProGlass.Saas.Domain.Tenancy;", ""]
        for n in names:
            t = tables[n]
            lines.append("namespace ProGlass.Saas.Domain.Entities")
            lines.append("{")
            iface = " : ITenantOwned" if t.has("tenant_id") else ""
            lines.append(f"    /// <summary>Maps to saas.{t.name}.</summary>")
            lines.append(f"    public sealed class {t.entity}{iface}")
            lines.append("    {")
            prop_names = {c.prop for c in t.columns}
            for c in t.columns:
                lines.append("        " + c.csharp_decl().strip())
            # one-way navigation properties for single-column FKs
            for fk in t.fks:
                if fk["col"] == "tenant_id":
                    continue  # tenancy is a filter concern, not a navigation
                target = tables[fk["target"]]
                nav = nav_name(fk["col"])
                if nav in prop_names:  # e.g. uploaded_by → UploadedBy collision
                    nav = nav + target.entity
                if t.col(fk["col"]).nullable:
                    lines.append(f"        public {target.entity}? {nav} {{ get; set; }} = null;")
                else:
                    lines.append(f"        public {target.entity} {nav} {{ get; set; }} = null!;")
            lines.append("    }")
            lines.append("}")
            lines.append("")
        (DOMAIN_ENTITIES / f"{group}.cs").write_text("\n".join(lines))


def _index_lambda(cols: list[str]) -> str:
    """['tenant_id','proforma_id'] → 'x => new { x.TenantId, x.ProformaId }' (or single)."""
    props = [pascal(c) for c in cols]
    if len(props) == 1:
        return f"x => x.{props[0]}"
    return "x => new { " + ", ".join(f"x.{p}" for p in props) + " }"


def emit_configurations(tables: dict[str, Table]):
    INFRA_CONFIG.mkdir(parents=True, exist_ok=True)
    for group, names in GROUPS.items():
        lines = [GENERATED_BANNER.rstrip(), "",
                 "using Microsoft.EntityFrameworkCore;",
                 "using Microsoft.EntityFrameworkCore.Metadata.Builders;",
                 "using ProGlass.Saas.Domain.Entities;", ""]
        lines.append("namespace ProGlass.Saas.Infrastructure.Persistence.Configuration")
        lines.append("{")
        for n in names:
            t = tables[n]
            lines.append(f"    public sealed class {t.entity}Configuration : IEntityTypeConfiguration<{t.entity}>")
            lines.append("    {")
            lines.append(f"        public void Configure(EntityTypeBuilder<{t.entity}> builder)")
            lines.append("        {")
            lines.append(f'            builder.ToTable("{t.name}", "saas");')
            if len(t.pk) == 1:
                lines.append(f"            builder.HasKey(x => x.{pascal(t.pk[0])});")
            elif t.pk:
                lines.append(f"            builder.HasKey({_index_lambda(t.pk)});")
            for c in t.columns:
                segs = [f'HasColumnName("{c.name}")', f'HasColumnType("{c.ef_type}")']
                if c.default:
                    segs.append(default_literal(c.default, c.csharp_type))
                lines.append(f"            builder.Property(x => x.{c.prop})." + ".".join(segs) + ";")
            for cname, cols in t.uniques:
                lines.append(f"            builder.HasIndex({_index_lambda(cols)})")
                lines.append(f"                .IsUnique().HasDatabaseName(\"{cname}\");")
            for iname, cols, unique in t.indexes:
                lines.append(f"            builder.HasIndex({_index_lambda(cols)})")
                if unique:
                    lines.append("                .IsUnique()")
                lines.append(f"                .HasDatabaseName(\"{iname}\");")
            for fk in t.fks:
                if fk["col"] == "tenant_id":
                    continue  # tenancy enforced by DB FK + query filter, no navigation
                target = tables[fk["target"]]
                nav = nav_name(fk["col"])
                if nav in {c.prop for c in t.columns}:
                    nav = nav + target.entity
                behavior = ON_DELETE_MAP[fk["on_delete"]]
                lines.append(f"            builder.HasOne(x => x.{nav})")
                lines.append(f"                .WithMany()")
                lines.append(f"                .HasForeignKey(x => x.{pascal(fk['col'])})")
                lines.append(f"                .OnDelete(DeleteBehavior.{behavior});")
            lines.append("        }")
            lines.append("    }")
            lines.append("")
        lines.append("}")
        (INFRA_CONFIG / f"{group}Configuration.cs").write_text("\n".join(lines))


def main():
    tables = parse_ddl()
    expected = {n for names in GROUPS.values() for n in names}
    missing = expected - set(tables)
    extra = set(tables) - expected
    if missing or extra:
        raise SystemExit(f"DDL/group mismatch — missing: {missing}, unmapped: {extra}")
    emit_entities(tables)
    emit_configurations(tables)
    print(f"Generated {len(tables)} entities + configurations from {len(list(MIGRATIONS.glob('*.sql')))} DDL files")
    for group in GROUPS:
        print(f"  {group}: {', '.join(singular(n) for n in GROUPS[group])}")


if __name__ == "__main__":
    main()
