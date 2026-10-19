export type GlobalChatMessage = {
  id: string;
  author_user_id: string;
  author_display_name: string;
  body: string;
  deleted_at: string | null;
  deleted_by: string | null;
  created_at: string;
};

export type GlobalChatReadState = {
  user_id: string;
  last_read_at: string;
  updated_at: string;
};
