using System.Collections.ObjectModel;
using System.Globalization;

namespace Velora.Text;

public sealed record InlineCommandFired(
    int CommandIndex,
    int RuneIndex,
    string Command,
    string Argument);

public sealed record TypewriterSnapshot(
    int VisibleRunes,
    double CharacterAccumulator,
    double WaitRemainingSeconds,
    double CharactersPerSecond,
    IReadOnlyList<int> FiredCommandIndexes,
    bool IsComplete);

/// <summary>Deterministic Rune-based reveal state driven only by caller-supplied deltas.</summary>
public sealed class TypewriterPlayer
{
    public const double DefaultCharactersPerSecond = 30;
    public const double MinimumInlineSpeed = 1;
    public const double MaximumInlineSpeed = 240;

    private RichTextDocument? _document;
    private bool[] _firedCommands = [];
    private double _baseCharactersPerSecond;
    private double _characterAccumulator;
    private double _waitRemainingSeconds;

    public TypewriterPlayer(double charactersPerSecond = DefaultCharactersPerSecond)
    {
        ValidateBaseSpeed(charactersPerSecond);
        _baseCharactersPerSecond = charactersPerSecond;
        CharactersPerSecond = charactersPerSecond;
    }

    public RichTextDocument? Document => _document;

    public int VisibleRunes { get; private set; }

    public double CharactersPerSecond { get; private set; }

    public double WaitRemainingSeconds => _waitRemainingSeconds;

    public bool IsActive => _document is not null;

    public bool IsComplete { get; private set; }

    public string VisibleText => _document?.GetVisibleText(VisibleRunes) ?? string.Empty;

    public void Start(RichTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        _firedCommands = new bool[document.Commands.Count];
        VisibleRunes = 0;
        CharactersPerSecond = _baseCharactersPerSecond;
        _characterAccumulator = 0;
        _waitRemainingSeconds = 0;
        IsComplete = false;
    }

    public IReadOnlyList<InlineCommandFired> Update(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);

        if (_document is null || IsComplete)
        {
            return [];
        }

        if (_waitRemainingSeconds > 0)
        {
            _waitRemainingSeconds = Math.Max(0, _waitRemainingSeconds - delta.TotalSeconds);
            return [];
        }

        _characterAccumulator += CharactersPerSecond * delta.TotalSeconds;
        List<InlineCommandFired> fired = [];
        while (_characterAccumulator >= 1 && VisibleRunes < _document.RuneCount)
        {
            _characterAccumulator -= 1;
            VisibleRunes++;
            FireReachedCommands(fired);
        }

        if (VisibleRunes >= _document.RuneCount)
        {
            IsComplete = true;
        }

        return new ReadOnlyCollection<InlineCommandFired>(fired);
    }

    public IReadOnlyList<InlineCommandFired> Skip()
    {
        if (_document is null || IsComplete)
        {
            return [];
        }

        VisibleRunes = _document.RuneCount;
        IsComplete = true;
        _waitRemainingSeconds = 0;
        List<InlineCommandFired> fired = [];
        for (int index = 0; index < _document.Commands.Count; index++)
        {
            if (_firedCommands[index])
            {
                continue;
            }

            _firedCommands[index] = true;
            InlineTextCommand command = _document.Commands[index];
            if (command.Command is not ("wait" or "speed"))
            {
                fired.Add(ToFired(index, command));
            }
        }

        return new ReadOnlyCollection<InlineCommandFired>(fired);
    }

    public TypewriterSnapshot CaptureState()
    {
        int[] fired = [
            .. _firedCommands
                .Select(static (value, index) => (value, index))
                .Where(static item => item.value)
                .Select(static item => item.index),
        ];
        return new TypewriterSnapshot(
            VisibleRunes,
            _characterAccumulator,
            _waitRemainingSeconds,
            CharactersPerSecond,
            Array.AsReadOnly(fired),
            IsComplete);
    }

    public void RestoreState(RichTextDocument document, TypewriterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.FiredCommandIndexes);
        if (snapshot.VisibleRunes < 0 || snapshot.VisibleRunes > document.RuneCount)
        {
            throw new ArgumentException("The visible Rune count is outside the document.", nameof(snapshot));
        }

        ValidateFiniteNonNegative(snapshot.CharacterAccumulator, nameof(snapshot));
        ValidateFiniteNonNegative(snapshot.WaitRemainingSeconds, nameof(snapshot));
        ValidateBaseSpeed(snapshot.CharactersPerSecond);
        if (snapshot.IsComplete && snapshot.VisibleRunes != document.RuneCount)
        {
            throw new ArgumentException("A complete typewriter snapshot must reveal the whole document.", nameof(snapshot));
        }

        bool[] fired = new bool[document.Commands.Count];
        foreach (int index in snapshot.FiredCommandIndexes)
        {
            if ((uint)index >= (uint)fired.Length || fired[index])
            {
                throw new ArgumentException("The fired command indexes are invalid or duplicated.", nameof(snapshot));
            }

            fired[index] = true;
        }

        _document = document;
        _firedCommands = fired;
        VisibleRunes = snapshot.VisibleRunes;
        _characterAccumulator = snapshot.CharacterAccumulator;
        _waitRemainingSeconds = snapshot.WaitRemainingSeconds;
        CharactersPerSecond = snapshot.CharactersPerSecond;
        IsComplete = snapshot.IsComplete;
    }

    public void SetBaseSpeed(double charactersPerSecond)
    {
        ValidateBaseSpeed(charactersPerSecond);
        _baseCharactersPerSecond = charactersPerSecond;
        if (_document is null || VisibleRunes == 0)
        {
            CharactersPerSecond = charactersPerSecond;
        }
    }

    private void FireReachedCommands(List<InlineCommandFired> fired)
    {
        if (_document is null)
        {
            return;
        }

        for (int index = 0; index < _document.Commands.Count; index++)
        {
            if (_firedCommands[index])
            {
                continue;
            }

            InlineTextCommand command = _document.Commands[index];
            if (command.RuneIndex > VisibleRunes)
            {
                break;
            }

            _firedCommands[index] = true;
            switch (command.Command)
            {
                case "wait":
                    _waitRemainingSeconds = ParseWait(command.Argument);
                    break;
                case "speed":
                    CharactersPerSecond = ParseSpeed(command.Argument);
                    break;
                default:
                    fired.Add(ToFired(index, command));
                    break;
            }
        }
    }

    private static double ParseWait(string argument) =>
        double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
        double.IsFinite(value)
            ? Math.Max(0, value)
            : 0;

    private double ParseSpeed(string argument) =>
        double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
        double.IsFinite(value)
            ? Math.Clamp(value, MinimumInlineSpeed, MaximumInlineSpeed)
            : _baseCharactersPerSecond;

    private static InlineCommandFired ToFired(int index, InlineTextCommand command) =>
        new(index, command.RuneIndex, command.Command, command.Argument);

    private static void ValidateBaseSpeed(double value)
    {
        if (!double.IsFinite(value) || value is <= 0 or > MaximumInlineSpeed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Typewriter speed must range from 1 through {MaximumInlineSpeed} characters per second.");
        }
    }

    private static void ValidateFiniteNonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentException("Typewriter state values must be finite and non-negative.", parameterName);
        }
    }
}
