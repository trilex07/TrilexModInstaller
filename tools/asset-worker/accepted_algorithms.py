# Copyright (c) 2026 letsgosportsteam; MIT, see third_party/SingularityVR-MIT.txt.
# Fixed accepted V7 algorithms from source 188c10a. No filesystem operations.
import numpy as np
from PIL import Image, ImageFilter
from dds_io import normal_to_signed, signed_to_normal

def fbm(shape: tuple[int, int], octaves: int, seed: int, base_scale: float = 0.02) -> np.ndarray:
    """Deterministic fractal noise in [0,1].

    Value noise per octave, upsampled in float32 (PIL mode 'F') so no uint8
    detour distorts the range, summed with halving amplitude.
    """
    rng = np.random.default_rng(seed)
    h, w = shape
    acc = np.zeros((h, w), np.float32)
    amp, freq, norm = 1.0, base_scale, 0.0
    for _ in range(octaves):
        gh, gw = max(2, int(h * freq)), max(2, int(w * freq))
        small = rng.random((gh, gw)).astype(np.float32)
        up = np.asarray(Image.fromarray(small, mode="F").resize((w, h), Image.BICUBIC),
                        dtype=np.float32)
        acc += np.clip(up, 0.0, 1.0) * amp
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return acc / max(norm, 1e-6)

def enhance_normal(orig_normal: Image.Image, enhanced_diffuse: Image.Image,
                   asset_type: str, strength: float = 1.0) -> tuple[Image.Image, list[str]]:
    """
    Rebuild the normal map with real relief.

    The original macro surface (from the shipped normal map) is kept, and
    micro relief is derived from the high-frequency luminance of the enhanced
    diffuse - i.e. the same detail we just authored - instead of sharpening
    colour. Vectors are renormalised after blending so every texel stays a unit
    vector (no washed-out shading).
    """
    notes = []

    def to_signed(a: np.ndarray) -> np.ndarray:
        a = a.astype(np.float32)
        xy = (a[:, :, 0:2] / 255.0) * 2.0 - 1.0
        return np.concatenate([xy, (a[:, :, 2:3] / 255.0)], axis=2)

    org = to_signed(np.asarray(orig_normal.convert("RGBA")))

    # detail height from the enhanced diffuse: blur - original = detail pass
    rgb = np.asarray(enhanced_diffuse.convert("RGB")).astype(np.float32).mean(axis=2)
    small = Image.fromarray(rgb.astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius=3.0))
    detail = rgb - np.asarray(small, np.float32)

    gy, gx = np.gradient(detail)
    if asset_type == "glove":
        amp = 0.55      # fabric weave is shallow
    elif asset_type == "environment":
        amp = 0.75
    elif asset_type == "organic":
        amp = 0.85      # chitin / hide relief reads strongly at grazing angles
    elif asset_type == "soldier":
        amp = 0.60      # uniform cloth + hard armor plates
    elif asset_type == "optic":
        amp = 0.35      # housings and lenses stay optically smooth
    else:
        amp = 0.65      # machined metal
    amp *= strength

    dx = -gx / 255.0 * amp
    dy = -gy / 255.0 * amp

    nx = np.clip(org[:, :, 0] + dx, -1, 1)
    ny = np.clip(org[:, :, 1] + dy, -1, 1)
    nz = np.clip(org[:, :, 2], 1e-3, 1.0)

    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / length, ny / length, nz / length

    out = np.concatenate([
        np.round((nx * 0.5 + 0.5) * 255.0)[:, :, None],
        np.round((ny * 0.5 + 0.5) * 255.0)[:, :, None],
        np.round(nz * 255.0)[:, :, None],
        np.full((org.shape[0], org.shape[1], 1), 255.0, np.float32),
    ], axis=2)

    notes.append(f"micro relief from enhanced diffuse high-pass, slope amplitude {amp}")
    notes.append("original macro normals preserved and renormalised to unit length")
    notes.append("alpha channel set to constant 255 (matches shipped DXT1 normal maps)")

    return Image.fromarray(out.astype(np.uint8), "RGBA"), notes

def enhance_normal_procedural(img: Image.Image, kind: str) -> tuple[Image.Image, list[str]]:
    """Micro relief without diffuse coupling (for normal-only assets).

    Used only where no true-mip0 diffuse exists to derive detail from. The
    perturbation is procedural fbm noise, renormalised per texel. Weaker
    provenance than diffuse-coupled relief - labelled as such in manifests.
    """
    import math
    from dds_io import normal_to_signed, signed_to_normal
    signed = normal_to_signed(np.asarray(img.convert("RGBA"))).astype(np.float32)
    h, w = signed.shape[:2]
    # deliberately gentler than diffuse-coupled relief: without a diffuse map
    # anchoring the detail, a strong global perturbation would rewrite the
    # material instead of refining it
    amp = {"weapon": 0.28, "device": 0.25}.get(kind, 0.25)
    dx = (fbm((h, w), 4, 8001, 0.10) - 0.5) * 2.0 * amp
    dy = (fbm((h, w), 4, 8002, 0.10) - 0.5) * 2.0 * amp
    signed[:, :, 0] = np.clip(signed[:, :, 0] + dx, -1, 1)
    signed[:, :, 1] = np.clip(signed[:, :, 1] + dy, -1, 1)
    out = signed_to_normal(signed)
    out[:, :, 3] = 255
    notes = [
        "procedural-only micro relief (fbm, 4 octaves) - NOT coupled to diffuse",
        "weaker provenance than diffuse-coupled relief; see manifest",
        "original macro normals preserved and renormalised to unit length",
        "alpha channel set to constant 255 (matches shipped DXT1 normal maps)",
    ]
    return Image.fromarray(out, "RGBA"), notes

def signed_upsample_2x(img: Image.Image) -> Image.Image:
    """Genuine 2x upscale of a normal map: LANCZOS in signed space + renorm."""
    from dds_io import normal_to_signed, signed_to_normal
    signed = normal_to_signed(np.asarray(img.convert("RGBA")))
    w, h = img.size
    planes = []
    for c in range(4):
        planes.append(np.asarray(
            Image.fromarray(signed[:, :, c], mode="F").resize((w * 2, h * 2), Image.LANCZOS),
            dtype=np.float32)[:, :, None])
    return Image.fromarray(signed_to_normal(np.concatenate(planes, axis=2)), "RGBA")

def unsharp_luminance(img: Image.Image, radius: float = 2.0,
                      percent: int = 30, threshold: int = 5) -> Image.Image:
    """Gentle edge-contrast restoration on RGB only; alpha byte-exact.

    This sharpens genuine edges softened by DXT compression. It invents no
    content: unsharp masking only re-weights existing local contrast, and the
    threshold leaves flat areas (and DXT noise floors) untouched. Applied to
    the source BEFORE the Lanczos 2x presentation upscale.
    """
    from PIL import ImageFilter
    rgb = img.convert("RGB")
    sharp = rgb.filter(ImageFilter.UnsharpMask(radius=radius, percent=percent,
                                               threshold=threshold))
    alpha = img.convert("RGBA").getchannel("A")
    out = sharp.convert("RGBA")
    out.putalpha(alpha)
    return out

