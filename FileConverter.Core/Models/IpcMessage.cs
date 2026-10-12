namespace FileConverter.Core.Models;

public class IpcRequestMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = string.Empty;
    public System.Text.Json.JsonElement? Payload { get; set; }
}

public class IpcResponseMessage
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public object? Data { get; set; }
    public string? Error { get; set; }
}
