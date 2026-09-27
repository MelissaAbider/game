import asyncio
import base64
import hashlib
import json
import logging
from dataclasses import dataclass
from pathlib import Path

import httpx

from backend.config import get_settings

logger = logging.getLogger(__name__)

ASSET_DIR = Path(__file__).resolve().parents[1] / "assets"
HERO_ASSET = ASSET_DIR / "echoshift_lab_keyart.png"

STYLE = (
    "Style: premium contemporary indie sci-fi game art, sharp, clean, readable silhouette, "
    "palette of deep graphite, electric cyan, hot red and soft violet accents, no text, no logos, no watermark."
)

POSE_RULE = (
    "Redraw EXACTLY the same character as in the reference image: identical white suit with electric-cyan trim, "
    "identical glowing cyan visor helmet, identical backpack unit, identical proportions, line weight and shading. "
    "Do not change the colors: the suit must stay WHITE. Strict side view facing right. Pose: "
)

CHROMA_RULE = (
    " The subject is fully centered and isolated on a solid flat pure green background (#00FF00), "
    "no floor, no shadow, no glow spilling onto the background, no other objects."
)

DESIGN_PROMPT = """
Create a polished 16:9 key art image for a modern voice-controlled sci-fi escape game named EchoShift Lab.
Scene: a premium neon laboratory maze, cinematic top-down side-view hybrid, glass floors, cyan guidance lines,
red security lasers, blue control terminal, red exit door, dramatic volumetric light, high-end indie game UI mood.
Style: contemporary PC/console game, sharp, readable, not cartoonish, no characters, no logos, no text, no UI overlays.
Palette: deep graphite, electric cyan, hot red, soft violet accents, metallic surfaces, strong contrast.
""".strip()


@dataclass(frozen=True)
class AssetSpec:
    id: str
    filename: str
    prompt: str
    aspect_ratio: str
    chroma: bool = False
    reference: str | None = None  # id of another asset sent as an image reference for consistency


ASSETS: dict[str, AssetSpec] = {
    spec.id: spec
    for spec in [
        AssetSpec("keyart", "echoshift_lab_keyart.png", DESIGN_PROMPT, "16:9"),
        AssetSpec(
            "maze",
            "maze_background.png",
            "Side-view 2D platformer background for a sci-fi laboratory labyrinth level, strictly frontal orthographic "
            "side view (no perspective, no isometric). A wide dark hall of layered graphite and glass maze walls, "
            "vertical support pillars, ventilation ducts, pipes, holographic wall panels, faint cyan guidance lines, "
            "dim violet backlighting, fog in the distance. Keep the overall image dark and low-contrast so gameplay "
            "objects in the foreground stay readable: no bright light sources, no characters, no doors, no lasers, "
            "no floor in the bottom 20 percent, no text. " + STYLE,
            "16:9",
        ),
        AssetSpec(
            "player",
            "player_sprite.png",
            "Full-body 2D game character sprite, strict side view facing right, relaxed standing idle pose, arms along "
            "the body, feet together. A lab test subject wearing a sleek white and electric-cyan hazmat-style suit with "
            "a glowing cyan visor helmet and a small backpack unit. Stylized clean vector-like game art with crisp "
            "outlines. " + STYLE + CHROMA_RULE,
            "1:1",
            chroma=True,
        ),
        AssetSpec(
            "player_run_a",
            "player_run_a.png",
            POSE_RULE + "a mid-run pose: left leg forward, right leg back, arms "
            "bent and swinging, slight forward lean. Clean crisp outlines, no text." + CHROMA_RULE,
            "1:1",
            chroma=True,
            reference="player",
        ),
        AssetSpec(
            "player_run_b",
            "player_run_b.png",
            POSE_RULE + "the opposite mid-run pose: right leg forward, left leg back, "
            "arms bent and swinging, slight forward lean. Clean crisp outlines, no text." + CHROMA_RULE,
            "1:1",
            chroma=True,
            reference="player",
        ),
        AssetSpec(
            "player_jump",
            "player_jump.png",
            POSE_RULE + "a dynamic jump pose in mid-air: knees tucked up, arms raised "
            "forward for balance, body slightly arched. Clean crisp outlines, no text." + CHROMA_RULE,
            "1:1",
            chroma=True,
            reference="player",
        ),
        AssetSpec(
            "player_crouch",
            "player_crouch.png",
            POSE_RULE + "a crouching pose, knees bent, torso leaning slightly forward, both arms relaxed, keeping the "
            "bright WHITE suit and cyan trim identical to the reference. Clean crisp outlines, no text. " + CHROMA_RULE,
            "1:1",
            chroma=True,
            reference="player",
        ),
        AssetSpec(
            "door",
            "door_sprite.png",
            "A tall futuristic security exit door for a 2D side-view game, seen exactly from the front, closed, "
            "heavy graphite metal blast door with a hot red glowing frame, a red warning light strip on top and a red "
            "lock indicator. Portrait proportions, roughly twice as tall as wide. Stylized clean game art with crisp "
            "outlines. " + STYLE + CHROMA_RULE,
            "3:4",
            chroma=True,
        ),
        AssetSpec(
            "terminal",
            "terminal_sprite.png",
            "A compact futuristic control terminal for a 2D side-view game, seen exactly from the front: a dark "
            "graphite pedestal console with a bright electric-blue holographic screen and small blue indicator lights. "
            "Stylized clean game art with crisp outlines. " + STYLE + CHROMA_RULE,
            "3:4",
            chroma=True,
        ),
    ]
}

TEXTURE_RULE = (
    " Seamless tileable square texture, perfectly flat orthographic view straight on, evenly lit with no shadows, "
    "no perspective, no vignette, no text, no logos, edges tile seamlessly."
)

ASSETS.update(
    {
        spec.id: spec
        for spec in [
            AssetSpec(
                "tex_wall",
                "tex_wall.png",
                "Sci-fi laboratory wall panel texture: dark graphite metal panels with thin seams, small bolts, "
                "subtle cyan light slits and faint scratches, premium AAA game material." + TEXTURE_RULE,
                "1:1",
            ),
            AssetSpec(
                "tex_floor",
                "tex_floor.png",
                "Sci-fi laboratory floor texture: dark brushed steel floor tiles with a fine grid, hazard-free, "
                "slight wear, thin inset cyan guide lines, premium AAA game material." + TEXTURE_RULE,
                "1:1",
            ),
            AssetSpec(
                "tex_holo",
                "tex_holo.png",
                "Futuristic holographic interface texture: glowing cyan circuit patterns, hexagon grid and data lines "
                "on pure black background, for an emissive hologram material." + TEXTURE_RULE,
                "1:1",
            ),
        ]
    }
)

HERO_STYLE = (
    "Premium stylized 3D video game character render, AAA hero splash-art quality, crisp details, glossy "
    "armor materials, cinematic rim lighting in electric cyan and hot pink, strong readable silhouette."
)

HERO_CHROMA_RULE = (
    " Entire body visible from helmet to boots, centered with generous margin. The character is isolated on a "
    "solid flat pure green background (#00FF00): no floor, no cast shadow, no glow spilling onto the background, "
    "no other objects, no text. Do not use any green color on the character."
)

# Home screen art: two personalities of the same hero plus a menu backdrop (Nano Banana image model).
ASSETS.update(
    {
        spec.id: spec
        for spec in [
            AssetSpec(
                "hero_serious",
                "hero_serious.png",
                "Full-body hero key art. Keep EXACTLY the character design of the reference image: sleek WHITE armored "
                "hazmat-style suit with electric-cyan trim, rounded white helmet with a large dark glass visor, small "
                "backpack unit. Upgrade the render to: " + HERO_STYLE + " Add a few thin neon pink accent light lines on "
                "the suit. The dark visor works as a digital face and displays two glowing cyan LED eyes. Pose: standing "
                "tall and proud like a starship captain or a general, chest out, shoulders back, boots planted apart, left "
                "arm straight along the body with a clenched fist, right hand giving a crisp, clean military salute with "
                "fingers together at the edge of the helmet. The LED eyes are narrowed and angled into a focused, "
                "determined, charismatic and slightly intimidating heroic expression, ready for an important mission. "
                "Three-quarter front view facing the viewer." + HERO_CHROMA_RULE,
                "3:4",
                chroma=True,
                reference="player",
            ),
            AssetSpec(
                "hero_fun",
                "hero_fun.png",
                "Same character as the reference image: identical suit, helmet, colors, pink accent lights, proportions "
                "and 3D render style. Now in a hilarious, exaggerated, playful pose: body leaning sideways with the hip "
                "popped out, one knee lifted, both hands raised next to the helmet making peace-sign V gestures (two "
                "fingers up, like saying 'cheese' for a photo). The visor LED face shows a goofy funny expression: one eye "
                "winking as a ^ shape, the other eye a big sparkling star, and a huge glowing LED grin with a cheeky "
                "tongue sticking out. Cartoon energy and squash-and-stretch attitude while staying premium 3D. "
                "Three-quarter front view facing the viewer." + HERO_CHROMA_RULE,
                "3:4",
                chroma=True,
                reference="hero_serious",
            ),
            AssetSpec(
                "player_confused",
                "player_confused.png",
                "Redraw EXACTLY the same character from the reference image, same white suit, cyan trim, helmet, "
                "backpack, proportions and art style, strict side view facing right. Funny confused 'what are you doing?' "
                "pose: standing completely still in an unmistakable sports-coach stance, torso bent far forward about "
                "45 degrees, both arms reaching DOWN, both palms firmly resting directly ON THE KNEECAPS (not hips, "
                "not waist), elbows angled sharply outward like triangles, helmet tilted to one side. "
                "The cyan visor eyes are uneven: one eyebrow raised high and the other narrowed, expressing disbelief and "
                "judgement. It should read as a clear comedic pause in a 2D game. Clean crisp outlines, no text." + CHROMA_RULE,
                "1:1",
                chroma=True,
                reference="player",
            ),
            AssetSpec(
                "home_bg",
                "home_bg.png",
                "Wide cinematic 16:9 background for the main menu of a futuristic sci-fi video game set in a secret AI "
                "voice laboratory: a vast dark high-tech lab hall with a glossy reflective black floor, towering server "
                "pillars, holographic screens and floating interface panels in the distance, cables, neon light strips in "
                "electric cyan and hot pink, volumetric light beams cutting through thin fog, strong depth and perspective, "
                "premium AAA game menu atmosphere. Composition: keep the lower center and center-right area open and "
                "uncluttered because a character will be composited there, keep the left third darker for a title. "
                "No characters, no people, no text, no logos.",
                "16:9",
            ),
        ]
    }
)

# ── In-game art for the Unity side-view game (Nano Banana) ────────────────────
# Every room backdrop uses the home-page backdrop as its style reference, every robot pose uses the
# saluting hero, so the whole game looks like the home page.

ROOM_RULE = (
    " Same art direction, lighting quality and realism as the reference image (dark premium high-tech laboratory, "
    "glossy reflective black floor, neon light strips, volumetric light and thin fog, AAA game concept art). "
    "STRICT SIDE VIEW for a 2D side-scrolling game: the camera looks straight at the side wall of a long corridor-like "
    "chamber, perpendicular to it, eye level, no vanishing point towards the viewer. A flat, continuous, empty walkable "
    "floor runs across the ENTIRE width of the image; the top edge of that floor is a straight horizontal line at "
    "exactly 18 percent of the image height from the bottom. Everything interesting is on the back wall, in the middle "
    "band of the image. The floor must stay completely clear: no obstacles, no doors, no holes, no steps. The left and "
    "right edges continue naturally as if the chamber extends beyond the frame. No characters, no people, no robots, "
    "no text, no letters, no logos, no UI."
)

ROOMS = {
    "room_a": "Chamber: a sterile decontamination airlock, white and graphite wall panels, rows of cyan light strips, "
              "sealed round hatches and pressure gauges on the back wall, soft steam near the floor. Dominant light: electric cyan.",
    "room_b": "Chamber: a high-security hall, armored dark wall panels with red warning beacons, hazard stripes, "
              "security cameras and laser emitter slots on the back wall. Dominant light: hot red with a little cyan.",
    "room_c": "Chamber: an acoustic research wing, back wall covered with soundproof foam wedge panels, giant speaker "
              "cones and floating sound-wave holograms. Dominant light: soft violet and magenta.",
    "room_d": "Chamber: a vast chrono hall, giant brass and gold clockwork gears and hourglass holograms behind glass on "
              "the back wall, floating clock hands. Dominant light: warm amber and gold.",
    "room_e": "Chamber: a resonance vault, tall crystal resonator pillars and huge tuning forks along the back wall, "
              "glowing musical staff holograms. Dominant light: deep blue with cyan highlights.",
    "room_f": "Chamber: an echo chamber, back wall of mirror glass panels reflecting faint holographic ghost silhouettes "
              "of a figure repeated many times. Dominant light: hot pink and magenta.",
    "room_g": "Chamber: an architect forge, huge industrial fabrication bay with robotic arms, scaffolding, glowing "
              "blueprint wireframe holograms and molten orange glow on the back wall. Dominant light: orange with cyan.",
    "room_h": "Chamber: the AI core, a colossal glowing reactor core sphere with data streams and server towers on the "
              "back wall. Dominant light: emerald green and cyan.",
}

ROBOT_RULE = (
    "Same character as the reference image: identical glossy WHITE suit, electric-cyan trim lines, thin hot-pink accent "
    "lines, rounded white helmet with a dark glass visor showing two glowing cyan LED eyes, grey backpack with a hose, "
    "grey knee pads, belt with red lights, identical proportions and identical premium 3D render style and lighting. "
    "STRICT SIDE PROFILE VIEW facing RIGHT (we see his right side, nose of the helmet pointing to the right edge), "
    "full body, boots touching the bottom margin. Pose: "
)

PROP_RULE = (
    " Premium realistic 3D video game asset render, same art direction as a dark neon cyber laboratory (graphite metal, "
    "electric cyan and hot pink accents), crisp details, seen from the side for a 2D side-scrolling game."
)

ROBOT_POSES = {
    "robot_idle": "standing upright and relaxed, arms along the body, looking ahead to the right.",
    "robot_run_a": "running to the right, mid-stride: left leg forward, right leg back, arms bent and swinging, slight forward lean.",
    "robot_run_b": "running to the right, the opposite mid-stride: right leg forward, left leg back, arms bent and swinging, slight forward lean.",
    "robot_jump": "jumping to the right in mid-air, knees tucked up high, arms raised forward, dynamic.",
    "robot_crouch": "crouching very low to sneak under something, knees fully bent, torso bent forward, helmet low, compact silhouette.",
    "robot_confused": "funny 'what are you doing?' coach stance: torso bent forward, both palms resting on the knees, elbows "
                      "out, helmet tilted towards the viewer, LED eyes uneven showing disbelief.",
}

PROPS = {
    "prop_laser_low": ("1:1", "A floor-mounted security laser barrier: a low heavy metal emitter base lying on the ground "
                              "with a dense vertical curtain of bright hot-red laser light rising from it to about knee "
                              "height (the red light occupies only the lower 45 percent of the image), glowing sparks."),
    "prop_laser_high": ("3:4", "A slim tall metal security pylon standing on the ground with a glowing red lens at chest "
                               "height emitting a bright horizontal hot-red laser beam that extends to both sides of the "
                               "pylon at chest height; completely empty space under the beam."),
    "prop_glass": ("9:16", "A tall narrow barrier of thick violet-tinted sonic glass in a heavy graphite metal frame, "
                           "floor to ceiling, faint violet resonance waves glowing inside the glass."),
    "prop_door": ("9:16", "A narrow tall closed sci-fi bulkhead door slab, heavy graphite metal with glowing cyan edge "
                          "lines, rivets and a small status light, floor to top."),
    "prop_vault": ("9:16", "A massive narrow resonance vault gate slab, dark metal with glowing blue concentric musical "
                           "rune circles and tuning-fork engravings, floor to top."),
    "prop_sentinel": ("1:1", "A ceiling-mounted acoustic sentinel security device hanging from a thin arm: a robotic "
                             "sensor head with a large microphone ear dish and one glowing amber eye."),
    "prop_sweeper": ("1:1", "A squat heavy golden time-sweeper turret standing on the ground: a round metal pylon with a "
                            "glowing amber ring and a rotating laser emitter head on top."),
    "prop_plate": ("16:9", "A round flat pressure plate floor pad seen from the side at a slight angle, graphite metal "
                           "with a glowing hot-pink ring."),
    "prop_terminal": ("3:4", "A futuristic core access terminal console standing on the ground with a large glowing "
                             "green holographic screen and a keypad."),
    "prop_portal": ("1:1", "A large circular exit portal gate standing on the ground: a thick metal ring filled with "
                           "swirling bright green and cyan energy."),
    "prop_pod": ("3:4", "A cylindrical teleport start pod: a glass tube standing on the ground between a base and a cap, "
                        "both with glowing cyan rings, light beam inside."),
}

ASSETS.update(
    {spec.id: spec for spec in (
        [AssetSpec(rid, f"game/{rid}.png", desc + ROOM_RULE, "16:9", reference="home_bg") for rid, desc in ROOMS.items()]
        + [AssetSpec(pid, f"game/{pid}.png", ROBOT_RULE + pose + HERO_CHROMA_RULE, "3:4", chroma=True, reference="hero_serious")
           for pid, pose in ROBOT_POSES.items()]
        + [AssetSpec(pid, f"game/{pid}.png", desc + PROP_RULE + CHROMA_RULE, ratio, chroma=True)
           for pid, (ratio, desc) in PROPS.items()]
    )}
)

GAME_IDS = [*ROOMS, *ROBOT_POSES, *PROPS]

FALLBACK_THEME = {
    "art_direction": "premium sci-fi voice maze",
    "image_model": "fallback",
    "image_url": None,
    "palette": {
        "void": "#04070d",
        "panel": "#0b1220",
        "cyan": "#40e6ff",
        "red": "#ff345f",
        "violet": "#8b5cff",
        "amber": "#f7c86a",
    },
    "prompt": DESIGN_PROMPT,
}

_locks: dict[str, asyncio.Lock] = {}
_image_slots = asyncio.Semaphore(4)  # stay under the image API rate limit when painting many assets at once


def asset_path(spec: AssetSpec) -> Path:
    return ASSET_DIR / spec.filename


def asset_manifest(errors: dict[str, str] | None = None) -> dict:
    settings = get_settings()
    return {
        **FALLBACK_THEME,
        "image_model": settings.gemini_image_model,
        "image_url": "/assets/echoshift_lab_keyart.png" if HERO_ASSET.exists() else None,
        "assets": {
            spec.id: {
                "url": f"/assets/{spec.filename}" if asset_path(spec).exists() else None,
                "chroma": spec.chroma,
                "aspect_ratio": spec.aspect_ratio,
            }
            for spec in ASSETS.values()
        },
        "errors": errors or {},
    }


async def generate_asset(spec: AssetSpec, force: bool = False) -> str | None:
    """Generate one asset with Gemini. Returns an error string, or None on success/cache hit."""
    settings = get_settings()
    path = asset_path(spec)
    lock = _locks.setdefault(spec.id, asyncio.Lock())
    async with lock:
        if path.exists() and not force:
            return None
        if not settings.gemini_api_key:
            return "Gemini API key missing."

        path.parent.mkdir(parents=True, exist_ok=True)
        if spec.reference:
            ref_error = await generate_asset(ASSETS[spec.reference])
            if ref_error:
                return f"Reference asset '{spec.reference}' unavailable: {ref_error}"

        url = f"https://generativelanguage.googleapis.com/v1beta/models/{settings.gemini_image_model}:generateContent"
        parts: list[dict] = []
        if spec.reference:
            ref_path = asset_path(ASSETS[spec.reference])
            parts.append({"inlineData": {"mimeType": guess_mime(ref_path), "data": base64.b64encode(ref_path.read_bytes()).decode()}})
        parts.append({"text": spec.prompt})
        payload = {
            "contents": [{"parts": parts}],
            "generationConfig": {
                "responseModalities": ["IMAGE"],
                "imageConfig": {"aspectRatio": spec.aspect_ratio},
            },
        }
        headers = {"x-goog-api-key": settings.gemini_api_key, "Content-Type": "application/json"}

        try:
            async with _image_slots, httpx.AsyncClient(timeout=180) as client:
                response = await client.post(url, headers=headers, json=payload)
                response.raise_for_status()
                data = response.json()
        except Exception as exc:
            logger.warning("Gemini image generation failed for %s: %s", spec.id, exc)
            return "Image generation failed."

        image_data = extract_image_data(data)
        if not image_data:
            logger.warning("Gemini image response for %s did not include image data.", spec.id)
            return "Image response empty."

        path.write_bytes(base64.b64decode(image_data))
        logger.info("Generated asset %s -> %s", spec.id, path.name)
        return None


async def generate_assets(ids: list[str] | None = None, force: bool = False) -> dict:
    specs = [ASSETS[i] for i in (ids or list(ASSETS)) if i in ASSETS]
    results = await asyncio.gather(*(generate_asset(spec, force=force) for spec in specs))
    errors = {spec.id: err for spec, err in zip(specs, results) if err}
    return asset_manifest(errors)


CUSTOM_DIR = ASSET_DIR / "custom"


async def generate_custom_texture(material: str) -> dict:
    """Paints a seamless texture for an object the player described with words (cached per description)."""
    description = " ".join(material.lower().split())[:60]
    if not description:
        return {"url": None, "error": "Empty material description."}
    digest = hashlib.sha1(description.encode("utf-8")).hexdigest()[:16]
    spec = AssetSpec(
        f"custom_{digest}",
        f"custom/{digest}.png",
        f"Game material texture of {description}, stylized premium sci-fi video game look, rich detail." + TEXTURE_RULE,
        "1:1",
    )
    (ASSET_DIR / "custom").mkdir(parents=True, exist_ok=True)
    error = await generate_asset(spec)
    if error:
        return {"url": None, "error": error}
    return {"url": f"/assets/{spec.filename}", "material": description}


HERO_META = ASSET_DIR / "hero_meta.json"
HOME_IDS = ["home_bg", "hero_serious", "hero_fun"]
_meta_lock = asyncio.Lock()

FACE_PROMPT = (
    "The image shows a full-body helmeted character whose dark visor displays glowing LED eyes. Return JSON only: "
    '{"visor": [ymin, xmin, ymax, xmax], "eyes": [ymin, xmin, ymax, xmax]} with coordinates normalized to 0-1000. '
    "visor = the whole visor glass. eyes = the smallest box containing both glowing LED eyes. Use null if not visible."
)


def _valid_box(box) -> list[int] | None:
    if not isinstance(box, list) or len(box) != 4:
        return None
    try:
        ymin, xmin, ymax, xmax = (int(round(float(v))) for v in box)
    except (TypeError, ValueError):
        return None
    if not (0 <= ymin < ymax <= 1000 and 0 <= xmin < xmax <= 1000):
        return None
    return [ymin, xmin, ymax, xmax]


async def detect_face_boxes(asset_id: str) -> dict:
    """Asks Gemini (vision) where the visor and LED eyes are so the home screen can make the hero blink. Cached per file."""
    path = asset_path(ASSETS[asset_id])
    if not path.exists():
        return {}
    stamp = int(path.stat().st_mtime)
    async with _meta_lock:
        meta = json.loads(HERO_META.read_text(encoding="utf-8")) if HERO_META.exists() else {}
        cached = meta.get(asset_id)
        if cached and cached.get("stamp") == stamp:
            return cached
        settings = get_settings()
        if not settings.gemini_api_key:
            return {}
        try:
            from google import genai
            from google.genai import types

            client = genai.Client(api_key=settings.gemini_api_key)
            response = await client.aio.models.generate_content(
                model=settings.gemini_model,
                contents=[types.Part.from_bytes(data=path.read_bytes(), mime_type=guess_mime(path)), FACE_PROMPT],
                config=types.GenerateContentConfig(response_mime_type="application/json", temperature=0),
            )
            data = json.loads(response.text or "{}")
        except Exception as exc:
            logger.warning("Face detection failed for %s: %s", asset_id, exc)
            return {}
        result = {"stamp": stamp, "visor": _valid_box(data.get("visor")), "eyes": _valid_box(data.get("eyes"))}
        meta[asset_id] = result
        HERO_META.write_text(json.dumps(meta, indent=2), encoding="utf-8")
        return result


def _cached_face_boxes(asset_id: str, path: Path) -> dict:
    if not HERO_META.exists():
        return {}
    cached = json.loads(HERO_META.read_text(encoding="utf-8")).get(asset_id) or {}
    return cached if cached.get("stamp") == int(path.stat().st_mtime) else {}


async def home_payload(ensure: bool) -> dict:
    """Everything the web home screen needs: backdrop, both hero personalities and their face boxes."""
    errors: dict[str, str] = {}
    if ensure:
        errors = (await generate_assets(HOME_IDS)).get("errors", {})
    heroes: dict[str, dict | None] = {}
    for key, asset_id in (("serious", "hero_serious"), ("fun", "hero_fun")):
        path = asset_path(ASSETS[asset_id])
        if not path.exists():
            heroes[key] = None
            continue
        boxes = await detect_face_boxes(asset_id) if ensure else _cached_face_boxes(asset_id, path)
        stamp = int(path.stat().st_mtime)
        heroes[key] = {
            "url": f"/assets/{ASSETS[asset_id].filename}?v={stamp}",
            "visor": boxes.get("visor"),
            "eyes": boxes.get("eyes"),
        }
    bg = asset_path(ASSETS["home_bg"])
    return {
        "image_model": get_settings().gemini_image_model,
        "background": f"/assets/{ASSETS['home_bg'].filename}?v={int(bg.stat().st_mtime)}" if bg.exists() else None,
        "heroes": heroes,
        "errors": errors,
    }


async def generate_key_art(force: bool = False) -> dict:
    error = await generate_asset(ASSETS["keyart"], force=force)
    return asset_manifest({"keyart": error} if error else None)


def theme_payload(model: str | None = None) -> dict:
    manifest = asset_manifest()
    if model:
        manifest["image_model"] = model
    return manifest


def guess_mime(path: Path) -> str:
    head = path.read_bytes()[:4]
    return "image/png" if head[1:4] == b"PNG" else "image/jpeg"


def extract_image_data(data: dict) -> str | None:
    direct = data.get("output_image")
    if isinstance(direct, dict) and isinstance(direct.get("data"), str):
        return direct["data"]

    interaction = data.get("interaction")
    if isinstance(interaction, dict):
        image = interaction.get("outputImage") or interaction.get("output_image")
        if isinstance(image, dict) and isinstance(image.get("data"), str):
            return image["data"]

    candidates = data.get("candidates")
    if isinstance(candidates, list):
        for candidate in candidates:
            parts = candidate.get("content", {}).get("parts", []) if isinstance(candidate, dict) else []
            for part in parts:
                inline = part.get("inlineData") or part.get("inline_data")
                if isinstance(inline, dict) and isinstance(inline.get("data"), str):
                    return inline["data"]

    steps = data.get("steps")
    if isinstance(steps, list):
        for step in steps:
            content = step.get("content") if isinstance(step, dict) else None
            if not isinstance(content, list):
                continue
            for item in content:
                if not isinstance(item, dict):
                    continue
                if item.get("type") == "image" and isinstance(item.get("data"), str):
                    return item["data"]

    return None
