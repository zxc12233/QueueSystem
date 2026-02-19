using StackExchange.Redis;
using QueueSystem.api.Services;
using QueueSystem.api.Hubs;
using Microsoft.AspNetCore.SignalR; // 必須引用以使用 IHubContext

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

// CORS 策略配置 (針對 SignalR 憑證需求優化)
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
app.UseCors("AllowAll"); // 必須位於 Map 端點定義之前

// --- API 端點定義 (Endpoints) ---

// 1. 【客戶端】取號 API：僅入列 Redis，不廣播至看板
app.MapPost("/api/tickets/issue/{branchId}", async (string branchId, TicketService ticketService, IHubContext<QueueHub> hubContext) =>
{
    var ticket = await ticketService.IssueTicketAsync(branchId);

    // 【關鍵】雖然不叫號，但要告訴看板「更新等待人數」
    await hubContext.Clients.All.SendAsync("UpdateWaitingCount", ticket.WaitingCount);

    return Results.Ok(new { Message = "取號成功，請等待叫號", Data = ticket });
})
.WithName("IssueTicket")
.WithOpenApi();

// 2. 【櫃檯端】叫號 API：從 Redis 提取並透過 SignalR 廣播至看板
app.MapPost("/api/tickets/call/{branchId}", async (
    string branchId,
    TicketService ticketService,
    IHubContext<QueueHub> hubContext) => // 注入 HubContext 以進行主動推播
{
    var ticket = await ticketService.CallNextAsync(branchId);

    if (ticket == null)
    {
        return Results.NotFound(new { Message = "目前無等待中號碼" });
    }

    // 叫號時才執行 SignalR 廣播 [cite: 2026-02-19]
    await hubContext.Clients.All.SendAsync("ReceiveNewTicket", ticket);

    return Results.Ok(ticket);
})
.WithName("CallNext")
.WithOpenApi();

// SignalR Hub 路由定義
app.MapHub<QueueHub>("/hubs/queue");

app.Run();