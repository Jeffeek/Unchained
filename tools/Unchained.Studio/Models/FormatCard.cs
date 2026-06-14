namespace Unchained.Studio.Models;

public sealed record FormatCard(
    string Name,
    string Tagline,
    string Icon,
    string Accent,
    bool Enabled,
    string Route,
    string[] Features
);
