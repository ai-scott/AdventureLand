class_name WorldMusic extends Node

# Per-world background-music driver. Attach to a child Node of each world
# scene; set the track name(s) in the Inspector. On _ready it asks
# MusicController to play the right thing for that world; on _exit_tree
# it stops everything so the next world's start_track/start_mix has a
# clean slate.
#
# Use one of two configurations in the Inspector:
#
#   • Single track — set base_track only, leave mid_track/high_track empty.
#     For interior worlds and the title screen (just Town).
#   • Layered mix — set all three. For worlds that need mood shifts
#     (Adventureland Happy/Stress/Danger).
#
# Track names are filenames without extension under res://assets/audio/music/
# — e.g. "town", "adventureland_happy". The path is resolved by
# MusicController, not this node, so renames there don't break scenes here.
#
# MusicController is idempotent on the same name(s), so repeatedly
# _ready-ing the same world (e.g. via FadeOverlay scene transitions) does
# not restart the track.

@export var base_track: String = ""
@export var mid_track: String = ""
@export var high_track: String = ""

func _ready() -> void:
	if base_track.is_empty():
		return

	# Mid + High both set = layered mix. Otherwise single track.
	if not mid_track.is_empty() and not high_track.is_empty():
		MusicController.start_mix(base_track, mid_track, high_track)
	else:
		MusicController.start_track(base_track)

func _exit_tree() -> void:
	# The next world's WorldMusic will _ready before this one is freed
	# in the scene-replace path, but FadeOverlay's transition shape is
	# "free old → instance new" sequentially, so stopping here is safe
	# and prevents leaks if the next scene has no WorldMusic at all
	# (e.g. game over).
	MusicController.stop_mix()
