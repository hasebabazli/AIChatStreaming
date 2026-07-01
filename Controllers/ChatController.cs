using AIChatStreaming.Models;
using AIChatStreaming.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace AIChatStreaming.Controllers;

public class ChatController : Controller
{
    private readonly ITokenStreamingService _streaming;
    private readonly IChatHistoryService _history;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ITokenStreamingService streaming,
        IChatHistoryService history,
        ILogger<ChatController> logger)
    {
        _streaming = streaming;
        _history = history;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var sessionId = GetOrCreateSession();
        ViewBag.SessionId = sessionId;
        ViewBag.History = _history.GetHistory(sessionId);
        return View();
    }

    
    public IActionResult History()
    {
        var sessionId = GetOrCreateSession();
        var messages = _history.GetHistory(sessionId);
        return Json(messages);
    }

    
    [HttpPost]
    public IActionResult ClearHistory()
    {
        var sessionId = GetOrCreateSession();
        _history.ClearHistory(sessionId);
        return Json(new { success = true });
    }


    [HttpPost]
    public IActionResult Cancel()
    {
        var sessionId = GetOrCreateSession();
        _history.CancelStream(sessionId);
        return Json(new { success = true, message = "Stream cancelled" });
    }

  
    [HttpGet]
    public async Task Stream(string message, string sessionId, double speedMs = 30)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.Headers["Content-Type"]  = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache, no-store";
        Response.Headers["X-Accel-Buffering"] = "no";  
        Response.Headers["Connection"] = "keep-alive";

        var userMsg = new ChatMessage
        {
            Role    = "user",
            Content = message
        };
        _history.AddMessage(sessionId, userMsg);

        var cts = new CancellationTokenSource();
        _history.SetStreaming(sessionId, true, cts);

        var assistantContent = new StringBuilder();

        try
        {
            var history = _history.GetHistory(sessionId);

            await foreach (var evt in _streaming.StreamResponseAsync(
                               message, history, speedMs, cts.Token))
            {
                if (cts.Token.IsCancellationRequested) break;

                if (evt.Type == "token")
                    assistantContent.Append(evt.Data);

                var json = JsonSerializer.Serialize(evt);
                var ssePayload = $"event: {evt.Type}\ndata: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(ssePayload);

                await Response.Body.WriteAsync(bytes, cts.Token);
                await Response.Body.FlushAsync(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            var cancelEvt = $"event: cancelled\ndata: {{\"message\":\"Stream stopped by user\"}}\n\n";
            var bytes = Encoding.UTF8.GetBytes(cancelEvt);
            await Response.Body.WriteAsync(bytes, CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming error for session {SessionId}", sessionId);
            var errEvt = $"event: error\ndata: {{\"message\":\"{ex.Message}\"}}\n\n";
            var bytes = Encoding.UTF8.GetBytes(errEvt);
            await Response.Body.WriteAsync(bytes, CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }
        finally
        {
            if (assistantContent.Length > 0)
            {
                _history.AddMessage(sessionId, new ChatMessage
                {
                    Role       = "assistant",
                    Content    = assistantContent.ToString(),
                    TokenCount = assistantContent.ToString().Split(' ').Length
                });
            }
            _history.SetStreaming(sessionId, false, null);
        }
    }

    private string GetOrCreateSession()
    {
        const string key = "ChatSessionId";
        if (HttpContext.Session.GetString(key) is not { } sid || string.IsNullOrEmpty(sid))
        {
            sid = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(key, sid);
        }
        return sid;
    }
}
