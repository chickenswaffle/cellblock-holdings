#!/usr/bin/env python3
"""Builds Cellblock's sound library from CC0 field recordings by Joseph Sardin, BigSoundBank (https://bigsoundbank.com, CC0 1.0).

    python3 Tools/Art/build_sounds.py <cache-dir-with-<id>.ogg>

Needs numpy, scipy and soundfile. The cache holds the recordings by BigSoundBank number (e.g. 3598.ogg). Outputs go to
Assets/Cellblock/Resources/Sounds/: seamless loops (amb_*, loop_*) and single-hit variations (<name>_<n>), all mono
44.1 kHz Vorbis except the stereo outdoor beds. Loops cross-fade their tail into their head so there is no click or gap;
one-shots are trimmed, faded and peak-levelled so the game sets every level in one place (FacilitySoundscape).
"""
import os, sys
import numpy as np
import soundfile as sf
from scipy import signal

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Cellblock", "Resources", "Sounds")
SR = 44100
CACHE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Temp", "sound-sources")


def load(number, stereo=False):
    x, sr = sf.read(os.path.join(CACHE, f"{number:04d}.ogg"), always_2d=True)
    x = x.astype(np.float64)
    if not stereo: x = x.mean(1, keepdims=True)
    elif x.shape[1] == 1: x = np.repeat(x, 2, 1)
    if sr != SR:
        g = np.gcd(SR, sr); x = signal.resample_poly(x, SR // g, sr // g, axis=0)
    return x


def filt(x, low=None, high=None):
    if low: x = signal.sosfiltfilt(signal.butter(2, low, "highpass", fs=SR, output="sos"), x, axis=0)
    if high: x = signal.sosfiltfilt(signal.butter(4, high, "lowpass", fs=SR, output="sos"), x, axis=0)
    return x


def rms_db(x): return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)


def limit(x, ceiling_db=-1, release=.25):
    # Peak limiter (instant attack, smooth release) so a bird call or slam can't stop a bed reaching its loudness.
    ceiling = 10 ** (ceiling_db / 20); peak = np.abs(x).max(1)
    need = np.minimum(1, ceiling / (peak + 1e-12))
    k = int(.005 * SR); need = -np.convolve(np.pad(-need, (k, k), mode="edge"), np.ones(1), "same")[k:-k]
    gain = np.empty_like(need); g = 1.0; r = np.exp(-1 / (release * SR))
    for i in range(len(need)):
        g = need[i] if need[i] < g else need[i] + (g - need[i]) * r
        gain[i] = g
    return x * gain[:, None]


def level(x, rms=None, peak=None):
    if rms is not None:
        for _ in range(3): x = limit(x * 10 ** ((rms - rms_db(x)) / 20), -1.5)
    p = np.abs(x).max() + 1e-12
    ceiling = 10 ** ((peak if peak is not None else -1) / 20)
    if p > ceiling: x = x * ceiling / p
    return x


def reverb(x, seconds=1.2, wet=.3, seed=1):
    # A small synthetic hall: exponentially decaying, low-passed noise. Concrete rooms ring; this blurs speech.
    rng = np.random.default_rng(seed); n = int(seconds * SR)
    ir = rng.normal(0, 1, n) * np.exp(-6.9 * np.arange(n) / n)
    ir = filt(ir[:, None], 200, 4500)[:, 0]; ir /= np.sqrt(np.sum(ir ** 2))
    wetx = np.stack([signal.fftconvolve(x[:, c], ir)[:len(x)] for c in range(x.shape[1])], 1)
    return (1 - wet) * x + wet * wetx * (np.sqrt(np.mean(x ** 2)) / (np.sqrt(np.mean(wetx ** 2)) + 1e-12))


def loop(x, start, seconds, fade):
    a, n, f = int(start * SR), int(seconds * SR), int(fade * SR)
    seg = x[a:a + n + f].copy()
    if len(seg) < n + f: raise ValueError("recording too short for loop")
    t = np.linspace(0, np.pi / 2, f)[:, None]
    head = seg[:f] * np.sin(t) + seg[n:n + f] * np.cos(t)   # equal-power: the old tail fades out under the new head
    return np.concatenate([head, seg[f:n]])


def envelope(x, ms=5):
    k = max(1, int(SR * ms / 1000)); e = np.sqrt(np.convolve(x[:, 0] ** 2, np.ones(k) / k, "same"))
    return e


def trim(x, floor_db=-45, pre=.004, tail=.06):
    e = envelope(x); thresh = e.max() * 10 ** (floor_db / 20)
    idx = np.where(e > thresh)[0]
    if len(idx) == 0: return x
    a = max(0, idx[0] - int(pre * SR)); b = min(len(x), idx[-1] + int(tail * SR))
    return fades(x[a:b])


def fades(x, fin=.003, fout=.04):
    x = x.copy(); i, o = min(len(x), int(fin * SR)), min(len(x), int(fout * SR))
    x[:i] *= np.linspace(0, 1, i)[:, None]; x[len(x) - o:] *= np.linspace(1, 0, o)[:, None] ** 2
    return x


def hits(x, count, length, gap=.18, pre=.008, low=None, high=None):
    """The `count` most typical transients: onsets from the envelope's rise, peaks near the median (no outliers)."""
    y = filt(x, low, high); e = envelope(y, 3)
    rise = np.diff(e, prepend=e[0]); smooth = np.convolve(rise, np.ones(int(.004 * SR)), "same")
    peaks, props = signal.find_peaks(smooth, distance=int(gap * SR), height=smooth.max() * .12)
    if len(peaks) == 0: return []
    strength = np.array([e[p:p + int(.05 * SR)].max() for p in peaks])
    order = np.argsort(np.abs(np.log(strength / np.median(strength))))
    chosen = sorted(peaks[order[:count]])
    out = []
    for p in chosen:
        a = max(0, p - int(pre * SR)); seg = y[a:a + int(length * SR)]
        out.append(fades(seg, .002, min(.08, length * .4)))
    return out


def save(name, x, quality=.45):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".ogg")
    sf.write(path, np.clip(x, -1, 1).astype(np.float32), SR, format="OGG", subtype="VORBIS")
    print(f"{name:20s} {len(x) / SR:6.2f}s  ch{x.shape[1]}  rms {rms_db(x):6.1f} dB  {os.path.getsize(path) // 1024} KB")


def series(name, clips, peak=-1):
    # Every variation peaks at the same level, so the game's per-sound gain is the only level control.
    for i, c in enumerate(clips, 1): save(f"{name}_{i}", c * 10 ** (peak / 20) / (np.abs(c).max() + 1e-12))


def main():
    # ---- beds (seamless loops, levelled to -22 dBFS RMS; the game sets the mix) ----
    save("amb_hvac", level(loop(filt(load(3598), 40, 9000), 5, 40, 3), -22))
    save("amb_hum", level(loop(filt(load(2118), 50, 2400), 1, 7, 1), -22))
    save("amb_crowd", level(reverb(loop(filt(load(3516), 120, 2600), 12, 60, 4), 1.4, .38), -22))
    save("amb_yard", level(reverb(loop(filt(load(3515), 120, 5500), 6, 50, 4), .9, .2, 2), -22))
    save("amb_gym", level(loop(filt(load(2445), 60, 7000), 6, 45, 3), -22))
    save("amb_day", level(loop(filt(load(905, True), 60), 20, 60, 4), -22))
    save("amb_dawn", level(loop(filt(load(1906, True), 80), 60, 60, 4), -22))
    save("amb_dusk", level(loop(filt(load(1859, True), 80), 30, 60, 4), -22))
    save("amb_night", level(loop(filt(load(555, True), 50), 1, 25, 2.5), -22))
    save("amb_crickets", level(loop(filt(load(1880, True), 300), 10, 45, 4), -22))
    save("amb_road", level(loop(filt(load(1080), 30, 520), 5, 40, 4), -22))
    save("loop_shower", level(loop(filt(load(171), 120, 9000), 6, 30, 3), -22))
    save("loop_gate", level(loop(filt(load(708), 60), 6, 8, 1), -22))
    save("loop_drill", level(loop(filt(load(184), 80), 4, 6, 1), -22))

    # Industrial bell: the steady middle of the ring, looped (an alarm that sounds like a building, not a beep).
    bell = filt(load(1925), 150); e = envelope(bell, 50); on = np.where(e > e.max() * .3)[0]
    a = on[0] / SR + .6; b = on[-1] / SR - .6
    save("loop_alarm_bell", level(loop(bell, a, max(1.5, min(6, b - a - .5)), .25), -16))

    # ---- one-shots ----
    series("gate_clank", [trim(filt(load(n), 50)) for n in (2359, 2355, 2357)])
    series("door_strike", [trim(filt(load(3442), 80), -40)])
    series("cuffs", [trim(filt(load(n), 150)) for n in (2556, 2557, 2558, 2559, 2560)])
    series("punch", [trim(filt(load(n), 50), -40, tail=.04) for n in (2459, 2460, 2461, 2462, 2463, 2464, 2465)])
    series("bodyfall", [trim(filt(load(n), 40)) for n in (2452, 2453, 2454)])
    series("radio", [trim(filt(load(n), 300, 3400), -40)[:int(1.4 * SR)] for n in (312, 313)] + [fades(trim(filt(load(310), 300, 3400))[:int(1.2 * SR)])])
    series("chime", [trim(filt(load(1605), 120))[:int(4 * SR)], trim(filt(load(2881), 120))])
    series("switch_big", [trim(filt(load(n), 60)) for n in (3061, 3062)])
    series("keys", hits(load(445), 4, .9, gap=.9, low=300))
    series("step_concrete", hits(load(1318), 10, .22, low=70, high=11000))
    series("step_squeak", hits(load(3440), 8, .32, low=90))
    series("step_gravel", hits(load(3202), 8, .34, gap=.25, low=70))
    series("step_grass", hits(load(854), 8, .34, gap=.25, low=70))
    series("step_run", hits(load(1319), 6, .22, gap=.15, low=70))
    series("tray", hits(load(53), 3, .6, gap=.6, low=120))
    series("weights", [trim(filt(load(n), 50)) for n in (1771, 1772, 1774)])
    series("hammer", hits(load(6), 4, .35, gap=.3, low=80))
    series("saw", [fades(trim(filt(load(959), 80))[:int(3 * SR)], .02, .4)])
    series("flush", [fades(trim(filt(load(838), 60))[:int(6 * SR)], .01, .8)])
    series("car_pass", [fades(trim(filt(load(2996), 40)), .2, 1.0)])
    series("ui_tick", [trim(filt(load(n), 200), -40, tail=.03) for n in (2191, 2192)], peak=-3)
    series("ui_click", hits(load(321), 3, .12, gap=.3, low=200), peak=-3)
    series("page", [trim(filt(load(n), 150)) for n in (2210, 2211, 2212)], peak=-3)
    series("sign", [trim(filt(load(n), 150)) for n in (3236, 3237, 3238)], peak=-3)
    series("stapler", hits(load(9), 2, .35, gap=.5, low=120), peak=-3)
    series("coin", [trim(filt(load(339), 200))], peak=-3)
    series("drawer", hits(load(792), 2, 1.0, gap=1.2, low=80))
    series("cabinet", hits(load(773), 2, 1.1, gap=1.4, low=80))


if __name__ == "__main__":
    main()
