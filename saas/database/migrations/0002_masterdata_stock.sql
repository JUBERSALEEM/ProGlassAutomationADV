-- ═══════════════════════════════════════════════════════════════════════
-- 0002_masterdata_stock.sql — ProGlass SaaS (Phase 3)
-- Master data (companies, clients, projects, salesmen — 1:1 with WPF
-- SavedCompany/SavedClient/SavedProject/SavedSalesman) + Sheet Store
-- (glass_sheets, inventory_items) + movement ledger (stock_transactions).
-- Stock/used quantities are ONLY written through stock_transactions rows
-- (service-layer rule, mirrors WPF SheetHistory auditability).
-- ═══════════════════════════════════════════════════════════════════════

CREATE TABLE saas.companies (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  name text NOT NULL,
  trn text NOT NULL DEFAULT '',
  location text NOT NULL DEFAULT '',
  representative text NOT NULL DEFAULT '',
  representative_mobile text NOT NULL DEFAULT '',
  email text NOT NULL DEFAULT '',
  account_number text NOT NULL DEFAULT '',
  bank_name text NOT NULL DEFAULT '',
  logo_storage_key text NOT NULL DEFAULT '',
  is_default boolean NOT NULL DEFAULT false,
  usage_count integer NOT NULL DEFAULT 1,
  last_used_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_companies PRIMARY KEY (id),
  CONSTRAINT fk_companies_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE
);
CREATE INDEX ix_companies_tenant_name ON saas.companies (tenant_id, name);

CREATE TABLE saas.clients (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  customer_name text NOT NULL,
  customer_trn text NOT NULL DEFAULT '',
  reference_code text NOT NULL DEFAULT '',
  sales_representative text NOT NULL DEFAULT '',
  billing_address text NOT NULL DEFAULT '',
  attention_executive_name text NOT NULL DEFAULT '',
  phone text NOT NULL DEFAULT '',
  usage_count integer NOT NULL DEFAULT 1,
  last_used_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_clients PRIMARY KEY (id),
  CONSTRAINT fk_clients_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE
);
CREATE INDEX ix_clients_tenant_name ON saas.clients (tenant_id, customer_name);

CREATE TABLE saas.projects (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  identifier_name text NOT NULL,
  contract_no text NOT NULL DEFAULT '',
  reference text NOT NULL DEFAULT '',
  site_delivery_location text NOT NULL DEFAULT '',
  lpo_number text NOT NULL DEFAULT '',
  customer_id uuid,
  usage_count integer NOT NULL DEFAULT 1,
  last_used_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_projects PRIMARY KEY (id),
  CONSTRAINT fk_projects_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_projects_client FOREIGN KEY (customer_id) REFERENCES saas.clients (id) ON DELETE SET NULL
);
CREATE INDEX ix_projects_tenant_name ON saas.projects (tenant_id, identifier_name);
CREATE INDEX ix_projects_tenant_customer ON saas.projects (tenant_id, customer_id);

CREATE TABLE saas.salesmen (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  name text NOT NULL,
  department text NOT NULL DEFAULT '',
  usage_count integer NOT NULL DEFAULT 1,
  last_used_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_salesmen PRIMARY KEY (id),
  CONSTRAINT fk_salesmen_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE
);
CREATE INDEX ix_salesmen_tenant_name ON saas.salesmen (tenant_id, name);

-- ── Sheet Store: glass sheets ─────────────────────────────────────────
-- Identity rule ported from WPF GlassSheet unique index:
-- (Category, ThicknessMm, Color, WidthMm, HeightMm, Supplier) — now
-- scoped per tenant. Money = NUMERIC(18,2); areas computed in the domain
-- at 4 dp (SqmCalculator.CalculateSheetArea).
CREATE TABLE saas.glass_sheets (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  category text NOT NULL,
  thickness_mm integer NOT NULL,
  color text NOT NULL DEFAULT 'Clear',
  width_mm integer NOT NULL,
  height_mm integer NOT NULL,
  supplier text NOT NULL DEFAULT '',
  purchase_price_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  sell_price_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  stock integer NOT NULL DEFAULT 0,
  used integer NOT NULL DEFAULT 0,
  last_purchase_date date,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_glass_sheets PRIMARY KEY (id),
  CONSTRAINT fk_glass_sheets_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT uq_glass_sheets_identity UNIQUE (tenant_id, category, thickness_mm, color, width_mm, height_mm, supplier),
  CONSTRAINT ck_glass_sheets_thickness CHECK (thickness_mm > 0),
  CONSTRAINT ck_glass_sheets_dims CHECK (width_mm > 0 AND height_mm > 0),
  CONSTRAINT ck_glass_sheets_stock CHECK (stock >= 0 AND used >= 0)
);
CREATE INDEX ix_glass_sheets_tenant_color ON saas.glass_sheets (tenant_id, color, thickness_mm);
CREATE INDEX ix_glass_sheets_tenant_category ON saas.glass_sheets (tenant_id, category);

-- ── Sheet Store: non-glass consumables ────────────────────────────────
-- Spacer is stocked in LINEAR METRES → numeric(14,2), not integer
-- (WPF InventoryItem parity; TEXT-affinity decimals in SQLite became
-- NUMERIC here, never float).
CREATE TABLE saas.inventory_items (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  category text NOT NULL,
  name text NOT NULL,
  supplier text NOT NULL DEFAULT '',
  stock numeric(14,2) NOT NULL DEFAULT 0,
  used numeric(14,2) NOT NULL DEFAULT 0,
  reorder_level numeric(14,2) NOT NULL DEFAULT 0,
  purchase_price_per_unit numeric(18,2) NOT NULL DEFAULT 0,
  sell_price_per_unit numeric(18,2) NOT NULL DEFAULT 0,
  unit text NOT NULL DEFAULT 'LM',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_inventory_items PRIMARY KEY (id),
  CONSTRAINT fk_inventory_items_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT ck_inventory_items_category CHECK (category IN ('spacer', 'sealant', 'desiccant', 'gas', 'hardware')),
  CONSTRAINT ck_inventory_items_stock CHECK (stock >= 0 AND used >= 0)
);
CREATE INDEX ix_inventory_items_tenant_category ON saas.inventory_items (tenant_id, category);
CREATE INDEX ix_inventory_items_tenant_supplier ON saas.inventory_items (tenant_id, supplier);

-- ── Stock movement ledger (immutable) ─────────────────────────────────
CREATE TABLE saas.stock_transactions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  item_type text NOT NULL,
  glass_sheet_id uuid,
  inventory_item_id uuid,
  transaction_type text NOT NULL,
  quantity_delta numeric(14,2) NOT NULL,
  balance_after numeric(14,2) NOT NULL,
  unit_cost numeric(18,2) NOT NULL DEFAULT 0,
  reference_type text NOT NULL DEFAULT '',
  reference_id uuid,
  notes text NOT NULL DEFAULT '',
  performed_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_stock_transactions PRIMARY KEY (id),
  CONSTRAINT fk_stock_transactions_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_stock_transactions_glass_sheet FOREIGN KEY (glass_sheet_id) REFERENCES saas.glass_sheets (id) ON DELETE CASCADE,
  CONSTRAINT fk_stock_transactions_inventory_item FOREIGN KEY (inventory_item_id) REFERENCES saas.inventory_items (id) ON DELETE CASCADE,
  CONSTRAINT fk_stock_transactions_performed_by FOREIGN KEY (performed_by) REFERENCES saas.users (id) ON DELETE SET NULL,
  CONSTRAINT ck_stock_transactions_item_type CHECK (item_type IN ('sheet', 'inventory')),
  CONSTRAINT ck_stock_transactions_type CHECK (transaction_type IN ('purchase', 'issue', 'return', 'adjustment', 'consume')),
  CONSTRAINT ck_stock_transactions_target CHECK (
    (item_type = 'sheet' AND glass_sheet_id IS NOT NULL AND inventory_item_id IS NULL) OR
    (item_type = 'inventory' AND inventory_item_id IS NOT NULL AND glass_sheet_id IS NULL)
  )
);
CREATE INDEX ix_stock_transactions_tenant_sheet ON saas.stock_transactions (tenant_id, glass_sheet_id, created_at);
CREATE INDEX ix_stock_transactions_tenant_inventory ON saas.stock_transactions (tenant_id, inventory_item_id, created_at);
CREATE INDEX ix_stock_transactions_tenant_created ON saas.stock_transactions (tenant_id, created_at);

-- updated_at triggers for every mutable table in this file
-- (stock_transactions is an immutable ledger — no trigger).
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['companies', 'clients', 'projects', 'salesmen',
                           'glass_sheets', 'inventory_items'] LOOP
    EXECUTE format('CREATE TRIGGER trg_%I_touch BEFORE UPDATE ON saas.%I '
      'FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at()', t, t);
  END LOOP;
END $$;
