using System.Globalization;

namespace AmphetamineNet.Services;

/// <summary>
/// Provides localized tray UI strings
/// </summary>
public static class Localization
{
    /// <summary>
    /// Default language code
    /// </summary>
    public const string DefaultLanguage = "en";

    /// <summary>
    /// Describes a supported UI language
    /// </summary>
    /// <param name="Code">BCP-47 language code</param>
    /// <param name="NativeName">Language name in its native script</param>
    public sealed record LanguageInfo(string Code, string NativeName);

    /// <summary>
    /// Supported UI languages
    /// </summary>
    /// <value>Read-only list of language descriptors</value>
    public static IReadOnlyList<LanguageInfo> Languages { get; } =
    [
        new("en", "English"),
        new("zh-Hans", "简体中文"),
        new("zh-Hant", "繁體中文"),
        new("es", "Español"),
        new("hi", "हिन्दी"),
        new("ar", "العربية"),
        new("pt", "Português"),
        new("bn", "বাংলা"),
        new("ru", "Русский"),
        new("ja", "日本語"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("ko", "한국어"),
        new("it", "Italiano"),
        new("tr", "Türkçe"),
        new("pl", "Polski"),
        new("uk", "Українська"),
        new("nl", "Nederlands"),
        new("vi", "Tiếng Việt"),
        new("th", "ไทย"),
        new("id", "Bahasa Indonesia"),
        new("sv", "Svenska"),
        new("cs", "Čeština"),
        new("ro", "Română"),
        new("el", "Ελληνικά"),
        new("hu", "Magyar"),
        new("fi", "Suomi"),
        new("da", "Dansk"),
        new("nb", "Norsk"),
        new("he", "עברית"),
    ];

    /// <summary>
    /// Currently selected language code
    /// </summary>
    private static string _language = DefaultLanguage;

    /// <summary>
    /// Currently active UI language code
    /// </summary>
    /// <value>Active BCP-47 language code</value>
    public static string CurrentLanguage
    {
        get => _language;
        private set => _language = value;
    }

    /// <summary>
    /// Raised when the UI language changes
    /// </summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>
    /// Applies a UI language and raises LanguageChanged
    /// </summary>
    /// <param name="code">Language code to activate</param>
    public static void SetLanguage(string? code)
    {
        var normalized = Normalize(code);
        if (normalized == _language)
            return;

        CurrentLanguage = normalized;
        try
        {
            var culture = CultureInfo.GetCultureInfo(normalized);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // Keep dictionary lookup working even if culture is unknown to the OS.
        }

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Maps a language code to a supported value
    /// </summary>
    /// <param name="code">Raw language code</param>
    /// <returns>Supported language code</returns>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return DefaultLanguage;

        var match = Languages.FirstOrDefault(l =>
            l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match.Code;

        var prefix = code.Split('-', '_')[0];
        match = Languages.FirstOrDefault(l =>
            l.Code.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            l.Code.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase));
        return match?.Code ?? DefaultLanguage;
    }

    /// <summary>
    /// Resolves a localized string by key
    /// </summary>
    /// <param name="key">Localization key</param>
    /// <returns>Localized string</returns>
    public static string T(string key) =>
        Table.TryGetValue(CurrentLanguage, out var map) && map.TryGetValue(key, out var value)
            ? value
            : Table[DefaultLanguage][key];

    /// <summary>
    /// Formats a duration for the tray menu
    /// </summary>
    /// <param name="minutes">Duration in minutes</param>
    /// <returns>Localized duration label</returns>
    public static string FormatDuration(int minutes) => minutes switch
    {
        0 => T("duration.indefinitely"),
        5 => T("duration.5m"),
        15 => T("duration.15m"),
        30 => T("duration.30m"),
        60 => T("duration.1h"),
        120 => T("duration.2h"),
        300 => T("duration.5h"),
        _ => string.Format(T("duration.custom_named"), minutes),
    };

    /// <summary>
    /// Formats remaining session time as a countdown
    /// </summary>
    /// <param name="remaining">Remaining time</param>
    /// <returns>Countdown text</returns>
    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var h = totalSeconds / 3600;
        var m = (totalSeconds % 3600) / 60;
        var s = totalSeconds % 60;
        return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m}:{s:D2}";
    }

    /// <summary>
    /// Builds the localization dictionary
    /// </summary>
    /// <returns>Language-to-string map</returns>
    private static readonly Dictionary<string, Dictionary<string, string>> Table = Build();

    /// <summary>
    /// Builds the localization dictionary
    /// </summary>
    /// <returns>Language-to-string map</returns>
    private static Dictionary<string, Dictionary<string, string>> Build()
    {
        var en = new Dictionary<string, string>
        {
            ["status.active"] = "Active",
            ["status.inactive"] = "Inactive",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modifiers",
            ["menu.language"] = "Language",
            ["menu.start"] = "Start Session",
            ["menu.stop"] = "Stop Session",
            ["menu.quit"] = "Quit",
            ["menu.custom_time"] = "Enter custom time…",
            ["mod.closed_lid"] = "Allow closed lid",
            ["mod.display"] = "Keep display awake",
            ["duration.indefinitely"] = "Indefinitely",
            ["duration.5m"] = "5 minutes",
            ["duration.15m"] = "15 minutes",
            ["duration.30m"] = "30 minutes",
            ["duration.1h"] = "1 hour",
            ["duration.2h"] = "2 hours",
            ["duration.5h"] = "5 hours",
            ["duration.custom_named"] = "{0} minutes",
            ["custom.title"] = "Custom timer",
            ["custom.prompt"] = "Duration in minutes:",
            ["custom.ok"] = "OK",
            ["custom.cancel"] = "Cancel",
            ["notify.body"] = "The menu bar icon is at the top right. Start a session from there.",
            ["tooltip.active"] = "AmphetamineNet — active ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inactive",
            ["os.unsupported"] = "This app only works on macOS.",
        };

        var table = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = en,
        };

        void Add(string code, Dictionary<string, string> overrides)
        {
            var map = new Dictionary<string, string>(en, StringComparer.Ordinal);
            foreach (var (k, v) in overrides)
                map[k] = v;
            table[code] = map;
        }

        Add("ru", new()
        {
            ["status.active"] = "Активна",
            ["status.inactive"] = "Неактивна",
            ["menu.timer"] = "Таймер",
            ["menu.modifiers"] = "Модификаторы",
            ["menu.language"] = "Язык",
            ["menu.start"] = "Запустить сессию",
            ["menu.stop"] = "Остановить сессию",
            ["menu.quit"] = "Выйти",
            ["menu.custom_time"] = "Ввести своё время…",
            ["mod.closed_lid"] = "Разрешить закрытую крышку",
            ["mod.display"] = "Не гасить экран",
            ["duration.indefinitely"] = "Бессрочно",
            ["duration.5m"] = "5 минут",
            ["duration.15m"] = "15 минут",
            ["duration.30m"] = "30 минут",
            ["duration.1h"] = "1 час",
            ["duration.2h"] = "2 часа",
            ["duration.5h"] = "5 часов",
            ["duration.custom_named"] = "{0} минут",
            ["custom.title"] = "Свой таймер",
            ["custom.prompt"] = "Длительность в минутах:",
            ["custom.ok"] = "OK",
            ["custom.cancel"] = "Отмена",
            ["notify.body"] = "Иконка в строке меню справа сверху. Запустите сессию оттуда.",
            ["tooltip.active"] = "AmphetamineNet — активна ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — неактивна",
            ["os.unsupported"] = "Это приложение работает только на macOS.",
        });

        Add("de", new()
        {
            ["status.active"] = "Aktiv",
            ["status.inactive"] = "Inaktiv",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modifikatoren",
            ["menu.language"] = "Sprache",
            ["menu.start"] = "Sitzung starten",
            ["menu.stop"] = "Sitzung stoppen",
            ["menu.quit"] = "Beenden",
            ["menu.custom_time"] = "Eigene Zeit eingeben…",
            ["mod.closed_lid"] = "Geschlossenen Deckel erlauben",
            ["mod.display"] = "Display wach halten",
            ["duration.indefinitely"] = "Unbegrenzt",
            ["duration.5m"] = "5 Minuten",
            ["duration.15m"] = "15 Minuten",
            ["duration.30m"] = "30 Minuten",
            ["duration.1h"] = "1 Stunde",
            ["duration.2h"] = "2 Stunden",
            ["duration.5h"] = "5 Stunden",
            ["duration.custom_named"] = "{0} Minuten",
            ["custom.title"] = "Eigener Timer",
            ["custom.prompt"] = "Dauer in Minuten:",
            ["custom.cancel"] = "Abbrechen",
            ["notify.body"] = "Das Menüleistensymbol ist oben rechts. Starte dort eine Sitzung.",
            ["tooltip.active"] = "AmphetamineNet — aktiv ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inaktiv",
            ["os.unsupported"] = "Diese App funktioniert nur unter macOS.",
        });

        Add("fr", new()
        {
            ["status.active"] = "Actif",
            ["status.inactive"] = "Inactif",
            ["menu.timer"] = "Minuteur",
            ["menu.modifiers"] = "Modificateurs",
            ["menu.language"] = "Langue",
            ["menu.start"] = "Démarrer la session",
            ["menu.stop"] = "Arrêter la session",
            ["menu.quit"] = "Quitter",
            ["menu.custom_time"] = "Saisir une durée…",
            ["mod.closed_lid"] = "Autoriser le couvercle fermé",
            ["mod.display"] = "Garder l’écran allumé",
            ["duration.indefinitely"] = "Indéfiniment",
            ["duration.5m"] = "5 minutes",
            ["duration.15m"] = "15 minutes",
            ["duration.30m"] = "30 minutes",
            ["duration.1h"] = "1 heure",
            ["duration.2h"] = "2 heures",
            ["duration.5h"] = "5 heures",
            ["duration.custom_named"] = "{0} minutes",
            ["custom.title"] = "Minuteur personnalisé",
            ["custom.prompt"] = "Durée en minutes :",
            ["custom.cancel"] = "Annuler",
            ["notify.body"] = "L’icône de la barre de menus est en haut à droite. Démarrez une session depuis là.",
            ["tooltip.active"] = "AmphetamineNet — actif ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inactif",
            ["os.unsupported"] = "Cette application ne fonctionne que sur macOS.",
        });

        Add("es", new()
        {
            ["status.active"] = "Activo",
            ["status.inactive"] = "Inactivo",
            ["menu.timer"] = "Temporizador",
            ["menu.modifiers"] = "Modificadores",
            ["menu.language"] = "Idioma",
            ["menu.start"] = "Iniciar sesión",
            ["menu.stop"] = "Detener sesión",
            ["menu.quit"] = "Salir",
            ["menu.custom_time"] = "Introducir tiempo…",
            ["mod.closed_lid"] = "Permitir tapa cerrada",
            ["mod.display"] = "Mantener pantalla encendida",
            ["duration.indefinitely"] = "Indefinidamente",
            ["duration.5m"] = "5 minutos",
            ["duration.15m"] = "15 minutos",
            ["duration.30m"] = "30 minutos",
            ["duration.1h"] = "1 hora",
            ["duration.2h"] = "2 horas",
            ["duration.5h"] = "5 horas",
            ["duration.custom_named"] = "{0} minutos",
            ["custom.title"] = "Temporizador personalizado",
            ["custom.prompt"] = "Duración en minutos:",
            ["custom.cancel"] = "Cancelar",
            ["notify.body"] = "El icono de la barra de menús está arriba a la derecha. Inicia una sesión desde ahí.",
            ["tooltip.active"] = "AmphetamineNet — activo ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inactivo",
            ["os.unsupported"] = "Esta aplicación solo funciona en macOS.",
        });

        Add("pt", new()
        {
            ["status.active"] = "Ativo",
            ["status.inactive"] = "Inativo",
            ["menu.timer"] = "Temporizador",
            ["menu.modifiers"] = "Modificadores",
            ["menu.language"] = "Idioma",
            ["menu.start"] = "Iniciar sessão",
            ["menu.stop"] = "Parar sessão",
            ["menu.quit"] = "Sair",
            ["menu.custom_time"] = "Inserir tempo…",
            ["mod.closed_lid"] = "Permitir tampa fechada",
            ["mod.display"] = "Manter ecrã ligado",
            ["duration.indefinitely"] = "Indefinidamente",
            ["duration.5m"] = "5 minutos",
            ["duration.15m"] = "15 minutos",
            ["duration.30m"] = "30 minutos",
            ["duration.1h"] = "1 hora",
            ["duration.2h"] = "2 horas",
            ["duration.5h"] = "5 horas",
            ["duration.custom_named"] = "{0} minutos",
            ["custom.title"] = "Temporizador personalizado",
            ["custom.prompt"] = "Duração em minutos:",
            ["custom.cancel"] = "Cancelar",
            ["notify.body"] = "O ícone da barra de menus está no canto superior direito. Inicie uma sessão a partir dele.",
            ["tooltip.active"] = "AmphetamineNet — ativo ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inativo",
            ["os.unsupported"] = "Esta aplicação só funciona no macOS.",
        });

        Add("it", new()
        {
            ["status.active"] = "Attivo",
            ["status.inactive"] = "Inattivo",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modificatori",
            ["menu.language"] = "Lingua",
            ["menu.start"] = "Avvia sessione",
            ["menu.stop"] = "Ferma sessione",
            ["menu.quit"] = "Esci",
            ["menu.custom_time"] = "Inserisci durata…",
            ["mod.closed_lid"] = "Consenti coperchio chiuso",
            ["mod.display"] = "Mantieni display acceso",
            ["duration.indefinitely"] = "A tempo indeterminato",
            ["duration.5m"] = "5 minuti",
            ["duration.15m"] = "15 minuti",
            ["duration.30m"] = "30 minuti",
            ["duration.1h"] = "1 ora",
            ["duration.2h"] = "2 ore",
            ["duration.5h"] = "5 ore",
            ["duration.custom_named"] = "{0} minuti",
            ["custom.title"] = "Timer personalizzato",
            ["custom.prompt"] = "Durata in minuti:",
            ["custom.cancel"] = "Annulla",
            ["notify.body"] = "L’icona nella barra dei menu è in alto a destra. Avvia una sessione da lì.",
            ["tooltip.active"] = "AmphetamineNet — attivo ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inattivo",
            ["os.unsupported"] = "Questa app funziona solo su macOS.",
        });

        Add("ja", new()
        {
            ["status.active"] = "動作中",
            ["status.inactive"] = "停止中",
            ["menu.timer"] = "タイマー",
            ["menu.modifiers"] = "修飾機能",
            ["menu.language"] = "言語",
            ["menu.start"] = "セッション開始",
            ["menu.stop"] = "セッション停止",
            ["menu.quit"] = "終了",
            ["menu.custom_time"] = "時間を入力…",
            ["mod.closed_lid"] = "フタを閉じても動作",
            ["mod.display"] = "ディスプレイをオンのまま",
            ["duration.indefinitely"] = "無期限",
            ["duration.5m"] = "5分",
            ["duration.15m"] = "15分",
            ["duration.30m"] = "30分",
            ["duration.1h"] = "1時間",
            ["duration.2h"] = "2時間",
            ["duration.5h"] = "5時間",
            ["duration.custom_named"] = "{0}分",
            ["custom.title"] = "カスタムタイマー",
            ["custom.prompt"] = "分数:",
            ["custom.cancel"] = "キャンセル",
            ["notify.body"] = "メニューバー右上のアイコンからセッションを開始できます。",
            ["tooltip.active"] = "AmphetamineNet — 動作中 ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — 停止中",
            ["os.unsupported"] = "このアプリは macOS 専用です。",
        });

        Add("zh-Hans", new()
        {
            ["status.active"] = "运行中",
            ["status.inactive"] = "未运行",
            ["menu.timer"] = "计时器",
            ["menu.modifiers"] = "修饰选项",
            ["menu.language"] = "语言",
            ["menu.start"] = "开始会话",
            ["menu.stop"] = "停止会话",
            ["menu.quit"] = "退出",
            ["menu.custom_time"] = "输入自定义时间…",
            ["mod.closed_lid"] = "允许合盖",
            ["mod.display"] = "保持屏幕常亮",
            ["duration.indefinitely"] = "无限期",
            ["duration.5m"] = "5 分钟",
            ["duration.15m"] = "15 分钟",
            ["duration.30m"] = "30 分钟",
            ["duration.1h"] = "1 小时",
            ["duration.2h"] = "2 小时",
            ["duration.5h"] = "5 小时",
            ["duration.custom_named"] = "{0} 分钟",
            ["custom.title"] = "自定义计时器",
            ["custom.prompt"] = "时长（分钟）:",
            ["custom.cancel"] = "取消",
            ["notify.body"] = "菜单栏图标在右上角。请从那里开始会话。",
            ["tooltip.active"] = "AmphetamineNet — 运行中 ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — 未运行",
            ["os.unsupported"] = "此应用仅适用于 macOS。",
        });

        Add("zh-Hant", new()
        {
            ["status.active"] = "執行中",
            ["status.inactive"] = "未執行",
            ["menu.timer"] = "計時器",
            ["menu.modifiers"] = "修飾選項",
            ["menu.language"] = "語言",
            ["menu.start"] = "開始工作階段",
            ["menu.stop"] = "停止工作階段",
            ["menu.quit"] = "結束",
            ["menu.custom_time"] = "輸入自訂時間…",
            ["mod.closed_lid"] = "允許合上蓋子",
            ["mod.display"] = "保持螢幕喚醒",
            ["duration.indefinitely"] = "無限期",
            ["duration.5m"] = "5 分鐘",
            ["duration.15m"] = "15 分鐘",
            ["duration.30m"] = "30 分鐘",
            ["duration.1h"] = "1 小時",
            ["duration.2h"] = "2 小時",
            ["duration.5h"] = "5 小時",
            ["duration.custom_named"] = "{0} 分鐘",
            ["custom.title"] = "自訂計時器",
            ["custom.prompt"] = "時長（分鐘）:",
            ["custom.cancel"] = "取消",
            ["notify.body"] = "選單列圖示在右上方。請從那裡開始工作階段。",
            ["tooltip.active"] = "AmphetamineNet — 執行中 ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — 未執行",
            ["os.unsupported"] = "此應用程式僅適用於 macOS。",
        });

        Add("ko", new()
        {
            ["status.active"] = "활성",
            ["status.inactive"] = "비활성",
            ["menu.timer"] = "타이머",
            ["menu.modifiers"] = "수정 옵션",
            ["menu.language"] = "언어",
            ["menu.start"] = "세션 시작",
            ["menu.stop"] = "세션 중지",
            ["menu.quit"] = "종료",
            ["menu.custom_time"] = "사용자 시간 입력…",
            ["mod.closed_lid"] = "뚜껑 닫힘 허용",
            ["mod.display"] = "디스플레이 켜 두기",
            ["duration.indefinitely"] = "무제한",
            ["duration.5m"] = "5분",
            ["duration.15m"] = "15분",
            ["duration.30m"] = "30분",
            ["duration.1h"] = "1시간",
            ["duration.2h"] = "2시간",
            ["duration.5h"] = "5시간",
            ["duration.custom_named"] = "{0}분",
            ["custom.title"] = "사용자 타이머",
            ["custom.prompt"] = "분 단위 시간:",
            ["custom.cancel"] = "취소",
            ["notify.body"] = "메뉴 막대 아이콘은 오른쪽 위에 있습니다. 거기에서 세션을 시작하세요.",
            ["tooltip.active"] = "AmphetamineNet — 활성 ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — 비활성",
            ["os.unsupported"] = "이 앱은 macOS에서만 작동합니다.",
        });

        Add("ar", new()
        {
            ["status.active"] = "نشط",
            ["status.inactive"] = "غير نشط",
            ["menu.timer"] = "المؤقت",
            ["menu.modifiers"] = "المعدِّلات",
            ["menu.language"] = "اللغة",
            ["menu.start"] = "بدء الجلسة",
            ["menu.stop"] = "إيقاف الجلسة",
            ["menu.quit"] = "خروج",
            ["menu.custom_time"] = "إدخال وقت مخصص…",
            ["mod.closed_lid"] = "السماح بالغطاء المغلق",
            ["mod.display"] = "إبقاء الشاشة مستيقظة",
            ["duration.indefinitely"] = "بلا نهاية",
            ["duration.5m"] = "5 دقائق",
            ["duration.15m"] = "15 دقيقة",
            ["duration.30m"] = "30 دقيقة",
            ["duration.1h"] = "ساعة واحدة",
            ["duration.2h"] = "ساعتان",
            ["duration.5h"] = "5 ساعات",
            ["duration.custom_named"] = "{0} دقيقة",
            ["custom.title"] = "مؤقت مخصص",
            ["custom.prompt"] = "المدة بالدقائق:",
            ["custom.cancel"] = "إلغاء",
            ["notify.body"] = "أيقونة شريط القوائم في أعلى اليمين. ابدأ جلسة من هناك.",
            ["tooltip.active"] = "AmphetamineNet — نشط ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — غير نشط",
            ["os.unsupported"] = "يعمل هذا التطبيق على macOS فقط.",
        });

        Add("hi", new()
        {
            ["status.active"] = "सक्रिय",
            ["status.inactive"] = "निष्क्रिय",
            ["menu.timer"] = "टाइमर",
            ["menu.modifiers"] = "संशोधक",
            ["menu.language"] = "भाषा",
            ["menu.start"] = "सेशन शुरू करें",
            ["menu.stop"] = "सेशन रोकें",
            ["menu.quit"] = "बाहर निकलें",
            ["menu.custom_time"] = "अपना समय दर्ज करें…",
            ["mod.closed_lid"] = "बंद ढक्कन की अनुमति दें",
            ["mod.display"] = "डिस्प्ले जगाए रखें",
            ["duration.indefinitely"] = "अनिश्चितकाल",
            ["duration.5m"] = "5 मिनट",
            ["duration.15m"] = "15 मिनट",
            ["duration.30m"] = "30 मिनट",
            ["duration.1h"] = "1 घंटा",
            ["duration.2h"] = "2 घंटे",
            ["duration.5h"] = "5 घंटे",
            ["duration.custom_named"] = "{0} मिनट",
            ["custom.title"] = "कस्टम टाइमर",
            ["custom.prompt"] = "मिनट में अवधि:",
            ["custom.cancel"] = "रद्द करें",
            ["notify.body"] = "मेनू बार आइकन ऊपर दाईं ओर है। वहीं से सेशन शुरू करें।",
            ["tooltip.active"] = "AmphetamineNet — सक्रिय ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — निष्क्रिय",
            ["os.unsupported"] = "यह ऐप केवल macOS पर काम करता है।",
        });

        Add("uk", new()
        {
            ["status.active"] = "Активна",
            ["status.inactive"] = "Неактивна",
            ["menu.timer"] = "Таймер",
            ["menu.modifiers"] = "Модифікатори",
            ["menu.language"] = "Мова",
            ["menu.start"] = "Запустити сесію",
            ["menu.stop"] = "Зупинити сесію",
            ["menu.quit"] = "Вийти",
            ["menu.custom_time"] = "Ввести свій час…",
            ["mod.closed_lid"] = "Дозволити закриту кришку",
            ["mod.display"] = "Не вимикати екран",
            ["duration.indefinitely"] = "Безстроково",
            ["duration.5m"] = "5 хвилин",
            ["duration.15m"] = "15 хвилин",
            ["duration.30m"] = "30 хвилин",
            ["duration.1h"] = "1 година",
            ["duration.2h"] = "2 години",
            ["duration.5h"] = "5 годин",
            ["duration.custom_named"] = "{0} хвилин",
            ["custom.title"] = "Свій таймер",
            ["custom.prompt"] = "Тривалість у хвилинах:",
            ["custom.cancel"] = "Скасувати",
            ["notify.body"] = "Іконка в рядку меню справа зверху. Запустіть сесію звідти.",
            ["tooltip.active"] = "AmphetamineNet — активна ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — неактивна",
            ["os.unsupported"] = "Ця програма працює лише на macOS.",
        });

        Add("pl", new()
        {
            ["status.active"] = "Aktywna",
            ["status.inactive"] = "Nieaktywna",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modyfikatory",
            ["menu.language"] = "Język",
            ["menu.start"] = "Uruchom sesję",
            ["menu.stop"] = "Zatrzymaj sesję",
            ["menu.quit"] = "Zakończ",
            ["menu.custom_time"] = "Wpisz własny czas…",
            ["mod.closed_lid"] = "Zezwól na zamkniętą klapę",
            ["mod.display"] = "Nie wyłączaj ekranu",
            ["duration.indefinitely"] = "Bezterminowo",
            ["duration.5m"] = "5 minut",
            ["duration.15m"] = "15 minut",
            ["duration.30m"] = "30 minut",
            ["duration.1h"] = "1 godzina",
            ["duration.2h"] = "2 godziny",
            ["duration.5h"] = "5 godzin",
            ["duration.custom_named"] = "{0} minut",
            ["custom.title"] = "Własny timer",
            ["custom.prompt"] = "Czas w minutach:",
            ["custom.cancel"] = "Anuluj",
            ["notify.body"] = "Ikona paska menu jest w prawym górnym rogu. Uruchom sesję stamtąd.",
            ["tooltip.active"] = "AmphetamineNet — aktywna ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — nieaktywna",
            ["os.unsupported"] = "Ta aplikacja działa tylko na macOS.",
        });

        Add("nl", new()
        {
            ["status.active"] = "Actief",
            ["status.inactive"] = "Inactief",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modifiers",
            ["menu.language"] = "Taal",
            ["menu.start"] = "Sessie starten",
            ["menu.stop"] = "Sessie stoppen",
            ["menu.quit"] = "Afsluiten",
            ["menu.custom_time"] = "Eigen tijd invoeren…",
            ["mod.closed_lid"] = "Gesloten deksel toestaan",
            ["mod.display"] = "Scherm wakker houden",
            ["duration.indefinitely"] = "Onbeperkt",
            ["duration.5m"] = "5 minuten",
            ["duration.15m"] = "15 minuten",
            ["duration.30m"] = "30 minuten",
            ["duration.1h"] = "1 uur",
            ["duration.2h"] = "2 uur",
            ["duration.5h"] = "5 uur",
            ["duration.custom_named"] = "{0} minuten",
            ["custom.title"] = "Aangepaste timer",
            ["custom.prompt"] = "Duur in minuten:",
            ["custom.cancel"] = "Annuleren",
            ["notify.body"] = "Het menubalkpictogram staat rechtsboven. Start daar een sessie.",
            ["tooltip.active"] = "AmphetamineNet — actief ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inactief",
            ["os.unsupported"] = "Deze app werkt alleen op macOS.",
        });

        Add("tr", new()
        {
            ["status.active"] = "Etkin",
            ["status.inactive"] = "Pasif",
            ["menu.timer"] = "Zamanlayıcı",
            ["menu.modifiers"] = "Değiştiriciler",
            ["menu.language"] = "Dil",
            ["menu.start"] = "Oturumu başlat",
            ["menu.stop"] = "Oturumu durdur",
            ["menu.quit"] = "Çıkış",
            ["menu.custom_time"] = "Özel süre gir…",
            ["mod.closed_lid"] = "Kapalı kapağa izin ver",
            ["mod.display"] = "Ekranı açık tut",
            ["duration.indefinitely"] = "Süresiz",
            ["duration.5m"] = "5 dakika",
            ["duration.15m"] = "15 dakika",
            ["duration.30m"] = "30 dakika",
            ["duration.1h"] = "1 saat",
            ["duration.2h"] = "2 saat",
            ["duration.5h"] = "5 saat",
            ["duration.custom_named"] = "{0} dakika",
            ["custom.title"] = "Özel zamanlayıcı",
            ["custom.prompt"] = "Dakika olarak süre:",
            ["custom.cancel"] = "İptal",
            ["notify.body"] = "Menü çubuğu simgesi sağ üstte. Oturumu oradan başlatın.",
            ["tooltip.active"] = "AmphetamineNet — etkin ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — pasif",
            ["os.unsupported"] = "Bu uygulama yalnızca macOS’ta çalışır.",
        });

        Add("vi", new()
        {
            ["status.active"] = "Đang bật",
            ["status.inactive"] = "Đang tắt",
            ["menu.timer"] = "Hẹn giờ",
            ["menu.modifiers"] = "Bổ sung",
            ["menu.language"] = "Ngôn ngữ",
            ["menu.start"] = "Bắt đầu phiên",
            ["menu.stop"] = "Dừng phiên",
            ["menu.quit"] = "Thoát",
            ["menu.custom_time"] = "Nhập thời gian…",
            ["mod.closed_lid"] = "Cho phép đóng nắp",
            ["mod.display"] = "Giữ màn hình sáng",
            ["duration.indefinitely"] = "Vô thời hạn",
            ["duration.5m"] = "5 phút",
            ["duration.15m"] = "15 phút",
            ["duration.30m"] = "30 phút",
            ["duration.1h"] = "1 giờ",
            ["duration.2h"] = "2 giờ",
            ["duration.5h"] = "5 giờ",
            ["duration.custom_named"] = "{0} phút",
            ["custom.title"] = "Hẹn giờ tùy chỉnh",
            ["custom.prompt"] = "Thời lượng (phút):",
            ["custom.cancel"] = "Hủy",
            ["notify.body"] = "Biểu tượng thanh menu ở góc trên bên phải. Hãy bắt đầu phiên từ đó.",
            ["tooltip.active"] = "AmphetamineNet — đang bật ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — đang tắt",
            ["os.unsupported"] = "Ứng dụng này chỉ chạy trên macOS.",
        });

        Add("th", new()
        {
            ["status.active"] = "ใช้งานอยู่",
            ["status.inactive"] = "ไม่ได้ใช้งาน",
            ["menu.timer"] = "ตัวจับเวลา",
            ["menu.modifiers"] = "ตัวปรับแต่ง",
            ["menu.language"] = "ภาษา",
            ["menu.start"] = "เริ่มเซสชัน",
            ["menu.stop"] = "หยุดเซสชัน",
            ["menu.quit"] = "ออก",
            ["menu.custom_time"] = "ใส่เวลาเอง…",
            ["mod.closed_lid"] = "อนุญาตฝาปิด",
            ["mod.display"] = "คงหน้าจอไว้",
            ["duration.indefinitely"] = "ไม่จำกัดเวลา",
            ["duration.5m"] = "5 นาที",
            ["duration.15m"] = "15 นาที",
            ["duration.30m"] = "30 นาที",
            ["duration.1h"] = "1 ชั่วโมง",
            ["duration.2h"] = "2 ชั่วโมง",
            ["duration.5h"] = "5 ชั่วโมง",
            ["duration.custom_named"] = "{0} นาที",
            ["custom.title"] = "ตัวจับเวลากำหนดเอง",
            ["custom.prompt"] = "ระยะเวลาเป็นนาที:",
            ["custom.cancel"] = "ยกเลิก",
            ["notify.body"] = "ไอคอนแถบเมนูอยู่ด้านบนขวา เริ่มเซสชันจากที่นั่น",
            ["tooltip.active"] = "AmphetamineNet — ใช้งานอยู่ ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — ไม่ได้ใช้งาน",
            ["os.unsupported"] = "แอปนี้ใช้ได้เฉพาะบน macOS",
        });

        Add("id", new()
        {
            ["status.active"] = "Aktif",
            ["status.inactive"] = "Nonaktif",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Pengubah",
            ["menu.language"] = "Bahasa",
            ["menu.start"] = "Mulai sesi",
            ["menu.stop"] = "Hentikan sesi",
            ["menu.quit"] = "Keluar",
            ["menu.custom_time"] = "Masukkan waktu…",
            ["mod.closed_lid"] = "Izinkan tutup tertutup",
            ["mod.display"] = "Jaga layar tetap nyala",
            ["duration.indefinitely"] = "Tanpa batas",
            ["duration.5m"] = "5 menit",
            ["duration.15m"] = "15 menit",
            ["duration.30m"] = "30 menit",
            ["duration.1h"] = "1 jam",
            ["duration.2h"] = "2 jam",
            ["duration.5h"] = "5 jam",
            ["duration.custom_named"] = "{0} menit",
            ["custom.title"] = "Timer khusus",
            ["custom.prompt"] = "Durasi dalam menit:",
            ["custom.cancel"] = "Batal",
            ["notify.body"] = "Ikon bilah menu ada di kanan atas. Mulai sesi dari sana.",
            ["tooltip.active"] = "AmphetamineNet — aktif ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — nonaktif",
            ["os.unsupported"] = "Aplikasi ini hanya berjalan di macOS.",
        });

        Add("sv", new()
        {
            ["status.active"] = "Aktiv",
            ["status.inactive"] = "Inaktiv",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modifierare",
            ["menu.language"] = "Språk",
            ["menu.start"] = "Starta session",
            ["menu.stop"] = "Stoppa session",
            ["menu.quit"] = "Avsluta",
            ["menu.custom_time"] = "Ange egen tid…",
            ["mod.closed_lid"] = "Tillåt stängt lock",
            ["mod.display"] = "Håll skärmen vaken",
            ["duration.indefinitely"] = "Obestämd tid",
            ["duration.5m"] = "5 minuter",
            ["duration.15m"] = "15 minuter",
            ["duration.30m"] = "30 minuter",
            ["duration.1h"] = "1 timme",
            ["duration.2h"] = "2 timmar",
            ["duration.5h"] = "5 timmar",
            ["duration.custom_named"] = "{0} minuter",
            ["custom.title"] = "Anpassad timer",
            ["custom.prompt"] = "Varaktighet i minuter:",
            ["custom.cancel"] = "Avbryt",
            ["notify.body"] = "Menyfältsikonen finns uppe till höger. Starta en session därifrån.",
            ["tooltip.active"] = "AmphetamineNet — aktiv ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inaktiv",
            ["os.unsupported"] = "Denna app fungerar bara på macOS.",
        });

        Add("cs", new()
        {
            ["status.active"] = "Aktivní",
            ["status.inactive"] = "Neaktivní",
            ["menu.timer"] = "Časovač",
            ["menu.modifiers"] = "Modifikátory",
            ["menu.language"] = "Jazyk",
            ["menu.start"] = "Spustit relaci",
            ["menu.stop"] = "Zastavit relaci",
            ["menu.quit"] = "Ukončit",
            ["menu.custom_time"] = "Zadat vlastní čas…",
            ["mod.closed_lid"] = "Povolit zavřené víko",
            ["mod.display"] = "Nechat displej vzhůru",
            ["duration.indefinitely"] = "Na neurčito",
            ["duration.5m"] = "5 minut",
            ["duration.15m"] = "15 minut",
            ["duration.30m"] = "30 minut",
            ["duration.1h"] = "1 hodina",
            ["duration.2h"] = "2 hodiny",
            ["duration.5h"] = "5 hodin",
            ["duration.custom_named"] = "{0} minut",
            ["custom.title"] = "Vlastní časovač",
            ["custom.prompt"] = "Délka v minutách:",
            ["custom.cancel"] = "Zrušit",
            ["notify.body"] = "Ikona v řádku nabídek je vpravo nahoře. Relaci spusťte odtud.",
            ["tooltip.active"] = "AmphetamineNet — aktivní ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — neaktivní",
            ["os.unsupported"] = "Tato aplikace funguje pouze na macOS.",
        });

        Add("ro", new()
        {
            ["status.active"] = "Activ",
            ["status.inactive"] = "Inactiv",
            ["menu.timer"] = "Temporizator",
            ["menu.modifiers"] = "Modificatori",
            ["menu.language"] = "Limbă",
            ["menu.start"] = "Pornește sesiunea",
            ["menu.stop"] = "Oprește sesiunea",
            ["menu.quit"] = "Ieșire",
            ["menu.custom_time"] = "Introdu timpul…",
            ["mod.closed_lid"] = "Permite capacul închis",
            ["mod.display"] = "Păstrează ecranul treaz",
            ["duration.indefinitely"] = "Nedeterminat",
            ["duration.5m"] = "5 minute",
            ["duration.15m"] = "15 minute",
            ["duration.30m"] = "30 minute",
            ["duration.1h"] = "1 oră",
            ["duration.2h"] = "2 ore",
            ["duration.5h"] = "5 ore",
            ["duration.custom_named"] = "{0} minute",
            ["custom.title"] = "Temporizator personalizat",
            ["custom.prompt"] = "Durata în minute:",
            ["custom.cancel"] = "Anulează",
            ["notify.body"] = "Pictograma din bara de meniu este sus în dreapta. Pornește o sesiune de acolo.",
            ["tooltip.active"] = "AmphetamineNet — activ ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inactiv",
            ["os.unsupported"] = "Această aplicație funcționează doar pe macOS.",
        });

        Add("el", new()
        {
            ["status.active"] = "Ενεργό",
            ["status.inactive"] = "Ανενεργό",
            ["menu.timer"] = "Χρονοδιακόπτης",
            ["menu.modifiers"] = "Τροποποιητές",
            ["menu.language"] = "Γλώσσα",
            ["menu.start"] = "Έναρξη συνεδρίας",
            ["menu.stop"] = "Διακοπή συνεδρίας",
            ["menu.quit"] = "Έξοδος",
            ["menu.custom_time"] = "Εισαγωγή χρόνου…",
            ["mod.closed_lid"] = "Να επιτρέπεται κλειστό καπάκι",
            ["mod.display"] = "Οθόνη ξύπνια",
            ["duration.indefinitely"] = "Αόριστα",
            ["duration.5m"] = "5 λεπτά",
            ["duration.15m"] = "15 λεπτά",
            ["duration.30m"] = "30 λεπτά",
            ["duration.1h"] = "1 ώρα",
            ["duration.2h"] = "2 ώρες",
            ["duration.5h"] = "5 ώρες",
            ["duration.custom_named"] = "{0} λεπτά",
            ["custom.title"] = "Προσαρμοσμένος χρόνος",
            ["custom.prompt"] = "Διάρκεια σε λεπτά:",
            ["custom.cancel"] = "Ακύρωση",
            ["notify.body"] = "Το εικονίδιο της γραμμής μενού είναι πάνω δεξιά. Ξεκινήστε συνεδρία από εκεί.",
            ["tooltip.active"] = "AmphetamineNet — ενεργό ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — ανενεργό",
            ["os.unsupported"] = "Αυτή η εφαρμογή λειτουργεί μόνο σε macOS.",
        });

        Add("hu", new()
        {
            ["status.active"] = "Aktív",
            ["status.inactive"] = "Inaktív",
            ["menu.timer"] = "Időzítő",
            ["menu.modifiers"] = "Módosítók",
            ["menu.language"] = "Nyelv",
            ["menu.start"] = "Munkamenet indítása",
            ["menu.stop"] = "Munkamenet leállítása",
            ["menu.quit"] = "Kilépés",
            ["menu.custom_time"] = "Egyéni idő megadása…",
            ["mod.closed_lid"] = "Zárt fedél engedélyezése",
            ["mod.display"] = "Kijelző ébren tartása",
            ["duration.indefinitely"] = "Határozatlan ideig",
            ["duration.5m"] = "5 perc",
            ["duration.15m"] = "15 perc",
            ["duration.30m"] = "30 perc",
            ["duration.1h"] = "1 óra",
            ["duration.2h"] = "2 óra",
            ["duration.5h"] = "5 óra",
            ["duration.custom_named"] = "{0} perc",
            ["custom.title"] = "Egyéni időzítő",
            ["custom.prompt"] = "Időtartam percben:",
            ["custom.cancel"] = "Mégse",
            ["notify.body"] = "A menüsor ikonja jobbra fent van. Onnan indítson munkamenetet.",
            ["tooltip.active"] = "AmphetamineNet — aktív ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inaktív",
            ["os.unsupported"] = "Ez az alkalmazás csak macOS-en működik.",
        });

        Add("fi", new()
        {
            ["status.active"] = "Aktiivinen",
            ["status.inactive"] = "Ei aktiivinen",
            ["menu.timer"] = "Ajastin",
            ["menu.modifiers"] = "Muokkaimet",
            ["menu.language"] = "Kieli",
            ["menu.start"] = "Aloita istunto",
            ["menu.stop"] = "Pysäytä istunto",
            ["menu.quit"] = "Lopeta",
            ["menu.custom_time"] = "Syötä oma aika…",
            ["mod.closed_lid"] = "Salli suljettu kansi",
            ["mod.display"] = "Pidä näyttö hereillä",
            ["duration.indefinitely"] = "Toistaiseksi",
            ["duration.5m"] = "5 minuuttia",
            ["duration.15m"] = "15 minuuttia",
            ["duration.30m"] = "30 minuuttia",
            ["duration.1h"] = "1 tunti",
            ["duration.2h"] = "2 tuntia",
            ["duration.5h"] = "5 tuntia",
            ["duration.custom_named"] = "{0} minuuttia",
            ["custom.title"] = "Mukautettu ajastin",
            ["custom.prompt"] = "Kesto minuuteissa:",
            ["custom.cancel"] = "Peruuta",
            ["notify.body"] = "Valikkorivin kuvake on oikeassa yläkulmassa. Aloita istunto sieltä.",
            ["tooltip.active"] = "AmphetamineNet — aktiivinen ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — ei aktiivinen",
            ["os.unsupported"] = "Tämä sovellus toimii vain macOS:llä.",
        });

        Add("da", new()
        {
            ["status.active"] = "Aktiv",
            ["status.inactive"] = "Inaktiv",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modifikatorer",
            ["menu.language"] = "Sprog",
            ["menu.start"] = "Start session",
            ["menu.stop"] = "Stop session",
            ["menu.quit"] = "Afslut",
            ["menu.custom_time"] = "Indtast egen tid…",
            ["mod.closed_lid"] = "Tillad lukket låg",
            ["mod.display"] = "Hold skærmen vågen",
            ["duration.indefinitely"] = "Ubegrænset",
            ["duration.5m"] = "5 minutter",
            ["duration.15m"] = "15 minutter",
            ["duration.30m"] = "30 minutter",
            ["duration.1h"] = "1 time",
            ["duration.2h"] = "2 timer",
            ["duration.5h"] = "5 timer",
            ["duration.custom_named"] = "{0} minutter",
            ["custom.title"] = "Tilpasset timer",
            ["custom.prompt"] = "Varighed i minutter:",
            ["custom.cancel"] = "Annuller",
            ["notify.body"] = "Menulinjeikonet er øverst til højre. Start en session derfra.",
            ["tooltip.active"] = "AmphetamineNet — aktiv ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inaktiv",
            ["os.unsupported"] = "Denne app virker kun på macOS.",
        });

        Add("nb", new()
        {
            ["status.active"] = "Aktiv",
            ["status.inactive"] = "Inaktiv",
            ["menu.timer"] = "Timer",
            ["menu.modifiers"] = "Modifikatorer",
            ["menu.language"] = "Språk",
            ["menu.start"] = "Start økt",
            ["menu.stop"] = "Stopp økt",
            ["menu.quit"] = "Avslutt",
            ["menu.custom_time"] = "Skriv inn egen tid…",
            ["mod.closed_lid"] = "Tillat lukket lokk",
            ["mod.display"] = "Hold skjermen våken",
            ["duration.indefinitely"] = "Ubegrenset",
            ["duration.5m"] = "5 minutter",
            ["duration.15m"] = "15 minutter",
            ["duration.30m"] = "30 minutter",
            ["duration.1h"] = "1 time",
            ["duration.2h"] = "2 timer",
            ["duration.5h"] = "5 timer",
            ["duration.custom_named"] = "{0} minutter",
            ["custom.title"] = "Egendefinert timer",
            ["custom.prompt"] = "Varighet i minutter:",
            ["custom.cancel"] = "Avbryt",
            ["notify.body"] = "Menylinjeikonet er øverst til høyre. Start en økt derfra.",
            ["tooltip.active"] = "AmphetamineNet — aktiv ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — inaktiv",
            ["os.unsupported"] = "Denne appen fungerer bare på macOS.",
        });

        Add("he", new()
        {
            ["status.active"] = "פעיל",
            ["status.inactive"] = "לא פעיל",
            ["menu.timer"] = "טיימר",
            ["menu.modifiers"] = "מגדירים",
            ["menu.language"] = "שפה",
            ["menu.start"] = "התחלת הפעלה",
            ["menu.stop"] = "עצירת הפעלה",
            ["menu.quit"] = "יציאה",
            ["menu.custom_time"] = "הזנת זמן…",
            ["mod.closed_lid"] = "אפשר מכסה סגור",
            ["mod.display"] = "השאר מסך ער",
            ["duration.indefinitely"] = "ללא הגבלה",
            ["duration.5m"] = "5 דקות",
            ["duration.15m"] = "15 דקות",
            ["duration.30m"] = "30 דקות",
            ["duration.1h"] = "שעה אחת",
            ["duration.2h"] = "שעתיים",
            ["duration.5h"] = "5 שעות",
            ["duration.custom_named"] = "{0} דקות",
            ["custom.title"] = "טיימר מותאם",
            ["custom.prompt"] = "משך בדקות:",
            ["custom.cancel"] = "ביטול",
            ["notify.body"] = "סמל שורת התפריטים נמצא למעלה מימין. התחל הפעלה משם.",
            ["tooltip.active"] = "AmphetamineNet — פעיל ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — לא פעיל",
            ["os.unsupported"] = "האפליקציה הזו פועלת רק ב‑macOS.",
        });

        Add("bn", new()
        {
            ["status.active"] = "সক্রিয়",
            ["status.inactive"] = "নিষ্ক্রিয়",
            ["menu.timer"] = "টাইমার",
            ["menu.modifiers"] = "পরিবর্তক",
            ["menu.language"] = "ভাষা",
            ["menu.start"] = "সেশন শুরু",
            ["menu.stop"] = "সেশন থামান",
            ["menu.quit"] = "প্রস্থান",
            ["menu.custom_time"] = "নিজস্ব সময় লিখুন…",
            ["mod.closed_lid"] = "বন্ধ ঢাকনা অনুমোদন",
            ["mod.display"] = "ডিসপ্লে জাগিয়ে রাখুন",
            ["duration.indefinitely"] = "অনির্দিষ্টকাল",
            ["duration.5m"] = "৫ মিনিট",
            ["duration.15m"] = "১৫ মিনিট",
            ["duration.30m"] = "৩০ মিনিট",
            ["duration.1h"] = "১ ঘণ্টা",
            ["duration.2h"] = "২ ঘণ্টা",
            ["duration.5h"] = "৫ ঘণ্টা",
            ["duration.custom_named"] = "{0} মিনিট",
            ["custom.title"] = "কাস্টম টাইমার",
            ["custom.prompt"] = "মিনিটে সময়কাল:",
            ["custom.cancel"] = "বাতিল",
            ["notify.body"] = "মেনু বার আইকন উপরের ডানদিকে। সেখান থেকে সেশন শুরু করুন।",
            ["tooltip.active"] = "AmphetamineNet — সক্রিয় ({0})",
            ["tooltip.inactive"] = "AmphetamineNet — নিষ্ক্রিয়",
            ["os.unsupported"] = "এই অ্যাপটি শুধুমাত্র macOS-এ কাজ করে।",
        });

        return table;
    }
}
