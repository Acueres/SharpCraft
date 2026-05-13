namespace SharpCraft.Input;

internal static class KeyMapper
{
    private static readonly Keys[] map = BuildMap();

    private static Keys[] BuildMap()
    {
        var map = new Keys[512];

        // ── Alphabet ────────────────────────────────────────────────────
        map[4] = Keys.A; map[5] = Keys.B; map[6] = Keys.C;
        map[7] = Keys.D; map[8] = Keys.E; map[9] = Keys.F;
        map[10] = Keys.G; map[11] = Keys.H; map[12] = Keys.I;
        map[13] = Keys.J; map[14] = Keys.K; map[15] = Keys.L;
        map[16] = Keys.M; map[17] = Keys.N; map[18] = Keys.O;
        map[19] = Keys.P; map[20] = Keys.Q; map[21] = Keys.R;
        map[22] = Keys.S; map[23] = Keys.T; map[24] = Keys.U;
        map[25] = Keys.V; map[26] = Keys.W; map[27] = Keys.X;
        map[28] = Keys.Y; map[29] = Keys.Z;

        // ── Number row ──────────────────────────────────────────────────
        map[30] = Keys.D1; map[31] = Keys.D2; map[32] = Keys.D3;
        map[33] = Keys.D4; map[34] = Keys.D5; map[35] = Keys.D6;
        map[36] = Keys.D7; map[37] = Keys.D8; map[38] = Keys.D9;
        map[39] = Keys.D0;

        // ── Control / whitespace ─────────────────────────────────────
        map[40] = Keys.Enter;
        map[41] = Keys.Escape;
        map[42] = Keys.Back;
        map[43] = Keys.Tab;
        map[44] = Keys.Space;

        // ── Punctuation / symbols ────────────────────────────────────
        map[45] = Keys.OemMinus;
        map[46] = Keys.OemPlus;
        map[47] = Keys.OemOpenBrackets;
        map[48] = Keys.OemCloseBrackets;
        map[49] = Keys.OemBackslash;
        map[50] = Keys.OemNonUsHash;
        map[51] = Keys.OemSemicolon;
        map[52] = Keys.OemQuotes;
        map[53] = Keys.OemTilde;
        map[54] = Keys.OemComma;
        map[55] = Keys.OemPeriod;
        map[56] = Keys.OemQuestion;

        // ── Lock keys ─────────────────────────────────────────────────
        map[57] = Keys.CapsLock;

        // ── Function keys ─────────────────────────────────────────────
        map[58] = Keys.F1; map[59] = Keys.F2; map[60] = Keys.F3;
        map[61] = Keys.F4; map[62] = Keys.F5; map[63] = Keys.F6;
        map[64] = Keys.F7; map[65] = Keys.F8; map[66] = Keys.F9;
        map[67] = Keys.F10; map[68] = Keys.F11; map[69] = Keys.F12;

        // ── System / navigation ───────────────────────────────────────
        map[70] = Keys.PrintScreen;
        map[71] = Keys.Scroll;
        map[72] = Keys.Pause;
        map[73] = Keys.Insert;
        map[74] = Keys.Home;
        map[75] = Keys.PageUp;
        map[76] = Keys.Delete;
        map[77] = Keys.End;
        map[78] = Keys.PageDown;
        map[79] = Keys.Right;
        map[80] = Keys.Left;
        map[81] = Keys.Down;
        map[82] = Keys.Up;

        // ── Numpad ────────────────────────────────────────────────────
        map[83] = Keys.NumLock;
        map[84] = Keys.Divide;
        map[85] = Keys.Multiply;
        map[86] = Keys.Subtract;
        map[87] = Keys.Add;
        map[88] = Keys.NumPadEnter;
        map[89] = Keys.NumPad1; map[90] = Keys.NumPad2; map[91] = Keys.NumPad3;
        map[92] = Keys.NumPad4; map[93] = Keys.NumPad5; map[94] = Keys.NumPad6;
        map[95] = Keys.NumPad7; map[96] = Keys.NumPad8; map[97] = Keys.NumPad9;
        map[98] = Keys.NumPad0;
        map[99] = Keys.Decimal;

        // ── Extra / international ─────────────────────────────────────
        map[100] = Keys.NonUsBackslash;
        map[101] = Keys.Application;
        map[102] = Keys.Power;
        map[103] = Keys.NumPadEquals;
        map[104] = Keys.F13; map[105] = Keys.F14; map[106] = Keys.F15;
        map[107] = Keys.F16; map[108] = Keys.F17; map[109] = Keys.F18;
        map[110] = Keys.F19; map[111] = Keys.F20; map[112] = Keys.F21;
        map[113] = Keys.F22; map[114] = Keys.F23; map[115] = Keys.F24;

        map[116] = Keys.Execute;
        map[117] = Keys.Help;
        map[118] = Keys.Menu;
        map[119] = Keys.Select;
        map[120] = Keys.Stop;
        map[121] = Keys.Again;
        map[122] = Keys.Undo;
        map[123] = Keys.Cut;
        map[124] = Keys.Copy;
        map[125] = Keys.Paste;
        map[126] = Keys.Find;
        map[127] = Keys.Mute;
        map[128] = Keys.VolumeUp;
        map[129] = Keys.VolumeDown;

        map[133] = Keys.NumPadComma;
        map[134] = Keys.NumPadEqualsAs400;

        // ── International ─────────────────────────────────────────────
        map[135] = Keys.International1; map[136] = Keys.International2;
        map[137] = Keys.International3; map[138] = Keys.International4;
        map[139] = Keys.International5; map[140] = Keys.International6;
        map[141] = Keys.International7; map[142] = Keys.International8;
        map[143] = Keys.International9;

        // ── Language ──────────────────────────────────────────────────
        map[144] = Keys.Lang1; map[145] = Keys.Lang2; map[146] = Keys.Lang3;
        map[147] = Keys.Lang4; map[148] = Keys.Lang5; map[149] = Keys.Lang6;
        map[150] = Keys.Lang7; map[151] = Keys.Lang8; map[152] = Keys.Lang9;

        // ── Misc ──────────────────────────────────────────────────────
        map[153] = Keys.AltErase;
        map[154] = Keys.SysReq;
        map[155] = Keys.Cancel;
        map[156] = Keys.Clear;
        map[157] = Keys.Prior;
        map[158] = Keys.Return2;
        map[159] = Keys.Separator;
        map[160] = Keys.Out;
        map[161] = Keys.Oper;
        map[162] = Keys.ClearAgain;
        map[163] = Keys.CrSel;
        map[164] = Keys.ExSel;

        // ── Extended numpad ───────────────────────────────────────────
        map[176] = Keys.NumPad00;
        map[177] = Keys.NumPad000;
        map[178] = Keys.ThousandsSeparator;
        map[179] = Keys.DecimalSeparator;
        map[180] = Keys.CurrencyUnit;
        map[181] = Keys.CurrencySubUnit;
        map[182] = Keys.NumPadLeftParen;
        map[183] = Keys.NumPadRightParen;
        map[184] = Keys.NumPadLeftBrace;
        map[185] = Keys.NumPadRightBrace;
        map[186] = Keys.NumPadTab;
        map[187] = Keys.NumPadBackspace;
        map[188] = Keys.NumPadA; map[189] = Keys.NumPadB; map[190] = Keys.NumPadC;
        map[191] = Keys.NumPadD; map[192] = Keys.NumPadE; map[193] = Keys.NumPadF;
        map[194] = Keys.NumPadXor;
        map[195] = Keys.NumPadPower;
        map[196] = Keys.NumPadPercent;
        map[197] = Keys.NumPadLess;
        map[198] = Keys.NumPadGreater;
        map[199] = Keys.NumPadAmpersand;
        map[200] = Keys.NumPadDblAmpersand;
        map[201] = Keys.NumPadVerticalBar;
        map[202] = Keys.NumPadDblVerticalBar;
        map[203] = Keys.NumPadColon;
        map[204] = Keys.NumPadHash;
        map[205] = Keys.NumPadSpace;
        map[206] = Keys.NumPadAt;
        map[207] = Keys.NumPadExclam;
        map[208] = Keys.NumPadMemStore;
        map[209] = Keys.NumPadMemRecall;
        map[210] = Keys.NumPadMemClear;
        map[211] = Keys.NumPadMemAdd;
        map[212] = Keys.NumPadMemSubtract;
        map[213] = Keys.NumPadMemMultiply;
        map[214] = Keys.NumPadMemDivide;
        map[215] = Keys.NumPadPlusMinus;
        map[216] = Keys.NumPadClear;
        map[217] = Keys.NumPadClearEntry;
        map[218] = Keys.NumPadBinary;
        map[219] = Keys.NumPadOctal;
        map[220] = Keys.NumPadDecimal;
        map[221] = Keys.NumPadHexadecimal;

        // ── Modifier keys ─────────────────────────────────────────────
        map[224] = Keys.LeftControl;
        map[225] = Keys.LeftShift;
        map[226] = Keys.LeftAlt;
        map[227] = Keys.LeftWindows;
        map[228] = Keys.RightControl;
        map[229] = Keys.RightShift;
        map[230] = Keys.RightAlt;
        map[231] = Keys.RightWindows;

        map[257] = Keys.Mode;

        // ── Consumer page ─────────────────────────────────────────────
        map[258] = Keys.Sleep;
        map[259] = Keys.Wake;
        map[260] = Keys.ChannelIncrement;
        map[261] = Keys.ChannelDecrement;
        map[262] = Keys.MediaPlay;
        map[263] = Keys.MediaPause;
        map[264] = Keys.MediaRecord;
        map[265] = Keys.MediaFastForward;
        map[266] = Keys.MediaRewind;
        map[267] = Keys.MediaNextTrack;
        map[268] = Keys.MediaPreviousTrack;
        map[269] = Keys.MediaStop;
        map[270] = Keys.MediaEject;
        map[271] = Keys.MediaPlayPause;
        map[272] = Keys.MediaSelect;
        map[273] = Keys.AcNew;
        map[274] = Keys.AcOpen;
        map[275] = Keys.AcClose;
        map[276] = Keys.AcExit;
        map[277] = Keys.AcSave;
        map[278] = Keys.AcPrint;
        map[279] = Keys.AcProperties;
        map[280] = Keys.AcSearch;
        map[281] = Keys.AcHome;
        map[282] = Keys.AcBack;
        map[283] = Keys.AcForward;
        map[284] = Keys.AcStop;
        map[285] = Keys.AcRefresh;
        map[286] = Keys.AcBookmarks;

        // ── Mobile ────────────────────────────────────────────────────
        map[287] = Keys.SoftLeft;
        map[288] = Keys.SoftRight;
        map[289] = Keys.Call;
        map[290] = Keys.EndCall;

        return map;
    }

    /// <summary>
    /// Converts an SDL3 scancode integer to a <see cref="Keys"/> value.
    /// Returns <see cref="Keys.None"/> for unknown or out-of-range scancodes.
    /// </summary>
    public static Keys ToKey(int scancode)
    {
        if ((uint)scancode >= (uint)map.Length)
            return Keys.None;

        return map[scancode];
    }

    /// <summary>
    /// Converts a <see cref="Keys"/> value back to its SDL3 scancode integer.
    /// Because <see cref="Keys"/> values are defined to equal their SDL3
    /// scancode, this is a direct cast.
    /// </summary>
    public static int ToScancode(Keys key) => (int)key;

}
