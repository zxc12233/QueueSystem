using QueueSystem.Shared;
using StackExchange.Redis;

// 注意：這裡必須與專案名稱 QueueSystem.api 的大小寫一致
namespace QueueSystem.api.Services
{
    public class TicketService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public TicketService(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
        }

        // 1. 取號邏輯：只產生號碼並入列 (RPUSH)，不發送廣播
        public async Task<TicketDto> IssueTicketAsync(string branchId)
        {
            var counterKey = $"branch:{branchId}:counter";
            var queueKey = $"branch:{branchId}:waiting";

            // 原子性產生號碼
            var ticketNumber = await _db.StringIncrementAsync(counterKey);

            // 將號碼推入 Redis List 末端 (Right Push)，代表進入排隊隊伍
            await _db.ListRightPushAsync(queueKey, ticketNumber.ToString());

            // 新增：取得當前佇列長度
            var waitingCount = await _db.ListLengthAsync(queueKey);

            return new TicketDto
            {
                BranchId = branchId,
                TicketNumber = ticketNumber.ToString(),
                IssuedAt = DateTime.Now,
                WaitingCount = (int)waitingCount // 將等待人數塞進 DTO
            };
        }

        // 2. 叫號邏輯：從隊伍最前面取出號碼 (LPOP)
        public async Task<TicketDto?> CallNextAsync(string branchId)
        {
            var queueKey = $"branch:{branchId}:waiting";

            // 從 Redis List 首端彈出 (Left Pop)，代表叫號服務
            var ticketNumber = await _db.ListLeftPopAsync(queueKey);

            if (ticketNumber.IsNull) return null;

            // 取得出列後，剩餘的等待人數 [cite: 2026-01-01]
            var count = await _db.ListLengthAsync(queueKey);

            return new TicketDto
            {
                BranchId = branchId,
                TicketNumber = ticketNumber.ToString(),
                IssuedAt = DateTime.Now,
                WaitingCount = (int)count // 填入剩餘人數
            };
        }
    }
}