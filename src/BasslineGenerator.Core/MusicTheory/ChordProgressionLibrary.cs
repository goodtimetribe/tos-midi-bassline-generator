namespace BasslineGenerator.MusicTheory;

/// <summary>A chord root in semitones above the tonic, and its third in semitones.</summary>
public sealed record Chord(int RootOffset, int Third);

/// <summary>A named progression, with written chord symbols and their playable voicings.</summary>
public sealed record ChordProgression(string Name, IReadOnlyList<Chord> Chords)
{
    public string Id { get; init; } = Name;
    public string DisplayName { get; init; } = Name;
    public string Description { get; init; } = "";
    public string SuggestedUse { get; init; } = "";
    public string Mood { get; init; } = "";
    public bool IsMajor { get; init; }
    public IReadOnlyList<string> ChordSequence { get; init; } = Array.AsReadOnly(Name.Split(" - "));
}

public static class ChordProgressionLibrary
{
    // Keep the first six entries in their original order for CLI numeric selections.
    private static readonly IReadOnlyList<ChordProgression> Progressions = Array.AsReadOnly(new[]
    {
        Create("synthwave", "Synthwave", "i - VI - III - VII", "Retro and nostalgic.", "Synthwave and retro electronic music.", "Nostalgic", false),
        Create("minor-classic", "Minor Classic", "i - iv - v", "A simple minor-key foundation.", "Traditional minor-key songs.", "Somber", false),
        Create("pop-anthem", "Pop Anthem", "I - V - vi - IV", "Modern pop and uplifting electronic music.", "Pop anthems and uplifting electronic music.", "Uplifting", true),
        Create("dark-journey", "Dark Journey", "i - VII - VI - VII", "Driving and emotional.", "Dark electronic journeys.", "Driving", false, "i - bVII - bVI - bVII"),
        Create("relative-major", "Relative Major", "vi - IV - I - V", "A minor opening with a major-key lift.", "Pop and electronic songwriting.", "Hopeful", true),
        Create("minor-dramatic", "Minor Dramatic", "i - V - VI - IV", "Minor drama with a major dominant.", "Dramatic electronic music.", "Dramatic", false),
        Create("classic-pop", "Classic Pop", "I - vi - IV - V", "Timeless radio-friendly songwriting.", "Classic pop songs.", "Warm", true),
        Create("heroic", "Heroic", "I - IV - V - I", "Triumphant and confident.", "Heroic themes and celebrations.", "Triumphant", true),
        Create("adventure", "Adventure", "I - V - IV - V", "Forward motion and exploration.", "Adventure and exploration themes.", "Adventurous", true),
        Create("dream-pop", "Dream Pop", "I - III - IV - V", "Floating and optimistic.", "Dream pop and bright electronic music.", "Dreamy", true),
        Create("melancholy", "Melancholy", "i - III - VII - VI", "Reflective and introspective.", "Reflective synth and ambient music.", "Reflective", false),
        Create("future-retro", "Future Retro", "i - v - VI - VII", "Modern synthwave.", "Modern synthwave tracks.", "Nostalgic", false),
        Create("dreamwave", "Dreamwave", "i - VI - iv - VII", "Ambient and emotional.", "Dreamwave and ambient soundscapes.", "Emotional", false),
        Create("classic-rave", "Classic Rave", "i - VII - VI - VII", "90s rave energy.", "Classic rave tracks.", "Energetic", false),
        Create("trance-lift", "Trance Lift", "i - VI - III - VII", "Euphoric trance progression.", "Euphoric trance builds.", "Euphoric", false),
        Create("progressive", "Progressive", "i - iv - VI - VII", "Building tension over time.", "Progressive electronic builds.", "Tense", false),
        Create("techno-minimal", "Techno Minimal", "i - i - VII - i", "Hypnotic and repetitive.", "Minimal techno grooves.", "Hypnotic", false),
        Create("acid-driver", "Acid Driver", "i - iv - i - v", "Aggressive and focused.", "Driving acid tracks.", "Aggressive", false),
        Create("epic-fantasy", "Epic Fantasy", "i - VI - VII - i", "Fantasy and adventure.", "Fantasy scores and adventure games.", "Epic", false),
        Create("ancient-ruins", "Ancient Ruins", "i - bII - VII - i", "Mysterious and exotic.", "Ancient locations and exploration scenes.", "Mysterious", false),
        Create("boss-battle", "Boss Battle", "i - iv - VII - III", "Conflict and danger.", "Boss encounters and action scores.", "Dangerous", false),
        Create("machine-world", "Machine World", "i - III - iv - VII", "Industrial and mechanical.", "Industrial soundtracks and mechanical worlds.", "Mechanical", false),
        Create("finale", "Finale", "i - VI - iv - V", "Climactic resolution.", "Finales and climactic endings.", "Climactic", false)
    });

    public static IReadOnlyList<ChordProgression> All => Progressions;

    /// <summary>Looks up an ID, display name, or legacy case-sensitive chord sequence.</summary>
    public static ChordProgression? Find(string name) =>
        Progressions.FirstOrDefault(p => p.Id == name || p.DisplayName == name || p.Name == name
            || string.Join(" - ", p.ChordSequence) == name);

    public static ChordProgression Select(string? name, string scaleType, Random random)
    {
        if (name is not null)
            return Find(name) ?? throw new ArgumentException("Unknown progression.", nameof(name));
        var major = scaleType.Equals("Major", StringComparison.OrdinalIgnoreCase)
            || scaleType.Equals("Mixolydian", StringComparison.OrdinalIgnoreCase)
            || scaleType.Equals("Lydian", StringComparison.OrdinalIgnoreCase)
            || scaleType.Equals("Major Pentatonic", StringComparison.OrdinalIgnoreCase);
        var candidates = Progressions.Where(p => p.IsMajor == major).ToArray();
        return candidates[random.Next(candidates.Length)];
    }

    private static ChordProgression Create(string id, string displayName, string sequence, string description,
        string suggestedUse, string mood, bool isMajor, string? legacyName = null)
    {
        var symbols = sequence.Split(" - ");
        var chords = symbols.Select(symbol => symbol switch
        {
            "I" => new Chord(0, 4), "i" => new Chord(0, 3),
            "bII" => new Chord(1, 4), "III" => new Chord(isMajor ? 4 : 3, 4),
            "IV" => new Chord(5, 4), "iv" => new Chord(5, 3),
            "V" => new Chord(7, 4), "v" => new Chord(7, 3),
            "VI" => new Chord(8, 4), "vi" => new Chord(9, 3),
            "VII" => new Chord(10, 4),
            _ => throw new InvalidOperationException($"Unsupported chord symbol: {symbol}.")
        }).ToArray();
        return new ChordProgression(legacyName ?? sequence, Array.AsReadOnly(chords))
        {
            Id = id, DisplayName = displayName, Description = description, SuggestedUse = suggestedUse,
            Mood = mood, IsMajor = isMajor, ChordSequence = Array.AsReadOnly(symbols)
        };
    }
}
