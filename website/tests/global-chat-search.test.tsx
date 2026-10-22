import { describe, expect, it } from "vitest";
import {
  GLOBAL_CHAT_SEARCH_MIN,
  globalChatMentionPattern,
  globalChatSearchExcerpt,
  globalChatSearchPattern,
  isGlobalChatSearchReady,
  matchesGlobalChatSearch,
  mentionsGlobalChatUser,
  normalizeGlobalChatSearchTerm,
  splitGlobalChatMentions,
} from "@/lib/global-chat";

/*
 * Room search is a server query, so the only client-side guarantees are that
 * the term cannot change the query, that a short term does not fire a request,
 * and that a match in the middle of a long message is still shown with its
 * surrounding words.
 */
describe("global chat search term", () => {
  it("normalises whitespace and caps the term", () => {
    expect(normalizeGlobalChatSearchTerm("  frame   thickness \n")).toBe("frame thickness");
    expect(normalizeGlobalChatSearchTerm("x".repeat(200))).toHaveLength(80);
    expect(normalizeGlobalChatSearchTerm(undefined as unknown as string)).toBe("");
  });

  it("needs two characters before a query is worth sending", () => {
    expect(GLOBAL_CHAT_SEARCH_MIN).toBe(2);
    expect(isGlobalChatSearchReady(" ")).toBe(false);
    expect(isGlobalChatSearchReady("a")).toBe(false);
    expect(isGlobalChatSearchReady(" ab ")).toBe(true);
  });

  it("quotes the pattern and escapes every PostgREST wildcard", () => {
    expect(globalChatSearchPattern("frame")).toBe('"%frame%"');
    expect(globalChatSearchPattern("50%")).toBe('"%50\\%%"');
    expect(globalChatSearchPattern("a_b")).toBe('"%a\\_b%"');
    expect(globalChatSearchPattern("a*b")).toBe('"%a\\*b%"');
    expect(globalChatSearchPattern('say "hi"')).toBe('"%say \\"hi\\"%"');
    expect(globalChatSearchPattern("c:\\work")).toBe('"%c:\\\\work%"');
    expect(globalChatSearchPattern("glass, frame")).toBe('"%glass, frame%"');
  });

  it("matches case-insensitively without treating the term as a pattern", () => {
    expect(matchesGlobalChatSearch("Toughened Glass", "glass")).toBe(true);
    expect(matchesGlobalChatSearch("Toughened Glass", "GLA")).toBe(true);
    expect(matchesGlobalChatSearch("Toughened Glass", "50%")).toBe(false);
    expect(matchesGlobalChatSearch("Toughened Glass", "")).toBe(false);
  });
});

describe("global chat search excerpt", () => {
  it("shows the words around a match in the middle of a long message", () => {
    const body = `${"a ".repeat(80)}frame thickness matters${" b".repeat(80)}`;
    const excerpt = globalChatSearchExcerpt(body, "frame");
    expect(excerpt).not.toBeNull();
    expect(excerpt?.match).toBe("frame");
    expect(excerpt?.before.startsWith("…")).toBe(true);
    expect(excerpt?.after.endsWith("…")).toBe(true);
    expect(excerpt?.after).toContain("thickness");
  });

  it("keeps the original casing of the matched words", () => {
    const excerpt = globalChatSearchExcerpt("Use Toughened Glass here", "glass");
    expect(excerpt?.match).toBe("Glass");
  });

  it("returns null when nothing matches or the term is empty", () => {
    expect(globalChatSearchExcerpt("Toughened glass", "laminate")).toBeNull();
    expect(globalChatSearchExcerpt("Toughened glass", "  ")).toBeNull();
    expect(globalChatSearchExcerpt("", "glass")).toBeNull();
  });
});

describe("global chat mentions", () => {
  it("reaches a member by full name or first word", () => {
    expect(mentionsGlobalChatUser("@Juber Saleem please check", "Juber Saleem")).toBe(true);
    expect(mentionsGlobalChatUser("@juber please check", "Juber Saleem")).toBe(true);
    expect(mentionsGlobalChatUser("hi @Juber, ready?", "Juber Saleem")).toBe(true);
  });

  it("does not reach a member through a longer word or another name", () => {
    expect(mentionsGlobalChatUser("@JuberX please check", "Juber Saleem")).toBe(false);
    expect(mentionsGlobalChatUser("@Saleem please check", "Juber Saleem")).toBe(false);
    expect(mentionsGlobalChatUser("no mention here", "Juber Saleem")).toBe(false);
    expect(mentionsGlobalChatUser("@anything", "")).toBe(false);
  });

  it("builds no pattern for an empty display name", () => {
    expect(globalChatMentionPattern("")).toBeNull();
    expect(globalChatMentionPattern("a")).toBeNull();
  });

  it("splits a body without losing or reordering a character", () => {
    const body = "Morning @Juber Saleem, the frame is ready. @Juber please confirm.";
    const segments = splitGlobalChatMentions(body, "Juber Saleem");
    expect(segments.map((segment) => segment.text).join("")).toBe(body);
    expect(segments.filter((segment) => segment.mention).map((segment) => segment.text)).toEqual([
      "@Juber Saleem",
      "@Juber",
    ]);
    expect(segments.filter((segment) => !segment.mention).every((segment) => !segment.text.includes("@"))).toBe(true);
  });

  it("leaves a body untouched when the member has no name", () => {
    expect(splitGlobalChatMentions("plain text", "")).toEqual([{ text: "plain text", mention: false }]);
    expect(splitGlobalChatMentions("", "Juber")).toEqual([]);
  });
});
