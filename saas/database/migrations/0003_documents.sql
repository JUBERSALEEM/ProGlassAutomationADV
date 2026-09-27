-- ═══════════════════════════════════════════════════════════════════════
-- 0003_documents.sql — ProGlass SaaS (Phase 3)
-- Business documents: inquiries, specifications, proforma invoices
-- (wizard core + children), job orders, production, delivery notes,
-- tax invoices, credit notes, statements.
--
-- Status vocabularies are the WPF contract (docs/WORKFLOW.md):
--   Inquiry:      Received | Prepared | Sent | Confirmed | Hold | Cancelled
--   Proforma doc: Draft | Sent | Approved | Rejected | Expired | Cancelled
--   PI lifecycle: Draft | PendingApproval | Approved | Sent | Confirmed | Cancelled | Rejected
--   Job Order:    InProduction | Completed | Delivered | Approved
--   Delivery:     Draft | Dispatching | Dispatched | Signed | Received | Delivered
--   Tax Invoice:  Issued | Paid | Overdue
-- Unknown legacy statuses from WPF migration land in status_text and pass
-- through untouched — never remapped (WPF ERP contract).
-- Fiscal documents use void-not-delete (WPF G-6 rule).
-- ═══════════════════════════════════════════════════════════════════════

-- ── inquiries ─────────────────────────────────────────────────────────
CREATE TABLE saas.inquiries (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  inquiry_no text NOT NULL,
  status text NOT NULL DEFAULT 'Received',
  status_text text NOT NULL DEFAULT '',
  priority text NOT NULL DEFAULT 'normal',
  customer_id uuid,
  project_id uuid,
  customer_name text NOT NULL DEFAULT '',
  project_name text NOT NULL DEFAULT '',
  salesman text NOT NULL DEFAULT '',
  notes text NOT NULL DEFAULT '',
  received_at timestamptz,
  boq_json jsonb,
  converted_proforma_id uuid,
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_inquiries PRIMARY KEY (id),
  CONSTRAINT fk_inquiries_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_inquiries_client FOREIGN KEY (customer_id) REFERENCES saas.clients (id) ON DELETE SET NULL,
  CONSTRAINT fk_inquiries_project FOREIGN KEY (project_id) REFERENCES saas.projects (id) ON DELETE SET NULL,
  CONSTRAINT ck_inquiries_status CHECK (status IN ('Received', 'Prepared', 'Sent', 'Confirmed', 'Hold', 'Cancelled')),
  CONSTRAINT ck_inquiries_priority CHECK (priority IN ('low', 'normal', 'high', 'urgent'))
);
CREATE INDEX ix_inquiries_tenant_no ON saas.inquiries (tenant_id, inquiry_no);
CREATE INDEX ix_inquiries_tenant_status ON saas.inquiries (tenant_id, status);
CREATE INDEX ix_inquiries_tenant_customer ON saas.inquiries (tenant_id, customer_name);

-- ── attachments (shared by all document types) ────────────────────────
CREATE TABLE saas.attachments (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  entity_type text NOT NULL,
  entity_id uuid NOT NULL,
  storage_key text NOT NULL,
  original_name text NOT NULL,
  mime_type text NOT NULL DEFAULT '',
  size_bytes bigint NOT NULL DEFAULT 0,
  uploaded_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_attachments PRIMARY KEY (id),
  CONSTRAINT fk_attachments_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_attachments_uploaded_by FOREIGN KEY (uploaded_by) REFERENCES saas.users (id) ON DELETE SET NULL
);
CREATE INDEX ix_attachments_tenant_entity ON saas.attachments (tenant_id, entity_type, entity_id);

-- ── specifications (priced glass configs — WPF specifications.db) ─────
CREATE TABLE saas.specifications (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  spec_name text NOT NULL,
  glass_type text NOT NULL,
  params_json jsonb,
  rate_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  cost_basis_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  usage_count integer NOT NULL DEFAULT 1,
  last_used_at timestamptz,
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_specifications PRIMARY KEY (id),
  CONSTRAINT fk_specifications_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT ck_specifications_glass_type CHECK (glass_type IN ('SGU', 'DGU', 'LAM', 'DGU+LAM'))
);
CREATE INDEX ix_specifications_tenant_name ON saas.specifications (tenant_id, spec_name);
CREATE INDEX ix_specifications_tenant_type ON saas.specifications (tenant_id, glass_type);

-- ── per-tenant pricing/VAT configuration (WPF defaults, tenant-editable)
CREATE TABLE saas.tenant_pricing_config (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  yield_factor_default numeric(8,4) NOT NULL DEFAULT 0.8500,
  margin_default numeric(5,2) NOT NULL DEFAULT 15,
  thickness_options jsonb NOT NULL DEFAULT '[]',
  treatment_options jsonb NOT NULL DEFAULT '[]',
  asp_widths jsonb NOT NULL DEFAULT '[]',
  interlayer_types jsonb NOT NULL DEFAULT '[]',
  interlayer_thicknesses jsonb NOT NULL DEFAULT '[]',
  polish_options jsonb NOT NULL DEFAULT '[]',
  panel_extra_options jsonb NOT NULL DEFAULT '[]',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_tenant_pricing_config PRIMARY KEY (id),
  CONSTRAINT fk_tenant_pricing_config_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT uq_tenant_pricing_config_tenant UNIQUE (tenant_id),
  CONSTRAINT ck_tenant_pricing_yield CHECK (yield_factor_default > 0)
);

CREATE TABLE saas.tenant_vat_config (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  vat_rate numeric(5,4) NOT NULL DEFAULT 0.0500,
  currency char(3) NOT NULL DEFAULT 'AED',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_tenant_vat_config PRIMARY KEY (id),
  CONSTRAINT fk_tenant_vat_config_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT uq_tenant_vat_config_tenant UNIQUE (tenant_id),
  CONSTRAINT ck_tenant_vat_rate CHECK (vat_rate >= 0 AND vat_rate <= 1)
);

-- ── proforma invoices (wizard core) ───────────────────────────────────
CREATE TABLE saas.proforma_invoices (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  proforma_no text NOT NULL,
  root_proforma_id uuid,
  revision_number integer NOT NULL DEFAULT 0,
  inquiry_id uuid,
  company_id uuid,
  client_id uuid,
  project_id uuid,
  salesman_id uuid,
  status text NOT NULL DEFAULT 'Draft',
  status_text text NOT NULL DEFAULT '',
  lifecycle_state text NOT NULL DEFAULT 'Draft',
  issue_date date,
  valid_until date,
  glass_materials_subtotal numeric(18,2) NOT NULL DEFAULT 0,
  other_charges_subtotal numeric(18,2) NOT NULL DEFAULT 0,
  discount_amount numeric(18,2) NOT NULL DEFAULT 0,
  vat_amount numeric(18,2) NOT NULL DEFAULT 0,
  grand_total_vat_inclusive numeric(18,2) NOT NULL DEFAULT 0,
  is_void boolean NOT NULL DEFAULT false,
  voided_at timestamptz,
  voided_by uuid,
  void_reason text NOT NULL DEFAULT '',
  furthest_step integer NOT NULL DEFAULT 1,
  snapshot_json jsonb,
  row_version integer NOT NULL DEFAULT 0,
  sent_at timestamptz,
  approved_at timestamptz,
  confirmed_at timestamptz,
  job_order_id uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_proforma_invoices PRIMARY KEY (id),
  CONSTRAINT fk_proforma_invoices_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_proforma_invoices_root FOREIGN KEY (root_proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE SET NULL,
  CONSTRAINT fk_proforma_invoices_inquiry FOREIGN KEY (inquiry_id) REFERENCES saas.inquiries (id) ON DELETE SET NULL,
  CONSTRAINT fk_proforma_invoices_company FOREIGN KEY (company_id) REFERENCES saas.companies (id) ON DELETE SET NULL,
  CONSTRAINT fk_proforma_invoices_client FOREIGN KEY (client_id) REFERENCES saas.clients (id) ON DELETE SET NULL,
  CONSTRAINT fk_proforma_invoices_project FOREIGN KEY (project_id) REFERENCES saas.projects (id) ON DELETE SET NULL,
  CONSTRAINT fk_proforma_invoices_salesman FOREIGN KEY (salesman_id) REFERENCES saas.salesmen (id) ON DELETE SET NULL,
  CONSTRAINT uq_proforma_invoices_tenant_no UNIQUE (tenant_id, proforma_no),
  CONSTRAINT ck_proforma_invoices_status CHECK (status IN ('Draft', 'Sent', 'Approved', 'Rejected', 'Expired', 'Cancelled')),
  CONSTRAINT ck_proforma_invoices_lifecycle CHECK (lifecycle_state IN ('Draft', 'PendingApproval', 'Approved', 'Sent', 'Confirmed', 'Cancelled', 'Rejected')),
  CONSTRAINT ck_proforma_invoices_revision CHECK (revision_number >= 0)
);
CREATE INDEX ix_proforma_invoices_tenant_status ON saas.proforma_invoices (tenant_id, status);
CREATE INDEX ix_proforma_invoices_tenant_issue_date ON saas.proforma_invoices (tenant_id, issue_date);
CREATE INDEX ix_proforma_invoices_tenant_client ON saas.proforma_invoices (tenant_id, client_id);
CREATE INDEX ix_proforma_invoices_tenant_root ON saas.proforma_invoices (tenant_id, root_proforma_id);

-- ── spec groups (Step 5 grid groups) ──────────────────────────────────
CREATE TABLE saas.spec_groups (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  proforma_id uuid NOT NULL,
  group_number integer NOT NULL,
  glass_type text NOT NULL,
  spec_name text NOT NULL DEFAULT '',
  spec_id uuid,
  rate_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  price_source text NOT NULL DEFAULT 'formula',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_spec_groups PRIMARY KEY (id),
  CONSTRAINT fk_spec_groups_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_spec_groups_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE CASCADE,
  CONSTRAINT fk_spec_groups_specification FOREIGN KEY (spec_id) REFERENCES saas.specifications (id) ON DELETE SET NULL,
  CONSTRAINT ck_spec_groups_glass_type CHECK (glass_type IN ('SGU', 'DGU', 'LAM', 'DGU+LAM')),
  CONSTRAINT ck_spec_groups_price_source CHECK (price_source IN ('formula', 'manual', 'target'))
);
CREATE INDEX ix_spec_groups_tenant_proforma ON saas.spec_groups (tenant_id, proforma_id);

CREATE TABLE saas.spec_group_items (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  spec_group_id uuid NOT NULL,
  row_serial integer NOT NULL DEFAULT 0,
  position text NOT NULL DEFAULT '',
  width_mm integer NOT NULL DEFAULT 0,
  height_mm integer NOT NULL DEFAULT 0,
  qty integer NOT NULL DEFAULT 1,
  w1 integer NOT NULL DEFAULT 0,
  h1 integer NOT NULL DEFAULT 0,
  w2 integer NOT NULL DEFAULT 0,
  h2 integer NOT NULL DEFAULT 0,
  sqm numeric(12,3) NOT NULL DEFAULT 0,
  total_sqm numeric(12,3) NOT NULL DEFAULT 0,
  unit_price numeric(18,2) NOT NULL DEFAULT 0,
  total numeric(18,2) NOT NULL DEFAULT 0,
  notes text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_spec_group_items PRIMARY KEY (id),
  CONSTRAINT fk_spec_group_items_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_spec_group_items_group FOREIGN KEY (spec_group_id) REFERENCES saas.spec_groups (id) ON DELETE CASCADE,
  CONSTRAINT ck_spec_group_items_qty CHECK (qty > 0)
);
CREATE INDEX ix_spec_group_items_tenant_group ON saas.spec_group_items (tenant_id, spec_group_id);

-- ── other charges (Step 6) ────────────────────────────────────────────
-- metric_key + checked_spec_group_numbers = WPF Chat-20 per-spec targeting.
CREATE TABLE saas.other_charges (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  proforma_id uuid NOT NULL,
  charge_name text NOT NULL,
  metric_key text NOT NULL DEFAULT 'Fixed',
  checked_spec_group_numbers jsonb NOT NULL DEFAULT '[]',
  rate numeric(18,2) NOT NULL DEFAULT 0,
  amount numeric(18,2) NOT NULL DEFAULT 0,
  is_active boolean NOT NULL DEFAULT true,
  sort_order integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_other_charges PRIMARY KEY (id),
  CONSTRAINT fk_other_charges_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_other_charges_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE CASCADE,
  CONSTRAINT ck_other_charges_metric CHECK (metric_key IN ('SQM', 'LM', 'Qty', 'Fixed'))
);
CREATE INDEX ix_other_charges_tenant_proforma ON saas.other_charges (tenant_id, proforma_id);

-- ── wastage rows (Step 6 wastages section) ────────────────────────────
CREATE TABLE saas.wastage_rows (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  proforma_id uuid NOT NULL,
  spec_group_id uuid,
  color_name text NOT NULL DEFAULT '',
  stock_sqm numeric(12,4) NOT NULL DEFAULT 0,
  glass_sqm numeric(12,4) NOT NULL DEFAULT 0,
  optimization_percent numeric(6,3) NOT NULL DEFAULT 0,
  sheet_quantity integer NOT NULL DEFAULT 0,
  formula_price_per_sqm numeric(18,2) NOT NULL DEFAULT 0,
  rate numeric(18,2) NOT NULL DEFAULT 0,
  result_amount numeric(18,2) NOT NULL DEFAULT 0,
  is_active boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_wastage_rows PRIMARY KEY (id),
  CONSTRAINT fk_wastage_rows_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_wastage_rows_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE CASCADE,
  CONSTRAINT fk_wastage_rows_spec_group FOREIGN KEY (spec_group_id) REFERENCES saas.spec_groups (id) ON DELETE SET NULL
);
CREATE INDEX ix_wastage_rows_tenant_proforma ON saas.wastage_rows (tenant_id, proforma_id);

-- ── job orders ────────────────────────────────────────────────────────
CREATE TABLE saas.job_orders (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  job_no text NOT NULL,
  proforma_id uuid,
  status text NOT NULL DEFAULT 'InProduction',
  status_text text NOT NULL DEFAULT '',
  customer_id uuid,
  project_id uuid,
  salesman_id uuid,
  due_date date,
  notes text NOT NULL DEFAULT '',
  row_version integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_job_orders PRIMARY KEY (id),
  CONSTRAINT fk_job_orders_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_job_orders_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE SET NULL,
  CONSTRAINT fk_job_orders_client FOREIGN KEY (customer_id) REFERENCES saas.clients (id) ON DELETE SET NULL,
  CONSTRAINT fk_job_orders_project FOREIGN KEY (project_id) REFERENCES saas.projects (id) ON DELETE SET NULL,
  CONSTRAINT fk_job_orders_salesman FOREIGN KEY (salesman_id) REFERENCES saas.salesmen (id) ON DELETE SET NULL,
  CONSTRAINT uq_job_orders_tenant_no UNIQUE (tenant_id, job_no),
  CONSTRAINT ck_job_orders_status CHECK (status IN ('InProduction', 'Completed', 'Delivered', 'Approved'))
);
CREATE INDEX ix_job_orders_tenant_status ON saas.job_orders (tenant_id, status);
CREATE INDEX ix_job_orders_tenant_proforma ON saas.job_orders (tenant_id, proforma_id);

-- ── production pieces + events ────────────────────────────────────────
CREATE TABLE saas.production_pieces (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  job_order_id uuid NOT NULL,
  piece_label text NOT NULL DEFAULT '',
  spec_group_item_id uuid,
  stage text NOT NULL DEFAULT 'cutting',
  status text NOT NULL DEFAULT 'pending',
  width_mm integer NOT NULL DEFAULT 0,
  height_mm integer NOT NULL DEFAULT 0,
  qty integer NOT NULL DEFAULT 1,
  optimization_sheet_id uuid,
  notes text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_production_pieces PRIMARY KEY (id),
  CONSTRAINT fk_production_pieces_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_pieces_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_pieces_item FOREIGN KEY (spec_group_item_id) REFERENCES saas.spec_group_items (id) ON DELETE SET NULL,
  CONSTRAINT ck_production_pieces_stage CHECK (stage IN ('cutting', 'lamination', 'edge_deletion', 'holes', 'mitring', 'pairing', 'polish', 'done')),
  CONSTRAINT ck_production_pieces_status CHECK (status IN ('pending', 'in_progress', 'qc_pass', 'qc_fail', 'broken', 'released'))
);
CREATE INDEX ix_production_pieces_tenant_job ON saas.production_pieces (tenant_id, job_order_id);
CREATE INDEX ix_production_pieces_tenant_stage ON saas.production_pieces (tenant_id, stage, status);

CREATE TABLE saas.production_events (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  production_piece_id uuid NOT NULL,
  event_type text NOT NULL,
  payload jsonb,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_production_events PRIMARY KEY (id),
  CONSTRAINT fk_production_events_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_production_events_piece FOREIGN KEY (production_piece_id) REFERENCES saas.production_pieces (id) ON DELETE CASCADE,
  CONSTRAINT ck_production_events_type CHECK (event_type IN ('stage', 'qc', 'breakage', 'cancel', 'release'))
);
CREATE INDEX ix_production_events_tenant_piece ON saas.production_events (tenant_id, production_piece_id, created_at);

CREATE TABLE saas.material_reservations (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  job_order_id uuid NOT NULL,
  glass_sheet_id uuid NOT NULL,
  reserved_qty integer NOT NULL,
  status text NOT NULL DEFAULT 'reserved',
  reserved_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_material_reservations PRIMARY KEY (id),
  CONSTRAINT fk_material_reservations_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_material_reservations_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE CASCADE,
  CONSTRAINT fk_material_reservations_glass_sheet FOREIGN KEY (glass_sheet_id) REFERENCES saas.glass_sheets (id) ON DELETE CASCADE,
  CONSTRAINT fk_material_reservations_user FOREIGN KEY (reserved_by) REFERENCES saas.users (id) ON DELETE SET NULL,
  CONSTRAINT ck_material_reservations_status CHECK (status IN ('reserved', 'consumed', 'released')),
  CONSTRAINT ck_material_reservations_qty CHECK (reserved_qty > 0)
);
CREATE INDEX ix_material_reservations_tenant_job ON saas.material_reservations (tenant_id, job_order_id);

-- ── delivery notes ────────────────────────────────────────────────────
CREATE TABLE saas.delivery_notes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  delivery_no text NOT NULL,
  job_order_id uuid,
  status text NOT NULL DEFAULT 'Draft',
  status_text text NOT NULL DEFAULT '',
  customer_id uuid,
  project_id uuid,
  delivery_date date,
  driver text NOT NULL DEFAULT '',
  vehicle text NOT NULL DEFAULT '',
  notes text NOT NULL DEFAULT '',
  row_version integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_delivery_notes PRIMARY KEY (id),
  CONSTRAINT fk_delivery_notes_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_delivery_notes_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE SET NULL,
  CONSTRAINT fk_delivery_notes_client FOREIGN KEY (customer_id) REFERENCES saas.clients (id) ON DELETE SET NULL,
  CONSTRAINT fk_delivery_notes_project FOREIGN KEY (project_id) REFERENCES saas.projects (id) ON DELETE SET NULL,
  CONSTRAINT uq_delivery_notes_tenant_no UNIQUE (tenant_id, delivery_no),
  CONSTRAINT ck_delivery_notes_status CHECK (status IN ('Draft', 'Dispatching', 'Dispatched', 'Signed', 'Received', 'Delivered'))
);
CREATE INDEX ix_delivery_notes_tenant_status ON saas.delivery_notes (tenant_id, status);
CREATE INDEX ix_delivery_notes_tenant_job ON saas.delivery_notes (tenant_id, job_order_id);

CREATE TABLE saas.delivery_note_items (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  delivery_note_id uuid NOT NULL,
  spec_group_item_id uuid,
  description text NOT NULL DEFAULT '',
  width_mm integer NOT NULL DEFAULT 0,
  height_mm integer NOT NULL DEFAULT 0,
  qty integer NOT NULL DEFAULT 1,
  sqm numeric(12,3) NOT NULL DEFAULT 0,
  delivered_qty integer NOT NULL DEFAULT 0,
  returned_qty integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_delivery_note_items PRIMARY KEY (id),
  CONSTRAINT fk_delivery_note_items_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_delivery_note_items_note FOREIGN KEY (delivery_note_id) REFERENCES saas.delivery_notes (id) ON DELETE CASCADE,
  CONSTRAINT fk_delivery_note_items_item FOREIGN KEY (spec_group_item_id) REFERENCES saas.spec_group_items (id) ON DELETE SET NULL,
  CONSTRAINT ck_delivery_note_items_qty CHECK (qty >= 0 AND delivered_qty >= 0 AND returned_qty >= 0)
);
CREATE INDEX ix_delivery_note_items_tenant_note ON saas.delivery_note_items (tenant_id, delivery_note_id);

-- ── tax invoices + credit notes ───────────────────────────────────────
CREATE TABLE saas.tax_invoices (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  invoice_no text NOT NULL,
  proforma_id uuid,
  delivery_note_reference text NOT NULL DEFAULT '',
  job_order_id uuid,
  status text NOT NULL DEFAULT 'Issued',
  status_text text NOT NULL DEFAULT '',
  issue_date date,
  due_date date,
  subtotal numeric(18,2) NOT NULL DEFAULT 0,
  discount_amount numeric(18,2) NOT NULL DEFAULT 0,
  vat_amount numeric(18,2) NOT NULL DEFAULT 0,
  grand_total numeric(18,2) NOT NULL DEFAULT 0,
  paid_amount numeric(18,2) NOT NULL DEFAULT 0,
  balance_amount numeric(18,2) NOT NULL DEFAULT 0,
  snapshot_json jsonb,
  row_version integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_tax_invoices PRIMARY KEY (id),
  CONSTRAINT fk_tax_invoices_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_tax_invoices_proforma FOREIGN KEY (proforma_id) REFERENCES saas.proforma_invoices (id) ON DELETE SET NULL,
  CONSTRAINT fk_tax_invoices_job_order FOREIGN KEY (job_order_id) REFERENCES saas.job_orders (id) ON DELETE SET NULL,
  CONSTRAINT uq_tax_invoices_tenant_no UNIQUE (tenant_id, invoice_no),
  CONSTRAINT ck_tax_invoices_status CHECK (status IN ('Issued', 'Paid', 'Overdue'))
);
CREATE INDEX ix_tax_invoices_tenant_status ON saas.tax_invoices (tenant_id, status);
CREATE INDEX ix_tax_invoices_tenant_issue_date ON saas.tax_invoices (tenant_id, issue_date);

CREATE TABLE saas.credit_notes (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  credit_note_no text NOT NULL,
  tax_invoice_id uuid,
  reason text NOT NULL DEFAULT '',
  subtotal numeric(18,2) NOT NULL DEFAULT 0,
  vat_amount numeric(18,2) NOT NULL DEFAULT 0,
  grand_total numeric(18,2) NOT NULL DEFAULT 0,
  status text NOT NULL DEFAULT 'draft',
  issued_at timestamptz,
  voided_at timestamptz,
  voided_by uuid,
  void_reason text NOT NULL DEFAULT '',
  row_version integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_credit_notes PRIMARY KEY (id),
  CONSTRAINT fk_credit_notes_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_credit_notes_tax_invoice FOREIGN KEY (tax_invoice_id) REFERENCES saas.tax_invoices (id) ON DELETE SET NULL,
  CONSTRAINT fk_credit_notes_voided_by FOREIGN KEY (voided_by) REFERENCES saas.users (id) ON DELETE SET NULL,
  CONSTRAINT uq_credit_notes_tenant_no UNIQUE (tenant_id, credit_note_no),
  CONSTRAINT ck_credit_notes_status CHECK (status IN ('draft', 'issued', 'void'))
);
CREATE INDEX ix_credit_notes_tenant_status ON saas.credit_notes (tenant_id, status);

-- ── statements ────────────────────────────────────────────────────────
CREATE TABLE saas.statements (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  statement_no text NOT NULL,
  title text NOT NULL DEFAULT '',
  filter_company text NOT NULL DEFAULT '',
  filter_customer text NOT NULL DEFAULT '',
  filter_project text NOT NULL DEFAULT '',
  filter_salesman text NOT NULL DEFAULT '',
  filter_date_from date,
  filter_date_to date,
  generated_at timestamptz,
  totals_json jsonb,
  storage_key text NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_statements PRIMARY KEY (id),
  CONSTRAINT fk_statements_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT uq_statements_tenant_no UNIQUE (tenant_id, statement_no)
);
CREATE INDEX ix_statements_tenant_generated ON saas.statements (tenant_id, generated_at);

CREATE TABLE saas.statement_lines (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  tenant_id uuid NOT NULL,
  statement_id uuid NOT NULL,
  line_type text NOT NULL DEFAULT '',
  document_no text NOT NULL DEFAULT '',
  document_date date,
  description text NOT NULL DEFAULT '',
  debit numeric(18,2) NOT NULL DEFAULT 0,
  credit numeric(18,2) NOT NULL DEFAULT 0,
  running_balance numeric(18,2) NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  created_by uuid,
  updated_by uuid,
  CONSTRAINT pk_statement_lines PRIMARY KEY (id),
  CONSTRAINT fk_statement_lines_tenant FOREIGN KEY (tenant_id) REFERENCES saas.tenants (id) ON DELETE CASCADE,
  CONSTRAINT fk_statement_lines_statement FOREIGN KEY (statement_id) REFERENCES saas.statements (id) ON DELETE CASCADE
);
CREATE INDEX ix_statement_lines_tenant_statement ON saas.statement_lines (tenant_id, statement_id);

-- updated_at triggers for every mutable table in this file
-- (attachments and production_events are immutable — no trigger).
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['inquiries', 'specifications', 'tenant_pricing_config',
                           'tenant_vat_config', 'proforma_invoices', 'spec_groups',
                           'spec_group_items', 'other_charges', 'wastage_rows',
                           'job_orders', 'production_pieces', 'material_reservations',
                           'delivery_notes', 'delivery_note_items', 'tax_invoices',
                           'credit_notes', 'statements', 'statement_lines'] LOOP
    EXECUTE format('CREATE TRIGGER trg_%I_touch BEFORE UPDATE ON saas.%I '
      'FOR EACH ROW EXECUTE FUNCTION saas.touch_updated_at()', t, t);
  END LOOP;
END $$;
