/*
 * Tiled auto-bake extension for Adventure Land's Godot prototype.
 *
 * Hooks Tiled's `assetSaved` signal. When any .tmx file is saved, runs the
 * project's `tools/bake_all.py` script, which regenerates trigger .tres files
 * (and eventually tile CSVs) for every TMX in the project.
 *
 * INSTALL
 *   macOS:
 *     mkdir -p "$HOME/Library/Preferences/Tiled/extensions"
 *     ln -sf "$PWD/tools/tiled-extensions/autobake.js" \
 *            "$HOME/Library/Preferences/Tiled/extensions/autobake.js"
 *
 *   (Run from the godot-prototype directory.)
 *
 *   Restart Tiled. You should see "AutoBake: armed." in View → Console.
 *
 * HOW IT WORKS
 *   The extension finds the Godot project root by walking up from the TMX's
 *   directory looking for `project.godot`. It then shells out to:
 *       python3 tools/bake_all.py --tmx <name>
 *   and logs the result to Tiled's console. On error, also surfaces a warning
 *   so the user sees something went wrong.
 *
 * SAFETY
 *   This is a local dev convenience. It runs arbitrary python from your
 *   working tree on every TMX save. If you don't trust the `tools/` folder,
 *   do not install.
 */

function findProjectRoot(filePath) {
    // Walk up the directory tree until we find `project.godot`.
    // filePath looks like: /.../godot-prototype/assets/tiles/tilemaps/World_00.tmx
    var dir = filePath;
    // Strip filename
    var lastSlash = dir.lastIndexOf("/");
    if (lastSlash > 0) dir = dir.substring(0, lastSlash);

    for (var i = 0; i < 10; i++) {
        var candidate = dir + "/project.godot";
        if (File.exists(candidate)) return dir;
        var parent = dir.lastIndexOf("/");
        if (parent <= 0) break;
        dir = dir.substring(0, parent);
    }
    return null;
}

function basename(path) {
    var i = path.lastIndexOf("/");
    return i >= 0 ? path.substring(i + 1) : path;
}

tiled.assetSaved.connect(function(asset) {
    if (!asset.fileName || !asset.fileName.toLowerCase().endsWith(".tmx")) return;

    var projectRoot = findProjectRoot(asset.fileName);
    if (!projectRoot) {
        tiled.warn("AutoBake: could not locate project.godot relative to " + asset.fileName);
        return;
    }

    var tmxName = basename(asset.fileName);
    tiled.log("AutoBake: rebaking " + tmxName + "...");

    var process = new Process();
    process.workingDirectory = projectRoot;
    // Exit code -> 0 on success.
    var exitCode = process.exec("/usr/bin/python3", ["tools/bake_all.py", "--tmx", tmxName]);
    var out = process.readStdOut() || "";
    var err = process.readStdErr() || "";

    if (exitCode === 0) {
        tiled.log("AutoBake: " + tmxName + " OK");
        if (out.trim()) tiled.log(out.trim());
    } else {
        tiled.warn("AutoBake: " + tmxName + " FAILED (exit " + exitCode + ")");
        if (err.trim()) tiled.warn(err.trim());
        if (out.trim()) tiled.log(out.trim());
    }
});

tiled.log("AutoBake: armed. TMX saves will trigger tools/bake_all.py.");
