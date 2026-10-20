export type GlobalChatMessage = {
  id: string;
  author_user_id: string;
  author_display_name: string;
  body: string;
  reply_to_message_id: string | null;
  deleted_at: string | null;
  deleted_by: string | null;
  created_at: string;
};

export type GlobalChatReadState = {
  user_id: string;
  last_read_at: string;
  updated_at: string;
};

export type GlobalChatReceipt = {
  message_id: string;
  user_id: string;
  display_name: string;
  delivered_at: string | null;
  read_at: string | null;
};
