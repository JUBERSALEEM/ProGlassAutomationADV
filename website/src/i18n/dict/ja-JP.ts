import type { Overlay } from "../core";

/**
 * Japanese — 日本語.
 *
 * Japan.
 *
 * The key of every entry is the English sentence the site renders; the value is
 * what a reader of this language sees instead. Entries come from two places:
 * the hand-written translations below, and the application's own language file
 * (`Themes/Languages/ja-JP.xaml`), imported by `scripts/i18n-merge-app.mjs` so
 * that a term the software already names — a base rate, a delivery note, a
 * stage — is called the same thing in both.
 *
 * A sentence with no entry here is shown in English rather than hidden, which
 * is why this file can be incomplete without the site being broken.
 */
const dict: Overlay = {
  "© 2026 Juber Saleem. Made in India. All rights reserved.": "© 2026 Juber Saleem。インド製。無断複写を禁じます。",
  "About": "情報",
  "Ageing brackets": "エージング区分",
  "AI Assistant": "AIアシスタント",
  "Appearance": "外観",
  "Available SQM by glass category": "ガラスカテゴリ別の利用可能M²",
  "Balance": "残数量",
  "Category mix": "カテゴリ構成",
  "Close": "閉じる",
  "Collections pulse": "回収パルス",
  "Company": "会社",
  "Company Center": "会社センター",
  "Country": "国",
  "Delivered": "納品済み",
  "Delivery Note": "納品書",
  "Delivery Note Report": "納品書レポート",
  "Developer": "開発者",
  "Doubling / Lamination": "合わせ / ラミネート",
  "Edge Deletion": "エッジ除去",
  "Email": "メール",
  "Features": "機能",
  "Finance & Accounting": "財務と会計",
  "Generate a statement to see customer balances here.": "明細書を生成すると、顧客残高がここに表示されます。",
  "GLASS REF": "ガラス参照",
  "GLASS REF · W1 H1 W2 H2 · QTY · SQM · T.SQM · RATE · VALUE.": "ガラス参照 · W1 H1 W2 H2 · 数量 · m² · 合計m² · 単価 · 金額。",
  "Glass type": "ガラスタイプ",
  "Holes & Cutouts": "穴あけ・切り抜き",
  "incl. critical": "重大含む",
  "Job Order": "作業指示書",
  "Job Order Report": "作業指示書レポート",
  "Job Orders": "作業指示書",
  "Job Profitability": "案件収益性",
  "Language": "言語",
  "last 500 kept in memory": "直近500件をメモリに保持",
  "Last updated": "最終更新",
  "License": "ライセンス",
  "LOG ENTRIES": "ログエントリ",
  "Logs & Activity": "ログとアクティビティ",
  "Master Data": "マスターデータ",
  "MODULE / FUNCTION": "モジュール / 機能",
  "Name": "名前",
  "Next": "次へ",
  "No log entries yet": "まだログエントリがありません",
  "None": "なし",
  "Notes": "メモ",
  "OPEN LOG FOLDER": "ログフォルダを開く",
  "Optimization": "最適化",
  "Ordered": "発注",
  "Overdue amounts grouped by age": "経過日数別にグループ化された期限超過額",
  "Paste": "貼り付け",
  "Polish": "研磨",
  "Privacy": "プライバシー",
  "Probe every database and refresh the row-count table": "すべてのデータベースを検査し、行数テーブルを更新",
  "Product": "製品",
  "Production pulse": "生産パルス",
  "Production status": "生産ステータス",
  "Proforma Invoice": "プロフォーマ請求書",
  "Proforma Invoice Report": "プロフォーマ請求書レポート",
  "Qty": "数量",
  "Quote pulse": "見積パルス",
  "Ready": "準備完了",
  "Runtime": "ランタイム",
  "Sales & Orders": "営業と注文",
  "Search": "検索",
  "Sheet Store": "ガラス在庫",
  "Single lite": "シングルペイン",
  "Specs": "仕様",
  "Startup, warnings, errors and crashes will appear here.": "起動、警告、エラー、クラッシュがここに表示されます。",
  "Statement Generator": "明細書生成",
  "Statement pulse": "明細書パルス",
  "Statement Report": "明細書レポート",
  "Status distribution": "ステータス分布",
  "Stock pulse": "在庫パルス",
  "Storage": "ストレージ",
  "SURCHARGE %": "割増 %",
  "System Center": "システムセンター",
  "Tax Invoice": "税務請求書",
  "Tax Invoice Report": "税務請求書レポート",
  "Tax Invoices": "税務請求書",
  "Technical diagnostics and per-module user activity — live": "技術診断とモジュール別ユーザーアクティビティ — ライブ",
  "Top customers": "上位顧客",
  "TOTAL SQM": "合計m²",
  "Total SQM (SQM × Qty)": "合計m²（m² × 数量）",
  "Unit panel area": "単位面積",
  "User Center": "ユーザーセンター",
  "VAT Submission": "付加価値税申告",
  "VAT Submission Report": "VAT申告レポート",
  "Workspace": "ワークスペース",
};

export default dict;
