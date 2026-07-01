namespace AIChatStreaming.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Role { get; set; } = "user"; 
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int TokenCount { get; set; }
    public double StreamingSpeedMs { get; set; } = 30;
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public double SpeedMs { get; set; } = 30;
}

public class ChatSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public bool IsStreaming { get; set; } = false;
    public CancellationTokenSource? StreamCts { get; set; }
}

public class StreamEvent
{
    public string Type { get; set; } = "token";   
    public string Data { get; set; } = string.Empty;
    public int TokenIndex { get; set; }
    public int TotalTokens { get; set; }
    public double Probability { get; set; } = 1.0;
}
