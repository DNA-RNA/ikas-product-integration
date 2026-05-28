namespace MultiSiteIkas.Data.Entities;

/// <summary>
/// Site/Firma Kayıtları (Master + Hedef Mağazalar)
/// Hedef mağazaların İkas API credentials burada saklanır (production'da encrypted)
/// </summary>
public class Company
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    
    // İkas API Credentials (target company için)
    public string? IkasApiKey { get; set; }
    public string? IkasApiSecret { get; set; }
    
    // Language code (mallofmolds: 'en', others: 'tr')
    public string? LanguageCode { get; set; } = "tr";
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
