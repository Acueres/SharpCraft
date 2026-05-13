namespace SharpCraft.Input;

internal enum Keys
{
    None = 0,

    // ── Alphabet ────────────────────────────────────────────────────────────
    A = 4,
    B = 5,
    C = 6,
    D = 7,
    E = 8,
    F = 9,
    G = 10,
    H = 11,
    I = 12,
    J = 13,
    K = 14,
    L = 15,
    M = 16,
    N = 17,
    O = 18,
    P = 19,
    Q = 20,
    R = 21,
    S = 22,
    T = 23,
    U = 24,
    V = 25,
    W = 26,
    X = 27,
    Y = 28,
    Z = 29,

    // ── Number row ──────────────────────────────────────────────────────────
    D1 = 30,
    D2 = 31,
    D3 = 32,
    D4 = 33,
    D5 = 34,
    D6 = 35,
    D7 = 36,
    D8 = 37,
    D9 = 38,
    D0 = 39,

    // ── Control / whitespace ─────────────────────────────────────────────
    Enter = 40,
    Escape = 41,
    Back = 42,   // Backspace
    Tab = 43,
    Space = 44,

    // ── Punctuation / symbols ────────────────────────────────────────────
    OemMinus = 45,   // -
    OemPlus = 46,   // = (plus/equals key)
    OemOpenBrackets = 47,   // [
    OemCloseBrackets = 48,   // ]
    OemBackslash = 49,   // backslash / vertical line
    OemNonUsHash = 50,   // ISO non-US hash / tilde
    OemSemicolon = 51,   // ;
    OemQuotes = 52,   // '
    OemTilde = 53,   // ` (grave accent)
    OemComma = 54,   // ,
    OemPeriod = 55,   // .
    OemQuestion = 56,   // /

    // ── Lock keys ───────────────────────────────────────────────────────
    CapsLock = 57,

    // ── Function keys ────────────────────────────────────────────────────
    F1 = 58,
    F2 = 59,
    F3 = 60,
    F4 = 61,
    F5 = 62,
    F6 = 63,
    F7 = 64,
    F8 = 65,
    F9 = 66,
    F10 = 67,
    F11 = 68,
    F12 = 69,

    // ── System / navigation ──────────────────────────────────────────────
    PrintScreen = 70,
    Scroll = 71,   // Scroll Lock
    Pause = 72,
    Insert = 73,
    Home = 74,
    PageUp = 75,
    Delete = 76,
    End = 77,
    PageDown = 78,
    Right = 79,
    Left = 80,
    Down = 81,
    Up = 82,

    // ── Numpad ──────────────────────────────────────────────────────────
    NumLock = 83,   // Num Lock / Clear (Mac)
    Divide = 84,   // KP /
    Multiply = 85,   // KP *
    Subtract = 86,   // KP -
    Add = 87,   // KP +
    NumPadEnter = 88,
    NumPad1 = 89,
    NumPad2 = 90,
    NumPad3 = 91,
    NumPad4 = 92,
    NumPad5 = 93,
    NumPad6 = 94,
    NumPad7 = 95,
    NumPad8 = 96,
    NumPad9 = 97,
    NumPad0 = 98,
    Decimal = 99,   // KP .

    // ── Extra / international ────────────────────────────────────────────
    NonUsBackslash = 100,   // ISO key between Left Shift and Z
    Application = 101,   // Windows context menu / Compose
    Power = 102,
    NumPadEquals = 103,   // KP =
    F13 = 104,
    F14 = 105,
    F15 = 106,
    F16 = 107,
    F17 = 108,
    F18 = 109,
    F19 = 110,
    F20 = 111,
    F21 = 112,
    F22 = 113,
    F23 = 114,
    F24 = 115,

    Execute = 116,
    Help = 117,
    Menu = 118,
    Select = 119,
    Stop = 120,
    Again = 121,   // AC Redo / Repeat
    Undo = 122,
    Cut = 123,
    Copy = 124,
    Paste = 125,
    Find = 126,
    Mute = 127,
    VolumeUp = 128,
    VolumeDown = 129,

    NumPadComma = 133,
    NumPadEqualsAs400 = 134,

    // ── International keys (Asian keyboards) ────────────────────────────
    International1 = 135,
    International2 = 136,
    International3 = 137,   // Yen
    International4 = 138,
    International5 = 139,
    International6 = 140,
    International7 = 141,
    International8 = 142,
    International9 = 143,

    // ── Language keys ───────────────────────────────────────────────────
    Lang1 = 144,   // Hangul / English toggle
    Lang2 = 145,   // Hanja conversion
    Lang3 = 146,   // Katakana
    Lang4 = 147,   // Hiragana
    Lang5 = 148,   // Zenkaku / Hankaku
    Lang6 = 149,
    Lang7 = 150,
    Lang8 = 151,
    Lang9 = 152,

    // ── Misc ─────────────────────────────────────────────────────────────
    AltErase = 153,
    SysReq = 154,
    Cancel = 155,
    Clear = 156,
    Prior = 157,
    Return2 = 158,
    Separator = 159,
    Out = 160,
    Oper = 161,
    ClearAgain = 162,
    CrSel = 163,
    ExSel = 164,

    // ── Extended numpad ─────────────────────────────────────────────────
    NumPad00 = 176,
    NumPad000 = 177,
    ThousandsSeparator = 178,
    DecimalSeparator = 179,
    CurrencyUnit = 180,
    CurrencySubUnit = 181,
    NumPadLeftParen = 182,
    NumPadRightParen = 183,
    NumPadLeftBrace = 184,
    NumPadRightBrace = 185,
    NumPadTab = 186,
    NumPadBackspace = 187,
    NumPadA = 188,
    NumPadB = 189,
    NumPadC = 190,
    NumPadD = 191,
    NumPadE = 192,
    NumPadF = 193,
    NumPadXor = 194,
    NumPadPower = 195,
    NumPadPercent = 196,
    NumPadLess = 197,
    NumPadGreater = 198,
    NumPadAmpersand = 199,
    NumPadDblAmpersand = 200,
    NumPadVerticalBar = 201,
    NumPadDblVerticalBar = 202,
    NumPadColon = 203,
    NumPadHash = 204,
    NumPadSpace = 205,
    NumPadAt = 206,
    NumPadExclam = 207,
    NumPadMemStore = 208,
    NumPadMemRecall = 209,
    NumPadMemClear = 210,
    NumPadMemAdd = 211,
    NumPadMemSubtract = 212,
    NumPadMemMultiply = 213,
    NumPadMemDivide = 214,
    NumPadPlusMinus = 215,
    NumPadClear = 216,
    NumPadClearEntry = 217,
    NumPadBinary = 218,
    NumPadOctal = 219,
    NumPadDecimal = 220,
    NumPadHexadecimal = 221,

    // ── Modifier keys ────────────────────────────────────────────────────
    LeftControl = 224,
    LeftShift = 225,
    LeftAlt = 226,   // Alt / Option
    LeftWindows = 227,   // Windows / Command (Apple) / Meta
    RightControl = 228,
    RightShift = 229,
    RightAlt = 230,   // AltGr / Option
    RightWindows = 231,   // Windows / Command (Apple) / Meta

    Mode = 257,   // ModeSwitch (SDL_KMOD_MODE)

    // ── Consumer page (0x0C) ─────────────────────────────────────────────
    Sleep = 258,
    Wake = 259,
    ChannelIncrement = 260,
    ChannelDecrement = 261,

    MediaPlay = 262,
    MediaPause = 263,
    MediaRecord = 264,
    MediaFastForward = 265,
    MediaRewind = 266,
    MediaNextTrack = 267,
    MediaPreviousTrack = 268,
    MediaStop = 269,
    MediaEject = 270,
    MediaPlayPause = 271,
    MediaSelect = 272,

    AcNew = 273,
    AcOpen = 274,
    AcClose = 275,
    AcExit = 276,
    AcSave = 277,
    AcPrint = 278,
    AcProperties = 279,
    AcSearch = 280,
    AcHome = 281,
    AcBack = 282,
    AcForward = 283,
    AcStop = 284,
    AcRefresh = 285,
    AcBookmarks = 286,

    // ── Mobile ───────────────────────────────────────────────────────────
    SoftLeft = 287,
    SoftRight = 288,
    Call = 289,
    EndCall = 290
}
