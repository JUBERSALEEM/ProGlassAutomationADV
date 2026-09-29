/**
 * Asset pipeline.
 *
 * The brand artwork and the product illustrations ship inside the WPF
 * repository as multi-megabyte PNGs (the Glass3D renders are ~3 MB each).
 * Serving those untouched would wreck Largest Contentful Paint, so this
 * script re-encodes them to AVIF + WebP at the widths the layout actually
 * requests, and writes a smaller PNG fallback beside each set.
 *
 * Sources are the real files, named as they are in the application:
 *
 *   Resources/Glass3D/sgu-3d.png       → public/images/glass3d/sgu-3d-*.{avif,webp,png}
 *   Resources/Glass3D/dgu-3d.png       → public/images/glass3d/dgu-3d-*
 *   Resources/Glass3D/lam-3d.png       → public/images/glass3d/lam-3d-*
 *   Resources/Glass3D/dgulam-3d.png    → public/images/glass3d/dgulam-3d-*
 *   Resources/proglass_icon_256.png    → public/images/brand/proglass-icon-256-*
 *   README_ASSETS/hero.png             → public/images/brand/proglass-hero-banner-*
 *
 * Usage:
 *   node scripts/optimize-assets.mjs --from ..      # run from website/, source = repo root
 *   node scripts/optimize-assets.mjs --from /path/to/ProGlassAutomationADV
 *
 * Idempotent: a target newer than its source is left alone. The manifest of
 * everything written is printed at the end, so the run can be audited without
 * inspecting the folder.
 */
import { mkdir, stat } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import sharp from "sharp";

const ROOT = path.resolve(import.meta.dirname, "..");
const IMAGES = path.join(ROOT, "public", "images");

/** Where the application's own assets live, when run from this folder. */
const DEFAULT_SOURCE_ROOT = path.resolve(ROOT, "..");

const args = process.argv.slice(2);
const fromIndex = args.indexOf("--from");
const SOURCE_ROOT = fromIndex >= 0 && args[fromIndex + 1] ? path.resolve(args[fromIndex + 1]) : DEFAULT_SOURCE_ROOT;

/**
 * source (inside the WPF repository) → destination base name + widths the
 * layout asks for. Widths above the source's own width are skipped, never
 * upscaled.
 */
const TARGETS = [
  { source: "README_ASSETS/hero.png", dest: "brand/proglass-hero-banner", widths: [1280, 1920, 2560] },
  { source: "Resources/proglass_icon_256.png", dest: "brand/proglass-icon-256", widths: [96, 192, 256] },
  { source: "Resources/Glass3D/sgu-3d.png", dest: "glass3d/sgu-3d", widths: [640, 1024, 1536] },
  { source: "Resources/Glass3D/dgu-3d.png", dest: "glass3d/dgu-3d", widths: [640, 1024, 1536] },
  { source: "Resources/Glass3D/lam-3d.png", dest: "glass3d/lam-3d", widths: [640, 1024, 1536] },
  { source: "Resources/Glass3D/dgulam-3d.png", dest: "glass3d/dgulam-3d", widths: [640, 1024, 1536] },
];

async function isStale(source, target) {
  if (!existsSync(target)) return true;
  const [s, t] = await Promise.all([stat(source), stat(target)]);
  return t.mtimeMs < s.mtimeMs;
}

async function encode(sourcePath, targetPath, width, format) {
  const pipeline = sharp(sourcePath).resize({ width, withoutEnlargement: true });
  if (format === "avif") {
    await pipeline.avif({ quality: 62, effort: 5 }).toFile(targetPath);
  } else if (format === "webp") {
    await pipeline.webp({ quality: 82, effort: 5 }).toFile(targetPath);
  } else {
    await pipeline.png({ compressionLevel: 9, palette: true }).toFile(targetPath);
  }
}

async function run() {
  if (!existsSync(SOURCE_ROOT)) {
    console.error(`Source root not found: ${SOURCE_ROOT}`);
    process.exitCode = 1;
    return;
  }

  console.log(`Source: ${SOURCE_ROOT}`);
  console.log(`Output: ${IMAGES}\n`);

  const manifest = {};
  const missing = [];

  for (const { source, dest, widths } of TARGETS) {
    const sourcePath = path.join(SOURCE_ROOT, source);
    if (!existsSync(sourcePath)) {
      missing.push(source);
      console.warn(`  skip    ${source} (not in the source tree)`);
      continue;
    }

    const targetDir = path.join(IMAGES, path.dirname(dest));
    await mkdir(targetDir, { recursive: true });

    const base = path.basename(dest);
    const { width: sourceWidth = 0 } = await sharp(sourcePath).metadata();
    const available = sourceWidth || 1;
    const chosen = widths.filter((w) => w <= available);
    if (chosen.length === 0) chosen.push(available);

    manifest[dest] = {};

    for (const width of chosen) {
      manifest[dest][width] ??= {};
      for (const format of ["avif", "webp"]) {
        const target = path.join(targetDir, `${base}-${width}.${format}`);
        if (await isStale(sourcePath, target)) {
          await encode(sourcePath, target, width, format);
          console.log(`  write   ${path.relative(ROOT, target)}`);
        }
        manifest[dest][width][format] = path.relative(ROOT, target);
      }
    }

    // A lean PNG fallback, so a browser without AVIF or WebP support gets a
    // reasonable payload rather than the original multi-megabyte raster.
    const fallbackWidth = chosen[chosen.length - 1] ?? available;
    const fallback = path.join(targetDir, `${base}-${fallbackWidth}.png`);
    if (await isStale(sourcePath, fallback)) {
      await encode(sourcePath, fallback, fallbackWidth, "png");
      console.log(`  write   ${path.relative(ROOT, fallback)}`);
    }
    manifest[dest][fallbackWidth].png = path.relative(ROOT, fallback);
  }

  // The site's own source is not required for the images to exist; if a
  // source is absent, its already-built derivatives stay as they are.
  if (missing.length > 0) {
    console.warn(`\n${missing.length} source file(s) not found; existing derivatives were left untouched.`);
    console.warn("Point the script at a checkout with --from <path> to refresh them.");
  }

  console.log("\nManifest:");
  console.log(JSON.stringify(manifest, null, 2));
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
