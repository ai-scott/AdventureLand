using Godot;
using System.Collections.Generic;
using System.Linq;

namespace AdventureLandPrototype;

/// <summary>
/// Folder-based SpriteFrames builder for enemies whose art ships as individual
/// PNG frames (one file per frame) rather than a packed spritesheet.
///
/// At _Ready, scans FramesFolder for *.png files, groups them by animation name,
/// sorts by frame index, and builds a SpriteFrames resource on the sibling
/// AnimatedSprite2D. Play() works identically to EnemySheetAnimator — the
/// EnemyController doesn't need to know which strategy is in use.
///
/// First user: Ooze (assets/sprites/enemies/ooze/).
/// </summary>
public partial class EnemyFolderAnimator : EnemyAnimatorBase
{
	[Export] public string FramesFolder = "";
	[Export] public float DefaultFps = 8;
	[Export] public bool DefaultLoop = true;

	/// <summary>
	/// Optional per-animation FPS overrides. Key = animation name (e.g. "hurt_down"),
	/// value = FPS. Animations not listed here use DefaultFps.
	/// </summary>
	[Export] public Godot.Collections.Dictionary<string, float> PerAnimationFps = new();

	private AnimatedSprite2D _sprite;
	private string _currentAnim = "";

	public override void _Ready()
	{
		_sprite = GetParent().GetNodeOrNull<AnimatedSprite2D>("Sprite2D");
		if (_sprite == null)
		{
			GD.PrintErr("[EnemyFolderAnimator] Parent must have an AnimatedSprite2D child named 'Sprite2D'");
			return;
		}

		if (string.IsNullOrEmpty(FramesFolder))
		{
			GD.PrintErr("[EnemyFolderAnimator] FramesFolder not set in Inspector");
			return;
		}

		BuildFrames();
	}

	private void BuildFrames()
	{
		var dir = DirAccess.Open(FramesFolder);
		if (dir == null)
		{
			GD.PrintErr($"[EnemyFolderAnimator] Cannot open folder: {FramesFolder}");
			return;
		}

		// Collect (animName, frameIndex, filePath) tuples.
		var groups = new Dictionary<string, List<(int frameIndex, string path)>>();

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (fileName != "")
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".png") && !fileName.EndsWith(".import"))
			{
				var parsed = ParseFrameName(fileName);
				if (parsed != null)
				{
					var (animName, frameIndex) = parsed.Value;
					if (!groups.ContainsKey(animName))
						groups[animName] = new List<(int, string)>();
					groups[animName].Add((frameIndex, FramesFolder.TrimEnd('/') + "/" + fileName));
				}
				else
				{
					GD.PushWarning($"[EnemyFolderAnimator] Skipping unrecognized file: {fileName}");
				}
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		if (groups.Count == 0)
		{
			GD.PrintErr($"[EnemyFolderAnimator] No animation frames found in {FramesFolder}");
			return;
		}

		// Build SpriteFrames.
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		foreach (var (animName, frameList) in groups)
		{
			var sorted = frameList.OrderBy(f => f.frameIndex).ToList();

			float fps = PerAnimationFps.ContainsKey(animName) ? PerAnimationFps[animName] : DefaultFps;

			frames.AddAnimation(animName);
			frames.SetAnimationSpeed(animName, fps);
			frames.SetAnimationLoop(animName, DefaultLoop);

			foreach (var (_, path) in sorted)
			{
				var tex = GD.Load<Texture2D>(path);
				if (tex == null)
				{
					GD.PushWarning($"[EnemyFolderAnimator] Failed to load texture: {path}");
					continue;
				}
				frames.AddFrame(animName, tex);
			}
		}

		_sprite.SpriteFrames = frames;
		GD.Print($"[EnemyFolderAnimator] Built {groups.Count} animations from {FramesFolder}");
	}

	// TODO: confirm pattern — currently matches real Ooze filenames like
	//   en_ooze_mask-idle_down-000.png → animName="idle_down", frameIndex=0
	//   en_ooze_mask-hop_left-002.png  → animName="hop_left",  frameIndex=2
	// Pattern: {prefix}-{animName}-{frameIndex:NNN}.png
	// If other enemies use a different naming convention, generalize this method.
	private static (string animName, int frameIndex)? ParseFrameName(string fileName)
	{
		// Strip .png extension.
		var name = fileName.Replace(".png", "");

		// Split on '-' — expect at least 3 segments: prefix parts, animName, frameIndex.
		// Real example: "en_ooze_mask-idle_down-000"
		//   segment[-1] = "000"  (frame index)
		//   segment[-2] = "idle_down"  (animation name)
		//   segment[0..-3] = prefix (ignored)
		var parts = name.Split('-');
		if (parts.Length < 3) return null;

		var frameStr = parts[^1];
		var animName = parts[^2];

		if (!int.TryParse(frameStr, out int frameIndex)) return null;
		if (string.IsNullOrEmpty(animName)) return null;

		return (animName, frameIndex);
	}

	public override void Play(string animName)
	{
		if (_sprite == null || _sprite.SpriteFrames == null) return;
		if (animName == _currentAnim && _sprite.IsPlaying()) return;

		if (!_sprite.SpriteFrames.HasAnimation(animName))
		{
			// Crab et al. don't ship every cardinal direction (no walk_down on a
			// sideways-walker). Walk back along progressively looser matches before
			// giving up: same prefix any direction → directionless prefix → idle*.
			var fallback = ResolveFallback(animName);
			if (fallback == null) return;
			animName = fallback;
		}

		_currentAnim = animName;
		_sprite.Play(animName);
	}

	private string ResolveFallback(string requested)
	{
		var frames = _sprite.SpriteFrames;
		int us = requested.IndexOf('_');
		string prefix = us >= 0 ? requested.Substring(0, us) : requested;

		// 1. Same prefix, any direction (walk_left, walk_right, walk_up, walk_down).
		foreach (var dir in new[] { "right", "left", "up", "down" })
		{
			var candidate = $"{prefix}_{dir}";
			if (candidate != requested && frames.HasAnimation(candidate)) return candidate;
		}
		// 2. Prefix without any direction (e.g. "hurt", "idle").
		if (frames.HasAnimation(prefix)) return prefix;
		// 3. Idle in any flavor.
		foreach (var idle in new[] { "idle_down", "idle", "idle_right", "idle_left", "idle_up" })
		{
			if (frames.HasAnimation(idle)) return idle;
		}
		// 4. First available animation — better something than nothing.
		var anims = frames.GetAnimationNames();
		return anims.Length > 0 ? anims[0] : null;
	}

	public override void Stop()
	{
		if (_sprite != null) _sprite.Stop();
	}
}
