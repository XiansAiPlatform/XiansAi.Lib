using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using TimeZoneConverter;

namespace XiansAi.Models;

/// <summary>
/// Platform-independent timezone identifiers using IANA standard.
/// Includes an AgentDecision option that defers selection to the agent.
/// </summary>
public enum Timezone
{
    [Description("HOST_ENVIRONMENT")]
    HostEnvironment,

    [Description("UTC")]
    Utc,

    // North America
    [Description("America/New_York")]
    EasternTime,

    [Description("America/Chicago")]
    CentralTime,

    [Description("America/Denver")]
    MountainTime,

    [Description("America/Los_Angeles")]
    PacificTime,

    [Description("America/Anchorage")]
    AlaskaTime,

    [Description("America/Adak")]
    HawaiiAleutianTime,

    [Description("Pacific/Honolulu")]
    HawaiiTime,

    [Description("America/Phoenix")]
    ArizonaTime,

    [Description("America/Toronto")]
    TorontoTime,

    [Description("America/Vancouver")]
    VancouverTime,

    [Description("America/Mexico_City")]
    MexicoCityTime,

    [Description("America/Cancun")]
    CancunTime,

    [Description("America/Merida")]
    MeridaTime,

    [Description("America/Monterrey")]
    MonterreyTime,

    [Description("America/Matamoros")]
    MatamorosTime,

    [Description("America/Mazatlan")]
    MazatlanTime,

    [Description("America/Chihuahua")]
    ChihuahuaTime,

    [Description("America/Ojinaga")]
    OjinagaTime,

    [Description("America/Hermosillo")]
    HermosilloTime,

    [Description("America/Tijuana")]
    TijuanaTime,

    [Description("America/Bahia_Banderas")]
    BahiaBanderasTime,

    [Description("America/Nassau")]
    NassauTime,

    [Description("America/Barbados")]
    BarbadosTime,

    [Description("America/Belize")]
    BelizeTime,

    [Description("America/Costa_Rica")]
    CostaRicaTime,

    [Description("America/Havana")]
    HavanaTime,

    [Description("America/Santo_Domingo")]
    SantoDomingoTime,

    [Description("America/El_Salvador")]
    ElSalvadorTime,

    [Description("America/Guatemala")]
    GuatemalaTime,

    [Description("America/Tegucigalpa")]
    TegucigalpaTime,

    [Description("America/Jamaica")]
    JamaicaTime,

    [Description("America/Managua")]
    ManaguaTime,

    [Description("America/Panama")]
    PanamaTime,

    [Description("America/Port-au-Prince")]
    PortAuPrinceTime,

    [Description("America/Puerto_Rico")]
    PuertoRicoTime,

    [Description("America/Port_of_Spain")]
    PortOfSpainTime,

    [Description("America/Curacao")]
    CuracaoTime,

    [Description("America/Martinique")]
    MartiniqueTime,

    [Description("America/Miquelon")]
    MiquelonTime,

    [Description("America/St_Johns")]
    StJohnsTime,

    [Description("America/Halifax")]
    HalifaxTime,

    [Description("America/Glace_Bay")]
    GlaceBayTime,

    [Description("America/Moncton")]
    MonctonTime,

    [Description("America/Goose_Bay")]
    GooseBayTime,

    [Description("America/Blanc-Sablon")]
    BlancSablonTime,

    [Description("America/Nipigon")]
    NipigonTime,

    [Description("America/Thunder_Bay")]
    ThunderBayTime,

    [Description("America/Iqaluit")]
    IqaluitTime,

    [Description("America/Pangnirtung")]
    PangnirtungTime,

    [Description("America/Atikokan")]
    AtikokanTime,

    [Description("America/Winnipeg")]
    WinnipegTime,

    [Description("America/Rainy_River")]
    RainyRiverTime,

    [Description("America/Resolute")]
    ResoluteTime,

    [Description("America/Rankin_Inlet")]
    RankinInletTime,

    [Description("America/Regina")]
    ReginaTime,

    [Description("America/Swift_Current")]
    SwiftCurrentTime,

    [Description("America/Edmonton")]
    EdmontonTime,

    [Description("America/Cambridge_Bay")]
    CambridgeBayTime,

    [Description("America/Yellowknife")]
    YellowknifeTime,

    [Description("America/Inuvik")]
    InuvikTime,

    [Description("America/Creston")]
    CrestonTime,

    [Description("America/Dawson_Creek")]
    DawsonCreekTime,

    [Description("America/Fort_Nelson")]
    FortNelsonTime,

    [Description("America/Whitehorse")]
    WhitehorseTime,

    [Description("America/Dawson")]
    DawsonTime,

    [Description("America/Detroit")]
    DetroitTime,

    [Description("America/Kentucky/Louisville")]
    LouisvilleTime,

    [Description("America/Kentucky/Monticello")]
    MonticelloTime,

    [Description("America/Indiana/Indianapolis")]
    IndianapolisTime,

    [Description("America/Indiana/Vincennes")]
    VincennesTime,

    [Description("America/Indiana/Winamac")]
    WinamacTime,

    [Description("America/Indiana/Marengo")]
    MarengoTime,

    [Description("America/Indiana/Petersburg")]
    PetersburgTime,

    [Description("America/Indiana/Vevay")]
    VevayTime,

    [Description("America/Indiana/Tell_City")]
    TellCityTime,

    [Description("America/Indiana/Knox")]
    KnoxTime,

    [Description("America/Menominee")]
    MenomineeTime,

    [Description("America/North_Dakota/Center")]
    CenterTime,

    [Description("America/North_Dakota/New_Salem")]
    NewSalemTime,

    [Description("America/North_Dakota/Beulah")]
    BeulahTime,

    [Description("America/Boise")]
    BoiseTime,

    [Description("America/Juneau")]
    JuneauTime,

    [Description("America/Sitka")]
    SitkaTime,

    [Description("America/Metlakatla")]
    MetlakatlaTime,

    [Description("America/Yakutat")]
    YakutatTime,

    [Description("America/Nome")]
    NomeTime,

    [Description("America/La_Paz")]
    LaPazTime,

    [Description("America/Noronha")]
    NoronhaTime,

    [Description("America/Belem")]
    BelemTime,

    [Description("America/Fortaleza")]
    FortalezaTime,

    [Description("America/Recife")]
    RecifeTime,

    [Description("America/Araguaina")]
    AraguainaTime,

    [Description("America/Maceio")]
    MaceioTime,

    [Description("America/Bahia")]
    BahiaTime,

    [Description("America/Campo_Grande")]
    CampoGrandeTime,

    [Description("America/Cuiaba")]
    CuiabaTime,

    [Description("America/Santarem")]
    SantaremTime,

    [Description("America/Porto_Velho")]
    PortoVelhoTime,

    [Description("America/Boa_Vista")]
    BoaVistaTime,

    [Description("America/Manaus")]
    ManausTime,

    [Description("America/Eirunepe")]
    EirunepeTime,

    [Description("America/Rio_Branco")]
    RioBrancoTime,

    [Description("America/Argentina/Cordoba")]
    CordobaTime,

    [Description("America/Argentina/Salta")]
    SaltaTime,

    [Description("America/Argentina/Jujuy")]
    JujuyTime,

    [Description("America/Argentina/Tucuman")]
    TucumanTime,

    [Description("America/Argentina/Catamarca")]
    CatamarcaTime,

    [Description("America/Argentina/La_Rioja")]
    LaRiojaTime,

    [Description("America/Argentina/San_Juan")]
    SanJuanTime,

    [Description("America/Argentina/Mendoza")]
    MendozaTime,

    [Description("America/Argentina/San_Luis")]
    SanLuisTime,

    [Description("America/Argentina/Rio_Gallegos")]
    RioGallegosTime,

    [Description("America/Argentina/Ushuaia")]
    UshuaiaTime,

    [Description("America/Punta_Arenas")]
    PuntaArenasTime,

    [Description("Pacific/Easter")]
    EasterTime,

    [Description("America/Guyana")]
    GuyanaTime,

    [Description("America/Asuncion")]
    AsuncionTime,

    [Description("America/Paramaribo")]
    ParamariboTime,

    [Description("America/Montevideo")]
    MontevideoTime,

    // South America
    [Description("America/Sao_Paulo")]
    BrasiliaTime,

    [Description("America/Argentina/Buenos_Aires")]
    BuenosAiresTime,

    [Description("America/Santiago")]
    SantiagoTime,

    [Description("America/Bogota")]
    BogotaTime,

    [Description("America/Caracas")]
    CaracasTime,

    [Description("America/Lima")]
    LimaTime,

    // Europe
    [Description("Europe/London")]
    LondonTime,

    [Description("Europe/Paris")]
    ParisTime,

    [Description("Europe/Berlin")]
    BerlinTime,

    [Description("Europe/Rome")]
    RomeTime,

    [Description("Europe/Madrid")]
    MadridTime,

    [Description("Europe/Amsterdam")]
    AmsterdamTime,

    [Description("Europe/Brussels")]
    BrusselsTime,

    [Description("Europe/Vienna")]
    ViennaTime,

    [Description("Europe/Stockholm")]
    StockholmTime,

    [Description("Europe/Athens")]
    AthensTime,

    [Description("Europe/Istanbul")]
    IstanbulTime,

    [Description("Europe/Moscow")]
    MoscowTime,

    [Description("Europe/Kiev")]
    KievTime,

    [Description("Europe/Warsaw")]
    WarsawTime,

    [Description("Europe/Zurich")]
    ZurichTime,

    [Description("Europe/Dublin")]
    DublinTime,

    [Description("Europe/Lisbon")]
    LisbonTime,

    [Description("Europe/Andorra")]
    AndorraTime,

    [Description("Europe/Tirane")]
    TiraneTime,

    [Description("Europe/Minsk")]
    MinskTime,

    [Description("Europe/Prague")]
    PragueTime,

    [Description("Europe/Copenhagen")]
    CopenhagenTime,

    [Description("Europe/Tallinn")]
    TallinnTime,

    [Description("Europe/Helsinki")]
    HelsinkiTime,

    [Description("Europe/Budapest")]
    BudapestTime,

    [Description("Europe/Riga")]
    RigaTime,

    [Description("Europe/Vilnius")]
    VilniusTime,

    [Description("Europe/Luxembourg")]
    LuxembourgTime,

    [Description("Europe/Monaco")]
    MonacoTime,

    [Description("Europe/Chisinau")]
    ChisinauTime,

    [Description("Europe/Oslo")]
    OsloTime,

    [Description("Europe/Bucharest")]
    BucharestTime,

    [Description("Europe/Belgrade")]
    BelgradeTime,

    [Description("Europe/Sofia")]
    SofiaTime,

    [Description("Europe/Simferopol")]
    SimferopolTime,

    [Description("Europe/Kaliningrad")]
    KaliningradTime,

    [Description("Europe/Kirov")]
    KirovTime,

    [Description("Europe/Volgograd")]
    VolgogradTime,

    [Description("Europe/Astrakhan")]
    AstrakhanTime,

    [Description("Europe/Saratov")]
    SaratovTime,

    [Description("Europe/Ulyanovsk")]
    UlyanovskTime,

    [Description("Europe/Samara")]
    SamaraTime,

    [Description("Europe/Uzhgorod")]
    UzhgorodTime,

    [Description("Europe/Zaporozhye")]
    ZaporozhyeTime,

    [Description("Atlantic/Canary")]
    CanaryTime,

    [Description("Africa/Ceuta")]
    CeutaTime,

    [Description("Atlantic/Madeira")]
    MadeiraTime,

    [Description("Europe/Gibraltar")]
    GibraltarTime,

    // Asia
    [Description("Asia/Dubai")]
    DubaiTime,

    [Description("Asia/Kolkata")]
    IndiaTime,

    [Description("Asia/Shanghai")]
    ChinaTime,

    [Description("Asia/Tokyo")]
    TokyoTime,

    [Description("Asia/Seoul")]
    SeoulTime,

    [Description("Asia/Hong_Kong")]
    HongKongTime,

    [Description("Asia/Singapore")]
    SingaporeTime,

    [Description("Asia/Bangkok")]
    BangkokTime,

    [Description("Asia/Jakarta")]
    JakartaTime,

    [Description("Asia/Manila")]
    ManilaTime,

    [Description("Asia/Taipei")]
    TaipeiTime,

    [Description("Asia/Karachi")]
    KarachiTime,

    [Description("Asia/Dhaka")]
    DhakaTime,

    [Description("Asia/Kathmandu")]
    KathmanduTime,

    [Description("Asia/Tehran")]
    TehranTime,

    [Description("Asia/Baghdad")]
    BaghdadTime,

    [Description("Asia/Jerusalem")]
    JerusalemTime,

    [Description("Asia/Riyadh")]
    RiyadhTime,

    [Description("Asia/Kuwait")]
    KuwaitTime,

    [Description("Asia/Beirut")]
    BeirutTime,

    [Description("Asia/Baku")]
    BakuTime,

    [Description("Asia/Kabul")]
    KabulTime,

    [Description("Asia/Yerevan")]
    YerevanTime,

    [Description("Asia/Thimphu")]
    ThimphuTime,

    [Description("Asia/Brunei")]
    BruneiTime,

    [Description("Asia/Urumqi")]
    UrumqiTime,

    [Description("Asia/Colombo")]
    ColomboTime,

    [Description("Asia/Tbilisi")]
    TbilisiTime,

    [Description("Asia/Macau")]
    MacauTime,

    [Description("Asia/Pontianak")]
    PontianakTime,

    [Description("Asia/Makassar")]
    MakassarTime,

    [Description("Asia/Jayapura")]
    JayapuraTime,

    [Description("Asia/Amman")]
    AmmanTime,

    [Description("Asia/Almaty")]
    AlmatyTime,

    [Description("Asia/Qyzylorda")]
    QyzylordaTime,

    [Description("Asia/Qostanay")]
    QostanayTime,

    [Description("Asia/Aqtobe")]
    AqtobeTime,

    [Description("Asia/Aqtau")]
    AqtauTime,

    [Description("Asia/Atyrau")]
    AtyrauTime,

    [Description("Asia/Oral")]
    OralTime,

    [Description("Asia/Bishkek")]
    BishkekTime,

    [Description("Asia/Vientiane")]
    VientianeTime,

    [Description("Asia/Kuala_Lumpur")]
    KualaLumpurTime,

    [Description("Asia/Kuching")]
    KuchingTime,

    [Description("Asia/Yangon")]
    YangonTime,

    [Description("Asia/Pyongyang")]
    PyongyangTime,

    [Description("Asia/Muscat")]
    MuscatTime,

    [Description("Asia/Gaza")]
    GazaTime,

    [Description("Asia/Hebron")]
    HebronTime,

    [Description("Asia/Qatar")]
    QatarTime,

    [Description("Asia/Damascus")]
    DamascusTime,

    [Description("Asia/Dushanbe")]
    DushanbeTime,

    [Description("Asia/Ashgabat")]
    AshgabatTime,

    [Description("Asia/Samarkand")]
    SamarkandTime,

    [Description("Asia/Tashkent")]
    TashkentTime,

    [Description("Asia/Ho_Chi_Minh")]
    HoChiMinhTime,

    [Description("Asia/Yekaterinburg")]
    YekaterinburgTime,

    [Description("Asia/Omsk")]
    OmskTime,

    [Description("Asia/Novosibirsk")]
    NovosibirskTime,

    [Description("Asia/Barnaul")]
    BarnaulTime,

    [Description("Asia/Tomsk")]
    TomskTime,

    [Description("Asia/Novokuznetsk")]
    NovokuznetskTime,

    [Description("Asia/Krasnoyarsk")]
    KrasnoyarskTime,

    [Description("Asia/Irkutsk")]
    IrkutskTime,

    [Description("Asia/Chita")]
    ChitaTime,

    [Description("Asia/Yakutsk")]
    YakutskTime,

    [Description("Asia/Khandyga")]
    KhandygaTime,

    [Description("Asia/Vladivostok")]
    VladivostokTime,

    [Description("Asia/Ust-Nera")]
    UstNeraTime,

    [Description("Asia/Magadan")]
    MagadanTime,

    [Description("Asia/Sakhalin")]
    SakhalinTime,

    [Description("Asia/Srednekolymsk")]
    SrednekolymskTime,

    [Description("Asia/Kamchatka")]
    KamchatkaTime,

    [Description("Asia/Anadyr")]
    AnadyrTime,

    [Description("Asia/Ulaanbaatar")]
    UlaanbaatarTime,

    [Description("Asia/Hovd")]
    HovdTime,

    [Description("Asia/Choibalsan")]
    ChoibalsanTime,

    [Description("Asia/Nicosia")]
    NicosiaTime,

    [Description("Asia/Famagusta")]
    FamagustaTime,

    // Africa
    [Description("Africa/Cairo")]
    CairoTime,

    [Description("Africa/Johannesburg")]
    JohannesburgTime,

    [Description("Africa/Lagos")]
    LagosTime,

    [Description("Africa/Nairobi")]
    NairobiTime,

    [Description("Africa/Casablanca")]
    CasablancaTime,

    [Description("Africa/Algiers")]
    AlgiersTime,

    [Description("Africa/Abidjan")]
    AbidjanTime,

    [Description("Africa/Accra")]
    AccraTime,

    [Description("Africa/Bissau")]
    BissauTime,

    [Description("Africa/El_Aaiun")]
    ElAaiunTime,

    [Description("Africa/Juba")]
    JubaTime,

    [Description("Africa/Khartoum")]
    KhartoumTime,

    [Description("Africa/Maputo")]
    MaputoTime,

    [Description("Africa/Monrovia")]
    MonroviaTime,

    [Description("Africa/Ndjamena")]
    NdjamenaTime,

    [Description("Africa/Sao_Tome")]
    SaoTomeTime,

    [Description("Africa/Tripoli")]
    TripoliTime,

    [Description("Africa/Tunis")]
    TunisTime,

    [Description("Africa/Windhoek")]
    WindhoekTime,

    // Australia & Pacific
    [Description("Australia/Sydney")]
    SydneyTime,

    [Description("Australia/Melbourne")]
    MelbourneTime,

    [Description("Australia/Brisbane")]
    BrisbaneTime,

    [Description("Australia/Perth")]
    PerthTime,

    [Description("Australia/Adelaide")]
    AdelaideTime,

    [Description("Australia/Darwin")]
    DarwinTime,

    [Description("Pacific/Auckland")]
    AucklandTime,

    [Description("Pacific/Fiji")]
    FijiTime,

    [Description("Pacific/Guam")]
    GuamTime,

    [Description("Australia/Lord_Howe")]
    LordHoweTime,

    [Description("Antarctica/Macquarie")]
    MacquarieTime,

    [Description("Australia/Hobart")]
    HobartTime,

    [Description("Australia/Broken_Hill")]
    BrokenHillTime,

    [Description("Australia/Lindeman")]
    LindemanTime,

    [Description("Australia/Eucla")]
    EuclaTime,

    [Description("Pacific/Chuuk")]
    ChuukTime,

    [Description("Pacific/Pohnpei")]
    PohnpeiTime,

    [Description("Pacific/Kosrae")]
    KosraeTime,

    [Description("Pacific/Noumea")]
    NoumeaTime,

    [Description("Pacific/Norfolk")]
    NorfolkTime,

    [Description("Pacific/Nauru")]
    NauruTime,

    [Description("Pacific/Niue")]
    NiueTime,

    [Description("Pacific/Palau")]
    PalauTime,

    [Description("Pacific/Port_Moresby")]
    PortMoresbyTime,

    [Description("Pacific/Bougainville")]
    BougainvilleTime,

    [Description("Pacific/Pitcairn")]
    PitcairnTime,

    [Description("Pacific/Chatham")]
    ChathamTime,

    [Description("Pacific/Rarotonga")]
    RarotongaTime,

    [Description("Pacific/Guadalcanal")]
    GuadalcanalTime,

    [Description("Pacific/Tarawa")]
    TarawaTime,

    [Description("Pacific/Enderbury")]
    EnderburyTime,

    [Description("Pacific/Kiritimati")]
    KiritimatiTime,

    [Description("Pacific/Majuro")]
    MajuroTime,

    [Description("Pacific/Kwajalein")]
    KwajaleinTime,

    [Description("Pacific/Tahiti")]
    TahitiTime,

    [Description("Pacific/Marquesas")]
    MarquesasTime,

    [Description("Pacific/Gambier")]
    GambierTime,

    [Description("Pacific/Tongatapu")]
    TongatapuTime,

    [Description("Pacific/Fakaofo")]
    FakaofoTime,

    [Description("Pacific/Funafuti")]
    FunafutiTime,

    [Description("Pacific/Wake")]
    WakeTime,

    [Description("Pacific/Wallis")]
    WallisTime,

    [Description("Pacific/Apia")]
    ApiaTime,

    [Description("Asia/Dili")]
    DiliTime,

    // Atlantic
    [Description("Atlantic/Reykjavik")]
    ReykjavikTime,

    [Description("Atlantic/Azores")]
    AzoresTime,

    [Description("Atlantic/Bermuda")]
    BermudaTime,

    [Description("Atlantic/Cape_Verde")]
    CapeVerdeTime,

    [Description("Atlantic/Faroe")]
    FaroeTime,

    [Description("Atlantic/Stanley")]
    StanleyTime,

    [Description("Atlantic/South_Georgia")]
    SouthGeorgiaTime,

    // Indian Ocean
    [Description("Indian/Chagos")]
    ChagosTime,

    [Description("Indian/Christmas")]
    ChristmasTime,

    [Description("Indian/Cocos")]
    CocosTime,

    [Description("Indian/Kerguelen")]
    KerguelenTime,

    [Description("Indian/Mahe")]
    MaheTime,

    [Description("Indian/Maldives")]
    MaldivesTime,

    [Description("Indian/Mauritius")]
    MauritiusTime,

    [Description("Indian/Reunion")]
    ReunionTime,

    // Antarctica
    [Description("Antarctica/Casey")]
    CaseyTime,

    [Description("Antarctica/Davis")]
    DavisTime,

    [Description("Antarctica/DumontDUrville")]
    DumontDUrvilleTime,

    [Description("Antarctica/Mawson")]
    MawsonTime,

    [Description("Antarctica/Palmer")]
    PalmerTime,

    [Description("Antarctica/Rothera")]
    RotheraTime,

    [Description("Antarctica/Syowa")]
    SyowaTime,

    [Description("Antarctica/Troll")]
    TrollTime,

    [Description("Antarctica/Vostok")]
    VostokTime
}

/// <summary>
/// Extension methods for working with the Timezone enum.
/// </summary>
public static class TimezoneExtensions
{
    /// <summary>
    /// Gets the IANA timezone ID from the enum value.
    /// </summary>
    public static string GetIanaId(this Timezone timezone)
    {
        var fieldInfo = timezone.GetType().GetField(timezone.ToString());
        var attribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? timezone.ToString();
    }

    /// <summary>
    /// Gets the platform-specific TimeZoneInfo object.
    /// </summary>
    public static TimeZoneInfo GetTimeZoneInfo(this Timezone timezone)
    {
        if (timezone == Timezone.HostEnvironment)
        {
            throw new InvalidOperationException("AgentDecision does not map to a concrete timezone.");
        }

        var ianaId = timezone.GetIanaId();

        try
        {
            return TZConvert.GetTimeZoneInfo(ianaId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not resolve timezone '{ianaId}' on this platform", ex);
        }
    }

    /// <summary>
    /// Converts the provided UTC DateTime to the specified timezone.
    /// </summary>
    public static DateTime ConvertToTimezone(this Timezone timezone, DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be UTC", nameof(utcDateTime));
        }

        var timeZoneInfo = timezone.GetTimeZoneInfo();
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZoneInfo);
    }

    /// <summary>
    /// Gets the current time in the timezone.
    /// </summary>
    public static DateTime GetCurrentTime(this Timezone timezone)
    {
        return timezone.ConvertToTimezone(DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the UTC offset for the timezone at the specified date/time.
    /// </summary>
    public static TimeSpan GetUtcOffset(this Timezone timezone, DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be UTC", nameof(utcDateTime));
        }

        var timeZoneInfo = timezone.GetTimeZoneInfo();
        return timeZoneInfo.GetUtcOffset(utcDateTime);
    }

    /// <summary>
    /// Gets a display name for the timezone.
    /// </summary>
    public static string GetDisplayName(this Timezone timezone)
    {
        if (timezone == Timezone.HostEnvironment)
        {
            return "Agent Decision";
        }

        return timezone.GetTimeZoneInfo().DisplayName;
    }
}

/// <summary>
/// Helper methods for working with the Timezone enum and IANA identifiers.
/// </summary>
public static class TimezoneHelper
{
    /// <summary>
    /// Gets all available timezones with display information.
    /// </summary>
    public static List<(Timezone Timezone, string DisplayName, string IanaId)> GetAllTimezones()
    {
        return Enum.GetValues<Timezone>()
            .Select(tz => (
                Timezone: tz,
                DisplayName: tz.GetDisplayName(),
                IanaId: tz.GetIanaId()
            ))
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    /// <summary>
    /// Tries to parse an IANA identifier into a Timezone enum value.
    /// </summary>
    public static bool TryParseIanaId(string ianaId, out Timezone timezone)
    {
        foreach (var tz in Enum.GetValues<Timezone>())
        {
            if (tz.GetIanaId().Equals(ianaId, StringComparison.OrdinalIgnoreCase))
            {
                timezone = tz;
                return true;
            }
        }

        timezone = default;
        return false;
    }

    /// <summary>
    /// Converts UTC time to the specified timezone.
    /// </summary>
    public static DateTime ConvertFromUtc(DateTime utcDateTime, Timezone timezone)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be UTC", nameof(utcDateTime));
        }

        return timezone.ConvertToTimezone(utcDateTime);
    }

    /// <summary>
    /// Converts a timezone-specific time to UTC.
    /// </summary>
    public static DateTime ConvertToUtc(DateTime dateTime, Timezone timezone)
    {
        if (timezone == Timezone.HostEnvironment)
        {
            throw new InvalidOperationException("AgentDecision cannot be converted to a specific timezone.");
        }

        var timeZoneInfo = timezone.GetTimeZoneInfo();
        return TimeZoneInfo.ConvertTimeToUtc(dateTime, timeZoneInfo);
    }
}

/// <summary>
/// Utility methods showing common timezone conversion scenarios.
/// </summary>
public static class TimezoneUtility
{
    /// <summary>
    /// Gets the current time in the specified timezone across all platforms.
    /// </summary>
    public static DateTime GetTimeInTimezone(Timezone timezone)
    {
        if (timezone == Timezone.HostEnvironment)
        {
            throw new InvalidOperationException("AgentDecision does not represent a concrete timezone.");
        }

        var ianaId = timezone.GetIanaId();
        var timeZoneInfo = TZConvert.GetTimeZoneInfo(ianaId);
        var utcNow = DateTime.UtcNow;
        return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZoneInfo);
    }

    /// <summary>
    /// Gets the current time in the specified timezone using the provided UTC DateTime.
    /// </summary>
    public static DateTime GetTimeInTimezone(Timezone timezone, DateTime? utcDateTime = null)
    {
        if (timezone == Timezone.HostEnvironment)
        {
            throw new InvalidOperationException("AgentDecision does not represent a concrete timezone.");
        }

        var utcTime = utcDateTime ?? DateTime.UtcNow;
        if (utcTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be in UTC", nameof(utcDateTime));
        }

        var ianaId = timezone.GetIanaId();
        var timeZoneInfo = TZConvert.GetTimeZoneInfo(ianaId);

        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZoneInfo);
    }
}

