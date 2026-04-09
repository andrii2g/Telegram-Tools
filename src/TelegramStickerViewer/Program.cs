using a2g.TelegramStickerViewer;
using a2g.TelegramStickerViewer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TelegramClientFactory>();
builder.Services.AddSingleton<StickerSetService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapPost("/api/telegram/login", async (
    TelegramClientFactory factory,
    CancellationToken ct) =>
{
    var user = await factory.LoginIfNeededAsync(ct);
    return Results.Ok(new
    {
        id = user.id,
        username = user.username,
        firstName = user.first_name,
        lastName = user.last_name
    });
});

app.MapGet("/api/sticker-sets/{shortName}", async (
    string shortName,
    StickerSetService stickerService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(shortName))
        return Results.BadRequest(new { error = "Sticker set short name is required." });

    var result = await stickerService.GetStickerSetAsync(shortName, ct);
    return result is null
        ? Results.NotFound(new { error = "Sticker set not found." })
        : Results.Ok(result);
});

app.MapGet("/api/stickers/file/{fileId}", async (
    string fileId,
    StickerSetService stickerService,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var file = await stickerService.GetFileStreamAsync(fileId, ct);
    if (file is null)
        return Results.NotFound();

    httpContext.Response.Headers.CacheControl = "public,max-age=3600";
    return Results.Stream(file.Stream, file.ContentType, file.FileName);
});

app.Run();
