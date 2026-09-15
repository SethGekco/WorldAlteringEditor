using Rampastring.Tools;
using System.Reflection;

namespace MapEditorLibrary;

public static class Constants
{
    public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

    public static int CellSizeX = 48;
    public static int CellSizeY = 24;
    public const int CellSizeInLeptons = 256;
    public static int CellHeight => CellSizeY / 2;
    public static int HighBridgeHeight = 4;
    public static int TileColorBufferSize = 576;

    public static int RenderPixelPadding = 50;

    public static bool IsFlatWorld = false;
    public static bool TheaterPaletteForTiberium = false;
    public static bool TheaterPaletteForVeins = false;
    public static bool TiberiumAffectedByLighting = false;
    public static bool TiberiumTreesAffectedByLighting = false;
    public static bool TerrainPaletteBuildingsAffectedByLighting = false;
    public static bool VoxelsAffectedByLighting = false;
    public static bool NewTheaterGenericBuilding = false;
    public static bool DrawBuildingAnimationShadows = false;
    public static bool IsRA2YR = false;
    public static bool WarnOfTooManyTriggerActions = true;
    public static bool DefaultPreview = false;

    public static string[] ExpectedClientExecutableNames = new string[] { "DTA.exe" };
    public static string GameRegistryInstallPath = "SOFTWARE\\DawnOfTheTiberiumAge";
    public static string OpenFileDialogFilter = "TS maps|*.map|All files|*.*";

    public static bool EnableIniInclude = false;
    public static bool EnableIniInheritance = false;

    public static bool IntegerVariables = false;

    public static string RulesIniPath;
    public static string FirestormIniPath;
    public static string ArtIniPath;
    public static string FirestormArtIniPath;
    public static string AIIniPath;
    public static string FirestormAIIniPath;
    public static string TutorialIniPath;
    public static string ThemeIniPath;
    public static string EvaIniPath;
    public static string SoundIniPath;

    public const int TextureSizeLimit = 16384;

    public static int MaxMapWidth;
    public static int MaxMapHeight;

    public const byte MaxMapHeightLevel = 12;
    public static int MapYBaseline => MaxMapHeightLevel * CellHeight;

    public static int MaxWaypoint = 100;

    public const int ObjectHealthMax = 256;
    public const int FacingMax = 255;

    public const int TurretFrameCount = 32;

    // TODO parse from Rules.ini
    public const int ConditionYellowHP = 128;

    public const int VeterancyElite = 200;
    public const int VeterancyVeteran = 100;

    public const int UIEmptySideSpace = 10;
    public const int UIEmptyTopSpace = 10;
    public const int UIEmptyBottomSpace = 10;

    public const int UIHorizontalSpacing = 6;
    public const int UIVerticalSpacing = 6;

    public const int UIDefaultFont = 0;
    public const int UIBoldFont = 1;

    public const int UITextBoxHeight = 21;
    public const int UIButtonHeight = 23;

    public const int UITopBarMenuHeight = 23;

    public static int UITreeViewLineHeight = 20;

    public static double UIAccidentalClickPreventionTime = 0.2;

    public static int MapPreviewMaxWidth = 800;
    public static int MapPreviewMaxHeight = 400;

    public static int MaxHouseTechLevel = 10;

    // stride-2048 fork: overlay pack stride / max cell coordinate raised from 512.
    // This is the on-disk OverlayPack index stride (overlayIndex = Y * this + X) and
    // must match the game engine's patched overlay-array stride (see MapSizeExt).
    public const int MAX_MAP_LENGTH_IN_DIMENSION = 2048;

    // The unmodified game's overlay stride / max cell coordinate (width + height <= 512).
    public const int VANILLA_MAP_LENGTH_IN_DIMENSION = 512;

    /// <summary>
    /// stride-2048 fork: the OverlayPack stride is baked into each map file, so it must be chosen
    /// per map. Vanilla-sized maps (width + height within the unmodified game's limit) use the
    /// vanilla 512 stride so they stay byte-compatible with the unmodified game and FA2/WAE;
    /// larger maps (which only run on a MapSizeExt-patched game) use the extended stride.
    /// TODO(MapSizeExt): once MapSizeExt defines its map tag, honor that tag here (both when
    /// reading, to trust the tag over the size heuristic, and when writing, to emit the tag on
    /// extended-stride maps).
    /// </summary>
    public static int GetOverlayPackStride(int mapWidth, int mapHeight)
    {
        return mapWidth + mapHeight <= VANILLA_MAP_LENGTH_IN_DIMENSION
            ? VANILLA_MAP_LENGTH_IN_DIMENSION
            : MAX_MAP_LENGTH_IN_DIMENSION;
    }

    // stride-2048 fork: the [MapSizeExt] per-map tag section, shared with the MapSizeExt game DLL.
    // CoordBase is the base used for packed cell coordinates (waypoint/celltag/terrain keys,
    // packed = Y * base + X). Vanilla packs in base 1000; maps with width + height > 1000 cannot,
    // and use the next power of two >= width + height instead. RequiredStride records the
    // OverlayPack stride so the DLL can warn/adapt when a map exceeds its configured stride.
    public const string MapSizeExtSectionName = "MapSizeExt";
    public const string MapSizeExtCoordBaseKey = "CoordBase";
    public const string MapSizeExtRequiredStrideKey = "RequiredStride";

    public const int VANILLA_CELL_COORD_BASE = 1000;

    /// <summary>
    /// The packed-cell-coordinate base of the currently loaded map. Set from the map's
    /// [MapSizeExt] CoordBase= tag (or derived from its size) on load, and re-derived on save.
    /// </summary>
    public static int CellCoordBase = VANILLA_CELL_COORD_BASE;

    public static int GetCellCoordBaseForMapSize(int mapWidth, int mapHeight)
    {
        int sum = mapWidth + mapHeight;
        if (sum <= VANILLA_CELL_COORD_BASE)
            return VANILLA_CELL_COORD_BASE;

        int powerOfTwo = 1;
        while (powerOfTwo < sum)
            powerOfTwo <<= 1;

        return powerOfTwo;
    }
    public const int NO_OVERLAY = -1;
    public const int OverlayPackFormat = 80;

    public const string NoneValue1 = "<none>";
    public const string NoneValue2 = "None";

    public const float RemapBrightenFactor = 1.25f;

    // The resolution of depth rendering. In other words, the minimum depth difference that is significant enough to have an impact on rendering order.
    public const float DepthEpsilon = 1e-5f;

    // Depth is between 0.0 and 1.0. How much of the scale is reserved for depth increasing as we go southwards on the map.
    public const float DownwardsDepthRenderSpace = 0.90f;

    // How much of the depth scale (0.0 to 1.0) is reserved for depth increasing as we go up the map height levels.
    // Calculated dynamically.
    public static float DepthRenderStep = 0;

    public const string ClipboardMapDataFormatValue = "ScenarioEditorCopiedMapData";
    public const string ClipboardTriggerActionEventFormatValue = "ScenarioEditorCopiedTriggerData";
    public const string ClipboardTriggerFormatValue = "ScenarioEditorCopiedTrigger";
    public const string UserDataFolder = "UserData";

    public const char NewTheaterGenericLetter = 'G';

    public const string VeinholeMonsterTypeName = "VEINHOLE";
    public const string VeinholeDummyTypeName = "VEINHOLEDUMMY";

    public const int MultiplayerMaxPlayers = 8;

    public const int TS_WAYPT_SPECIAL = 100;

    public const string DefaultHouseTypeName = "Neutral";

    public static void Init()
    {
        const string ConstantsSectionName = "Constants";
        const string FilePathsSectionName = "FilePaths";

        IniFile constantsIni = Helpers.ReadConfigINI("Constants.ini");

        CellSizeX = constantsIni.GetIntValue(ConstantsSectionName, nameof(CellSizeX), CellSizeX);
        CellSizeY = constantsIni.GetIntValue(ConstantsSectionName, nameof(CellSizeY), CellSizeY);

        // stride-2048 fork: maps larger than a single GPU texture are rendered with camera-follow
        // render targets, so the per-axis limits are no longer bound by the texture size limit.
        // The effective limit is the extended coordinate stride (also applied as width + height).
        MaxMapWidth = MAX_MAP_LENGTH_IN_DIMENSION;
        MaxMapHeight = MAX_MAP_LENGTH_IN_DIMENSION;

        TileColorBufferSize = constantsIni.GetIntValue(ConstantsSectionName, nameof(TileColorBufferSize), TileColorBufferSize);

        RenderPixelPadding = constantsIni.GetIntValue(ConstantsSectionName, nameof(RenderPixelPadding), RenderPixelPadding);

        IsFlatWorld = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(IsFlatWorld), IsFlatWorld);
        TheaterPaletteForTiberium = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(TheaterPaletteForTiberium), TheaterPaletteForTiberium);
        TheaterPaletteForVeins = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(TheaterPaletteForVeins), TheaterPaletteForVeins);
        TiberiumAffectedByLighting = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(TiberiumAffectedByLighting), TiberiumAffectedByLighting);
        TiberiumTreesAffectedByLighting = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(TiberiumTreesAffectedByLighting), TiberiumTreesAffectedByLighting);
        TerrainPaletteBuildingsAffectedByLighting = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(TerrainPaletteBuildingsAffectedByLighting), TerrainPaletteBuildingsAffectedByLighting);
        VoxelsAffectedByLighting = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(VoxelsAffectedByLighting), VoxelsAffectedByLighting);
        NewTheaterGenericBuilding = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(NewTheaterGenericBuilding), NewTheaterGenericBuilding);
        DrawBuildingAnimationShadows = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(DrawBuildingAnimationShadows), DrawBuildingAnimationShadows);
        IsRA2YR = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(IsRA2YR), IsRA2YR);
        WarnOfTooManyTriggerActions = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(WarnOfTooManyTriggerActions), WarnOfTooManyTriggerActions);
        DefaultPreview = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(DefaultPreview), DefaultPreview);

        // Check two keys for backwards compatibility
        if (constantsIni.KeyExists(ConstantsSectionName, "ExpectedClientExecutableName"))
            ExpectedClientExecutableNames = constantsIni.GetSection(ConstantsSectionName).GetListValue("ExpectedClientExecutableName", ',', s => s).ToArray();
        else
            ExpectedClientExecutableNames = constantsIni.GetSection(ConstantsSectionName).GetListValue(nameof(ExpectedClientExecutableNames), ',', s => s).ToArray();

        GameRegistryInstallPath = constantsIni.GetStringValue(ConstantsSectionName, nameof(GameRegistryInstallPath), GameRegistryInstallPath);
        OpenFileDialogFilter = constantsIni.GetStringValue(ConstantsSectionName, nameof(OpenFileDialogFilter), OpenFileDialogFilter);

        EnableIniInclude = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(EnableIniInclude), EnableIniInclude);
        EnableIniInheritance = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(EnableIniInheritance), EnableIniInheritance);

        IntegerVariables = constantsIni.GetBooleanValue(ConstantsSectionName, nameof(IntegerVariables), IntegerVariables);

        MaxWaypoint = constantsIni.GetIntValue(ConstantsSectionName, nameof(MaxWaypoint), MaxWaypoint);

        MapPreviewMaxWidth = constantsIni.GetIntValue(ConstantsSectionName, nameof(MapPreviewMaxWidth), MapPreviewMaxWidth);
        MapPreviewMaxHeight = constantsIni.GetIntValue(ConstantsSectionName, nameof(MapPreviewMaxHeight), MapPreviewMaxHeight);

        MaxHouseTechLevel = constantsIni.GetIntValue(ConstantsSectionName, nameof(MaxHouseTechLevel), MaxHouseTechLevel);

        RulesIniPath = constantsIni.GetStringValue(FilePathsSectionName, "Rules", "INI/Rules.ini");
        FirestormIniPath = constantsIni.GetStringValue(FilePathsSectionName, "Firestorm", "INI/Enhance.ini");
        ArtIniPath = constantsIni.GetStringValue(FilePathsSectionName, "Art", "INI/Art.ini");
        FirestormArtIniPath = constantsIni.GetStringValue(FilePathsSectionName, "ArtFS", "INI/ArtE.ini");
        AIIniPath = constantsIni.GetStringValue(FilePathsSectionName, "AI", "INI/AI.ini");
        FirestormAIIniPath = constantsIni.GetStringValue(FilePathsSectionName, "AIFS", "INI/AIE.ini");
        TutorialIniPath = constantsIni.GetStringValue(FilePathsSectionName, "Tutorial", "INI/Tutorial.ini");
        ThemeIniPath = constantsIni.GetStringValue(FilePathsSectionName, "Theme", "INI/Theme.ini");
        EvaIniPath = constantsIni.GetStringValue(FilePathsSectionName, "EVA", "INI/Eva.ini");
        SoundIniPath = constantsIni.GetStringValue(FilePathsSectionName, "Sound", "INI/Sound01.ini");

        InitUIConstants();
    }

    public static void InitUIConstants()
    {
        IniFile uiConstantsIni = Helpers.ReadConfigINI("UI/UIConstants.ini");

        UITreeViewLineHeight = uiConstantsIni.GetIntValue("UI", nameof(UITreeViewLineHeight), UITreeViewLineHeight);
    }
}
