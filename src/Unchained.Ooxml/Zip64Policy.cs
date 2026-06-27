namespace Unchained.Ooxml;

/// <summary>Controls how ZIP64 extensions are used when serializing a package.</summary>
public enum Zip64Policy
{
    /// <summary>Use ZIP64 extensions only when the package exceeds the classic ZIP limits.</summary>
    IfNecessary,

    /// <summary>Never use ZIP64 extensions.</summary>
    Never,

    /// <summary>Always use ZIP64 extensions.</summary>
    Always
}
