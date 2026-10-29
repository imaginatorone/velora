namespace Velora.Script.Semantics;

/// <summary>
/// Describes the language role of a source reference.
/// </summary>
public enum SemanticReferenceKind
{
    VariableRead,
    VariableInterpolation,
    Jump,
    ScreenActionJump,
    DialogueSpeaker,
    EmotionCharacter,
    ShowScreen,
    HideScreen,
}
