using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WebViewUtils;


public record PdfOptions(
    Thickness Margin = default, 
    PdfPageBreakMode? PageBreak = null, 
    PdfImageSettings? Image = null, 
    bool? EnableLinks = null, 
    PdfPageOrientation? Orientation = null, 
    PdfUnits? Unit = null,
    PdfPageSize? Format = null,
    bool? Compress = null,
    PdfEncryption? Encryption = null,  
    bool? AllowTaint = null,
    string? BackgroundColor = null,
    bool? ForeignObjectRendering = null,
    int? ImageTimeout = null,
    string? IngoreElements = null,
    bool? Logging = null,
    string? Proxy = null,
    double? Scale = null,
    bool? UseCORS = null,
    double? Width = null,
    double? Height = null,
    double? X = null,
    double? Y = null,
    double? ScrollX = null,
    double? ScrollY = null,
    double? WindowWidth = null,
    double? WindowHeight = null
    )
{
    
}

[Flags]
public enum PdfPageBreakMode
{
    /// <summary>
    /// Automatically adds page-breaks to avoid splitting any elements across pages.
    /// </summary>
    [JsonStringEnumMemberName("avoid-all")]
    AvoidAll = 0,
    /// <summary>
    /// Adds page-breaks according to the CSS break-before, break-after, and break-inside properties. Only recognizes always/left/right for before/after, and avoid for inside.
    /// </summary>
    [JsonStringEnumMemberName("css")]
    Css = 1,
    /// <summary>
    /// Adds page-breaks after elements with class html2pdf__page-break. This feature may be removed in the future.
    /// </summary>
    [JsonStringEnumMemberName("legacy")]
    Legacy = 2,
}

public enum PdfImageType
{
    [JsonStringEnumMemberName("jpeg")]
    Jpeg,
    [JsonStringEnumMemberName("png")]
    Png
}

public record PdfImageSettings(PdfImageType Type = PdfImageType.Jpeg, float Quality = 0.95f)
{
    public static PdfImageSettings Default = new();

}

public enum PdfPageOrientation
{
    [JsonStringEnumMemberName("portrait")]
    Portrait,
    [JsonStringEnumMemberName("landscape")]
    Landscape,
}

public enum PdfUnits
{
    [JsonStringEnumMemberName("mm")]
    Mm,
    [JsonStringEnumMemberName("cm")]
    Cm,
    [JsonStringEnumMemberName("in")]
    In,
    [JsonStringEnumMemberName("px")]
    Px,
    [JsonStringEnumMemberName("pc")]
    Pc,
    [JsonStringEnumMemberName("em")]
    Em,
    [JsonStringEnumMemberName("ex")]
    Ex
}

public enum PdfPageSize
{
    [JsonStringEnumMemberName("a0")]
    A0,
    [JsonStringEnumMemberName("a1")]
    A1,
    [JsonStringEnumMemberName("a2")]
    A2,
    [JsonStringEnumMemberName("a3")]
    A3,
    [JsonStringEnumMemberName("a4")]
    A4,
    [JsonStringEnumMemberName("a5")]
    A5,
    [JsonStringEnumMemberName("a6")]
    A6,
    [JsonStringEnumMemberName("a7")]
    A7,
    [JsonStringEnumMemberName("a8")]
    A8,
    [JsonStringEnumMemberName("a9")]
    A9,
    [JsonStringEnumMemberName("a10")]
    A10,
    [JsonStringEnumMemberName("b0")]
    B0,
    [JsonStringEnumMemberName("b1")]
    B1,
    [JsonStringEnumMemberName("b2")]
    B2,
    [JsonStringEnumMemberName("b3")]
    B3,
    [JsonStringEnumMemberName("b4")]
    B4,
    [JsonStringEnumMemberName("b5")]
    B5,
    [JsonStringEnumMemberName("b6")]
    B6,
    [JsonStringEnumMemberName("b7")]
    B7,
    [JsonStringEnumMemberName("b8")]
    B8,
    [JsonStringEnumMemberName("b9")]
    B9,
    [JsonStringEnumMemberName("b10")]
    B10,
    [JsonStringEnumMemberName("c0")]
    C0,
    [JsonStringEnumMemberName("c1")]
    C1,
    [JsonStringEnumMemberName("c2")]
    C2,
    [JsonStringEnumMemberName("c3")]
    C3,
    [JsonStringEnumMemberName("c4")]
    C4,
    [JsonStringEnumMemberName("c5")]
    C5,
    [JsonStringEnumMemberName("c6")]
    C6,
    [JsonStringEnumMemberName("c7")]
    C7,
    [JsonStringEnumMemberName("c8")]
    C8,
    [JsonStringEnumMemberName("c9")]
    C9,
    [JsonStringEnumMemberName("c10")]
    C10,
    [JsonStringEnumMemberName("dl")]
    Dl,
    [JsonStringEnumMemberName("letter")]
    Letter,
    [JsonStringEnumMemberName("government-letter")]
    GovernmentLetter,
    [JsonStringEnumMemberName("legal")]
    Legal,
    [JsonStringEnumMemberName("junior-legal")]
    JuniorLegal,
    [JsonStringEnumMemberName("ledger")]
    Ledger,
    [JsonStringEnumMemberName("tabloid")]
    Tabloid,
    [JsonStringEnumMemberName("credit-card")]
    CreditCard
}

public enum PdfUserPermissions
{
    [JsonStringEnumMemberName("print")]
    Print,
    [JsonStringEnumMemberName("modify")]
    Modify,
    [JsonStringEnumMemberName("copy")]
    Copy,
    [JsonStringEnumMemberName("annot-forms")]
    AnnotForms
}

public record PdfEncryption(string OwnerPassword, string? UserPassword = default, PdfUserPermissions? UserPermissions = null)
{

}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PdfOptions))]
internal partial class PdfOptionsSourceGenerationContext : JsonSerializerContext { }
