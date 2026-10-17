-- PHASE 8C REPAIR — ALLOW THE AUDITED INBOX DELIVERY EVENT
-- Required only where phase8c-inbox.sql was applied before this repair.
BEGIN;

ALTER TABLE public.inquiry_customer_events
  DROP CONSTRAINT IF EXISTS inquiry_customer_events_event_type_check;
ALTER TABLE public.inquiry_customer_events
  ADD CONSTRAINT inquiry_customer_events_event_type_check
  CHECK (event_type IN (
    'draft_created',
    'submitted',
    'document_registered',
    'upload_failed',
    'status_changed',
    'download_authorized',
    'inbox_delivered'
  ));

COMMIT;
