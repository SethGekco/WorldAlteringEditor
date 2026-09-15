# Stride-2048 fork notes

Goal: raise the World-Altering Editor's map-coordinate stride from **512** to **2048** so
it can read/write and edit maps for a game engine patched for larger maps (see the
MapSizeExt project). The 512 "stride" is the on-disk `OverlayPack` index stride
(`overlayIndex = Y * 512 + X`) and, equivalently, the maximum cell coordinate `width + height`.

This document tracks what was changed and the plan for the remaining renderer work.

## Phase 1 — format + limits (DONE, low risk)

These are mechanical and correct regardless of how rendering is handled. All are keyed off the
single constant `Constants.MAX_MAP_LENGTH_IN_DIMENSION` so there is one source of truth.

| File | Change |
| --- | --- |
| `MapEditorLibrary/Constants.cs` | `MAX_MAP_LENGTH_IN_DIMENSION` 512 -> 2048 (overlay stride / max cell coord) |
| `MapEditorLibrary/Models/Map.cs` | `TileBufferSize` 600 -> `MAX_MAP_LENGTH_IN_DIMENSION + 88` (cell buffer, was 512+88) |
| `MapEditorLibrary/Models/GameObject.cs` | `GetHashCode` Y multiplier 512 -> constant |
| `MapEditorLibrary/Models/Infantry.cs` | `GetHashCode` Y multiplier 512 -> constant |
| `TSMapEditor/UI/Windows/MainMenuWindows/CreateNewMapWindow.cs` | `MaxMapSize` (the width+height cap) 512 -> constant |
| `TSMapEditor/UI/CursorActions/CheckDistancePathfindingCursorAction.cs` | pathfinding cache sized to actual map extent (was fixed 512) instead of the full 2048+ grid |

`MapLoader.cs` / `MapWriter.cs` already reference the constant, so the `OverlayPack` /
`OverlayDataPack` arrays automatically become `2048 * 2048` (x2 when the extended overlay pack is
needed). This must match the game engine's patched overlay-array stride.

### What Phase 1 unlocks by itself
The New Map dialog still enforces the **per-axis** caps `Constants.MaxMapWidth` (341) and
`Constants.MaxMapHeight` (682), which come from the 16384 px single render-target texture limit
(`TextureSizeLimit / CellSize`). So Phase 1 is self-consistent and safe: you can now create maps up
to ~341 x 682 with `width + height <= 2048` (previously `<= 512`), roughly doubling the usable cell
count, with **no** renderer changes and no risk of exceeding the GPU texture limit.

Going beyond the per-axis caps (true 2048 x 2048 maps) requires Phase 2.

## Phase 2 — chunked renderer (the hard part, needs Windows/GPU build loop)

### The blocker
`MapView` caches the entire map in single full-map render targets:
`CreateFullMapRenderTarget` (`TSMapEditor/Rendering/MapView.cs`) allocates a `RenderTarget2D` of
`Map.WidthInPixels` x `Map.HeightInPixels` (= `Size.X * 48` by `Size.Y * 24`). Direct3D 11 caps a
texture dimension at **16384 px**, hence `MaxMapWidth = 16384 / 48 = 341` and
`MaxMapHeight = 16384 / 24 = 682`. A 2048-wide map would need ~49000 px, which is impossible in a
single texture.

There are **8** such full-map targets, all allocated in `RefreshRenderTargets()`
(`MapView.cs:627-633`):

1. `mapRenderTarget` — terrain + objects, has a depth/stencil buffer
2. `transparencyRenderTarget` — map UI refreshed on map change
3. `transparencyPerFrameRenderTarget` — map UI refreshed each frame
4. `compositeRenderTarget` (+ `compositeRenderTargetCopy`) — combination of the above
5. `alphaRenderTarget` — alpha map
6. `minimapRenderTarget` — minimap / megamap

### Interlocking concerns to preserve
- **Depth encoding.** `Constants.DepthRenderStep = CellSizeY / mapRenderTarget.Height` (`MapView.cs:635`).
  Depth is a global `0..1` gradient over the full map height; with chunks this must become either a
  per-chunk depth range or a global depth with a per-chunk vertical offset. Depth precision
  (`DepthEpsilon`) degrades as height grows, so per-chunk depth is preferable.
- **Incremental invalidation.** The map target is only partially redrawn when cells change
  (`mapInvalidated` + per-cell refresh), not re-rendered each frame. Chunk system must track dirty
  regions per chunk.
- **Object renderers** (`AircraftRenderer`, `BuildingRenderer`, `InfantryRenderer`,
  `OverlayRenderer`, `SmudgeRenderer`, `TerrainRenderer`, `UnitRenderer`, `AnimRenderer`) draw at
  absolute map-pixel coordinates into `mapRenderTarget`. They need a chunk origin offset.
- **Composite + copy** feed the minimap, megamap, and PNG export (`DrawVisibleMapPortion`,
  the minimap block at `MapView.cs:1994-2011`, and PNG export at `MapView.cs:411-450`).
- **Camera/scroll** math (`GetCameraRightXCoord`, `GetCameraBottomYCoord`, `camRight/camBottom` at
  `MapView.cs:845`) assumes one full-map coordinate space.

### IMPLEMENTED: camera-follow render targets (chosen over a chunk grid)
Rather than a grid of chunk textures, the renderer now sizes its targets per map:
- **Whole-map targets** (original behavior, zero change) when the map's pixel size fits a single
  GPU texture.
- **Camera-follow targets** for larger maps: the 7 targets are sized to the visible area (current
  zoom + scroll padding, grown on zoom-out, capped at the texture limit), and `renderOrigin` — the
  world-space coordinate of the targets' top-left — follows the camera. All world-space passes
  project `[renderOrigin, renderOrigin + targetSize]` onto the target: the batchers via a
  `WorldRenderOrigin` offset in their orthographic projection, the SpriteBatch passes via
  `ApplyWorldProjection` on their shared `WorldViewProj` effects, the two previously effect-less
  map-UI passes via a new `PassthroughDraw.fx` (XNAUI exposes no SpriteBatch transform matrix),
  and the PS-only alpha-image pass via direct rectangle offset. Camera movement re-anchors the
  origin and invalidates (the editor already redrew all visible cells on every camera move, so
  scroll cost is unchanged). Depth is anchored to the map's pixel height (identical value to the
  old target-derived one for whole-map targets).

This removes both blockers at once: the 16384 px per-axis cap AND the VRAM blowup (a 500x500 map
renders in ~5000x1600 targets, tens of MB, instead of 7 impossible 30000 px textures).

**Verified on Linux/YR:** mag_balanced (whole-map mode, unchanged); 300x300 and 500x500 load and
render in camera-follow mode with correct terrain and smooth scrolling (re-anchor redraw seam-free).
Per-axis limits now `MAX_MAP_LENGTH_IN_DIMENSION` (the W+H <= 2048 stride cap is the real bound).

**Gated in camera-follow mode (v1 limitations, log + message):** minimap/megamap contents,
map preview generation on save, and screenshots — all need whole-map rendering; the future fix is
tiled whole-map passes stitched on the CPU. Zooming out further than the (texture-limit-capped)
targets leaves screen edges unrendered in extreme cases.

### Original design sketch (superseded): a tile grid of render targets
Introduce a `ChunkedRenderTarget` abstraction that presents the same "one big surface" API the rest
of `MapView` expects, but is internally a grid of sub-targets each `<= MaxChunkDimension` (e.g.
8192 px, safely under 16384 and a power of two for clean math).

Responsibilities:
- Allocate/dispose the grid; map absolute map-pixel rects to (chunk, local-rect) pairs.
- `PushRenderTarget(absoluteClipRect)` -> iterate only the chunks the clip rect overlaps, pushing
  each with a translated clip and a world->local origin offset supplied to the draw effects.
- `Blit(sourceAbsoluteRect, destination)` -> stitch the overlapping chunk regions into the
  destination (used by the composite, minimap downscale and PNG export).
- Per-chunk dirty tracking for incremental invalidation.

Only the visible chunks (plus a margin) need to be resident for editing; off-screen chunks can be
allocated lazily and evicted under memory pressure. A 2048 x 2048 map at full residency is
~49000 x 24000 px x 4 bytes x (several targets) = multiple GB, so lazy residency is required, not
optional.

### Suggested implementation order (each step is independently testable in the Windows build)
1. **Extract the target abstraction. (DONE — needs Windows build verification.)** The 7 persistent
   full-map `RenderTarget2D` fields in `MapView` are now `MapRenderTarget`
   (`TSMapEditor/Rendering/MapRenderTarget.cs`), created via `CreateFullMapRenderTarget`. Today it
   wraps a single texture and carries an implicit `RenderTarget2D` conversion, so every call site
   (`Renderer.PushRenderTarget`/`DrawTexture`, `GraphicsDevice.SetRenderTarget`, effect parameters,
   `.Width`/`.Height`, `?.Dispose()`) compiles unchanged — a behavior-neutral refactor. The
   export/preview scratch targets (`cropRenderTarget`, megamap `texture`, `finalPreviewRenderTarget`)
   are intentionally left as raw `RenderTarget2D`; they are separate single-texture paths addressed
   in step 4. **Next:** when chunking lands, delete the implicit operator so the compiler enumerates
   every call site that still assumes one texture.
2. **Chunk the terrain/object target only** (`mapRenderTarget`), keeping the others full-size but
   still capped at 341/682 — i.e. raise only that one target's limit first, to shake out the origin
   offset + depth-per-chunk work in isolation.
3. **Per-chunk depth.** Move `DepthRenderStep` to a per-chunk value; verify render ordering across a
   chunk boundary.
4. **Chunk the remaining targets** (transparency, composite, alpha) and the minimap/megamap/PNG
   stitch paths.
5. **Lazy residency + eviction**, then raise `MaxMapWidth`/`MaxMapHeight` to allow true 2048 maps.
6. Raise the per-axis caps in `CreateNewMapWindow` / `MapLoader` once the renderer no longer needs
   them.

### Verifying on Linux (DesktopGL build)
This branch also ports the editor to build and run on Linux (MonoGame DesktopGL), which gives a
local build/verify loop for the renderer work. See "Linux / DesktopGL build" below.

## Linux / DesktopGL build

The editor now builds and runs on Linux by retargeting from `net8.0-windows` + MonoGame WindowsDX to
`net8.0` + MonoGame DesktopGL. The Windows build is unaffected (Windows-only code is guarded with
`#if WINDOWS`, which the SDK auto-defines only for the `-windows` target framework).

### Code changes
- All three projects retargeted to `net8.0`; packages switched to `MonoGame.Framework.DesktopGL`
  and `Rampastring.XNAUI.UniversalGL`. **Version pinning matters:** `Rampastring.XNAUI.UniversalGL`
  3.2.0 is built against `MonoGame.Framework.DesktopGL` **3.8.1.303** (not 3.8.3); using 3.8.3
  throws `MissingMethodException: Game.add_Exiting` at startup. The content builder task is pinned
  to the same 3.8.1.303.
- WinForms removed on non-Windows: file dialogs and window-chrome calls were already `#if WINDOWS`
  guarded upstream; the remaining pieces were ported — `System.Windows.Forms.Clipboard` custom-data
  calls now use `MapEditorLibrary/Misc/CrossPlatformClipboard.cs` (in-process), `Screen.PrimaryScreen`
  and `Screen.FromHandle` use `GraphicsAdapter.DefaultAdapter.CurrentDisplayMode`, the Windows
  registry game-path lookup is guarded, `NativeMethods` P/Invoke is guarded (DPI falls back to 96),
  and `Program.cs` drops the WinForms bootstrap. XNAUI WindowsDX-only members
  (`WindowSizeChangedByUser`, `HideWindow`, `SetMaximizeBox`, `Cursor.LoadNativeCursor`) are
  guarded. Fixed a latent bug in an upstream untested `#else` branch (`mapUI` -> `mapView` in
  `MapUI.cs`).
- Two shaders needed OpenGL-profile (ps_3_0) fixes: `PalettedColorDraw.fx` bool uniforms became
  floats compared against 0.5 (ps_3_0 cannot branch on boolean constant registers); `BackdropBlur.fx`
  omits its screen-position glass bevel on GL (reading SV_POSITION as a PS input needs VPOS in
  ps_3_0). The unused auto-generated `Properties/Settings.Designer.cs` is excluded on non-Windows.

### Toolchain setup (one-time, for the shader content pipeline)
MonoGame compiles `.fx` effects on Linux by running the DX shader compiler under Wine, so:
1. Wine 8+ (`wine --version`), plus a `wine64` shim if your distro only ships `wine`:
   `ln -sf "$(which wine)" ~/.local/bin/wine64`.
2. `WINEPREFIX=~/.winemonogame WINEARCH=win64 wineboot --init`
3. `WINEPREFIX=~/.winemonogame winetricks -q d3dcompiler_47`
4. Extract the Windows .NET SDK into the prefix so `dotnet.exe` runs under Wine (MGFXC runs itself
   there): download `dotnet-sdk-8.0.201-win-x64.zip` and `7z x` it into
   `~/.winemonogame/drive_c/windows/system32/` (this is what MonoGame's `mgfxc_wine_setup.sh` does).
5. Export `MGFXC_WINE_PATH=~/.winemonogame` before building.
Then `dotnet build src/TSMapEditor/TSMapEditor.csproj -c Debug` (with `DISPLAY` set) builds and the
editor launches with `dotnet WorldAlteringEditor.dll` from the output directory.

### Additional cross-platform bug fixes found while verifying
- **`Helpers.NormalizePath` upper-cased paths** (`.ToUpperInvariant()`). Harmless on case-insensitive
  Windows (it dedupes search dirs), but on Linux it corrupts real filesystem paths so **every game
  MIX/file fails to load** (`Required MIX file not found: langmd.mix`). Fixed to only upper-case on
  Windows (`OperatingSystem.IsWindows()`).
- **Cursor invisibility**: `Cursor.LoadNativeCursor` is Windows-only and was guarded out; MonoGame
  hides the hardware cursor by default, so the cursor was intermittently invisible. Fixed by setting
  `IsMouseVisible = true` on non-Windows (GameClass).

### Making the editor actually load + render a map on Linux
Getting from "menu renders" to "a map loads and renders" needed several more DesktopGL fixes:
- **Multithreaded texture loading deadlocks on OpenGL.** `TheaterGraphics` loads textures on 16
  background `Task`s, but OpenGL is single-threaded for GPU resource creation, so a task
  (`ReadBuildingTextures`) hangs and `Task.WaitAll` blocks forever. Fixed by forcing serial loading
  off Windows (`... && OperatingSystem.IsWindows()` in `TheaterGraphics`).
- **Texture shader parameters are named differently on DesktopGL.** MojoShader exposes a texture
  parameter as `SamplerName+TextureName` (e.g. `PaletteSampler+PaletteTexture`), not the bare
  texture name, so every `effect.Parameters["PaletteTexture"]` / `["MainTexture"]` returned null and
  threw `NullReferenceException` on the first render. Added `Effect.FindTextureParameter(name)`
  (`EffectExtensions.cs`) and routed all six texture-parameter lookups through it.
- **Native file dialogs.** The Browse buttons were `#if WINDOWS`-only no-ops. Added
  `NativeFileDialog` (kdialog/zenity) and wired the game-directory and map-path Browse buttons.

With those, the editor **loads and renders a real YR map** on Linux (full editor UI + isometric
terrain), verified with `Desktop/mag_balanced.map` (146x169).

### Known remaining issue: grayscale terrain (paletted rendering on DesktopGL)
Terrain (and paletted sprites) render in grayscale on DesktopGL. Diagnosed precisely:
- The shader takes the correct **paletted** branch (confirmed: a diagnostic that painted the
  non-paletted branch red showed no red).
- The palette texture is correct: 256x1 `SurfaceFormat.Color`, colored (read back via `GetData`).
- Yet `PaletteTexture.Sample(PaletteSampler, ...)` returns grayscale — i.e. the palette (the second
  texture, sampler unit 1) is **not bound to its sampler** on the custom `TerrainBatcher`'s
  `DrawUserIndexedPrimitives` path. Setting `GraphicsDevice.SamplerStates[1]` and even binding
  `GraphicsDevice.Textures[1]` directly did not help, suggesting MojoShader collapses the two
  samplers or maps `PaletteSampler` to a unit that neither the effect parameter nor a direct bind
  reaches.
- **Next step:** compare with how the CnCNet client (also `Rampastring.XNAUI.UniversalGL`) binds its
  palette on DesktopGL — it renders C&C paletted graphics correctly on Linux, so there is a known
  working pattern (likely explicit sampler `register(sN)` assignment in the .fx, or binding both
  textures through the SpriteBatch/Renderer path rather than the custom batcher). The batcher now
  sets `SamplerStates[0]/[1] = PointClamp`, which is required once the palette binds.

### Per-map stride + the [MapSizeExt] tag (replaces the global 2048 stride)
The OverlayPack stride and the packed-cell-coordinate base are baked into each map file, so a
global 2048 stride corrupted every vanilla map's overlays (ore/sandbags smeared into diagonal
bands — the same artifact the mis-strided game engine shows on oversized maps). Both are now chosen
per map, with two independent thresholds:

- **OverlayPack stride** (`Constants.GetOverlayPackStride`): `width + height <= 512` -> vanilla 512
  (byte-compatible with the unmodified game); larger -> 2048.
- **Packed cell coordinate base** (`Constants.CellCoordBase`, used for [Waypoints]/[CellTags]/
  [Terrain] keys, packed = `Y * base + X`): `width + height <= 1000` -> vanilla base 1000;
  larger -> next power of two >= width + height (e.g. 700x700 -> 2048).
  `Helpers.CoordStringToPoint` and the three pack sites now use this base (numeric div/mod —
  identical results to the old base-1000 string slicing for vanilla maps).

The per-map **[MapSizeExt]** tag section is shared with the MapSizeExt game DLL:
- Written on save by `MapWriter.WriteMapSizeExtSection`: omitted entirely for vanilla-sized maps;
  `RequiredStride=` whenever the file uses the extended overlay stride (so the DLL can warn/adapt
  when a map exceeds its configured global stride); `CoordBase=` additionally when
  width + height > 1000.
- Honored on load: `CoordBase=` and `RequiredStride=` are trusted over the size heuristics.
- The MapSizeExt DLL currently takes its stride from MAPSIZEEXT.INI globally; long-term it can
  derive it per map from [Map] Size / this tag.

**Stride self-detection (reading):** legacy oversized maps (e.g. RA2MapGen output) carry
vanilla-sized (512^2), typically empty, OverlayPacks with no tag — the size heuristic alone would
mis-read them. The loader therefore detects the written stride from the pack's total decoded size
(`GetFormat5DecodedSize` walks the Format5 block headers without decompressing; stride^2 x
bytes-per-cell is content-independent ground truth). Priority: decoded-size detection >
`RequiredStride=` tag > size heuristic. Out-of-pack cell indices are treated as "no overlay"
(bounds-guarded in both loader and writer). `Format5.DecodeInto` also gained a source-bounds check —
it previously read past the end of the source array when the destination was larger than the
encoded data.

Verified end-to-end:
- `mag_balanced.map` (vanilla 146x169): correct overlays, no sandbag bands or misplaced ore.
- `mapsizeext_260x260.map` (W+H=520, legacy 512-stride empty overlay, no tag): detection picks 512,
  renders clean; the heuristic alone would have read overlay type 0 (sandbags) across the lower map.
- Save round-trip of the 260x260 (Ctrl+S in the editor): `[Map] Size`/`LocalSize` preserved,
  waypoints byte-identical (base-1000, W+H <= 1000 so no CoordBase per spec), `[MapSizeExt]`
  written with `RequiredStride=2048`, OverlayPack re-encoded at stride 2048; reloading the saved
  file detects 2048 and renders identically.

Note for the game side: WAE-saved oversized maps carry 2048-stride OverlayPacks, so the MapSizeExt
DLL must be configured for (or taught to read) stride 2048 for them; the `RequiredStride=` tag is
what it can validate against. Current render caps until the chunked renderer lands: width <= 273,
height <= 546 for YR (16384 px / cell size), so 260x260 works but 300x300+ needs renderer Step 2.

### Dual-platform build (one branch, both targets)
All three .csproj files select their configuration by build OS via an `IsWindowsBuild` property:
building on Windows produces the upstream `net8.0-windows` + MonoGame WindowsDX + WinForms editor
(upstream package versions, `Content.mgcb` platform overridden via `MonoGamePlatform`); building
elsewhere produces `net8.0` + DesktopGL. Windows-only code stays behind `#if WINDOWS`; shader
differences behind `#if OPENGL` (defined only by the GL effect profile). The Windows flavor can be
built and tested via the repo's GitHub Actions workflow (`.github/workflows/build.yml`) without a
local Windows machine.

### Autosave on Linux
Auto-saves anchored on `Environment.ProcessPath`, which under `dotnet WorldAlteringEditor.dll` is
the dotnet host (`/usr/bin/dotnet`), so auto-saves targeted an unwritable system directory and
failed. Now anchored on `AppContext.BaseDirectory` and the directory is created before saving
(`TSMapEditor/Misc/AutosaveTimer.cs`).

### Verified end-to-end on Linux
With the official YR release config dropped into the build (`Config/` overlay) and the game directory
pointed at a CnCNet YR install, WAE on Linux loads a real 256x256 YR map: all theater graphics
(tile/infantry/unit/overlay/smudge/animation textures, voxel models) load to the GPU and
`IsoMapPack5 read successfully`. To validate against a non-retail CnCNet install, add its executable
names to `Constants.ExpectedClientExecutableName` (e.g. `gamemd-spawn.exe`).

### Renderer Phase 2 findings (investigation)
- The terrain **batcher** chunks cleanly: `AbstractBatcher.End()` builds its projection from the
  viewport with vertices in absolute map-pixel coords, and it sets `WorldViewProj` itself — so a
  per-chunk offset projection (`CreateOrthographicOffCenter(cx, cx+w, cy+h, cy, ...)`) is trivial.
- The **object/smudge/UI path** (XNAUI `Renderer.DrawTexture` / SpriteBatch) is the hard part:
  `SpriteBatchSettings` exposes no transform matrix, so offsetting it per chunk needs either a
  verified per-chunk `WorldViewProj` override or coordinate-offset threading through the renderers.
- **VRAM motivation**: even a 256x256 YR map allocates 7 full-map render targets at 15360x8040 px
  (~494 MB each, ~3.4 GB total). A 2048 map is impossible without chunking, independent of the
  16384 px texture cap. This is the core Step 2 work.

With the Linux build + YR config, these steps can be built and visually verified locally
(`DISPLAY=:0`, real GPU) instead of only on Windows/CI.
