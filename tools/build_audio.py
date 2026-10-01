"""Original placeholder audio, synthesised from scratch (no samples), plus cuts of the delivered hellhound growls.

    python tools/build_audio.py

Writes Assets/Audio/{SFX,Music,Ambient}/*.wav|.ogg and Assets/Audio/manifest.json (read by ContentBuilder to fill
the AudioLibrary). Every synthesised file is a PLACEHOLDER (flag in the manifest, "placeholder_" prefix for music):
replace the files keeping the ids and re-run the content builder.
"""
import json
import os
import subprocess
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Audio")
SR = 44100
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- primitives

def t_axis(d):
    return np.arange(int(d * SR)) / SR


def noise(d):
    return rng.uniform(-1, 1, int(d * SR))


def osc(freq, d, kind="sine", phase=0.0):
    """freq: number or array (sweeps). Phase-accumulated, so sweeps are click-free."""
    n = int(d * SR)
    f = np.broadcast_to(np.asarray(freq, float), (n,)) if np.ndim(freq) else np.full(n, float(freq))
    ph = np.cumsum(f) / SR + phase
    if kind == "sine":
        return np.sin(2 * np.pi * ph)
    if kind == "saw":
        return 2 * (ph % 1.0) - 1
    if kind == "square":
        return np.sign(np.sin(2 * np.pi * ph))
    if kind == "tri":
        return 2 * np.abs(2 * (ph % 1.0) - 1) - 1
    raise ValueError(kind)


def sweep(f0, f1, d, curve=1.0):
    t = np.linspace(0, 1, int(d * SR)) ** curve
    return f0 * (f1 / f0) ** t


def env(d, a=0.005, decay=None, hold=0.0, release=None):
    """Attack then exponential decay (tau = decay) or linear release."""
    n = int(d * SR)
    t = np.arange(n) / SR
    e = np.minimum(1, t / max(a, 1e-4))
    if decay:
        e = e * np.where(t > a + hold, np.exp(-(t - a - hold) / decay), 1)
    if release:
        e = e * np.clip((d - t) / release, 0, 1)
    return e


def fft_filter(x, lo=None, hi=None, order=2):
    """Zero-phase Butterworth-shaped band filter in the frequency domain (fast for long signals)."""
    n = len(x)
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    h = np.ones_like(f)
    if hi:
        h *= 1 / np.sqrt(1 + (f / hi) ** (2 * order))
    if lo:
        h *= 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** (2 * order))
    return np.fft.irfft(X * h, n)


def lp_sweep(x, f0, f1, curve=1.0):
    """Time-varying one-pole lowpass (short sounds only: plain Python loop)."""
    n = len(x)
    fc = f0 * (f1 / f0) ** (np.linspace(0, 1, n) ** curve)
    a = np.exp(-2 * np.pi * fc / SR)
    y = np.empty(n)
    acc = 0.0
    xs = x.tolist()
    al = a.tolist()
    for i in range(n):
        acc = (1 - al[i]) * xs[i] + al[i] * acc
        y[i] = acc
    return y


def bp_sweep(x, f0, f1, q=4.0):
    """Time-varying resonant bandpass (state-variable filter), short sounds only."""
    n = len(x)
    fc = np.geomspace(f0, f1, n)
    low = band = 0.0
    y = np.empty(n)
    damp = 1 / q
    xs = x.tolist()
    for i in range(n):
        f = 2 * np.sin(np.pi * min(fc[i], SR / 6) / SR)
        high = xs[i] - low - damp * band
        band += f * high
        low += f * band
        y[i] = band
    return y


def pluck(freq, d, damping=0.996):
    """Karplus-Strong string."""
    n = int(d * SR)
    p = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, p)
    out = np.empty(n)
    for i in range(n):
        v = buf[i % p]
        out[i] = v
        buf[i % p] = damping * 0.5 * (v + buf[(i + 1) % p])
    return out


def bell(f0, d, ratios=(1, 2.32, 4.25, 5.43, 6.8), decay=1.2):
    t = t_axis(d)
    out = np.zeros_like(t)
    for k, r in enumerate(ratios):
        out += np.sin(2 * np.pi * f0 * r * t) * np.exp(-t / (decay / (1 + k * 0.6))) / (1 + k * 0.5)
    return out


def metal(f0, d, decay=0.25):
    return bell(f0, d, ratios=(1, 1.47, 2.09, 2.56, 3.9, 5.1), decay=decay)


def formant(x, formants=((700, 1220, 2600), (1, 0.6, 0.3))):
    out = np.zeros_like(x)
    for f, g in zip(*formants):
        out += fft_filter(x, f * 0.8, f * 1.25, 2) * g
    return out


def reverb(x, seconds=1.2, mix=0.25, damp=3000):
    n = int(seconds * SR)
    ir = rng.normal(0, 1, n) * np.exp(-np.arange(n) / (n / 5))
    ir = fft_filter(ir, None, damp, 1)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    m = len(x) + n
    wet = np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(ir, m), m)
    dry = np.concatenate([x, np.zeros(n)])
    return dry * (1 - mix) + wet * mix


def drive(x, k=2.0):
    return np.tanh(x * k) / np.tanh(k)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, offset, total):
    out = np.zeros(int(total * SR))
    i = int(offset * SR)
    seg = x[: max(0, len(out) - i)]
    out[i:i + len(seg)] += seg
    return out


def norm(x, peak=0.9):
    m = np.max(np.abs(x)) + 1e-9
    return x / m * peak


def fade(x, fin=0.003, fout=0.02):
    n = len(x)
    a, b = int(fin * SR), int(fout * SR)
    e = np.ones(n)
    if a:
        e[:a] = np.linspace(0, 1, a)
    if b:
        e[-b:] = np.linspace(1, 0, b)
    return x * e


def write_wav(path, x, stereo=False):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1, 1)
    if stereo and x.ndim == 1:
        x = np.stack([x, x], axis=1)
    data = (x * 32000).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(2 if data.ndim == 2 else 1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def to_ogg(wav_path, ogg_path, quality=4):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libvorbis", "-q:a", str(quality), ogg_path], check=True)
    os.remove(wav_path)


# ---------------------------------------------------------------- sound design

def whoosh(d=0.3, f0=600, f1=2400, q=2.5):
    return bp_sweep(noise(d), f0, f1, q) * env(d, a=d * 0.35, decay=d * 0.25)


def thud(f=70, d=0.35, click=0.3):
    t = t_axis(d)
    freq = f * (1 + 1.5 * np.exp(-t * 30))          # pitch drops fast: weight of the blow
    body = np.sin(2 * np.pi * np.cumsum(freq) / SR) * np.exp(-t / (d * 0.3))
    hit = fft_filter(noise(d), None, 1500) * np.exp(-t / 0.02) * click
    return body + hit


def knock(f=220, d=0.18):
    t = t_axis(d)
    return (np.sin(2 * np.pi * f * t) * 0.6 + fft_filter(noise(d), 500, 2500) * 0.6) * np.exp(-t / 0.035)


def SFX():
    s = {}
    s["ui_click"] = [mix(knock(900, 0.06) * 0.6, osc(1400, 0.03) * env(0.03, decay=0.008) * 0.4)]
    s["ui_hover"] = [osc(2200, 0.04) * env(0.04, a=0.002, decay=0.01) * 0.35]
    s["ui_select"] = [mix(osc(660, 0.09) * env(0.09, decay=0.03), at(osc(990, 0.1) * env(0.1, decay=0.04), 0.05, 0.16)) * 0.6]
    s["ui_error"] = [mix(osc(150, 0.1, "square") * env(0.1, decay=0.05), at(osc(120, 0.12, "square") * env(0.12, decay=0.05), 0.11, 0.25)) * 0.35]
    s["tower_select"] = [mix(knock(240, 0.2), knock(480, 0.15) * 0.4) * 0.7]
    s["build"] = [mix(*[at(knock(200 + 40 * i, 0.16), 0.11 * i, 0.75) for i in range(3)],
                      at(thud(55, 0.4), 0.33, 0.75) * 0.8, at(fft_filter(noise(0.45), 200, 2000) * env(0.45, a=0.02, decay=0.12) * 0.3, 0.3, 0.75))]
    up = osc(sweep(380, 1150, 0.55), 0.55) * env(0.55, a=0.02, decay=0.25)
    s["upgrade"] = [reverb(mix(up * 0.5, at(bell(880, 0.8, decay=0.5) * 0.4, 0.25, 0.9), at(metal(1320, 0.5) * 0.25, 0.4, 0.9)), 0.8, 0.3)]
    s["sell"] = [mix(*[at(bell(2200 + 500 * k, 0.25, decay=0.08) * 0.35, 0.05 * k, 0.45) for k in range(4)])]

    def bow(f):
        return mix(pluck(f, 0.3, 0.99) * 0.7, whoosh(0.22, 900, 3000) * 0.5)
    s["archer_shot"] = [bow(170), bow(185), bow(160)]
    s["arrow_impact"] = [mix(knock(300, 0.12), fft_filter(noise(0.08), 800, 3000) * env(0.08, decay=0.015) * 0.5) for _ in range(2)]
    s["ballista_shot"] = [mix(pluck(85, 0.6, 0.995) * 0.9, knock(140, 0.2) * 0.7, at(whoosh(0.35, 300, 1500) * 0.6, 0.03, 0.6))]
    s["ballista_impact"] = [drive(mix(thud(48, 0.5) * 1.2, fft_filter(noise(0.3), 100, 1500) * env(0.3, decay=0.06) * 0.9), 1.6)]
    fire = bp_sweep(noise(0.65), 300, 2200, 1.5) * env(0.65, a=0.15, decay=0.2)
    crackle = np.zeros(int(0.65 * SR))
    for i in rng.integers(0, len(crackle) - 400, 18):
        crackle[i:i + 300] += fft_filter(noise(300 / SR), 2000, 8000)[:300] * rng.uniform(0.2, 0.6)
    s["pyre_cast"] = [mix(fire * 0.8, crackle * 0.4)]
    boom = drive(mix(fft_filter(noise(0.8), None, 900) * env(0.8, a=0.004, decay=0.18), thud(60, 0.6) * 0.7), 1.8)
    s["fire_impact"] = [mix(boom, crackle * 0.5), mix(boom * 0.9, crackle[::-1] * 0.4)]
    chord = mix(*[osc(f, 1.6, "saw") * 0.2 for f in (220, 261.6, 329.6, 440, 220.8)])
    s["chapel_aura"] = [reverb(fft_filter(chord, None, 1500) * env(1.6, a=0.4, release=0.8) * 0.6, 1.5, 0.45)]

    def caw():
        d = 0.32
        f = sweep(780, 520, d) * (1 + 0.05 * osc(28, d))
        tone = drive(osc(f, d, "saw") * (0.6 + 0.4 * osc(70, d)), 3)
        return formant(tone, ((900, 1500, 2700), (1, 0.7, 0.4))) * env(d, a=0.02, decay=0.12)
    s["crow_curse"] = [mix(caw(), at(caw() * 0.7, 0.36, 0.75)), caw()]
    s["crow_impact"] = [mix(whoosh(0.3, 200, 800) * 0.6, osc(sweep(140, 70, 0.4), 0.4) * env(0.4, decay=0.15) * 0.6)]
    zap_f = sweep(1800, 180, 0.55, 0.6)
    zap = drive(osc(zap_f * (1 + 0.5 * osc(zap_f * 1.5, 0.55)), 0.55), 2) * env(0.55, a=0.005, decay=0.18)
    s["obelisk_cast"] = [reverb(mix(zap * 0.6, fft_filter(noise(0.4), 2000, 9000) * env(0.4, decay=0.08) * 0.4), 0.7, 0.3)]
    s["trap_trigger"] = [mix(bp_sweep(noise(0.25), 4000, 1500, 3) * env(0.25, decay=0.07) * 0.8, knock(180, 0.2) * 0.8, metal(900, 0.3, 0.06) * 0.25)]
    s["enemy_hit"] = [mix(fft_filter(noise(0.1), 80, 900) * env(0.1, decay=0.025), osc(110, 0.1) * env(0.1, decay=0.03) * 0.5) for _ in range(3)]

    def groan(f0, d):
        tone = osc(sweep(f0, f0 * 0.6, d) * (1 + 0.02 * osc(5, d)), d, "saw")
        return formant(tone, ((600, 1000, 2400), (1, 0.6, 0.25))) * env(d, a=0.05, decay=d * 0.4)
    s["enemy_death"] = [mix(groan(140, 0.7) * 0.8, at(thud(70, 0.3) * 0.6, 0.35, 0.8)), mix(groan(110, 0.8) * 0.8, at(thud(60, 0.3) * 0.6, 0.45, 0.9))]
    s["enemy_attack"] = [mix(whoosh(0.2, 1500, 600) * 0.6, at(metal(700, 0.25, 0.05) * 0.4, 0.12, 0.35))]
    s["hound_bite"] = [mix(fft_filter(noise(0.06), 1000, 6000) * env(0.06, decay=0.01), at(groan(90, 0.25) * 0.5, 0.03, 0.3))]
    sq = mix(*[at(osc(sweep(f, f * 0.4, 0.15), 0.15) * env(0.15, decay=0.05) * 0.4, o, 0.6) for f, o in ((300, 0), (220, 0.08), (380, 0.17), (180, 0.3))])
    s["slime_death"] = [mix(fft_filter(noise(0.5), None, 600) * env(0.5, a=0.01, decay=0.15) * 0.8, sq)]
    whisper = formant(noise(0.9), ((800, 1800, 3200), (1, 0.8, 0.5))) * env(0.9, a=0.25, release=0.3)
    s["warlock_cast"] = [reverb(mix(whisper * 0.6, osc(sweep(400, 900, 0.9), 0.9) * env(0.9, a=0.3, release=0.4) * 0.2), 1.0, 0.4)]
    s["commander_swing"] = [whoosh(0.28, 2400, 500, 2) * 0.8, whoosh(0.3, 2000, 450, 2) * 0.8]
    s["commander_attack"] = [drive(mix(metal(620, 0.4, 0.12) * 0.7, thud(80, 0.3) * 0.9, fft_filter(noise(0.08), 1500, 6000) * env(0.08, decay=0.02) * 0.6), 1.4)
                             for _ in range(2)]
    s["commander_hit"] = [mix(metal(540, 0.3, 0.07) * 0.5, groan(130, 0.3) * 0.6)]
    spin = mix(whoosh(0.5, 300, 2600, 2) * 0.9, at(whoosh(0.45, 2600, 300, 2) * 0.8, 0.18, 0.7))
    s["commander_ultimate"] = [reverb(drive(mix(spin, at(thud(42, 0.6) * 1.2, 0.33, 1.0), at(metal(480, 0.5, 0.15) * 0.5, 0.34, 1.0),
                                                 at(groan(95, 0.6) * 0.5, 0.0, 1.0)), 1.5), 1.2, 0.3)]
    s["commander_death"] = [reverb(mix(groan(120, 1.0) * 0.8, at(metal(400, 1.0, 0.3) * 0.6, 0.5, 1.6), at(thud(50, 0.7), 0.6, 1.6)), 1.2, 0.3)]

    def roar(d=1.8, f0=72):
        f = sweep(f0 * 1.4, f0, d, 0.5) * (1 + 0.04 * osc(7, d))
        tone = drive(osc(f, d, "saw") + 0.5 * osc(f * 1.01, d, "square"), 3)
        grit = fft_filter(noise(d), 200, 2500) * 0.5
        return formant(tone + grit, ((500, 900, 2200), (1, 0.7, 0.3))) * env(d, a=0.15, decay=d * 0.45)
    s["boss_roar"] = [reverb(roar(), 1.6, 0.35)]
    s["boss_attack"] = [drive(mix(thud(38, 0.9) * 1.4, fft_filter(noise(0.9), 60, 2500) * env(0.9, decay=0.2) * 0.9, at(metal(300, 0.6, 0.15) * 0.5, 0.02, 0.9)), 1.6)]
    s["boss_death"] = [reverb(mix(roar(2.2, 60) * 0.9, at(drive(thud(35, 1.2) * 1.3, 1.5), 1.2, 3.0), at(fft_filter(noise(1.5), 40, 400) * env(1.5, a=0.1, decay=0.6), 1.3, 3.0)), 2.0, 0.35)]
    drone = osc(sweep(55, 110, 1.3), 1.3, "saw")
    s["boss_summon"] = [reverb(mix(fft_filter(drone, None, 600) * env(1.3, a=0.6, release=0.3) * 0.7, whisper * 0.5), 1.4, 0.4)]

    def horn(f, d):
        v = 1 + 0.008 * osc(5.5, d)
        tone = osc(f * v, d, "saw") * 0.7 + osc(f * 2 * v, d, "saw") * 0.25 + osc(f * 3 * v, d, "saw") * 0.12
        return fft_filter(tone, None, 1600) * env(d, a=0.12, release=0.4)
    s["wave_start"] = [reverb(mix(horn(110, 1.6), at(horn(110, 0.9) * 0.8, 1.7, 2.7)), 1.6, 0.35)]
    s["wave_complete"] = [reverb(bell(196, 2.4, decay=1.4) * 0.7, 1.6, 0.35)]
    s["victory"] = [reverb(mix(horn(146.8, 0.8), at(horn(196, 0.8), 0.7, 3.2), at(horn(220, 1.8), 1.4, 3.2), at(horn(293.7, 1.8) * 0.6, 1.4, 3.2)), 2.0, 0.35)]
    s["defeat"] = [reverb(mix(bell(98, 3.0, decay=2.0) * 0.8, fft_filter(osc(sweep(110, 55, 3.0), 3.0, "saw"), None, 500) * env(3.0, a=0.3, release=1.0) * 0.5), 2.0, 0.4)]
    s["fortress_hit"] = [drive(mix(thud(45, 0.6), fft_filter(noise(0.6), 80, 1800) * env(0.6, decay=0.12) * 0.8), 1.5)]
    return s


SFX_META = {   # id: (category, volume, pitch jitter, cooldown)
    "ui_click": ("UI", 0.7, 0.05, 0.03), "ui_hover": ("UI", 0.35, 0.05, 0.05), "ui_select": ("UI", 0.6, 0.03, 0.05),
    "ui_error": ("UI", 0.55, 0.0, 0.2), "tower_select": ("UI", 0.6, 0.05, 0.05),
    "build": ("Sfx", 0.8, 0.04, 0.1), "upgrade": ("Sfx", 0.75, 0.0, 0.1), "sell": ("Sfx", 0.7, 0.05, 0.1),
    "archer_shot": ("Sfx", 0.45, 0.08, 0.05), "arrow_impact": ("Sfx", 0.35, 0.1, 0.05),
    "ballista_shot": ("Sfx", 0.65, 0.05, 0.1), "ballista_impact": ("Sfx", 0.7, 0.06, 0.08),
    "pyre_cast": ("Sfx", 0.5, 0.08, 0.1), "fire_impact": ("Sfx", 0.6, 0.08, 0.08), "chapel_aura": ("Sfx", 0.35, 0.0, 1.5),
    "crow_curse": ("Sfx", 0.45, 0.1, 0.15), "crow_impact": ("Sfx", 0.35, 0.1, 0.1), "obelisk_cast": ("Sfx", 0.55, 0.06, 0.1),
    "trap_trigger": ("Sfx", 0.5, 0.1, 0.08), "enemy_hit": ("Sfx", 0.3, 0.15, 0.04), "enemy_death": ("Sfx", 0.45, 0.12, 0.08),
    "enemy_attack": ("Sfx", 0.4, 0.1, 0.1), "hound_bite": ("Sfx", 0.45, 0.1, 0.1), "slime_death": ("Sfx", 0.5, 0.1, 0.1),
    "warlock_cast": ("Sfx", 0.5, 0.05, 0.3), "commander_swing": ("Sfx", 0.55, 0.06, 0.1), "commander_attack": ("Sfx", 0.7, 0.06, 0.1),
    "commander_hit": ("Sfx", 0.6, 0.06, 0.3), "commander_death": ("Sfx", 0.9, 0.0, 1.0), "commander_ultimate": ("Sfx", 0.95, 0.0, 1.0),
    "boss_roar": ("Sfx", 0.95, 0.0, 1.0), "boss_attack": ("Sfx", 0.9, 0.04, 0.3), "boss_death": ("Sfx", 1.0, 0.0, 1.0),
    "boss_summon": ("Sfx", 0.7, 0.0, 0.5), "wave_start": ("Sfx", 0.75, 0.0, 1.0), "wave_complete": ("Sfx", 0.7, 0.0, 1.0),
    "victory": ("Sfx", 0.85, 0.0, 1.0), "defeat": ("Sfx", 0.85, 0.0, 1.0), "fortress_hit": ("Sfx", 0.8, 0.05, 0.15),
    "hound_growl": ("Sfx", 0.5, 0.06, 1.5),
}


# ---------------------------------------------------------------- music (placeholders)

def pad(freqs, d, cutoff=1200, detune=0.004):
    out = np.zeros(int(d * SR))
    for f in freqs:
        for dt in (-detune, 0, detune):
            out += osc(f * (1 + dt), d, "saw")
    return fft_filter(out, 40, cutoff, 2) / (len(freqs) * 3)


def drum(f=70, d=0.6, snap=0.3):
    t = t_axis(d)
    body = np.sin(2 * np.pi * np.cumsum(f * (1 + 2.5 * np.exp(-t * 25))) / SR) * np.exp(-t / 0.18)
    skin = fft_filter(noise(d), 100, 1200) * np.exp(-t / 0.05) * snap
    return body + skin


def place(track, sound, t, gain=1.0):
    i = max(0, int(t * SR))
    if i >= len(track):
        return
    seg = sound[: len(track) - i]
    track[i:i + len(seg)] += seg * gain


def loopify(x, xfade=1.5):
    """Crossfades the tail into the head so the loop point is seamless."""
    n = int(xfade * SR)
    head, tail = x[:n].copy(), x[-n:]
    w = np.linspace(0, 1, n)
    x = x[:-n].copy()
    x[:n] = head * w + tail * (1 - w)
    return x


def music_menu(d=64.0):
    t = np.zeros(int(d * SR))
    chords = [(73.4, 110, 146.8, 174.6), (65.4, 98, 130.8, 155.6), (58.3, 87.3, 116.5, 146.8), (65.4, 98, 130.8, 164.8)]
    seg = d / len(chords)
    for i, c in enumerate(chords):
        p = pad(c, seg + 2, 900) * env(seg + 2, a=2.5, release=2.5)
        place(t, p, i * seg, 0.9)
    for k in range(int(d / 8)):
        place(t, bell(293.7 if k % 2 == 0 else 220, 6, decay=3.0) * 0.18, k * 8 + 3)
    t = reverb(t, 3.0, 0.35)[: len(t)]
    return loopify(norm(t, 0.7))


def music_war(d=64.0, bpm=72, root=55.0, cutoff=1000):
    t = np.zeros(int(d * SR))
    beat = 60 / bpm
    n = int(d / beat)
    for i in range(n):
        if i % 4 in (0, 2):
            place(t, drum(60, 0.8, 0.4), i * beat, 0.9)
        if i % 8 == 7:
            place(t, drum(90, 0.4, 0.5), i * beat + beat / 2, 0.6)
        if i % 4 == 3:
            place(t, drum(120, 0.3, 0.6), i * beat, 0.35)
    prog = [(root, root * 1.5, root * 2), (root * 0.944, root * 1.414, root * 1.888), (root * 0.84, root * 1.26, root * 1.68), (root * 0.944, root * 1.414, root * 2)]
    seg = d / len(prog)
    for i, c in enumerate(prog):
        place(t, pad(c, seg + 2, cutoff) * env(seg + 2, a=1.5, release=2.0), i * seg, 0.8)
        hornline = osc(c[0] * 4, seg * 0.5, "saw") * 0.5 + osc(c[0] * 6, seg * 0.5, "saw") * 0.2
        place(t, fft_filter(hornline, None, 1400) * env(seg * 0.5, a=0.6, release=1.2) * 0.25, i * seg + seg * 0.3)
    t = reverb(t, 2.2, 0.28)[: len(t)]
    return loopify(norm(drive(t, 1.2), 0.75))


def music_swamp(d=64.0):
    t = np.zeros(int(d * SR))
    place(t, pad((49, 73.4, 98, 116.5), d, 700) * env(d, a=3, release=3), 0, 0.8)
    notes = [196, 233, 220, 174.6, 196, 146.8, 164.8, 130.8]
    for k in range(int(d / 1.6)):
        f = notes[k % len(notes)] * (0.5 if k % 5 == 4 else 1)
        p = pluck(f, 1.4, 0.993) * 0.35
        place(t, p, k * 1.6 + rng.uniform(-0.05, 0.05))
        place(t, p * 0.3, k * 1.6 + 0.4)
        place(t, p * 0.12, k * 1.6 + 0.8)
    for k in range(int(d / 4)):
        place(t, drum(48, 1.0, 0.15) * 0.6, k * 4 + 0.2)
    insects = fft_filter(noise(d), 5000, 9000) * (0.5 + 0.5 * osc(0.13, d)) * 0.05
    t += insects
    t = reverb(t, 2.5, 0.35)[: len(t)]
    return loopify(norm(t, 0.7))


def music_boss(d=48.0):
    t = music_war(d, bpm=118, root=46.25, cutoff=1500) * 0.9
    beat = 60 / 118
    for i in range(int(d / beat)):
        place(t, osc(46.25 * (2 if i % 8 in (3, 7) else 1), beat * 0.9, "saw") * env(beat * 0.9, a=0.01, decay=0.15) * 0.25, i * beat)
        if i % 16 == 0:
            place(t, drive(fft_filter(noise(1.2), 60, 3000), 2) * env(1.2, a=0.01, decay=0.4) * 0.3, i * beat)
    return norm(drive(t, 1.3), 0.8)


def music_stinger(victory=True, d=9.0):
    t = np.zeros(int(d * SR))
    if victory:
        seq = [(146.8, 0), (196, 0.9), (220, 1.8), (293.7, 2.7)]
        for f, o in seq:
            place(t, pad((f, f * 1.5, f * 2), d - o, 1600) * env(d - o, a=0.2, release=3) * 0.8, o)
        place(t, bell(587, 5, decay=2.5) * 0.25, 2.7)
    else:
        seq = [(146.8, 0), (130.8, 1.5), (116.5, 3.0), (98, 4.5)]
        for f, o in seq:
            place(t, pad((f, f * 1.2, f * 1.5), 2.5, 900) * env(2.5, a=0.3, release=1.2) * 0.8, o)
        place(t, bell(98, 6, decay=3.0) * 0.4, 4.5)
    return norm(reverb(t, 3, 0.4)[: len(t)], 0.75)


def ambient_ruins(d=40.0):
    wind = fft_filter(noise(d), 120, 900) * (0.55 + 0.45 * osc(0.07, d) * osc(0.031, d, phase=0.3))
    t = wind * 0.5
    crackle = np.zeros_like(t)
    for i in rng.integers(0, len(t) - 600, 260):
        crackle[i:i + 500] += fft_filter(noise(500 / SR), 1500, 7000)[:500] * rng.uniform(0.05, 0.25)
    t += crackle * 0.5
    caw = formant(drive(osc(sweep(760, 520, 0.35), 0.35, "saw"), 3), ((900, 1500, 2700), (1, 0.7, 0.4))) * env(0.35, a=0.02, decay=0.12)
    for o in (6.0, 6.45, 23.0, 31.5, 31.9):
        place(t, caw * 0.12, o)
    return loopify(norm(reverb(t, 2.0, 0.3)[: len(t)], 0.6))


def ambient_swamp(d=40.0):
    t = fft_filter(noise(d), 80, 500) * (0.5 + 0.5 * osc(0.05, d)) * 0.35
    t += fft_filter(noise(d), 4500, 8500) * (0.4 + 0.6 * np.abs(osc(11, d))) * (0.5 + 0.5 * osc(0.09, d)) * 0.08
    for o in rng.uniform(0, d - 1, 70):
        f = rng.uniform(250, 600)
        place(t, osc(sweep(f, f * 1.8, 0.08), 0.08) * env(0.08, a=0.005, decay=0.02) * 0.12, o)
    for o in (4.0, 4.5, 19.0, 19.6, 33.0):
        croak = fft_filter(osc(95, 0.25, "square") * (0.5 + 0.5 * osc(28, 0.25)), None, 900) * env(0.25, a=0.02, decay=0.08)
        place(t, croak * 0.18, o)
    return loopify(norm(reverb(t, 2.0, 0.35)[: len(t)], 0.6))


# ---------------------------------------------------------------- main

def growl_cuts(manifest):
    """Two short cuts of the delivered hellhound growls (original files untouched)."""
    src = os.path.join(OUT, "Source")
    files = sorted(f for f in os.listdir(src) if f.endswith(".mp3")) if os.path.isdir(src) else []
    out = []
    for i, f in enumerate(files):
        dst = os.path.join(OUT, "SFX", f"hound_growl_{i}.ogg")
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-ss", "0.4", "-t", "2.2", "-i", os.path.join(src, f),
                        "-af", "afade=t=in:d=0.05,afade=t=out:st=1.8:d=0.4", "-ac", "1", "-c:a", "libvorbis", "-q:a", "5", dst], check=True)
        out.append(f"Assets/Audio/SFX/hound_growl_{i}.ogg")
    if out:
        c, v, p, cd = SFX_META["hound_growl"]
        manifest.append(dict(id="hound_growl", category=c, files=out, volume=v, pitch=p, cooldown=cd, placeholder=False))


def main():
    manifest = []
    for sid, variants in SFX().items():
        files = []
        for i, x in enumerate(variants):
            path = os.path.join(OUT, "SFX", f"{sid}_{i}.wav")
            write_wav(path, fade(norm(x, 0.92)))
            files.append(f"Assets/Audio/SFX/{sid}_{i}.wav")
        c, v, p, cd = SFX_META[sid]
        manifest.append(dict(id=sid, category=c, files=files, volume=v, pitch=p, cooldown=cd, placeholder=True))
        print("sfx", sid, len(variants))
    growl_cuts(manifest)
    tracks = {
        "music_menu": ("Music", music_menu(), 0.55), "music_map1": ("Music", music_war(), 0.5), "music_map2": ("Music", music_swamp(), 0.5),
        "music_boss": ("Music", music_boss(), 0.6), "music_victory": ("Music", music_stinger(True), 0.6), "music_defeat": ("Music", music_stinger(False), 0.6),
        "ambient_map1": ("Ambient", ambient_ruins(), 0.45), "ambient_map2": ("Ambient", ambient_swamp(), 0.45),
    }
    for tid, (cat, x, vol) in tracks.items():
        folder = "Music" if cat == "Music" else "Ambient"
        name = f"placeholder_{tid}"
        wav = os.path.join(OUT, folder, name + ".wav")
        write_wav(wav, fade(x, 0.01, 0.01), stereo=True)
        to_ogg(wav, os.path.join(OUT, folder, name + ".ogg"))
        manifest.append(dict(id=tid, category=cat, files=[f"Assets/Audio/{folder}/{name}.ogg"], volume=vol, pitch=0.0, cooldown=0.0, placeholder=True))
        print("track", tid, f"{len(x) / SR:.0f}s")
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)
    print("ok", len(manifest), "entries")


if __name__ == "__main__":
    main()
