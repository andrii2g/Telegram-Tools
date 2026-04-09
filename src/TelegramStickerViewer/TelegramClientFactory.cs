using TL;
using WTelegram;

namespace a2g.TelegramStickerViewer;

public sealed class TelegramClientFactory
{
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Client? _client;
    private User? _me;

    public TelegramClientFactory(IConfiguration config)
    {
        _config = config;
    }

    public async Task<Client> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
            return _client;

        await _lock.WaitAsync(ct);
        try
        {
            if (_client is not null)
                return _client;

            _client = new Client(Config);

            await _client.ConnectAsync();
            return _client;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<User> LoginIfNeededAsync(CancellationToken ct)
    {
        if (_me is not null)
            return _me;

        var client = await GetClientAsync(ct);
        _me = await client.LoginUserIfNeeded();
        return _me;
    }

    private string? Config(string what)
    {
        return what switch
        {
            "api_id" => _config["Telegram:ApiId"],
            "api_hash" => _config["Telegram:ApiHash"],
            "phone_number" => _config["Telegram:PhoneNumber"],
            "session_pathname" => _config["Telegram:SessionPath"],

            _ => null
        };
    }
}
