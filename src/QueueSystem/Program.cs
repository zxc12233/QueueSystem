using StackExchange.Redis;
using QueueSystem.Api.Services;
using QueueSystem.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --- 服務註冊 (Service Registration) ---

// 1. 基礎架構服務
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// 2. Redis 連線配置 (Singleton)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string not found.");
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// 3. 業務邏輯服務
builder.Services.AddScoped<TicketService>();

// 4. CORS 策略配置
// 注意：SignalR 客戶端通常需要憑證 (Credentials)，因此不能使用 AllowAnyOrigin (*)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // 允許任何來源連線，解決開發環境 Port 變動問題
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();      // SignalR 必備：允許傳遞憑證
    });
});

var app = builder.Build();

// --- 中介軟體管線 (Middleware Pipeline) ---

// 1. 開發環境工具
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. 基礎安全性與導向
app.UseHttpsRedirection();

// 3. CORS 配置 (必須位於 MapHub 與 MapPost 之前)
app.UseCors("AllowAll");

// --- API 端點定義 (Endpoints) ---

// 發號 API
app.MapPost("/api/tickets/{branchId}", async (string branchId, TicketService ticketService) =>
{
    var ticket = await ticketService.IssueTicketAsync(branchId);
    return Results.Ok(ticket);
})
.WithName("IssueTicket")
.WithOpenApi();

// SignalR 即時通訊 Hub
app.MapHub<QueueHub>("/hubs/queue");

app.Run();