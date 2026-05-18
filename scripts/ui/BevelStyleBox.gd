class_name BevelStyleBox extends StyleBox

# Pixel-art StyleBox with an ink border + asymmetric inset bevel
# (top/left = highlight, bottom/right = shadow) and the classic
# "corners don't connect" gap at each of the four corners.
#
# Drawing layers (back to front):
#   1. Body fill (innermost rect)
#   2. Bevel highlight strips (top + left)
#   3. Bevel shadow strips (bottom + right)
#   4. Border ink -- 4 separate strips with corner_gap-sized gaps so
#      the four outermost corner pixels stay transparent
#
# Set bevel_width = 0 for a flat (no-bevel) bordered shape; set
# corner_gap = 0 for fully connected borders.

@export var fill: Color = Color.WHITE
@export var bevel_hi: Color = Color.WHITE
@export var bevel_lo: Color = Color.BLACK
@export var border: Color = Color.BLACK
@export var border_width: int = 3
@export var bevel_width: int = 3

# Inner content padding. Inherited content_margin_* properties are set
# by UiFrames.make_bevel_panel based on this so layout containers know
# the safe drawing area.
@export var padding: int = 16

# Pixels of transparent gap at each of the four corners -- classic
# NES/SNES "corners don't connect" pixel-art look. Defaults to 3 so
# the gap fully cuts the typical 3px ink border. Smaller styleboxes
# (like the 1px-bordered kbd chip) override this.
@export var corner_gap: int = 3


func _draw(to_canvas_item: RID, rect: Rect2) -> void:
	var g := corner_gap
	var b := border_width
	var v := bevel_width
	var pos := rect.position
	var size := rect.size

	# Body fill -- innermost rect, inset past the corners by
	# (border + bevel) so it never touches the corner-gap zone.
	_draw_rect(to_canvas_item, pos.x + b + v, pos.y + b + v,
			size.x - 2 * (b + v), size.y - 2 * (b + v), fill)

	if v > 0:
		# Bevel highlight (top + left strips). Inset by corner_gap on
		# their long axis so the strip ends don't reach the outer
		# corners -- keeps the corner pixels transparent.
		_draw_rect(to_canvas_item, pos.x + g, pos.y + b, size.x - 2 * g, v, bevel_hi)  # top
		_draw_rect(to_canvas_item, pos.x + b, pos.y + g, v, size.y - 2 * g, bevel_hi)  # left
		# Bevel shadow (bottom + right strips). Painted after the
		# highlight, so the top-right and bottom-left intersections
		# resolve to shadow -- matches typical pixel-art panels.
		_draw_rect(to_canvas_item, pos.x + g, pos.y + size.y - b - v, size.x - 2 * g, v, bevel_lo)  # bottom
		_draw_rect(to_canvas_item, pos.x + size.x - b - v, pos.y + g, v, size.y - 2 * g, bevel_lo)  # right

	# Border ink -- 4 separate strips with corner_gap-sized gaps at
	# each corner, leaving the four corner pixels transparent.
	_draw_rect(to_canvas_item, pos.x + g, pos.y, size.x - 2 * g, b, border)                       # top
	_draw_rect(to_canvas_item, pos.x + g, pos.y + size.y - b, size.x - 2 * g, b, border)          # bottom
	_draw_rect(to_canvas_item, pos.x, pos.y + g, b, size.y - 2 * g, border)                       # left
	_draw_rect(to_canvas_item, pos.x + size.x - b, pos.y + g, b, size.y - 2 * g, border)          # right


static func _draw_rect(canvas: RID, x: float, y: float, w: float, h: float, c: Color) -> void:
	if w <= 0 or h <= 0:
		return
	RenderingServer.canvas_item_add_rect(canvas, Rect2(x, y, w, h), c)
