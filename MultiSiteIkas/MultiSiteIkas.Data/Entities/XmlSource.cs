namespace MultiSiteIkas.Data.Entities;

/// <summary>
/// Master mağaza (hobizubi.com) XML kaynakları
/// Her XML URL'i bir XmlSource kaydı
/// </summary>
public class XmlSource
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public long SourceCompanyId { get; set; }
    public string XmlUrl { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int SyncFrequencyHours { get; set; } = 24;
    public DateTime? LastSyncDate { get; set; }
    public DateTime? NextSyncDate { get; set; }
    public string? LastSyncStatus { get; set; } // 'Success', 'Failed', 'InProgress'
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
