using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueueSystem.Shared;

/// <summary>
/// 叫號票據傳輸物件 (DTO)
/// </summary>
public class TicketDto
{
    /// <summary>
    /// 票號 (由 Redis Atomic Increment 產生)
    /// </summary>
    public long TicketNumber { get; init; }

    /// <summary>
    /// 分店或櫃檯代號 (必填)
    /// </summary>
    public string BranchId { get; init; } = string.Empty;

    /// <summary>
    /// 票據建立時間 (UTC)
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 當前等待人數
    /// </summary>
    public int WaitingCount { get; set; }
}
