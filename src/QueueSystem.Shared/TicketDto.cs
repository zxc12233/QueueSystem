namespace QueueSystem.Shared;

/// <summary>
/// 叫號票據傳輸物件 (DTO)
/// </summary>
public class TicketDto
{
    /// <summary>
    /// 票號 (配合 Redis 字串處理，改為 string 以增加擴充性)
    /// </summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// 分店或櫃檯代號 (必填)
    /// </summary>
    public string BranchId { get; set; } = string.Empty;

    /// <summary>
    /// 票據建立/叫號時間 (配合 Service 命名為 IssuedAt)
    /// </summary>
    public DateTime IssuedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 當前等待人數 
    /// </summary>
    public int WaitingCount { get; set; }
}