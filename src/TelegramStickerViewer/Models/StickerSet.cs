namespace a2g.TelegramStickerViewer.Models;

public sealed class StickerSetModel
{
    public long Id { get; set; }
    public long AccessHash { get; set; }
    public string ShortName { get; set; } = "";
    public string Title { get; set; } = "";
    public int Count { get; set; }
    public bool IsAnimated { get; set; }
    public bool IsVideo { get; set; }
    public List<StickerModel> Stickers { get; set; } = [];
}
