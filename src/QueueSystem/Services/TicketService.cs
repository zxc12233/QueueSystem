using StackExchange.Redis;
using QueueSystem.Shared;
using Microsoft.AspNetCore.SignalR;
using QueueSystem.Api.Hubs;

namespace QueueSystem.Api.Services;

public class TicketService(IConnectionMultiplexer redis, IHubContext<QueueHub> hubContext)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<TicketDto> IssueTicketAsync(string branchId)
    {
        // 定義 Redis Key，例如 queue:Taipei01:counter
        var counterKey = $"queue:{branchId}:counter";

        // 原子遞增並取得新號碼
        long newNumber = await _db.StringIncrementAsync(counterKey);

        var ticket = new TicketDto
        {
            TicketNumber = newNumber,
            BranchId = branchId,
            CreatedAt = DateTime.UtcNow
        };

        // 【新增】即時推送給所有連線的客戶端
        await hubContext.Clients.All.SendAsync("ReceiveNewTicket", ticket);

        return ticket;
    }
}