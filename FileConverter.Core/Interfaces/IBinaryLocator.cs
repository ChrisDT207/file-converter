namespace FileConverter.Core.Interfaces;

public interface IBinaryLocator
{
    string? FindBinary(string binaryName);
    bool IsBinaryAvailable(string binaryName);
    Dictionary<string, bool> GetToolsStatus();
    string GetBinaryPathOrThrow(string binaryName);
}
