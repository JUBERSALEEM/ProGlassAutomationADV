# 🤖 AI Domain — ProGlass Automation

<!-- pg-nav -->
> **[Repository root](../../README.md)** · [All documents](../../docs/README.md) · [Application workflow](../../docs/WORKFLOW.md) · [Design system](../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
This folder contains **all AI-related infrastructure and features**.
Everything internet-dependent, provider-integrated, or AI-powered lives here.

---

## 📁 Sub-Modules

```
Services/AI/
├── README.md                ← You are here (master roadmap)
├── AIService/               ← AI provider integration (Gemini, Groq, etc.)
│   ├── IAiService.cs        (contract)
│   ├── AiSettings.cs        (user config POCO)
│   └── GeminiAiService.cs   (Google Gemini implementation)
├── AIConnectivity/          ← Online/offline detection
│   ├── IConnectivityService.cs
│   └── ConnectivityService.cs
└── AIConfig/                ← Persistent config storage
    ├── AppConfig.cs
    └── ConfigService.cs
```

---

## 🎯 Current Provider: Google Gemini

- **Model**: `gemini-1.5-flash`
- **Free tier**: 15 req/min, 1,500 req/day, forever free
- **Sign up**: https://aistudio.google.com/ → "Get API Key"
- **No credit card required**

---

## ✅ Completed (Phase 1 — Foundation)

- [x] Domain folder structure (`Services/AI/AIService/`, `AIConnectivity/`, `AIConfig/`)
- [x] `IAiService` interface (provider-agnostic)
- [x] `GeminiAiService` implementation with `AskAsync()` + `TestConnectionAsync()`
- [x] `AiSettings` POCO (Enabled, ApiKey, Model, MaxTokens, Temperature)
- [x] `IConnectivityService` + polling implementation with OS event integration
- [x] `AppConfig` + `ConfigService` (JSON persistence in `%AppData%`)
- [x] Wire into `App.xaml.cs` startup (`App.Ai`, `App.Connectivity` globals)
- [x] Settings drawer: Enable AI toggle, API Key input, Test Connection button
- [x] Auto-save config on change and app exit

---

## 🚧 To-Do Roadmap

### 🟢 Phase 2 — First Real AI Feature *(after Sheet Store module is built)*
- [ ] **Supplier Invoice OCR** — Sheet Store: scan supplier invoice image/PDF → auto-fill items
- [ ] Create `AIPrompts/` folder with prompt templates library
- [ ] Add `IsAiOnline` computed property (Enabled + Configured + Connectivity.IsOnline)
- [ ] Online status indicator in top bar (small green/gray dot)

### 🟡 Phase 3 — Proforma Invoice AI Assistance
- [ ] **Product Description Generator** — "AI Suggest" button in Proforma line items
- [ ] **Smart Customer Lookup** — type partial name/GST → AI suggests matches from history
- [ ] **Auto-calculate discounts** — AI-driven pricing suggestions

### 🟠 Phase 4 — Chat Assistant
- [ ] Floating AI chat button (bottom-right corner of MainWindow)
- [ ] `Views/AI/AiChatView.xaml` + `ViewModels/AI/AiChatViewModel.cs`
- [ ] Streaming responses (chunked display)
- [ ] Chat history (in-memory first, DB later)
- [ ] Context-aware ("summarize current invoice", "show last month's sales")

### 🔴 Phase 5 — Advanced Intelligence
- [ ] **Cutting Optimization AI** — analyze pending job orders → suggest optimal cut layout
- [ ] **Report Insights** — natural language summaries in Sales Report
- [ ] **Voice Input** — Speech-to-Text (Hindi + English) for invoice creation
- [ ] Add **Groq** as fallback provider (`AIService/GroqAiService.cs`)
- [ ] Provider auto-failover (Gemini → Groq if rate-limited)

### 🟣 Phase 6 — Polish & Optimization
- [ ] Rate-limit indicator (show remaining quota)
- [ ] Response caching (avoid duplicate API calls)
- [ ] Prompt library UI (edit/customize system prompts)
- [ ] Encrypted API key storage (currently plain JSON — will migrate to DPAPI)
- [ ] Usage analytics (local only, no telemetry)

---

## 🎯 Design Principles

1. **Provider-agnostic** — never reference `GeminiAiService` in ViewModels; always use `IAiService`
2. **Offline-first** — app works 100% without internet; AI is optional bonus
3. **User-controlled** — user provides own API key, can disable AI globally
4. **Cost-conscious** — cache responses, use short prompts, respect rate limits
5. **MVVM discipline** — all AI features flow through `ICommand` + async patterns
6. **Graceful degradation** — if AI fails, feature still works manually

---

## 🔧 How to Add a New AI Feature

1. **Create prompt template** in `AIService/Prompts/YourFeaturePrompts.cs` (when Phase 2+ starts)
2. **Inject** `IAiService` into your ViewModel constructor
3. **Add** an `ICommand` that calls `await _ai.AskAsync(...)`
4. **Bind** result to a property; show loading indicator during call
5. **Handle errors** gracefully via `AiResponse.Success`

Example:
```csharp
public class SheetStoreViewModel : ViewModelBase
{
    private readonly IAiService _ai;

    public SheetStoreViewModel(IAiService ai)
    {
        _ai = ai;
        AiSuggestCommand = new RelayCommand(async _ => await SuggestAsync());
    }

    private async Task SuggestAsync()
    {
        IsBusy = true;
        var res = await _ai.AskAsync("Suggest glass thickness for a shower door");
        if (res.Success) Suggestion = res.Text;
        else ErrorMessage = res.ErrorMessage;
        IsBusy = false;
    }
}
```

---

## 🔑 Getting a Gemini API Key

1. Go to **https://aistudio.google.com/**
2. Sign in with Google
3. Click **"Get API Key"** → **"Create API key in new project"**
4. Copy the key
5. In ProGlass → **Settings** (top-right logo button) → paste in **API Key** field
6. Click **"Test Connection"** — should say "Connection successful"
7. Toggle **"Enable AI"** ON

---

## 📊 Free Tier Limits

| Metric | Limit |
|---|---|
| Requests per minute | 15 |
| Requests per day | 1,500 |
| Tokens per day | 1,000,000 |
| Cost | $0 forever |

For small glass businesses (10+ users), this is more than enough for years.

---

## 🐛 Common Issues

| Issue | Cause | Fix |
|---|---|---|
| "API key not configured" | Empty key field | Enter key in Settings |
| "API error (403)" | Invalid key | Regenerate at aistudio.google.com |
| "API error (429)" | Rate limit hit | Wait 1 min |
| "Exception: timeout" | No internet | Check connection |
| Test button stays "Testing..." | Very slow network | Wait or retry |

---

## 📂 File Locations

- **Source code**: `Services/AI/` (this folder)
- **Config file**: `%AppData%\ProGlassAutomation\config.json`
- **Log file** (future): `%AppData%\ProGlassAutomation\logs\ai.log`

---

*Last updated: 2025-01 — Phase 1 complete, ready for Phase 2 when Sheet Store module lands.*

<!-- pg-nav -->
---

*This README documents `Services/AI/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../docs/README.md).*
<!-- pg-nav -->
