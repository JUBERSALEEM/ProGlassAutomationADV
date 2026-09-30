import type { Overlay } from "../core";

/**
 * Russian — Русский.
 *
 * Russia, Kazakhstan and Belarus.
 *
 * The key of every entry is the English sentence the site renders; the value is
 * what a reader of this language sees instead. Entries come from two places:
 * the hand-written translations below, and the application's own language file
 * (`Themes/Languages/ru-RU.xaml`), imported by `scripts/i18n-merge-app.mjs` so
 * that a term the software already names — a base rate, a delivery note, a
 * stage — is called the same thing in both.
 *
 * A sentence with no entry here is shown in English rather than hidden, which
 * is why this file can be incomplete without the site being broken.
 */
const dict: Overlay = {
  "© 2026 Juber Saleem. Made in India. All rights reserved.": "© 2026 Джубер Салим. Сделано в Индии. Все права защищены.",
  "About": "О программе",
  "Ageing brackets": "Группы по срокам давности",
  "AI Assistant": "ИИ-помощник",
  "Appearance": "Оформление",
  "Available SQM by glass category": "Доступные м² по категориям стекла",
  "Balance": "Остаток",
  "Category mix": "Смесь категорий",
  "Close": "Закрыть",
  "Collections pulse": "Пульс сборов",
  "Company": "Компания",
  "Company Center": "Центр компаний",
  "Country": "Страна",
  "Delivered": "Доставлено",
  "Delivery Note": "Накладная",
  "Delivery Note Report": "Отчёт по товарным накладным",
  "Developer": "Разработчик",
  "Doubling / Lamination": "Дублирование / ламинация",
  "Edge Deletion": "Удаление кромки",
  "Email": "Эл. почта",
  "Features": "Возможности",
  "Finance & Accounting": "Финансы и бухгалтерия",
  "Generate a statement to see customer balances here.": "Сгенерируйте выписку, чтобы увидеть здесь остатки клиентов.",
  "GLASS REF": "REF СТЕКЛА",
  "GLASS REF · W1 H1 W2 H2 · QTY · SQM · T.SQM · RATE · VALUE.": "REF СТЕКЛА · W1 H1 W2 H2 · КОЛ-ВО · м² · ОБЩ. м² · СТАВКА · СУММА.",
  "Glass type": "Тип стекла",
  "Holes & Cutouts": "Отверстия и вырезы",
  "incl. critical": "вкл. критические",
  "Job Order": "Заказ на работу",
  "Job Order Report": "Отчёт по заказам на работу",
  "Job Orders": "Заказы на работу",
  "Job Profitability": "Рентабельность заказов",
  "Language": "Язык",
  "last 500 kept in memory": "последние 500 в памяти",
  "Last updated": "Последнее обновление",
  "License": "Лицензия",
  "LOG ENTRIES": "ЗАПИСИ ЖУРНАЛА",
  "Logs & Activity": "Журналы и активность",
  "Master Data": "Основные данные",
  "MODULE / FUNCTION": "МОДУЛЬ / ФУНКЦИЯ",
  "Name": "Имя",
  "Next": "Далее",
  "No log entries yet": "Записей журнала пока нет",
  "None": "Нет",
  "Notes": "Примечания",
  "OPEN LOG FOLDER": "ОТКРЫТЬ ПАПКУ ЖУРНАЛА",
  "Optimization": "Оптимизация",
  "Ordered": "Заказано",
  "Overdue amounts grouped by age": "Просроченные суммы, сгруппированные по сроку давности",
  "Paste": "Вставить",
  "Polish": "Полировка",
  "Privacy": "Конфиденциальность",
  "Probe every database and refresh the row-count table": "Проверить каждую базу данных и обновить таблицу количества строк",
  "Product": "Продукт",
  "Production pulse": "Пульс производства",
  "Production status": "Статус производства",
  "Proforma Invoice": "Счёт-проформа",
  "Proforma Invoice Report": "Отчёт по счетам-проформам",
  "Qty": "Кол-во",
  "Quote pulse": "Пульс котировок",
  "Ready": "Готово",
  "Runtime": "Средство выполнения",
  "Sales & Orders": "Продажи и заказы",
  "Search": "Поиск",
  "Sheet Store": "Склад листов",
  "Single lite": "Одиночный лист",
  "Specs": "Спецификации",
  "Startup, warnings, errors and crashes will appear here.": "Запуски, предупреждения, ошибки и сбои будут появляться здесь.",
  "Statement Generator": "Генератор выписок",
  "Statement pulse": "Пульс выписок",
  "Statement Report": "Отчёт по выписке",
  "Status distribution": "Распределение статусов",
  "Stock pulse": "Пульс запаса",
  "Storage": "Хранилище",
  "SURCHARGE %": "НАЦЕНКА %",
  "System Center": "Системный центр",
  "Tax Invoice": "Налоговый счёт",
  "Tax Invoice Report": "Отчёт по налоговым счетам",
  "Tax Invoices": "Налоговые счета",
  "Technical diagnostics and per-module user activity — live": "Техническая диагностика и активность пользователя по модулям — вживую",
  "Top customers": "Лучшие клиенты",
  "TOTAL SQM": "ОБЩ. м²",
  "Total SQM (SQM × Qty)": "Общая м² (м² × кол-во)",
  "Unit panel area": "Площадь изделия",
  "User Center": "Центр пользователей",
  "VAT Submission": "Подача НДС",
  "VAT Submission Report": "Отчёт по налоговой декларации НДС",
  "Workspace": "Рабочее пространство",
};

export default dict;
