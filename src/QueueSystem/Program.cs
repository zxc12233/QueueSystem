using StackExchange.Redis;
using QueueSystem.api.Services;
using QueueSystem.api.Hubs;
using QueueSystem.Shared; // 確保引用 DTO 命名空間
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// --- 服務註冊 (Service Registration) ---

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Redis 連線配置
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string not found.");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// 業務邏輯服務
builder.Services.AddScoped<TicketService>();

// CORS 策略配置
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- 中介軟體管線 (Middleware Pipeline) ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // 允許 API 提供 wwwroot 下的 html 檔案
app.UseCors("AllowAll");

// --- API 端點定義 (Endpoints) ---

// 1. 【客戶端】取號 API：入列後同步「等待人數」
app.MapPost("/api/tickets/issue/{branchId}", async (string branchId, TicketService ticketService, IHubContext<QueueHub> hubContext) =>
{
    var ticket = await ticketService.IssueTicketAsync(branchId);

    // 通知所有端點（看板、櫃檯）更新等待人數
    await hubContext.Clients.All.SendAsync("UpdateWaitingCount", ticket.WaitingCount);

    return Results.Ok(new { Message = "取號成功", Data = ticket });
})
.WithName("IssueTicket")
.WithOpenApi();

// 2. 【櫃檯端】叫號 API：提取號碼並廣播
app.MapPost("/api/tickets/call/{branchId}", async (string branchId, TicketService ticketService, IHubContext<QueueHub> hubContext) =>
{
    var ticket = await ticketService.CallNextAsync(branchId);

    if (ticket == null)
    {
        return Results.NotFound(new { Message = "目前無等待中號碼" });
    }

    // 廣播跳號訊息，內容含最新 WaitingCount
    await hubContext.Clients.All.SendAsync("ReceiveNewTicket", ticket);

    return Results.Ok(ticket);
})
.WithName("CallNext")
.WithOpenApi();

// 3. 【櫃檯端】重叫 API：純廣播不操作 Redis
app.MapPost("/api/tickets/recall", async (TicketDto ticket, IHubContext<QueueHub> hubContext) =>
{
    // 重複發送最後一次叫號的資訊，觸發看板語音與閃爍
    await hubContext.Clients.All.SendAsync("ReceiveNewTicket", ticket);

    return Results.Ok(new { Message = "已發送重叫通知" });
})
.WithName("RecallTicket")
.WithOpenApi();

// SignalR Hub 路由定義
app.MapHub<QueueHub>("/hubs/queue");

app.Run();