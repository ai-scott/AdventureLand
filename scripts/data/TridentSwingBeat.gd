class_name TridentSwingBeat extends Resource

# One beat of the Magic Trident swing animation. The swing has 5 beats
# per direction (windup -> 3 strike beats -> recovery hold), each lasting
# 0.18 / 0.08 / 0.08 / 0.08 / 0.3 seconds respectively.
#
# Edit offset in the Inspector to nudge the trident sprite's position at
# each beat -- changes take effect on the next swing. Negative Y is up.
# Coordinates are pixels relative to the player's SpriteLayers origin
# (the same coordinate frame MSCA's farmer_1h_weapon uses), and they
# layer on top of the per-direction TridentSwingOffset bias.

@export var offset: Vector2 = Vector2.ZERO

# Sprite rotation in degrees, applied around the trident's position
# pivot. Positive = clockwise. The trident PNGs are already drawn at a
# default pose per frame, so 0 keeps the C3 art unchanged; non-zero
# rotates on top of that.
@export var rotation_deg: float = 0.0

# Mirror the trident sprite horizontally for this beat. Useful when MSCA
# flips the weapon to switch which hand carries it during a swing arc.
@export var flip_h: bool = false
