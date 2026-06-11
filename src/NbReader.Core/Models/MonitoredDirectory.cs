namespace NbReader.Core.Models;

/// <summary>
/// 监控目录配置。
/// </summary>
public class MonitoredDirectory
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>绝对路径</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;
}
