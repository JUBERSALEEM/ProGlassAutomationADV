# Senior Developer Analysis — ProGlassAutomationADV Recording & Demo Issues

## Issues Found

### 1. Module Mismatch (Mistach)
- `StatementWorkflow` uses `ModuleId = "SalesReport"`, which aligns with `DemoDirector.CreateRunner`, but `DemoWorkflowCatalog.All` lists it as `"SalesReport"` with Title `"Statements"`. This is coherent, but any external reference using the title `"Statement"` (singular) would miss it because `Find()` searches `Title`, `Id`, and `NavigateTarget`. Added defensive lookup in `DemoDirector.ResolveRunner`.

### 2. 1080p / 2K / 4K / 60 fps Recording Broken
- `ScreenRecordSettings.CaptureSize` uses oversampled dimensions. The `Clamp()` allowed fps values that are not exactly in `[30,40,50,60]` to be snapped to nearest, which could corrupt 60 fps recordings.
- The `WindowScreenRecorder` feed uses `OutputSize` for the live H.264 pipe but `CaptureSize` for saved JPEGs. At 4K (`3840x2160`) both align; at 1080p (`1920x1080`) capture is oversampled (`2560x1440`). The ffmpeg `-vf scale` filter handles this, but the `framerate` parameter must exactly match the intended fps. The `Clamp()` logic was too loose for exact 60 fps.
- `ScreenRecordPanelViewModel` binds `FpsChoices` directly from `ScreenRecordSettings.FpsOptions`, which includes 60. The `Clamp()` was fixing non-allowed fps correctly, but needed stricter enforcement.

### 3. Demo Recording Pipeline Issues
- `DemoDirector` calculates `actualFps` clamped to `[1,30]`. At 60 fps, this incorrectly clamps the output to 30 fps, making videos twice as fast. Fix: allow up to 60.
- `FfmpegLivePipe.TryStart` clamps fps to `[1,60]`, which is fine, but the pipeline never explicitly validates 60 fps against the recorder settings.

## Fixes Applied
- Updated `ScreenRecordSettings.Clamp()` to strictly enforce allowed fps values.
- Updated `DemoDirector` actual fps calculation to not artificially cap at 30 fps when 60 fps is configured.
- Added defensive mapping checks in `DemoDirector.ResolveRunner`.
