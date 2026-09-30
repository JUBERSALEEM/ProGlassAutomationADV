import type { Overlay } from "../core";

/**
 * French — Français.
 *
 * France and Belgium.
 *
 * The key of every entry is the English sentence the site renders; the value is
 * what a reader of this language sees instead. Entries come from two places:
 * the hand-written translations below, and the application's own language file
 * (`Themes/Languages/fr-FR.xaml`), imported by `scripts/i18n-merge-app.mjs` so
 * that a term the software already names — a base rate, a delivery note, a
 * stage — is called the same thing in both.
 *
 * A sentence with no entry here is shown in English rather than hidden, which
 * is why this file can be incomplete without the site being broken.
 */
const dict: Overlay = {
  "© 2026 Juber Saleem. Made in India. All rights reserved.": "© 2026 Juber Saleem. Fabriqué en Inde. Tous droits réservés.",
  "About": "À propos",
  "Ageing brackets": "Tranches d'ancienneté",
  "AI Assistant": "Assistant IA",
  "Appearance": "Apparence",
  "Available SQM by glass category": "M² disponibles par catégorie de verre",
  "Balance": "Solde",
  "Category mix": "Mélange de catégories",
  "Close": "Fermer",
  "Collections pulse": "Pouls des encaissements",
  "Company": "Entreprise",
  "Company Center": "Centre des sociétés",
  "Country": "Pays",
  "Delivered": "Livré",
  "Delivery Note": "Bon de livraison",
  "Delivery Note Report": "Rapport des notes de livraison",
  "Developer": "Développeur",
  "Doubling / Lamination": "Doublage / Stratification",
  "Edge Deletion": "Suppression des bords",
  "Email": "E-mail",
  "Features": "Fonctions",
  "Finance & Accounting": "Finances et comptabilité",
  "Generate a statement to see customer balances here.": "Générez un état de compte pour voir les soldes clients ici.",
  "GLASS REF": "RÉF. VERRE",
  "GLASS REF · W1 H1 W2 H2 · QTY · SQM · T.SQM · RATE · VALUE.": "RÉF. VERRE · W1 H1 W2 H2 · QTÉ · m² · m² TOT. · TAUX · VALEUR.",
  "Glass type": "Type de verre",
  "Holes & Cutouts": "Percages et découpes",
  "incl. critical": "dont critiques",
  "Job Order": "Ordre de fabrication",
  "Job Order Report": "Rapport des ordres de travail",
  "Job Orders": "Ordres de travail",
  "Job Profitability": "Rentabilité des travaux",
  "Language": "Langue",
  "last 500 kept in memory": "500 dernières conservées en mémoire",
  "Last updated": "Dernière mise à jour",
  "License": "Licence",
  "LOG ENTRIES": "ENTRÉES DE JOURNAL",
  "Logs & Activity": "Journaux et activité",
  "Master Data": "Données de référence",
  "MODULE / FUNCTION": "MODULE / FONCTION",
  "Name": "Nom",
  "Next": "Suivant",
  "No log entries yet": "Aucune entrée de journal pour l'instant",
  "None": "Aucun",
  "OPEN LOG FOLDER": "OUVRIR LE DOSSIER DE JOURNAL",
  "Optimization": "Optimisation",
  "Ordered": "Commandé",
  "Overdue amounts grouped by age": "Montants en retard groupés par ancienneté",
  "Paste": "Coller",
  "Polish": "Polissage",
  "Privacy": "Confidentialité",
  "Probe every database and refresh the row-count table": "Sonder chaque base de données et actualiser le tableau des nombres de lignes",
  "Product": "Produit",
  "Production pulse": "Pouls de la production",
  "Production status": "Statut de production",
  "Proforma Invoice": "Facture proforma",
  "Proforma Invoice Report": "Rapport des factures proforma",
  "Qty": "Qté",
  "Quote pulse": "Pouls des cotations",
  "Ready": "Prêt",
  "Runtime": "Environnement d'exécution",
  "Sales & Orders": "Ventes et commandes",
  "Search": "Rechercher",
  "Sheet Store": "Stock de plaques",
  "Single lite": "Vitre unique",
  "Specs": "Spécifications",
  "Startup, warnings, errors and crashes will appear here.": "Démarrages, avertissements, erreurs et plantages apparaîtront ici.",
  "Statement Generator": "Générateur de relevés",
  "Statement pulse": "Pouls des états de compte",
  "Statement Report": "Rapport d'état de compte",
  "Status distribution": "Distribution des statuts",
  "Stock pulse": "Pouls du stock",
  "Storage": "Stockage",
  "SURCHARGE %": "MAJORATION %",
  "System Center": "Centre système",
  "Tax Invoice": "Facture de taxe",
  "Tax Invoice Report": "Rapport des factures fiscales",
  "Tax Invoices": "Factures fiscales",
  "Technical diagnostics and per-module user activity — live": "Diagnostics techniques et activité utilisateur par module — en direct",
  "Top customers": "Principaux clients",
  "TOTAL SQM": "m² TOTAL",
  "Total SQM (SQM × Qty)": "m² total (m² × quantité)",
  "Unit panel area": "Surface unitaire",
  "User Center": "Centre des utilisateurs",
  "VAT Submission": "Déclaration de TVA",
  "VAT Submission Report": "Rapport de déclaration de TVA",
  "Workspace": "Espace de travail",
};

export default dict;
