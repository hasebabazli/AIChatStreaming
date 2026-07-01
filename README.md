# 🔥 AI Chat Token Streaming

> **Advanced Web Development — Streaming Project #5**
> Idlib University · Faculty of IT Engineering

---

## 🎬 Demo Video
<!-- Replace with your YouTube link after recording -->
https://youtu.be/YOUR_LINK_HERE

---

## 📡 Streaming Details

| Item | Details |
|------|---------|
| **Technique** | Server-Sent Events (SSE) |
| **What is streamed** | AI reply tokens pushed one-by-one over a single open HTTP connection |
| **Smart Idea #1** | 🛑 Cancel/stop mid-stream — client closes `EventSource`, server respects `CancellationToken` |
| **Smart Idea #2** | ⚡ Adjustable streaming speed (throttle) — 10ms to 200ms per token |
| **Smart Idea #3** | 📊 Live token probability visualization — confidence bar per token |
| **Reconnect** | `EventSource` auto-reconnects; UI shows reconnect attempts |

---

## 🏗️ Architecture

```
Browser (EventSource)
    │
    │  GET /Chat/Stream?message=...&speedMs=30
    │  Content-Type: text/event-stream
    │
    ▼
ChatController.Stream()
    │
    │  await foreach (var evt in _streaming.StreamResponseAsync(...))
    │  {
    │      await Response.Body.WriteAsync("event: token\ndata: {...}\n\n");
    │      await Response.Body.FlushAsync();      ← pushed immediately
    │  }
    │
    ▼
TokenStreamingService (IAsyncEnumerable<StreamEvent>)
    - Tokenizes response
    - Yields token by token with adaptive delay
    - Respects CancellationToken for mid-stream stop
```

---

## 🚀 How to Run

```bash
# Prerequisites: .NET 8 SDK
dotnet restore
dotnet run
# Open: https://localhost:5001
```

---

## ✅ Features

- [x] SSE streaming channel (continuous, single connection)
- [x] Live browser UI — page never reloads
- [x] Stop mid-stream (server `CancellationToken` + client `EventSource.close()`)
- [x] Adjustable speed (10–200 ms/token)
- [x] Auto-reconnect with attempt counter
- [x] Token probability visualization
- [x] Chat history per session
- [x] Professional dark UI (Arabic RTL support)

---

## 👨‍💻 Student

**Name:** [Your Name Here]
**Course:** Advanced Web Development — Streaming (ASP.NET Core MVC)
**University:** Idlib University — Faculty of IT Engineering
