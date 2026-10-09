using MaterialColorUtilities.Palettes;
using MaterialColorUtilities.Schemes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace RikkaHub.App.Theme;

/// <summary>
/// Material 3 theme engine — 1:1 port of the Android app's dynamic color system.
/// Uses Google's material-color-utilities (C# port) HCT color space, exactly like
/// the Android app (material3 module), to produce M3 color schemes from a seed.
/// Scheme roles are exposed as Windows UI Colors and pushed into XAML resources
/// (both M3 role names and WinUI accent overrides so native controls follow M3).
/// </summary>
public class M3Palette
{
    public Color Primary { get; init; }
    public Color OnPrimary { get; init; }
    public Color PrimaryContainer { get; init; }
    public Color OnPrimaryContainer { get; init; }
    public Color Secondary { get; init; }
    public Color OnSecondary { get; init; }
    public Color SecondaryContainer { get; init; }
    public Color OnSecondaryContainer { get; init; }
    public Color Tertiary { get; init; }
    public Color OnTertiary { get; init; }
    public Color TertiaryContainer { get; init; }
    public Color OnTertiaryContainer { get; init; }
    public Color Error { get; init; }
    public Color OnError { get; init; }
    public Color ErrorContainer { get; init; }
    public Color OnErrorContainer { get; init; }
    public Color Background { get; init; }
    public Color OnBackground { get; init; }
    public Color Surface { get; init; }
    public Color OnSurface { get; init; }
    public Color SurfaceVariant { get; init; }
    public Color OnSurfaceVariant { get; init; }
    public Color Outline { get; init; }
    public Color OutlineVariant { get; init; }
    public Color InverseSurface { get; init; }
    public Color InverseOnSurface { get; init; }
    public Color InversePrimary { get; init; }
    public Color SurfaceDim { get; init; }
    public Color SurfaceBright { get; init; }
    public Color SurfaceContainerLowest { get; init; }
    public Color SurfaceContainerLow { get; init; }
    public Color SurfaceContainer { get; init; }
    public Color SurfaceContainerHigh { get; init; }
    public Color SurfaceContainerHighest { get; init; }

    public bool IsDark { get; init; }

    private static Color C(uint argb)
    {
        byte a = (byte)(argb >> 24);
        byte r = (byte)(argb >> 16);
        byte g = (byte)(argb >> 8);
        byte b = (byte)argb;
        return Color.FromArgb(a, r, g, b);
    }

    public static M3Palette FromScheme(Scheme<uint> s, bool dark) => new()
    {
        IsDark = dark,
        Primary = C(s.Primary),
        OnPrimary = C(s.OnPrimary),
        PrimaryContainer = C(s.PrimaryContainer),
        OnPrimaryContainer = C(s.OnPrimaryContainer),
        Secondary = C(s.Secondary),
        OnSecondary = C(s.OnSecondary),
        SecondaryContainer = C(s.SecondaryContainer),
        OnSecondaryContainer = C(s.OnSecondaryContainer),
        Tertiary = C(s.Tertiary),
        OnTertiary = C(s.OnTertiary),
        TertiaryContainer = C(s.TertiaryContainer),
        OnTertiaryContainer = C(s.OnTertiaryContainer),
        Error = C(s.Error),
        OnError = C(s.OnError),
        ErrorContainer = C(s.ErrorContainer),
        OnErrorContainer = C(s.OnErrorContainer),
        Background = C(s.Background),
        OnBackground = C(s.OnBackground),
        Surface = C(s.Surface),
        OnSurface = C(s.OnSurface),
        SurfaceVariant = C(s.SurfaceVariant),
        OnSurfaceVariant = C(s.OnSurfaceVariant),
        Outline = C(s.Outline),
        OutlineVariant = C(s.OutlineVariant),
        InverseSurface = C(s.InverseSurface),
        InverseOnSurface = C(s.InverseOnSurface),
        InversePrimary = C(s.InversePrimary),
        SurfaceDim = C(s.SurfaceDim),
        SurfaceBright = C(s.SurfaceBright),
        SurfaceContainerLowest = C(s.SurfaceContainerLowest),
        SurfaceContainerLow = C(s.SurfaceContainerLow),
        SurfaceContainer = C(s.SurfaceContainer),
        SurfaceContainerHigh = C(s.SurfaceContainerHigh),
        SurfaceContainerHighest = C(s.SurfaceContainerHighest),
    };

    public static M3Palette FromSeed(int argb, bool dark)
    {
        var core = CorePalette.Of((uint)argb);
        var scheme = dark
            ? new DarkSchemeMapper().Map(core)
            : new LightSchemeMapper().Map(core);
        return FromScheme(scheme, dark);
    }
}

/// <summary>Applies an M3 palette into XAML resources + WinUI accent overrides.</summary>
public static class M3Theme
{
    public static M3Palette Current { get; private set; } = M3Palette.FromSeed(unchecked((int)0xFF6750A4), false);

    public static event Action<M3Palette>? Changed;

    public static void Apply(Application app, int seedArgb, bool dark)
    {
        Current = M3Palette.FromSeed(seedArgb, dark);
        var p = Current;

        // Merge into live Application.Resources (preserves XamlControlsResources and
        // any other merged dictionaries) — overrides win over theme defaults.
        var res = app.Resources;

        void Put(string key, Color c) => res[key] = new SolidColorBrush(c);

        // M3 roles
        Put("M3Primary", p.Primary);
        Put("M3OnPrimary", p.OnPrimary);
        Put("M3PrimaryContainer", p.PrimaryContainer);
        Put("M3OnPrimaryContainer", p.OnPrimaryContainer);
        Put("M3Secondary", p.Secondary);
        Put("M3OnSecondary", p.OnSecondary);
        Put("M3SecondaryContainer", p.SecondaryContainer);
        Put("M3OnSecondaryContainer", p.OnSecondaryContainer);
        Put("M3Tertiary", p.Tertiary);
        Put("M3OnTertiary", p.OnTertiary);
        Put("M3TertiaryContainer", p.TertiaryContainer);
        Put("M3OnTertiaryContainer", p.OnTertiaryContainer);
        Put("M3Error", p.Error);
        Put("M3OnError", p.OnError);
        Put("M3ErrorContainer", p.ErrorContainer);
        Put("M3OnErrorContainer", p.OnErrorContainer);
        Put("M3Background", p.Background);
        Put("M3OnBackground", p.OnBackground);
        Put("M3Surface", p.Surface);
        Put("M3OnSurface", p.OnSurface);
        Put("M3SurfaceVariant", p.SurfaceVariant);
        Put("M3OnSurfaceVariant", p.OnSurfaceVariant);
        Put("M3Outline", p.Outline);
        Put("M3OutlineVariant", p.OutlineVariant);
        Put("M3InverseSurface", p.InverseSurface);
        Put("M3InverseOnSurface", p.InverseOnSurface);
        Put("M3InversePrimary", p.InversePrimary);
        Put("M3SurfaceDim", p.SurfaceDim);
        Put("M3SurfaceBright", p.SurfaceBright);
        Put("M3SurfaceContainerLowest", p.SurfaceContainerLowest);
        Put("M3SurfaceContainerLow", p.SurfaceContainerLow);
        Put("M3SurfaceContainer", p.SurfaceContainer);
        Put("M3SurfaceContainerHigh", p.SurfaceContainerHigh);
        Put("M3SurfaceContainerHighest", p.SurfaceContainerHighest);

        // WinUI accent overrides — native controls follow Material You
        Put("AccentFillColorDefaultBrush", p.Primary);
        Put("AccentFillColorSecondaryBrush", p.Primary);
        Put("AccentFillColorTertiaryBrush", p.PrimaryContainer);
        Put("AccentFillColorDefaultBackgroundBrush", p.Primary);
        Put("AccentFillColorDisabledBrush", p.SurfaceVariant);
        Put("AccentFillColorInputActiveBrush", p.Primary);
        Put("AccentTextFillColorPrimaryBrush", p.Primary);
        Put("AccentTextFillColorSecondaryBrush", p.Primary);
        Put("AccentTextFillColorTertiaryBrush", p.Primary);
        Put("AccentTextFillColorDisabledBrush", p.Outline);
        Put("AccentLight2FillColorDefaultBrush", p.PrimaryContainer);
        Put("AccentLight3FillColorDefaultBrush", p.SecondaryContainer);
        Put("AccentBaseFillColorDefaultBrush", p.Primary);
        Put("AccentBaseFillColorSecondaryBrush", p.PrimaryContainer);
        Put("AccentBaseFillColorTertiaryBrush", p.SecondaryContainer);

        // Text
        Put("TextFillColorPrimaryBrush", p.OnSurface);
        Put("TextFillColorSecondaryBrush", p.OnSurfaceVariant);
        Put("TextFillColorTertiaryBrush", p.OnSurfaceVariant);
        Put("TextFillColorDisabledBrush", p.Outline);
        Put("TextFillColorInverseBrush", p.InverseOnSurface);
        Put("TextControlForeground", p.OnSurface);

        // Fills for cards/inputs
        Put("CardBackgroundFillColorDefaultBrush", p.SurfaceContainer);
        Put("CardBackgroundFillColorSecondaryBrush", p.SurfaceContainerHigh);
        Put("ControlFillColorDefaultBrush", p.SurfaceContainerHigh);
        Put("ControlFillColorSecondaryBrush", p.SurfaceContainer);
        Put("ControlFillColorTertiaryBrush", p.SurfaceContainer);
        Put("ControlFillColorDisabledBrush", p.SurfaceContainerLowest);
        Put("ControlStrokeColorDefaultBrush", p.OutlineVariant);
        Put("ControlStrokeColorSecondaryBrush", p.Outline);
        Put("SubtleFillColorSecondaryBrush", p.SurfaceContainerHigh);
        Put("SubtleFillColorTertiaryBrush", p.SurfaceContainerHighest);
        Put("DividerStrokeColorDefaultBrush", p.OutlineVariant);
        Put("SolidBackgroundFillColorBaseBrush", p.Surface);
        Put("SolidBackgroundFillColorSecondaryBrush", p.SurfaceContainerLow);
        Put("LayerFillColorDefaultBrush", p.SurfaceContainerLow);
        Put("NavigationViewContentBackground", p.Background);
        Put("NavigationViewDefaultPaneBackground", p.SurfaceContainerLow);
        Put("MenuFlyoutPresenterBackground", p.SurfaceContainer);
        Put("FlyoutPresenterBackground", p.SurfaceContainer);
        Put("AcrylicBackgroundFillColorDefaultBrush", Color.FromArgb(0xF2, p.SurfaceContainerHigh.R, p.SurfaceContainerHigh.G, p.SurfaceContainerHigh.B));
        Put("AppBarBackground", p.Surface);

        res["AcrylicBackgroundFillColorDefaultBrush"] = new SolidColorBrush(Color.FromArgb(0xF2, p.SurfaceContainerHigh.R, p.SurfaceContainerHigh.G, p.SurfaceContainerHigh.B));
        Put("AppBarBackground", p.Surface);

        // refresh control styling for the requested theme
        if (app is App a)
        {
            try { a.RequestedTheme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light; }
            catch { /* best effort; resource overrides above already recolor controls */ }
        }

        Changed?.Invoke(Current);
    }
}
