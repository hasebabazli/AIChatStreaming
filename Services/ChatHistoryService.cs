using AIChatStreaming.Models;
using System.Collections.Concurrent;

namespace AIChatStreaming.Services;

public interface IChatHistoryService
{
    ChatSession GetOrCreate(string sessionId);
    void AddMessage(string sessionId, ChatMessage message);
    List<ChatMessage> GetHistory(string sessionId);
    void CancelStream(string sessionId);
    void SetStreaming(string sessionId, bool streaming, CancellationTokenSource? cts);
    CancellationTokenSource? GetCts(string sessionId);
    void ClearHistory(string sessionId);
    IEnumerable<ChatSession> GetAllSessions();
}

public class ChatHistoryService : IChatHistoryService
{
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();

    public ChatSession GetOrCreate(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, id => new ChatSession { SessionId = id });
    }

    public void AddMessage(string sessionId, ChatMessage message)
    {
        var session = GetOrCreate(sessionId);
        session.Messages.Add(message);
        session.LastActivity = DateTime.UtcNow;
    }

    public List<ChatMessage> GetHistory(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var s) ? s.Messages : new();
    }

    public void CancelStream(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            s.StreamCts?.Cancel();
            s.IsStreaming = false;
        }
    }

    public void SetStreaming(string sessionId, bool streaming, CancellationTokenSource? cts)
    {
        var session = GetOrCreate(sessionId);
        session.IsStreaming = streaming;
        session.StreamCts = cts;
    }

    public CancellationTokenSource? GetCts(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var s) ? s.StreamCts : null;
    }

    public void ClearHistory(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
        {
            s.Messages.Clear();
        }
    }

    public IEnumerable<ChatSession> GetAllSessions() => _sessions.Values;
}
