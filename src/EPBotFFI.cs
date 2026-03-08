using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Thin C FFI layer over Edward Piwowar's EPBot bridge bidding engine.
/// Each exported function maps 1:1 to a public EPBot method.
/// The caller is responsible for orchestration (creating multiple instances,
/// managing auctions, etc.).
///
/// Memory convention:
/// - String return values are written to caller-provided buffers (ptr + size).
/// - Array return values are written to caller-provided buffers with a count out-param.
/// - Instance handles are IntPtr (opaque pointer to managed EPBot object).
/// - Return value: 0 = success, negative = error code.
/// </summary>
public static class EPBotFFI
{
    // Instance handle management via GCHandle
    private static IntPtr Alloc(EPBot bot)
    {
        var handle = GCHandle.Alloc(bot);
        return GCHandle.ToIntPtr(handle);
    }

    private static EPBot? Get(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        var handle = GCHandle.FromIntPtr(ptr);
        return handle.Target as EPBot;
    }

    private static void Free(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return;
        var handle = GCHandle.FromIntPtr(ptr);
        handle.Free();
    }

    // Error codes
    private const int OK = 0;
    private const int ERR_NULL_HANDLE = -1;
    private const int ERR_EXCEPTION = -2;
    private const int ERR_BUFFER_TOO_SMALL = -3;

    // Thread-local error message
    [ThreadStatic] private static string? _lastError;
    [ThreadStatic] private static IntPtr _lastErrorPtr;

    private static void SetError(string msg) { _lastError = msg; }

    private static int WriteString(string? value, IntPtr buffer, int bufferSize)
    {
        string s = value ?? "";
        byte[] bytes = Encoding.UTF8.GetBytes(s + '\0');
        if (bytes.Length > bufferSize) return ERR_BUFFER_TOO_SMALL;
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        return OK;
    }

    private static int WriteIntArray(int[]? arr, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        if (arr == null) { Marshal.WriteInt32(countOut, 0); return OK; }
        int needed = arr.Length * sizeof(int);
        if (needed > bufferSize) return ERR_BUFFER_TOO_SMALL;
        Marshal.Copy(arr, 0, buffer, arr.Length);
        Marshal.WriteInt32(countOut, arr.Length);
        return OK;
    }

    private static int WriteStringArray(string[]? arr, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        if (arr == null) { Marshal.WriteInt32(countOut, 0); return OK; }
        // Write as newline-separated string
        string joined = string.Join("\n", arr);
        int result = WriteString(joined, buffer, bufferSize);
        if (result == OK) Marshal.WriteInt32(countOut, arr.Length);
        return result;
    }

    // ========================================================================
    // Instance lifecycle
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_create")]
    public static IntPtr Create()
    {
        try { return Alloc(new EPBot()); }
        catch (Exception ex) { SetError(ex.Message); return IntPtr.Zero; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_destroy")]
    public static void Destroy(IntPtr instance)
    {
        Free(instance);
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_last_error")]
    public static IntPtr GetLastError()
    {
        if (_lastError == null) return IntPtr.Zero;
        if (_lastErrorPtr != IntPtr.Zero) Marshal.FreeHGlobal(_lastErrorPtr);
        byte[] bytes = Encoding.UTF8.GetBytes(_lastError + '\0');
        _lastErrorPtr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, _lastErrorPtr, bytes.Length);
        return _lastErrorPtr;
    }

    // ========================================================================
    // Core bidding
    // ========================================================================

    /// <summary>
    /// Initialize a hand for a player.
    /// longerPtr: newline-separated suit strings (C.D.H.S order, 4 suits)
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "epbot_new_hand")]
    public static int NewHand(IntPtr instance, int playerPosition, IntPtr longerPtr,
                              int dealer, int vulnerability, int repeating, int bPlaying)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string longerStr = Marshal.PtrToStringUTF8(longerPtr) ?? "";
            string[] longer = longerStr.Split('\n');
            bot.new_hand(playerPosition, ref longer, dealer, vulnerability,
                        repeating != 0, bPlaying != 0);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_bid")]
    public static int GetBid(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_bid();
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    /// <summary>
    /// Broadcast a bid to this player instance.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "epbot_set_bid")]
    public static int SetBid(IntPtr instance, int spare, int newValue, IntPtr strAlertPtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string alert = Marshal.PtrToStringUTF8(strAlertPtr) ?? "";
            bot.set_bid(spare, newValue, alert);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_arr_bids")]
    public static int SetArrBids(IntPtr instance, IntPtr bidsPtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string bidsStr = Marshal.PtrToStringUTF8(bidsPtr) ?? "";
            string[] bids = bidsStr.Split('\n');
            bot.set_arr_bids(ref bids);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_interpret_bid")]
    public static int InterpretBid(IntPtr instance, int bidCode)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.interpret_bid(bidCode);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_ask")]
    public static int Ask(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.ask();
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    // ========================================================================
    // Conventions
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_conventions")]
    public static int GetConventions(IntPtr instance, int site, IntPtr conventionPtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string convention = Marshal.PtrToStringUTF8(conventionPtr) ?? "";
            return bot.get_conventions(site, convention) ? 1 : 0;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_conventions")]
    public static int SetConventions(IntPtr instance, int site, IntPtr conventionPtr, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string convention = Marshal.PtrToStringUTF8(conventionPtr) ?? "";
            bot.set_conventions(site, convention, value != 0);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_system_type")]
    public static int GetSystemType(IntPtr instance, int systemNumber)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_system_type(systemNumber);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_system_type")]
    public static int SetSystemType(IntPtr instance, int systemNumber, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.set_system_type(systemNumber, value);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_opponent_type")]
    public static int GetOpponentType(IntPtr instance, int systemNumber)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_opponent_type(systemNumber);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_opponent_type")]
    public static int SetOpponentType(IntPtr instance, int systemNumber, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.set_opponent_type(systemNumber, value);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_convention_index")]
    public static int ConventionIndex(IntPtr instance, IntPtr namePtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string name = Marshal.PtrToStringUTF8(namePtr) ?? "";
            return bot.convention_index(name);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_convention_name")]
    public static int ConventionName(IntPtr instance, int index, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.convention_name(index), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_convention_name")]
    public static int GetConventionName(IntPtr instance, int index, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.get_convention_name(index), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_selected_conventions")]
    public static int SelectedConventions(IntPtr instance, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteStringArray(bot.selected_conventions(), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_system_name")]
    public static int SystemName(IntPtr instance, int systemNumber, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.system_name(systemNumber), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    // ========================================================================
    // Scoring & settings
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_scoring")]
    public static int GetScoring(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.scoring;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_scoring")]
    public static int SetScoring(IntPtr instance, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.scoring = value;
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_playing_skills")]
    public static int GetPlayingSkills(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.Playing_Skills;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_playing_skills")]
    public static int SetPlayingSkills(IntPtr instance, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.Playing_Skills = value;
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_defensive_skills")]
    public static int GetDefensiveSkills(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.Defensive_Skills;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_defensive_skills")]
    public static int SetDefensiveSkills(IntPtr instance, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.Defensive_Skills = value;
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_licence")]
    public static int GetLicence(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.Licence;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_licence")]
    public static int SetLicence(IntPtr instance, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.Licence = value;
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_bcalconsole_path")]
    public static int GetBcalconsolePath(IntPtr instance, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.bcalconsole_path, buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_bcalconsole_path")]
    public static int SetBcalconsolePath(IntPtr instance, IntPtr pathPtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string path = Marshal.PtrToStringUTF8(pathPtr) ?? "";
            bot.bcalconsole_path = path;
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    // ========================================================================
    // State queries
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_position")]
    public static int GetPosition(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_Position();
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_dealer")]
    public static int GetDealer(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_Dealer();
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_vulnerability")]
    public static int GetVulnerability(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_Vulnerability();
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_version")]
    public static int Version(IntPtr instance)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.version();
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_copyright")]
    public static int Copyright(IntPtr instance, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.Copyright(), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_last_epbot_error")]
    public static int GetLastEpbotError(IntPtr instance, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.LastError, buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_str_bidding")]
    public static int GetStrBidding(IntPtr instance, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.get_str_bidding(), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    // ========================================================================
    // Analysis
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_probable_level")]
    public static int GetProbableLevel(IntPtr instance, int strain)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_probable_level(strain);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_probable_levels")]
    public static int GetProbableLevels(IntPtr instance, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_probable_levels(), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_sd_tricks")]
    public static int GetSDTricks(IntPtr instance, IntPtr partnerLongerPtr,
                                   IntPtr tricksBuffer, int tricksBufferSize, IntPtr tricksCountOut,
                                   IntPtr pctBuffer, int pctBufferSize, IntPtr pctCountOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string partnerStr = Marshal.PtrToStringUTF8(partnerLongerPtr) ?? "";
            string[] partnerLonger = partnerStr.Split('\n');
            int[] percentages = Array.Empty<int>();
            int[] tricks = bot.get_SD_tricks(ref partnerLonger, ref percentages);
            int r1 = WriteIntArray(tricks, tricksBuffer, tricksBufferSize, tricksCountOut);
            if (r1 != OK) return r1;
            return WriteIntArray(percentages, pctBuffer, pctBufferSize, pctCountOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    // ========================================================================
    // Info / meaning (bid interpretation data)
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_meaning")]
    public static int GetInfoMeaning(IntPtr instance, int k, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.get_info_meaning(k), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_meaning")]
    public static int SetInfoMeaning(IntPtr instance, int k, IntPtr valuePtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string value = Marshal.PtrToStringUTF8(valuePtr) ?? "";
            bot.set_info_meaning(k, value);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_meaning_extended")]
    public static int GetInfoMeaningExtended(IntPtr instance, int position, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.get_info_meaning_extended(position), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_meaning_extended")]
    public static int SetInfoMeaningExtended(IntPtr instance, int position, IntPtr valuePtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string value = Marshal.PtrToStringUTF8(valuePtr) ?? "";
            bot.set_info_meaning_extended(position, value);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_feature")]
    public static int GetInfoFeature(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_feature(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_feature")]
    public static int SetInfoFeature(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_feature(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_min_length")]
    public static int GetInfoMinLength(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_min_length(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_min_length")]
    public static int SetInfoMinLength(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_min_length(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_max_length")]
    public static int GetInfoMaxLength(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_max_length(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_max_length")]
    public static int SetInfoMaxLength(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_max_length(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_probable_length")]
    public static int GetInfoProbableLength(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_probable_length(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_probable_length")]
    public static int SetInfoProbableLength(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_probable_length(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_honors")]
    public static int GetInfoHonors(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_honors(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_honors")]
    public static int SetInfoHonors(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_honors(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_suit_power")]
    public static int GetInfoSuitPower(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_suit_power(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_suit_power")]
    public static int SetInfoSuitPower(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_suit_power(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_strength")]
    public static int GetInfoStrength(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_strength(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_strength")]
    public static int SetInfoStrength(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_strength(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_stoppers")]
    public static int GetInfoStoppers(IntPtr instance, int position, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteIntArray(bot.get_info_stoppers(position), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_stoppers")]
    public static int SetInfoStoppers(IntPtr instance, int position, IntPtr dataPtr, int count)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            int[] arr = new int[count];
            Marshal.Copy(dataPtr, arr, 0, count);
            bot.set_info_stoppers(position, arr);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_info_alerting")]
    public static int GetInfoAlerting(IntPtr instance, int k)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_info_alerting(k) ? 1 : 0;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_info_alerting")]
    public static int SetInfoAlerting(IntPtr instance, int k, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.set_info_alerting(k, value != 0);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_used_conventions")]
    public static int GetUsedConventions(IntPtr instance, int item)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return bot.get_used_conventions(item);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_used_conventions")]
    public static int SetUsedConventions(IntPtr instance, int item, int value)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            bot.set_used_conventions(item, value);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    // ========================================================================
    // Card play
    // ========================================================================

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_lead")]
    public static int GetLead(IntPtr instance, int forceLead, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.get_lead(forceLead != 0), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_lead")]
    public static int SetLead(IntPtr instance, IntPtr cardPtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string card = Marshal.PtrToStringUTF8(cardPtr) ?? "";
            bot.set_lead(card);
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_set_dummy")]
    public static int SetDummy(IntPtr instance, int dummy, IntPtr cardsPtr, int allData, IntPtr withoutFinalLengthPtr)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            string cardsStr = Marshal.PtrToStringUTF8(cardsPtr) ?? "";
            string[] cards = cardsStr.Split('\n');
            bool withoutFinalLength = Marshal.ReadByte(withoutFinalLengthPtr) != 0;
            bot.set_dummy(dummy, ref cards, allData != 0, ref withoutFinalLength);
            Marshal.WriteByte(withoutFinalLengthPtr, (byte)(withoutFinalLength ? 1 : 0));
            return OK;
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_cards")]
    public static int GetCards(IntPtr instance, IntPtr buffer, int bufferSize)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteString(bot.get_cards(), buffer, bufferSize);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_hand")]
    public static int GetHand(IntPtr instance, int playerPosition, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteStringArray(bot.get_hand(playerPosition), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }

    [UnmanagedCallersOnly(EntryPoint = "epbot_get_arr_suits")]
    public static int GetArrSuits(IntPtr instance, int currentLongers, IntPtr buffer, int bufferSize, IntPtr countOut)
    {
        try
        {
            var bot = Get(instance);
            if (bot == null) return ERR_NULL_HANDLE;
            return WriteStringArray(bot.get_arr_suits(currentLongers != 0), buffer, bufferSize, countOut);
        }
        catch (Exception ex) { SetError(ex.Message); return ERR_EXCEPTION; }
    }
}
