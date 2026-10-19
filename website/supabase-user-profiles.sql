-- ProGlass customer/community user profiles.
-- Run after supabase-global-chat.sql and supabase-global-chat-v2-replies-receipts.sql.
--
-- This table intentionally contains only chat-safe public profile fields:
-- display name, chosen avatar icon, and avatar colour. Private inquiry
-- profile data (company, phone, VAT/TRN) stays in inquiry_customer_profiles
-- behind its own-row policy and is never exposed through this table.

CREATE TABLE IF NOT EXISTS public.user_profiles (
  user_id uuid PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  display_name text NOT NULL CHECK (char_length(btrim(display_name)) BETWEEN 1 AND 60),
  avatar_icon text NOT NULL DEFAULT 'initials' CHECK (
    avatar_icon IN (
      'initials', 'smile', 'star', 'gem', 'home', 'building', 'ruler',
      'tools', 'leaf', 'sun', 'wave', 'shield', 'sparkles'
    )
  ),
  avatar_color text NOT NULL DEFAULT 'brand' CHECK (
    avatar_color IN ('brand', 'emerald', 'sky', 'amber', 'rose', 'violet', 'slate')
  ),
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE OR REPLACE FUNCTION public.touch_user_profiles_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.updated_at := now();
  RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS user_profiles_touch_updated_at ON public.user_profiles;
CREATE TRIGGER user_profiles_touch_updated_at
BEFORE UPDATE ON public.user_profiles
FOR EACH ROW EXECUTE FUNCTION public.touch_user_profiles_updated_at();

ALTER TABLE public.user_profiles ENABLE ROW LEVEL SECURITY;

REVOKE ALL ON TABLE public.user_profiles FROM PUBLIC, anon;
GRANT SELECT, INSERT, UPDATE ON TABLE public.user_profiles TO authenticated;

DROP POLICY IF EXISTS "Users read own or community participant profiles" ON public.user_profiles;
CREATE POLICY "Users read own or community participant profiles" ON public.user_profiles
  FOR SELECT TO authenticated
  USING (
    user_id = auth.uid()
    OR EXISTS (
      SELECT 1
        FROM public.global_chat_messages m
       WHERE m.author_user_id = public.user_profiles.user_id
    )
    OR EXISTS (
      SELECT 1
        FROM public.global_chat_receipts r
       WHERE r.user_id = public.user_profiles.user_id
    )
  );

DROP POLICY IF EXISTS "Users create own profile" ON public.user_profiles;
CREATE POLICY "Users create own profile" ON public.user_profiles
  FOR INSERT TO authenticated
  WITH CHECK (user_id = auth.uid());

DROP POLICY IF EXISTS "Users update own profile" ON public.user_profiles;
CREATE POLICY "Users update own profile" ON public.user_profiles
  FOR UPDATE TO authenticated
  USING (user_id = auth.uid())
  WITH CHECK (user_id = auth.uid());

-- Give existing community participants a safe initials profile. They can
-- change the icon/colour from the customer account page at any time.
INSERT INTO public.user_profiles(user_id, display_name, avatar_icon, avatar_color)
SELECT
  u.id,
  left(
    regexp_replace(
      btrim(coalesce(
        nullif(btrim(u.raw_user_meta_data->>'full_name'), ''),
        nullif(btrim(u.raw_user_meta_data->>'name'), ''),
        nullif(split_part(coalesce(u.email, ''), '@', 1), ''),
        'ProGlass user'
      )),
      '\s+',
      ' ',
      'g'
    ),
    60
  ),
  'initials',
  'brand'
FROM auth.users u
WHERE EXISTS (
        SELECT 1 FROM public.global_chat_messages m WHERE m.author_user_id = u.id
      )
   OR EXISTS (
        SELECT 1 FROM public.global_chat_receipts r WHERE r.user_id = u.id
      )
   OR EXISTS (
        SELECT 1 FROM public.global_chat_reads gr WHERE gr.user_id = u.id
      )
ON CONFLICT (user_id) DO NOTHING;

ALTER TABLE public.user_profiles REPLICA IDENTITY FULL;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_publication WHERE pubname = 'supabase_realtime')
     AND NOT EXISTS (
       SELECT 1
         FROM pg_publication_tables
        WHERE pubname = 'supabase_realtime'
          AND schemaname = 'public'
          AND tablename = 'user_profiles'
     ) THEN
    ALTER PUBLICATION supabase_realtime ADD TABLE public.user_profiles;
  END IF;
END
$$;

NOTIFY pgrst, 'reload schema';
