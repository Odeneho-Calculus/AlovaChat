using AlovaChat.Hubs;
using AlovaChat.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Add custom services
builder.Services.AddScoped<IChatSessionService, ChatSessionService>();
builder.Services.AddSingleton<ISessionEventService, SessionEventService>();
// Use Wikipedia Search AI service (no API keys required, real Wikipedia content)
builder.Services.AddSingleton<IAIModelService, WikipediaSearchAIService>();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapHub<ChatHub>("/chathub");
app.MapFallbackToPage("/_Host");

// Initialize AI service
using (var scope = app.Services.CreateScope())
{
    var aiService = scope.ServiceProvider.GetRequiredService<IAIModelService>();
    await aiService.InitializeAsync();
}

app.Run();