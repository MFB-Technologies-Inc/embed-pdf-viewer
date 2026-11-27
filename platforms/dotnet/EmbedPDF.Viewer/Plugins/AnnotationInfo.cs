using System.Text.Json;
using System.Text.Json.Nodes;
using GA = uniffi.epdf_plugin_annotation;

namespace EmbedPDF;

/// <summary>
/// An annotation as reads hand it out: the engine's record with its
/// timestamps, subtype and the common subtypes' members typed. Every member
/// the engine reads is still in <see cref="FieldsJson"/>; <see cref="Details"/>
/// types those of ink, highlight, note, the text markups, shapes, lines,
/// polygons, polylines, free text, carets, stamps, links, file attachments
/// and redactions, and is <see langword="null"/> for any other subtype or a
/// record whose members don't have the shapes the engine writes.
/// </summary>
public sealed record AnnotationInfo
{
    internal AnnotationInfo(GA.AnnotationRecord record)
        : this(
            Ffi.ToPublic(record.AnnotationRef), Ffi.ToPublic(record.Page), new AnnotationSubtype(record.Subtype), Ffi.ToPublic(record.Rect),
            record.Contents, record.Author, record.CreatedAt, record.ModifiedAt, record.GroupId,
            record.InReplyTo is { } parent ? Ffi.ToPublic(parent) : null, record.CanUpdate, record.CanDelete, record.FieldsJson)
    {
        Accent = Ffi.ToPublic(record.Accent);
    }

    internal AnnotationInfo(
        AnnotationRef annotationRef, PageRef page, AnnotationSubtype subtype, PdfRect rect, string? contents, string? author,
        string? createdAt, string? modifiedAt, string? groupId, AnnotationRef? inReplyTo, bool canUpdate, bool canDelete, string fieldsJson)
    {
        AnnotationRef = annotationRef;
        Page = page;
        Subtype = subtype;
        Rect = rect;
        Contents = contents;
        Author = author;
        RawCreatedAt = createdAt;
        CreatedAt = createdAt is null ? null : Instant(createdAt);
        RawModifiedAt = modifiedAt;
        ModifiedAt = modifiedAt is null ? null : Instant(modifiedAt);
        GroupId = groupId;
        InReplyTo = inReplyTo;
        CanUpdate = canUpdate;
        CanDelete = canDelete;
        FieldsJson = fieldsJson;
        var fields = Parse(fieldsJson);
        Details = fields is null ? null : AnnotationDetails.Read(subtype, fields);
        BlendMode = fields is null ? null : Try(() => fields.Enum<AnnotationBlendMode>("blendMode"));
    }

    /// <summary>The annotation.</summary>
    public AnnotationRef AnnotationRef { get; }

    /// <summary>Its page.</summary>
    public PageRef Page { get; }

    /// <summary>Its subtype; one the engine doesn't name keeps its text.</summary>
    public AnnotationSubtype Subtype { get; }

    /// <summary><c>/Rect</c>, page space.</summary>
    public PdfRect Rect { get; }

    /// <summary><c>/Contents</c>.</summary>
    public string? Contents { get; }

    /// <summary><c>/T</c>, the author.</summary>
    public string? Author { get; }

    /// <summary>When it was created, or <see langword="null"/> when <see cref="RawCreatedAt"/> is absent or doesn't read as a date.</summary>
    public DateTimeOffset? CreatedAt { get; }

    /// <summary>The creation date as the engine wrote it.</summary>
    public string? RawCreatedAt { get; }

    /// <summary>When it last changed, or <see langword="null"/> when <see cref="RawModifiedAt"/> is absent or doesn't read as a date.</summary>
    public DateTimeOffset? ModifiedAt { get; }

    /// <summary>The modification date as the engine wrote it.</summary>
    public string? RawModifiedAt { get; }

    /// <summary>The group it belongs to.</summary>
    public string? GroupId { get; }

    /// <summary>The annotation it replies to.</summary>
    public AnnotationRef? InReplyTo { get; }

    /// <summary>Whether this session may change it.</summary>
    public bool CanUpdate { get; }

    /// <summary>Whether this session may delete it.</summary>
    public bool CanDelete { get; }

    /// <summary>
    /// A lent record's accent from its lender: a form widget's group colour
    /// while the annotation plugin edits it, not part of the record itself.
    /// <see langword="null"/> for every other record.
    /// </summary>
    public RecordAccent? Accent { get; }

    /// <summary>How its appearance blends with the page, when the record names a mode a draft can write.</summary>
    public AnnotationBlendMode? BlendMode { get; }

    /// <summary>Every member the engine reads, as JSON.</summary>
    public string FieldsJson { get; }

    /// <summary>The subtype's members typed, or <see langword="null"/>.</summary>
    public AnnotationDetails? Details { get; }

    /// <summary>The ink members, for an ink annotation.</summary>
    public AnnotationInkDetails? Ink => Details as AnnotationInkDetails;

    /// <summary>The highlight members, for a highlight.</summary>
    public AnnotationHighlightDetails? Highlight => Details as AnnotationHighlightDetails;

    /// <summary>The note members, for a <c>text</c> annotation.</summary>
    public AnnotationNoteDetails? Note => Details as AnnotationNoteDetails;

    /// <summary>The members of an underline.</summary>
    public AnnotationTextMarkupDetails? Underline => Subtype == AnnotationSubtype.Underline ? Details as AnnotationTextMarkupDetails : null;

    /// <summary>The members of a squiggly underline.</summary>
    public AnnotationTextMarkupDetails? Squiggly => Subtype == AnnotationSubtype.Squiggly ? Details as AnnotationTextMarkupDetails : null;

    /// <summary>The members of a strikeout.</summary>
    public AnnotationStrikeoutDetails? Strikeout => Details as AnnotationStrikeoutDetails;

    /// <summary>The members of a square.</summary>
    public AnnotationShapeDetails? Square => Subtype == AnnotationSubtype.Square ? Details as AnnotationShapeDetails : null;

    /// <summary>The members of a circle.</summary>
    public AnnotationShapeDetails? Circle => Subtype == AnnotationSubtype.Circle ? Details as AnnotationShapeDetails : null;

    /// <summary>The members of a line.</summary>
    public AnnotationLineDetails? Line => Details as AnnotationLineDetails;

    /// <summary>The members of a polygon.</summary>
    public AnnotationPolygonDetails? Polygon => Details as AnnotationPolygonDetails;

    /// <summary>The members of a polyline.</summary>
    public AnnotationPolylineDetails? Polyline => Details as AnnotationPolylineDetails;

    /// <summary>The members of a free text.</summary>
    public AnnotationFreeTextDetails? FreeText => Details as AnnotationFreeTextDetails;

    /// <summary>The members of a caret.</summary>
    public AnnotationCaretDetails? Caret => Details as AnnotationCaretDetails;

    /// <summary>The members of a stamp.</summary>
    public AnnotationStampDetails? Stamp => Details as AnnotationStampDetails;

    /// <summary>The members of a link.</summary>
    public AnnotationLinkDetails? Link => Details as AnnotationLinkDetails;

    /// <summary>The members of a file attachment.</summary>
    public AnnotationFileAttachmentDetails? FileAttachment => Details as AnnotationFileAttachmentDetails;

    /// <summary>The members of a redaction mark.</summary>
    public AnnotationRedactionDetails? Redaction => Details as AnnotationRedactionDetails;

    internal GA.AnnotationRecord ToRecord() => new(
        Ffi.ToFfi(AnnotationRef), Ffi.ToFfi(Page), Subtype.Value, Ffi.ToFfi(Rect), Contents, Author, RawCreatedAt, RawModifiedAt, GroupId,
        InReplyTo is { } parent ? Ffi.ToFfi(parent) : null, CanUpdate, CanDelete,
        Accent is { } accent ? new uniffi.epdf_ffi.RecordAccent(accent.Color, accent.Strength) : null, FieldsJson);

    static FieldReader? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject fields ? new FieldReader(fields) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static T? Try<T>(Func<T?> read) where T : struct
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is FieldBagException or JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// The instant of an engine timestamp: <c>YYYY-MM-DDTHH:MM[:SS[.fff]]</c>
    /// with <c>Z</c>, an offset (<c>+05:30</c> or <c>+0530</c>) or none (UTC).
    /// Anything else, an impossible date, or one outside what
    /// <see cref="DateTimeOffset"/> holds, is <see langword="null"/>.
    /// </summary>
    internal static DateTimeOffset? Instant(string text)
    {
        var index = 0;
        int? Number(int length)
        {
            if (index + length > text.Length) return null;
            var value = 0;
            for (var i = index; i < index + length; i++)
            {
                if (text[i] is < '0' or > '9') return null;
                value = value * 10 + (text[i] - '0');
            }
            index += length;
            return value;
        }
        bool Skip(char c)
        {
            if (index >= text.Length || text[index] != c) return false;
            index++;
            return true;
        }
        if (Number(4) is not { } year || !Skip('-') || Number(2) is not { } month || !Skip('-') || Number(2) is not { } day || !Skip('T')
            || Number(2) is not { } hour || !Skip(':') || Number(2) is not { } minute)
        {
            return null;
        }
        var second = 0;
        long fractionTicks = 0;
        if (Skip(':'))
        {
            if (Number(2) is not { } whole) return null;
            second = whole;
            if (Skip('.'))
            {
                var start = index;
                while (index < text.Length && text[index] is >= '0' and <= '9') index++;
                if (index == start) return null;
                fractionTicks = (long)Math.Round(double.Parse("0." + text[start..index], System.Globalization.CultureInfo.InvariantCulture) * TimeSpan.TicksPerSecond);
            }
        }
        var offsetMinutes = 0;
        if (Skip('Z'))
        {
        }
        else if (index < text.Length && text[index] is '+' or '-')
        {
            var sign = text[index] == '-' ? -1 : 1;
            index++;
            if (Number(2) is not { } hours) return null;
            Skip(':');
            if (Number(2) is not { } minutes || hours > 23 || minutes > 59) return null;
            offsetMinutes = sign * (hours * 60 + minutes);
        }
        if (index != text.Length || month is < 1 or > 12 || day < 1 || day > DaysIn(month, year) || hour > 23 || minute > 59 || second > 59) return null;
        var days = DaysFromCivil(year, month, day);
        var seconds = ((days * 24 + hour) * 60 + minute - offsetMinutes) * 60 + second;
        // The engine writes years DateTimeOffset can't hold (0000, or 0001 and
        // 9999 shifted past the edge by an offset); those keep only their text.
        var ticks = seconds * TimeSpan.TicksPerSecond + fractionTicks;
        var earliest = (DateTimeOffset.MinValue - DateTimeOffset.UnixEpoch).Ticks;
        var latest = (DateTimeOffset.MaxValue - DateTimeOffset.UnixEpoch).Ticks;
        return ticks < earliest || ticks > latest ? null : DateTimeOffset.UnixEpoch.AddTicks(ticks);
    }

    static int DaysIn(int month, int year) => month switch
    {
        2 => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        _ => 31,
    };

    static long DaysFromCivil(long year, long month, long day)
    {
        var y = month <= 2 ? year - 1 : year;
        var era = (y >= 0 ? y : y - 399) / 400;
        var yearOfEra = y - era * 400;
        var dayOfYear = (153 * (month > 2 ? month - 3 : month + 9) + 2) / 5 + day - 1;
        var dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear;
        return era * 146_097 + dayOfEra - 719_468;
    }
}
