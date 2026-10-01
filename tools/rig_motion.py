"""Motion templates for the cutout rigs. Everything is authored here once and consumed twice:
rigkit.preview (contact sheets) and Unity's RigBuilder (AnimationClips), so what is checked is what ships.

Conventions (Unity): angles in degrees, counter-clockwise positive; positions in art pixels, y up; seconds.
A clip is {"length": s, "loop": bool, "curves": {part: {"rot": [[t, deg]], "pos": [[t, dx, dy]], "scale": [[t, sx, sy]]}}}.
"""
import math


def keys(period, values, phase=0.0):
    """Evenly spaced keys over one period; values[i] at t = period * i / len(values), closed loop."""
    n = len(values)
    out = []
    for i in range(n + 1):
        t = period * i / n
        v = values[(i + int(round(phase * n))) % n]
        out.append([round(t, 4)] + (list(v) if isinstance(v, (tuple, list)) else [v]))
    return out


def wave(period, amp, phase=0.0, offset=0.0, steps=8):
    return [[round(period * i / steps, 4), round(offset + amp * math.sin(2 * math.pi * (i / steps + phase)), 3)] for i in range(steps + 1)]


def wave2(period, ax, ay, phase=0.0, steps=8, ox=0.0, oy=0.0, double=False):
    f = 2 if double else 1
    return [[round(period * i / steps, 4), round(ox + ax * math.sin(2 * math.pi * (f * i / steps + phase)), 3),
             round(oy + ay * math.sin(2 * math.pi * (f * i / steps + phase)), 3)] for i in range(steps + 1)]


def bob(period, amp, phase=0.0, steps=8):
    """Vertical bob that peaks twice per stride (lowest at both contacts)."""
    return [[round(period * i / steps, 4), 0.0, round(amp * abs(math.sin(2 * math.pi * (i / steps + phase))), 3)] for i in range(steps + 1)]


def track(*pairs):
    """track((t, value), ...) -> keys list."""
    return [[round(t, 4)] + (list(v) if isinstance(v, (tuple, list)) else [v]) for t, v in pairs]


def add(curves, part, kind, k):
    if part is None:
        return
    curves.setdefault(part, {})[kind] = k


# ---------------------------------------------------------------- humanoid

def humanoid(p):
    """p: dict with part names (legF, legB, hips, torso, head, armF, armB, weapon, cape, skirt, shield)
    and tuning (stride, arm_swing, bob, lean, walk_period, attack=dict(...))."""
    P = lambda k: p.get(k)
    clips = {}

    # ---- idle: breathing, cloth and weapon settle
    T = p.get("idle_period", 1.8)
    c = {}
    add(c, P("torso"), "pos", track((0, (0, 0)), (T / 2, (0, p.get("breath", 1))), (T, (0, 0))))
    add(c, P("head"), "pos", track((0, (0, 0)), (T * 0.55, (0, p.get("breath", 1))), (T, (0, 0))))
    add(c, P("head"), "rot", wave(T, 1.2, 0.1))
    add(c, P("armF"), "rot", wave(T, p.get("idle_arm", 2.0), 0.25, p.get("armF_rest", 0)))
    add(c, P("armB"), "rot", wave(T, p.get("idle_arm", 2.0), 0.75, p.get("armB_rest", 0)))
    add(c, P("weapon"), "rot", wave(T, 1.5, 0.4))
    add(c, P("cape"), "rot", wave(T, p.get("cape_idle", 2.5), 0.3))
    add(c, P("skirt"), "rot", wave(T, 1.2, 0.6))
    add(c, P("shield"), "rot", wave(T, 1.0, 0.5))
    clips["Idle"] = {"length": T, "loop": True, "curves": c}

    # ---- walk: legs alternate at the hips, opposite arm follows, body bobs, cloth trails
    T = p.get("walk_period", 0.9)
    A = p.get("stride", 22)
    B = p.get("arm_swing", 14)
    lift = p.get("lift", 1.5)
    c = {}
    add(c, P("legF"), "rot", keys(T, [A, A * 0.35, -A, -A * 0.2, A * 0.6, A]))
    add(c, P("legB"), "rot", keys(T, [-A, -A * 0.2, A * 0.6, A, A * 0.35, -A]))
    # the leg moving forward is in the air: lift it
    add(c, P("legF"), "pos", keys(T, [(0, 0), (0, 0), (0, 0), (0, lift), (0, lift * 0.6), (0, 0)]))
    add(c, P("legB"), "pos", keys(T, [(0, 0), (0, lift), (0, lift * 0.6), (0, 0), (0, 0), (0, 0)]))
    add(c, P("hips"), "pos", bob(T, p.get("bob", 1.5)))
    add(c, P("torso"), "rot", wave(T, 1.2, 0.0, -p.get("lean", 3), steps=8))
    add(c, P("head"), "rot", wave(T, 1.0, 0.5, p.get("lean", 3) * 0.6))
    add(c, P("armF"), "rot", wave(T, B * p.get("armF_swing", 1.0), 0.5, p.get("armF_rest", 0)))
    add(c, P("armB"), "rot", wave(T, B * p.get("armB_swing", 1.0), 0.0, p.get("armB_rest", 0)))
    add(c, P("weapon"), "rot", wave(T, p.get("weapon_sway", 3), 0.65))
    add(c, P("cape"), "rot", wave(T, p.get("cape_walk", 4), 0.2, p.get("cape_trail", 4), steps=8))
    add(c, P("skirt"), "rot", wave(T, 3.0, 0.1))
    add(c, P("shield"), "rot", wave(T, 2.0, 0.3))
    clips["Walk"] = {"length": T, "loop": True, "curves": c}

    # ---- attack: anticipation -> raise -> strike -> impact hold -> recovery
    a = dict(length=0.8, wind=-120, wind_weapon=-50, strike=35, strike_weapon=15, lean_back=7, lean_fwd=-11,
             t_wind=0.3, t_strike=0.42, t_hold=0.52, lunge=2.5, arm="armF")
    a.update(p.get("attack", {}))
    L = a["length"]
    tw, ts, th = a["t_wind"] * L / 0.8, a["t_strike"] * L / 0.8, a["t_hold"] * L / 0.8
    c = {}
    arm = P(a["arm"])
    rest = p.get(a["arm"] + "_rest", 0)
    add(c, arm, "rot", track((0, rest), (tw * 0.45, rest + a["wind"] * 0.55), (tw, rest + a["wind"]), (tw + (ts - tw) * 0.3, rest + a["wind"] * 0.95),
                             (ts, rest + a["strike"]), (th, rest + a["strike"] * 1.08), (L, rest)))
    add(c, P("weapon"), "rot", track((0, 0), (tw, a["wind_weapon"]), (ts, a["strike_weapon"]), (th, a["strike_weapon"]), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (tw, a["lean_back"]), (ts, a["lean_fwd"]), (th, a["lean_fwd"]), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (tw, a["lean_back"] * 0.4), (ts, a["lean_fwd"] * 0.3), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (tw, (-1, 0)), (ts, (a["lunge"], -1)), (th, (a["lunge"], -1)), (L, (0, 0))))
    other = P("armB" if a["arm"] == "armF" else "armF")
    add(c, other, "rot", track((0, 0), (tw, 12), (ts, -18), (L, 0)))
    add(c, P("legF"), "rot", track((0, 0), (tw, -4), (ts, 10), (th, 10), (L, 0)))
    add(c, P("legB"), "rot", track((0, 0), (tw, 4), (ts, -8), (th, -8), (L, 0)))
    add(c, P("cape"), "rot", track((0, 0), (tw, -4), (ts, 9), (th + 0.08, 6), (L, 0)))
    add(c, P("shield"), "rot", track((0, 0), (tw, 6), (ts, -6), (L, 0)))
    clips["Attack"] = {"length": L, "loop": False, "curves": c, "impact": round(ts, 3)}

    # ---- hit: torso recoils, head snaps, arms flail
    L = 0.32
    c = {}
    add(c, P("torso"), "rot", track((0, 0), (0.06, 10), (0.18, 4), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.07, 8), (0.2, 2), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.06, (-2.5, 0)), (L, (0, 0))))
    add(c, P("armF"), "rot", track((0, p.get("armF_rest", 0)), (0.07, p.get("armF_rest", 0) + 12), (L, p.get("armF_rest", 0))))
    add(c, P("armB"), "rot", track((0, p.get("armB_rest", 0)), (0.07, p.get("armB_rest", 0) - 10), (L, p.get("armB_rest", 0))))
    add(c, P("cape"), "rot", track((0, 0), (0.08, -6), (L, 0)))
    clips["Hit"] = {"length": L, "loop": False, "curves": c}

    # ---- death: knees buckle, falls backwards, weapon drops; last pose holds
    L = 1.1
    fall = p.get("fall", 1)          # 1 = falls backwards, -1 forwards
    drop = p.get("death_drop", 10)
    c = {}
    add(c, P("legF"), "rot", track((0, 0), (0.25, 25), (0.6, 70 * fall), (L, 80 * fall)))
    add(c, P("legB"), "rot", track((0, 0), (0.25, -15), (0.6, 60 * fall), (L, 75 * fall)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.25, (-1, -drop * 0.3)), (0.6, (-4 * fall, -drop)), (L, (-6 * fall, -drop * 1.15))))
    add(c, P("hips"), "rot", track((0, 0), (0.3, 5 * fall), (0.65, 55 * fall), (L, 80 * fall)))
    add(c, P("torso"), "rot", track((0, 0), (0.2, -8), (0.6, 12 * fall), (L, 8 * fall)))
    add(c, P("head"), "rot", track((0, 0), (0.3, -10), (0.7, 15 * fall), (L, 20 * fall)))
    add(c, P("armF"), "rot", track((0, 0), (0.3, 30), (0.7, 70), (L, 85)))
    add(c, P("armB"), "rot", track((0, 0), (0.3, -20), (0.7, 40), (L, 55)))
    add(c, P("weapon"), "rot", track((0, 0), (0.35, 20), (0.7, 60), (L, 70)))
    add(c, P("cape"), "rot", track((0, 0), (0.4, 12), (L, 20 * fall)))
    add(c, P("shield"), "rot", track((0, 0), (0.4, -30), (L, -60)))
    clips["Death"] = {"length": L, "loop": False, "curves": c}

    # ---- spawn: rises out of the ground (scaled from the feet, code fades the colour)
    L = 0.7
    c = {"_root": {"scale": track((0, (1.25, 0.15)), (0.35, (0.9, 0.85)), (0.55, (1.05, 1.05)), (L, (1, 1))),
                   "pos": track((0, (0, -2)), (L, (0, 0)))}}
    add(c, P("armF"), "rot", track((0, 60), (0.45, 30), (L, p.get("armF_rest", 0))))
    add(c, P("armB"), "rot", track((0, 50), (0.45, 20), (L, p.get("armB_rest", 0))))
    add(c, P("head"), "rot", track((0, 15), (L, 0)))
    clips["Spawn"] = {"length": L, "loop": False, "curves": c}

    if p.get("cast"):
        clips["Cast"] = cast_clip(p)
    if p.get("special"):
        clips["Special"] = special_clip(p)
    if p.get("intro"):
        clips["Intro"] = intro_clip(p)
    return clips


def cast_clip(p):
    P = lambda k: p.get(k)
    L = p.get("cast_length", 0.9)
    c = {}
    add(c, P("armF"), "rot", track((0, 0), (0.3, 95), (0.62, 105), (L, 0)))
    add(c, P("armB"), "rot", track((0, 0), (0.3, 70), (0.62, 80), (L, 0)))
    add(c, P("weapon"), "rot", track((0, 0), (0.3, -20), (0.62, -25), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (0.3, 5), (0.62, 6), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.3, 6), (L, 0)))
    add(c, P("skirt"), "rot", track((0, 0), (0.35, 4), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.3, (0, 1.5)), (0.62, (0, 2)), (L, (0, 0))))
    return {"length": L, "loop": False, "curves": c, "impact": 0.5}


def special_clip(p):
    """Overhead slam: weapon all the way back, body coils, crashes into the ground."""
    P = lambda k: p.get(k)
    s = dict(length=1.3, wind=-165, slam=40, t_wind=0.55, t_slam=0.72, arm="armF")
    s.update(p.get("special", {}))
    L = s["length"]
    tw, ts = s["t_wind"], s["t_slam"]
    c = {}
    add(c, P(s["arm"]), "rot", track((0, 0), (tw * 0.7, s["wind"] * 0.9), (tw, s["wind"]), (ts, s["slam"]), (ts + 0.25, s["slam"]), (L, 0)))
    add(c, P("weapon"), "rot", track((0, 0), (tw, -35), (ts, 25), (ts + 0.25, 25), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (tw, 11), (ts, -16), (ts + 0.25, -14), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (tw, 8), (ts, -6), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (tw, (-2, 2)), (ts, (3, -4)), (ts + 0.25, (3, -4)), (L, (0, 0))))
    add(c, P("legF"), "rot", track((0, 0), (tw, -6), (ts, 16), (ts + 0.25, 16), (L, 0)))
    add(c, P("legB"), "rot", track((0, 0), (tw, 6), (ts, -14), (ts + 0.25, -14), (L, 0)))
    other = P("armB" if s["arm"] == "armF" else "armF")
    add(c, other, "rot", track((0, 0), (tw, 25), (ts, -25), (L, 0)))
    add(c, P("cape"), "rot", track((0, 0), (tw, -8), (ts, 14), (L, 0)))
    return {"length": L, "loop": False, "curves": c, "impact": ts}


def intro_clip(p):
    """Boss entrance: plants its feet, raises the weapon and roars (head back)."""
    P = lambda k: p.get(k)
    L = 2.2
    c = {}
    add(c, P("armF"), "rot", track((0, 0), (0.6, -150), (1.6, -155), (L, 0)))
    add(c, P("weapon"), "rot", track((0, 0), (0.6, -30), (1.6, -32), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (0.6, 9), (1.0, 12), (1.6, 10), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.7, 14), (1.0, 18), (1.6, 14), (L, 0)))
    add(c, P("armB"), "rot", track((0, 0), (0.6, 35), (1.6, 40), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.6, (0, 1)), (1.0, (0, 2)), (1.6, (0, 1)), (L, (0, 0))))
    add(c, P("cape"), "rot", track((0, 0), (0.8, -10), (1.2, -6), (1.6, -10), (L, 0)))
    return {"length": L, "loop": False, "curves": c, "impact": 1.0}


# ---------------------------------------------------------------- quadruped (ghoul hound, hound rig states)

def quadruped(p):
    P = lambda k: p.get(k)
    clips = {}
    T = p.get("idle_period", 1.4)
    c = {}
    add(c, P("body"), "pos", track((0, (0, 0)), (T / 2, (0, 1)), (T, (0, 0))))
    add(c, P("head"), "rot", wave(T, 3, 0.2))
    add(c, P("jaw"), "rot", wave(T, 2, 0.4, -1))
    add(c, P("tail"), "rot", wave(T * 0.5, 7, 0.0))
    for leg in ("legFN", "legFF", "legBN", "legBF"):
        add(c, P(leg), "rot", wave(T, 1.0, 0.3))
    clips["Idle"] = {"length": T, "loop": True, "curves": c}

    # trot: diagonal pairs (front-near + back-far) together
    T = p.get("walk_period", 0.6)
    A = p.get("stride", 26)
    c = {}
    add(c, P("legFN"), "rot", keys(T, [A, 0, -A, -A * 0.3, A * 0.7]))
    add(c, P("legBF"), "rot", keys(T, [A * 0.9, 0, -A * 0.9, -A * 0.3, A * 0.6]))
    add(c, P("legFF"), "rot", keys(T, [-A, -A * 0.3, A * 0.7, A, 0]))
    add(c, P("legBN"), "rot", keys(T, [-A * 0.9, -A * 0.3, A * 0.6, A * 0.9, 0]))
    add(c, P("legFN"), "pos", keys(T, [(0, 0), (0, 0), (0, 0), (0, 1.5), (0, 1)]))
    add(c, P("legFF"), "pos", keys(T, [(0, 0), (0, 1.5), (0, 1), (0, 0), (0, 0)]))
    add(c, P("body"), "pos", bob(T, p.get("bob", 1.5)))
    add(c, P("body"), "rot", wave(T, p.get("pitch", 2.5), 0.25, steps=8))
    add(c, P("head"), "rot", wave(T, 4, 0.75))
    add(c, P("tail"), "rot", wave(T, 10, 0.1, 6))
    clips["Walk"] = {"length": T, "loop": True, "curves": c}

    L = 0.55
    c = {}
    add(c, P("body"), "pos", track((0, (0, 0)), (0.18, (-3, -1)), (0.3, (7, 1)), (0.4, (6, 0)), (L, (0, 0))))
    add(c, P("body"), "rot", track((0, 0), (0.18, 6), (0.3, -7), (0.42, -4), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.18, 10), (0.3, -16), (0.42, -12), (L, 0)))
    add(c, P("jaw"), "rot", track((0, 0), (0.18, -6), (0.3, -28), (0.4, -4), (L, 0)))
    add(c, P("legFN"), "rot", track((0, 0), (0.18, -15), (0.3, 30), (L, 0)))
    add(c, P("legFF"), "rot", track((0, 0), (0.18, -10), (0.3, 25), (L, 0)))
    add(c, P("legBN"), "rot", track((0, 0), (0.18, 15), (0.3, -25), (L, 0)))
    add(c, P("legBF"), "rot", track((0, 0), (0.18, 12), (0.3, -20), (L, 0)))
    add(c, P("tail"), "rot", track((0, 0), (0.18, 15), (0.3, -10), (L, 0)))
    clips["Attack"] = {"length": L, "loop": False, "curves": c, "impact": 0.3}

    L = 0.3
    c = {}
    add(c, P("body"), "pos", track((0, (0, 0)), (0.06, (-3, 1)), (L, (0, 0))))
    add(c, P("body"), "rot", track((0, 0), (0.06, 8), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.07, 14), (L, 0)))
    add(c, P("tail"), "rot", track((0, 0), (0.07, -20), (L, 0)))
    clips["Hit"] = {"length": L, "loop": False, "curves": c}

    L = 1.0
    drop = p.get("death_drop", 6)
    c = {}
    add(c, P("body"), "pos", track((0, (0, 0)), (0.2, (-1, 1)), (0.55, (-2, -drop)), (L, (-2, -drop - 1))))
    add(c, P("body"), "rot", track((0, 0), (0.2, 8), (0.55, -10), (L, -12)))
    add(c, P("head"), "rot", track((0, 0), (0.2, 15), (0.55, -25), (L, -30)))
    add(c, P("jaw"), "rot", track((0, 0), (0.4, -15), (L, -10)))
    add(c, P("legFN"), "rot", track((0, 0), (0.5, 50), (L, 70)))
    add(c, P("legFF"), "rot", track((0, 0), (0.5, 40), (L, 60)))
    add(c, P("legBN"), "rot", track((0, 0), (0.5, -45), (L, -65)))
    add(c, P("legBF"), "rot", track((0, 0), (0.5, -35), (L, -55)))
    add(c, P("tail"), "rot", track((0, 0), (0.4, 20), (L, -10)))
    clips["Death"] = {"length": L, "loop": False, "curves": c}

    L = 0.6
    c = {"_root": {"scale": track((0, (1.3, 0.1)), (0.3, (0.9, 0.9)), (0.45, (1.05, 1.05)), (L, (1, 1)))}}
    clips["Spawn"] = {"length": L, "loop": False, "curves": c}
    return clips


# ---------------------------------------------------------------- robed casters (legs hidden: the hem walks)

def robed(p):
    P = lambda k: p.get(k)
    clips = {}
    T = 1.8
    c = {}
    add(c, P("torso"), "pos", track((0, (0, 0)), (T / 2, (0, 1)), (T, (0, 0))))
    add(c, P("head"), "rot", wave(T, 2, 0.2))
    add(c, P("armF"), "rot", wave(T, 3, 0.25))
    add(c, P("armB"), "rot", wave(T, 3, 0.75))
    add(c, P("hem"), "rot", wave(T, 1.5, 0.5))
    add(c, P("weapon"), "rot", wave(T, 2, 0.35))
    clips["Idle"] = {"length": T, "loop": True, "curves": c}

    T = p.get("walk_period", 0.85)
    c = {}
    add(c, P("hem"), "rot", wave(T, p.get("hem_swing", 6), 0.0))
    add(c, P("hem"), "scale", wave2(T, 0.04, -0.03, 0.25, double=True, ox=1.0, oy=1.0))
    add(c, P("hips"), "pos", bob(T, 1.2))
    add(c, P("torso"), "rot", wave(T, 2, 0.1, -3))
    add(c, P("head"), "rot", wave(T, 2, 0.6, 1))
    add(c, P("armF"), "rot", wave(T, p.get("arm_swing", 12), 0.5))
    add(c, P("armB"), "rot", wave(T, p.get("arm_swing", 12), 0.0))
    add(c, P("weapon"), "rot", wave(T, 4, 0.6))
    clips["Walk"] = {"length": T, "loop": True, "curves": c}

    L = 0.7
    c = {}
    add(c, P("armF"), "rot", track((0, 0), (0.25, 40), (0.36, -60), (0.5, -50), (L, 0)))
    add(c, P("weapon"), "rot", track((0, 0), (0.25, 20), (0.36, -30), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (0.25, 6), (0.36, -10), (0.5, -8), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.36, (2, 0)), (L, (0, 0))))
    add(c, P("hem"), "rot", track((0, 0), (0.36, -5), (L, 0)))
    clips["Attack"] = {"length": L, "loop": False, "curves": c, "impact": 0.36}

    clips["Cast"] = cast_clip(dict(p, hips=p.get("hips"), skirt=p.get("hem")))

    L = 0.3
    c = {}
    add(c, P("torso"), "rot", track((0, 0), (0.06, 12), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.07, 10), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.06, (-2, 0)), (L, (0, 0))))
    add(c, P("hem"), "rot", track((0, 0), (0.08, 6), (L, 0)))
    clips["Hit"] = {"length": L, "loop": False, "curves": c}

    L = 1.2
    c = {}
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.5, (0, -4)), (L, (-3, -10))))
    add(c, P("hips"), "rot", track((0, 0), (0.5, 8), (L, 70)))
    add(c, P("hem"), "scale", track((0, (1, 1)), (0.6, (1.15, 0.7)), (L, (1.3, 0.45))))
    add(c, P("torso"), "rot", track((0, 0), (0.4, -12), (L, 10)))
    add(c, P("head"), "rot", track((0, 0), (0.4, -15), (L, 20)))
    add(c, P("armF"), "rot", track((0, 0), (0.4, 50), (L, 80)))
    add(c, P("armB"), "rot", track((0, 0), (0.4, 40), (L, 60)))
    add(c, P("weapon"), "rot", track((0, 0), (0.5, 40), (L, 75)))
    clips["Death"] = {"length": L, "loop": False, "curves": c}

    L = 0.7
    c = {"_root": {"scale": track((0, (1.2, 0.1)), (0.4, (0.95, 0.95)), (L, (1, 1)))}}
    clips["Spawn"] = {"length": L, "loop": False, "curves": c}
    return clips


# ---------------------------------------------------------------- slime

def slime(p):
    P = lambda k: p.get(k)
    clips = {}
    T = 1.4
    c = {}
    add(c, P("body"), "scale", wave2(T, 0.05, -0.05, 0.0, ox=1, oy=1))
    add(c, P("eyes"), "pos", track((0, (0, 0)), (T / 2, (0, -1)), (T, (0, 0))))
    add(c, P("drip"), "pos", track((0, (0, 0)), (T / 2, (0, -1)), (T, (0, 0))))
    clips["Idle"] = {"length": T, "loop": True, "curves": c}
    T = p.get("walk_period", 0.7)
    c = {}
    add(c, P("body"), "scale", keys(T, [(1.12, 0.86), (0.92, 1.12), (0.96, 1.06), (1.06, 0.94)]))
    add(c, P("body"), "pos", keys(T, [(0, 0), (1, 3), (1, 2), (0, 0)]))
    add(c, P("eyes"), "pos", keys(T, [(0, -1), (1, 3), (1, 2), (0, 0)]))
    add(c, P("drip"), "pos", keys(T, [(0, 0), (0, 1), (0, 1), (0, 0)]))
    clips["Walk"] = {"length": T, "loop": True, "curves": c}
    L = 0.6
    c = {}
    add(c, P("body"), "scale", track((0, (1, 1)), (0.2, (0.85, 1.2)), (0.32, (1.35, 0.8)), (0.45, (1.1, 0.92)), (L, (1, 1))))
    add(c, P("body"), "pos", track((0, (0, 0)), (0.2, (-2, 1)), (0.32, (5, 0)), (L, (0, 0))))
    add(c, P("eyes"), "pos", track((0, (0, 0)), (0.2, (-2, 3)), (0.32, (6, -1)), (L, (0, 0))))
    clips["Attack"] = {"length": L, "loop": False, "curves": c, "impact": 0.32}
    L = 0.3
    c = {}
    add(c, P("body"), "scale", track((0, (1, 1)), (0.06, (1.2, 0.8)), (0.16, (0.92, 1.08)), (L, (1, 1))))
    add(c, P("eyes"), "pos", track((0, (0, 0)), (0.06, (0, -2)), (L, (0, 0))))
    clips["Hit"] = {"length": L, "loop": False, "curves": c}
    L = 0.9
    c = {}
    add(c, P("body"), "scale", track((0, (1, 1)), (0.15, (0.85, 1.25)), (0.4, (1.6, 0.35)), (L, (1.9, 0.18))))
    add(c, P("eyes"), "pos", track((0, (0, 0)), (0.15, (0, 4)), (0.4, (0, -8)), (L, (0, -11))))
    add(c, P("eyes"), "scale", track((0, (1, 1)), (0.4, (1, 0.6)), (L, (0.6, 0.2))))
    add(c, P("drip"), "scale", track((0, (1, 1)), (0.4, (1.6, 0.4)), (L, (2.0, 0.2))))
    clips["Death"] = {"length": L, "loop": False, "curves": c}
    L = 0.6
    c = {}
    add(c, P("body"), "scale", track((0, (0.2, 0.2)), (0.3, (1.2, 0.8)), (0.45, (0.95, 1.08)), (L, (1, 1))))
    add(c, P("eyes"), "scale", track((0, (0, 0)), (0.3, (0.6, 0.6)), (L, (1, 1))))
    clips["Spawn"] = {"length": L, "loop": False, "curves": c}
    return clips


# ---------------------------------------------------------------- commander: hand-keyed on top of the humanoid set

def commander(p):
    """Heavier, more readable motion for the main character (front and back views share part names)."""
    clips = humanoid(p)
    P = lambda k: p.get(k)
    back = p.get("back", False)

    T = 2.0
    c = {}
    add(c, P("torso"), "pos", track((0, (0, 0)), (T * 0.5, (0, 1)), (T, (0, 0))))
    add(c, P("torso"), "rot", wave(T, 0.8, 0.0))
    add(c, P("head"), "rot", wave(T, 1.6, 0.15))
    add(c, P("head"), "pos", track((0, (0, 0)), (T * 0.55, (0, 1)), (T, (0, 0))))
    add(c, P("armF"), "rot", wave(T, 2.0, 0.3))
    add(c, P("weapon"), "rot", wave(T, 1.6, 0.8))
    add(c, P("armB"), "rot", wave(T, 2.0, 0.7))
    add(c, P("skirt"), "rot", wave(T, 1.0, 0.5))
    add(c, P("cape"), "rot", [[round(T * i / 16, 4), round(3.0 * math.sin(2 * math.pi * i / 16) + 1.2 * math.sin(4 * math.pi * i / 16 + 1), 3)] for i in range(17)])
    clips["Idle"] = {"length": T, "loop": True, "curves": c}

    T = 0.9
    A = 25
    c = {}
    add(c, P("legF"), "rot", keys(T, [A, A * 0.3, -A * 0.9, -A * 0.25, A * 0.55, A]))
    add(c, P("legB"), "rot", keys(T, [-A * 0.9, -A * 0.25, A * 0.55, A, A * 0.3, -A * 0.9]))
    add(c, P("legF"), "pos", keys(T, [(0, 0), (0, 0), (0, 0), (0, 2), (0, 1.2), (0, 0)]))
    add(c, P("legB"), "pos", keys(T, [(0, 0), (0, 2), (0, 1.2), (0, 0), (0, 0), (0, 0)]))
    add(c, P("hips"), "pos", [[round(T * i / 8, 4), round(0.4 * math.sin(2 * math.pi * i / 8), 3), round(1.8 * abs(math.sin(2 * math.pi * i / 8)), 3)] for i in range(9)])
    add(c, P("torso"), "rot", wave(T, 1.6, 0.25, -4 if not back else 2))
    add(c, P("head"), "rot", wave(T, 1.2, 0.75, 2))
    add(c, P("armB"), "rot", wave(T, 15, 0.5))
    add(c, P("armF"), "rot", wave(T, 5, 0.0))
    add(c, P("weapon"), "rot", wave(T, 4, 0.2))
    add(c, P("skirt"), "rot", wave(T, 3, 0.1))
    add(c, P("cape"), "rot", [[round(T * i / 8, 4), round(6 + 5 * math.sin(2 * math.pi * (i / 8 - 0.2)), 3)] for i in range(9)])
    clips["Walk"] = {"length": T, "loop": True, "curves": c}

    # anticipation → raise → strike → impact hold → recovery (impact at 0.42 s of 0.8 s)
    L = 0.8
    c = {}
    a = p.get("arm_sign", 1)
    add(c, P("armF"), "rot", track((0, 0), (0.1, 25 * a), (0.3, 150 * a), (0.36, 125 * a), (0.42, -25 * a), (0.55, -30 * a), (L, 0)))
    add(c, P("weapon"), "rot", track((0, 0), (0.1, 5 * a), (0.3, 22 * a), (0.36, 12 * a), (0.42, -10 * a), (0.55, -12 * a), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (0.12, 4), (0.3, 8), (0.37, 2), (0.42, -12), (0.55, -13), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.3, 4), (0.42, -5), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.12, (0, -1.5)), (0.3, (-1, -1)), (0.42, (3, -1.5)), (0.55, (3, -1)), (L, (0, 0))))
    add(c, P("legF"), "rot", track((0, 0), (0.12, -4), (0.3, -6), (0.42, 12), (0.55, 12), (L, 0)))
    add(c, P("legB"), "rot", track((0, 0), (0.12, 4), (0.3, 6), (0.42, -10), (0.55, -10), (L, 0)))
    add(c, P("armB"), "rot", track((0, 0), (0.3, 14), (0.42, -20), (L, 0)))
    add(c, P("cape"), "rot", track((0, 0), (0.3, -7), (0.42, 4), (0.5, 13), (0.65, 6), (L, 0)))
    add(c, P("skirt"), "rot", track((0, 0), (0.42, 5), (L, 0)))
    clips["Attack"] = {"length": L, "loop": False, "curves": c, "impact": 0.42}

    # Fúria Negra: crouch, raise, whirl the greatsword a full turn around him, plant it (impact at 0.35 s)
    L = 0.95
    c = {}
    add(c, P("armF"), "rot", track((0, 0), (0.12, 60 * a), (0.22, 160 * a), (0.3, 20 * a), (0.35, -110 * a), (0.45, -200 * a), (0.6, -250 * a), (L, -360 * a)))   # ends a full turn later = rest pose, no unwinding
    add(c, P("weapon"), "rot", track((0, 0), (0.12, 10 * a), (0.3, -10 * a), (0.45, -25 * a), (0.6, -25 * a), (L, 0)))
    add(c, P("torso"), "rot", track((0, 0), (0.12, 6), (0.22, 10), (0.35, -14), (0.45, -10), (0.6, -6), (L, 0)))
    add(c, P("head"), "rot", track((0, 0), (0.22, 6), (0.35, -8), (L, 0)))
    add(c, P("hips"), "pos", track((0, (0, 0)), (0.12, (0, -2.5)), (0.22, (-1, 1)), (0.35, (2, -2.5)), (0.6, (2, -2)), (L, (0, 0))))
    add(c, P("legF"), "rot", track((0, 0), (0.12, -8), (0.35, 16), (0.6, 14), (L, 0)))
    add(c, P("legB"), "rot", track((0, 0), (0.12, 8), (0.35, -14), (0.6, -12), (L, 0)))
    add(c, P("armB"), "rot", track((0, 0), (0.22, 30), (0.35, -30), (0.6, -20), (L, 0)))
    add(c, P("cape"), "rot", track((0, 0), (0.22, -12), (0.35, 18), (0.5, 22), (0.7, 8), (L, 0)))
    add(c, P("skirt"), "rot", track((0, 0), (0.35, 8), (L, 0)))
    c["_root"] = {"scale": track((0, (1, 1)), (0.12, (1.06, 0.92)), (0.3, (0.96, 1.05)), (0.36, (1.08, 0.94)), (0.5, (1, 1)), (L, (1, 1)))}
    clips["Special"] = {"length": L, "loop": False, "curves": c, "impact": 0.35}
    return clips
