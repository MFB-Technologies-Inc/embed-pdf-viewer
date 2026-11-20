using System.Text.Json.Nodes;

namespace EmbedPDF;

/// <summary>What an annotation replies to: <c>/IRT</c> and <c>/RT</c>.</summary>
/// <param name="To">The annotation it answers or joins.</param>
public sealed record AnnotationReply(AnnotationRef To)
{
    /// <summary>A reply or a group member; left out is a reply.</summary>
    public AnnotationReplyType? Type { get; init; }

    internal JsonNode ToNode(string key)
    {
        var node = new JsonObject { ["to"] = AnnotationWire.Annotation(To, key) };
        if (Type is { } type) node["type"] = Wire<AnnotationReplyType>.Name(type, key);
        return node;
    }

    internal static AnnotationReply Read(JsonNode node, string key)
    {
        if (node is not JsonObject o || o["to"] is not { } to) throw new FieldBagException($"{key} is not a reply");
        return new(AnnotationWire.ReadAnnotation(to, key))
        {
            Type = o["type"] is { } type ? AnnotationWire.ReadEnum<AnnotationReplyType>(type, key) : null,
        };
    }
}

/// <summary>
/// The fields every annotation kind declares besides <c>contents</c> and
/// <c>blendMode</c>. Every typed draft and patch carries them as
/// <c>Common</c>, written flat beside the kind's own members. A member left
/// <see langword="null"/> is left out.
/// </summary>
public sealed record AnnotationCommonFields
{
    /// <summary><c>/NM</c>, the annotation's name. A patch never writes it; a draft sets it.</summary>
    public JsonNullable<string>? Nm { get; init; }

    /// <summary><c>/Subj</c>.</summary>
    public JsonNullable<string>? Subject { get; init; }

    /// <summary>What it replies to. <see cref="JsonNullable{T}.Null"/> makes it a thread root.</summary>
    public JsonNullable<AnnotationReply>? Reply { get; init; }

    /// <summary>The group it belongs to.</summary>
    public JsonNullable<string>? GroupId { get; init; }

    /// <summary>The <c>/F</c> invisible flag.</summary>
    public bool? Invisible { get; init; }

    /// <summary>The <c>/F</c> hidden flag.</summary>
    public bool? Hidden { get; init; }

    /// <summary>The <c>/F</c> print flag.</summary>
    public bool? Print { get; init; }

    /// <summary>The <c>/F</c> no-zoom flag.</summary>
    public bool? NoZoom { get; init; }

    /// <summary>The <c>/F</c> no-rotate flag.</summary>
    public bool? NoRotate { get; init; }

    /// <summary>The <c>/F</c> no-view flag.</summary>
    public bool? NoView { get; init; }

    /// <summary>The <c>/F</c> read-only flag.</summary>
    public bool? ReadOnly { get; init; }

    /// <summary>The <c>/F</c> locked flag.</summary>
    public bool? Locked { get; init; }

    /// <summary>The <c>/F</c> toggle-no-view flag.</summary>
    public bool? ToggleNoView { get; init; }

    /// <summary>The <c>/F</c> locked-contents flag.</summary>
    public bool? LockedContents { get; init; }

    internal void Write(FieldBag bag, bool patch)
    {
        if (!patch) bag.Put("nm", Nm);
        bag.Put("subject", Subject);
        bag.Put("reply", Reply, r => r.ToNode("reply"));
        bag.Put("groupId", GroupId);
        bag.Put("invisible", Invisible);
        bag.Put("hidden", Hidden);
        bag.Put("print", Print);
        bag.Put("noZoom", NoZoom);
        bag.Put("noRotate", NoRotate);
        bag.Put("noView", NoView);
        bag.Put("readOnly", ReadOnly);
        bag.Put("locked", Locked);
        bag.Put("toggleNoView", ToggleNoView);
        bag.Put("lockedContents", LockedContents);
    }

    internal static AnnotationCommonFields Read(FieldReader r) => new()
    {
        Nm = r.NullableString("nm"),
        Subject = r.NullableString("subject"),
        Reply = r.Nullable("reply", AnnotationReply.Read),
        GroupId = r.NullableString("groupId"),
        Invisible = r.Bool("invisible"),
        Hidden = r.Bool("hidden"),
        Print = r.Bool("print"),
        NoZoom = r.Bool("noZoom"),
        NoRotate = r.Bool("noRotate"),
        NoView = r.Bool("noView"),
        ReadOnly = r.Bool("readOnly"),
        Locked = r.Bool("locked"),
        ToggleNoView = r.Bool("toggleNoView"),
        LockedContents = r.Bool("lockedContents"),
    };
}
