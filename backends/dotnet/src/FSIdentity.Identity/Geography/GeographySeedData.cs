namespace FSIdentity.Identity.Geography;

internal static class GeographySeedData
{
    internal record CountrySeed(
        string Code, string Name, string FormatCode, string LanguageCode, string DateFormat,
        string DateTimeFormat, string CurrencyCode, string CurrencySymbol, string PriceFormat, string TimeZone);

    internal record LanguageSeed(string Code, string Name, string NativeName, bool IsRtl, int DisplayOrder, bool IsFallback);

    internal static readonly LanguageSeed[] Languages =
    [
        new("en", "English", "English", false, 1, true),
        new("tr", "Turkish", "Türkçe", false, 2, false),
        new("de", "German", "Deutsch", false, 3, false),
        new("ar", "Arabic", "العربية", true, 4, false),
    ];

    internal static readonly CountrySeed[] Countries =
    [
        new("TR", "Turkey", "tr-TR", "tr", "dd.MM.yyyy", "dd.MM.yyyy HH:mm", "TRY", "₺", "#,##0.00 ₺", "Europe/Istanbul"),
        new("US", "United States", "en-US", "en", "MM/dd/yyyy", "MM/dd/yyyy hh:mm tt", "USD", "$", "$#,##0.00", "America/New_York"),
        new("GB", "United Kingdom", "en-GB", "en", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "GBP", "£", "£#,##0.00", "Europe/London"),
        new("DE", "Germany", "de-DE", "de", "dd.MM.yyyy", "dd.MM.yyyy HH:mm", "EUR", "€", "#,##0.00 €", "Europe/Berlin"),
        new("FR", "France", "fr-FR", "fr", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "EUR", "€", "#,##0.00 €", "Europe/Paris"),
        new("ES", "Spain", "es-ES", "es", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "EUR", "€", "#,##0.00 €", "Europe/Madrid"),
        new("IT", "Italy", "it-IT", "it", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "EUR", "€", "#,##0.00 €", "Europe/Rome"),
        new("NL", "Netherlands", "nl-NL", "nl", "dd-MM-yyyy", "dd-MM-yyyy HH:mm", "EUR", "€", "€ #,##0.00", "Europe/Amsterdam"),
        new("AZ", "Azerbaijan", "az-AZ", "az", "dd.MM.yyyy", "dd.MM.yyyy HH:mm", "AZN", "₼", "#,##0.00 ₼", "Asia/Baku"),
        new("SA", "Saudi Arabia", "ar-SA", "ar", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "SAR", "﷼", "#,##0.00 ﷼", "Asia/Riyadh"),
    ];

    internal static readonly (string Code, string Name)[] TurkeyRegions =
    [
        ("TR-MAR", "Marmara"),
        ("TR-EGE", "Ege"),
        ("TR-AKD", "Akdeniz"),
        ("TR-ICA", "İç Anadolu"),
        ("TR-KAR", "Karadeniz"),
        ("TR-DAN", "Doğu Anadolu"),
        ("TR-GDA", "Güneydoğu Anadolu"),
    ];

    internal static readonly (string Plate, string Name, string Region)[] TurkeyProvinces =
    [
        ("01", "Adana", "TR-AKD"), ("02", "Adıyaman", "TR-GDA"), ("03", "Afyonkarahisar", "TR-EGE"),
        ("04", "Ağrı", "TR-DAN"), ("05", "Amasya", "TR-KAR"), ("06", "Ankara", "TR-ICA"),
        ("07", "Antalya", "TR-AKD"), ("08", "Artvin", "TR-KAR"), ("09", "Aydın", "TR-EGE"),
        ("10", "Balıkesir", "TR-MAR"), ("11", "Bilecik", "TR-MAR"), ("12", "Bingöl", "TR-DAN"),
        ("13", "Bitlis", "TR-DAN"), ("14", "Bolu", "TR-KAR"), ("15", "Burdur", "TR-AKD"),
        ("16", "Bursa", "TR-MAR"), ("17", "Çanakkale", "TR-MAR"), ("18", "Çankırı", "TR-ICA"),
        ("19", "Çorum", "TR-KAR"), ("20", "Denizli", "TR-EGE"), ("21", "Diyarbakır", "TR-GDA"),
        ("22", "Edirne", "TR-MAR"), ("23", "Elazığ", "TR-DAN"), ("24", "Erzincan", "TR-DAN"),
        ("25", "Erzurum", "TR-DAN"), ("26", "Eskişehir", "TR-ICA"), ("27", "Gaziantep", "TR-GDA"),
        ("28", "Giresun", "TR-KAR"), ("29", "Gümüşhane", "TR-KAR"), ("30", "Hakkâri", "TR-DAN"),
        ("31", "Hatay", "TR-AKD"), ("32", "Isparta", "TR-AKD"), ("33", "Mersin", "TR-AKD"),
        ("34", "İstanbul", "TR-MAR"), ("35", "İzmir", "TR-EGE"), ("36", "Kars", "TR-DAN"),
        ("37", "Kastamonu", "TR-KAR"), ("38", "Kayseri", "TR-ICA"), ("39", "Kırklareli", "TR-MAR"),
        ("40", "Kırşehir", "TR-ICA"), ("41", "Kocaeli", "TR-MAR"), ("42", "Konya", "TR-ICA"),
        ("43", "Kütahya", "TR-EGE"), ("44", "Malatya", "TR-DAN"), ("45", "Manisa", "TR-EGE"),
        ("46", "Kahramanmaraş", "TR-AKD"), ("47", "Mardin", "TR-GDA"), ("48", "Muğla", "TR-EGE"),
        ("49", "Muş", "TR-DAN"), ("50", "Nevşehir", "TR-ICA"), ("51", "Niğde", "TR-ICA"),
        ("52", "Ordu", "TR-KAR"), ("53", "Rize", "TR-KAR"), ("54", "Sakarya", "TR-MAR"),
        ("55", "Samsun", "TR-KAR"), ("56", "Siirt", "TR-GDA"), ("57", "Sinop", "TR-KAR"),
        ("58", "Sivas", "TR-ICA"), ("59", "Tekirdağ", "TR-MAR"), ("60", "Tokat", "TR-KAR"),
        ("61", "Trabzon", "TR-KAR"), ("62", "Tunceli", "TR-DAN"), ("63", "Şanlıurfa", "TR-GDA"),
        ("64", "Uşak", "TR-EGE"), ("65", "Van", "TR-DAN"), ("66", "Yozgat", "TR-ICA"),
        ("67", "Zonguldak", "TR-KAR"), ("68", "Aksaray", "TR-ICA"), ("69", "Bayburt", "TR-KAR"),
        ("70", "Karaman", "TR-ICA"), ("71", "Kırıkkale", "TR-ICA"), ("72", "Batman", "TR-GDA"),
        ("73", "Şırnak", "TR-GDA"), ("74", "Bartın", "TR-KAR"), ("75", "Ardahan", "TR-DAN"),
        ("76", "Iğdır", "TR-DAN"), ("77", "Yalova", "TR-MAR"), ("78", "Karabük", "TR-KAR"),
        ("79", "Kilis", "TR-GDA"), ("80", "Osmaniye", "TR-AKD"), ("81", "Düzce", "TR-KAR"),
    ];

    internal static readonly (string Code, string Name)[] UsStates =
    [
        ("US-CA", "California"),
        ("US-NY", "New York"),
    ];

    internal static readonly (string Code, string Name, string State)[] UsCities =
    [
        ("US-CA-001", "Los Angeles", "US-CA"),
        ("US-NY-001", "New York City", "US-NY"),
    ];
}