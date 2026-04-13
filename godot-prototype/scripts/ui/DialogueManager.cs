using Godot;

namespace AdventureLandPrototype;

public partial class DialogueManager : CanvasLayer
{
    private PanelContainer _dialogueBox;
    private Label _nameLabel;
    private Label _textLabel;
    private Label _continueHint;

    private string[] _lines;
    private int _currentLine = 0;
    public bool IsActive { get; private set; } = false;

    private PlayerController _player;

    public override void _Ready()
    {
        _dialogueBox = GetNode<PanelContainer>("DialogueBox");
        _nameLabel = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/NameLabel");
        _textLabel = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/TextLabel");
        _continueHint = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/ContinueHint");

        _dialogueBox.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!IsActive) return;

        if (Input.IsActionJustPressed("dialogue_advance"))
        {
            AdvanceDialogue();
        }
    }

    public void StartDialogue(string speakerName, string[] lines)
    {
        _lines = lines;
        _currentLine = 0;
        IsActive = true;

        _nameLabel.Text = speakerName;
        _textLabel.Text = _lines[0];
        _dialogueBox.Visible = true;

        UpdateContinueHint();

        // Lock player movement
        _player ??= GetTree().Root.FindChild("Player", true, false) as PlayerController;
        if (_player != null)
        {
            _player.InputLocked = true;
        }
    }

    private void AdvanceDialogue()
    {
        _currentLine++;

        if (_currentLine >= _lines.Length)
        {
            EndDialogue();
            return;
        }

        _textLabel.Text = _lines[_currentLine];
        UpdateContinueHint();
    }

    private void EndDialogue()
    {
        IsActive = false;
        _dialogueBox.Visible = false;
        _currentLine = 0;
        _lines = null;

        // Unlock player movement
        if (_player != null)
        {
            _player.InputLocked = false;
        }
    }

    private void UpdateContinueHint()
    {
        _continueHint.Text = _currentLine < _lines.Length - 1
            ? "[E] Continue"
            : "[E] Close";
    }
}
