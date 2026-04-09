namespace a2g.TelegramStickerViewer.Models;

public sealed class StickerModel
{
    public string FileId { get; set; } = "";
    public string AccessHash { get; set; } = "";
    public string FileReference { get; set; } = "";
    public string? MimeType { get; set; }
    public long Size { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Emoji { get; set; }
    public bool IsAnimated { get; set; }
    public bool IsVideo { get; set; }
    public string MediaUrl { get; set; } = "";
}
