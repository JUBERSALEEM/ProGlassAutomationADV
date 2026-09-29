/**
 * One-shot asset pipeline.
 *
 * The brand and product illustrations in `public/images` ship inside the WPF
 * repository as multi-megabyte PNGs (Glass3D renders are ~3 MB each). Serving
 * those untouched would wreck Largest Contentful Paint, so this script
 * re-encodes them to AVIF + WebP at the widths the layout actually uses and
 * drops the originals to a smaller PNG fallback.
 *
 * Run with:  npm run assets:optimize
 *
 * The script is idempotent: it reads from `public/images`, writes beside the
 * source, and skips a target that is already newer than its source.
 */
import { stat, mkdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import sharp from "sharp";

const ROOT = path.resolve(import.meta.dirname, "..");
const IMAGES = path.join(ROOT, "public", "images");

/** Widths the layout requests, per logical role. */
const TARGETS = {
  /** Full-bleed brand banner. */
  "brand/proglass-hero-banner.png": [1280, 1920, 2560],
  /** App icon — small only. */
  "brand/proglass-icon-256.png": [96, 192, 256],
  /** Glass build-up illustrations — used in cards and sliders. */
  "glass3d/sgu-3d.png": [640, 1024, 1536],
  "glass3d/dgu-3d.png": [640, 1024, 1536],
  "glass3d/lam-3d.png": [640, 1024, 1536],
  "glass3d/dgulam-3d.png": [640, 1024, 1536],
};

async function isStale(source, target) {
  if (!existsSync(target)) return true;
  const [s, t] = await Promise.all([stat(source), stat(target)]);
  return t.mtimeMs < s.mtimeMs;
}

async function run() {
  const manifest = {};
  for (const [relative, widths] of Object.entries(TARGETS)) {
    const source = path.join(IMAGES, relative);
    if (!existsSync(source)) {
      console.warn(`  skip  ${relative} (not found)`);
      continue;
    }
    const dir = path.dirname(source);
    const base = path.basename(relative, path.extname(relative));
    const meta = await sharp(source).metadata();
    const available = (meta.width ?? 0) || 1;
    const chosen = widths.filter((w) => w <= available);
    if (chosen.length === 0) chosen.push(available);

    manifest[relative] = {};
    for (const width of chosen) {
      for (const format of ["avif", "webp"]) {
        const target = path.join(dir, `${base}-${width}.${format}`);
        if (!(await isStale(source, target))) {
          manifest[relative][`${width}`] ??= {};
          manifest[relative][`${width}`][format] = path.relative(ROOT, target);
          continue;
        }
        const pipeline = sharp(source).resize({ width, withoutEnlargement: true });
        if (format === "avif") {
          await pipeline.avif({ quality: 62, effort: 5 }).toFile(target);
        } else {
          await pipeline.webp({ quality: 82, effort: 5 }).toFile(target);
        }
        console.log(`  write ${path.relative(ROOT, target)}`);
        manifest[relative][`${width}`] ??= {};
        manifest[relative][`${width}`][format] = path.relative(ROOT, target);
      }
    }

    // Keep a lean PNG fallback so a browser without AVIF/WebP still gets a
    // reasonable payload instead of the 3 MB original.
    const fallbackWidth = chosen[chosen.length - 1] ?? available;
    const fallback = path.join(dir, `${base}-${fallbackWidth}.png`);
    if (await isStale(source, fallback)) {
      await sharp(source)
        .resize({ width: fallbackWidth, withoutEnlargement: true })
        .png({ compressionLevel: 9, palette: true })
        .toFile(fallback);
      console.log(`  write ${path.relative(ROOT, fallback)}`);
      manifest[relative][`${fallbackWidth}`].png = path.relative(ROOT, fallback);
    } else {
      manifest[relative][`${fallbackWidth}`].png = path.relative(ROOT, fallback);
    }
  }

  await mkdir(path.join(ROOT, "src", "generated"), { recursive: true });
  console.log("\nManifest:");
  console.log(JSON.stringify(manifest, null, 2));
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
