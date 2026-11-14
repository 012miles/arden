# Turns the downloaded music and sound effects (Downloads/oga, all CC0 or CC-BY 3.0, see CREDITS in the game) into the
# game's audio: loudness-matched music as OGG, and effects as WAV, with loops crossfaded end-into-start so they repeat
# without a click. Needs ffmpeg on the PATH.
#   python Tools/art/build_audio.py Downloads/oga Assets/Game/Resources/Audio
import os, subprocess, sys, wave, tempfile
import numpy as np

src, out = sys.argv[1], sys.argv[2]
os.makedirs(os.path.join(out, "Music"), exist_ok=True)
os.makedirs(os.path.join(out, "Sfx"), exist_ok=True)

def ff(*args):
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args], check=True)

# name: (file, target loudness LUFS)
MUSIC = {
    "title_space":        ("space-orchestral/space.flac", -18),
    "builder_august":     ("another-august/013_Another_August.mp3", -21),
    "builder_exploration":("exploration-theme/exploration_theme.ogg", -21),
    "builder_home":       ("at-home-orchestral/cinematic-calm.wav", -21),
    "launch_arclight":    ("arclight-epic-orchestralrock-soundtrack/ArcLight (192 Kbps).mp3", -17),
    "space_outthere":     ("space-music-out-there/OutThere.ogg", -21),
    "space_temple":       ("galactic-temple/GalacticTemple.ogg", -21),
    "space_firstlight":   ("first-light-particles-%E2%80%93-cc0-atmospheric-pianoambient-track/first_light_particles_0.wav", -21),
    "space_beyond":       ("beyond-the-stars-ambient/Beyond_the_Stars_Ambient_.ogg", -21),
    "space_drifting":     ("drifting-beyond-the-stars-background-ambient/Drifting Beyond the Stars.ogg", -21),
    "space_atmosphere":   ("space-atmospere/Space Atmosphere.mp3", -21),
}
for name, (f, lufs) in MUSIC.items():
    ff("-i", os.path.join(src, f), "-af", f"loudnorm=I={lufs}:TP=-1.5:LRA=11", "-ar", "44100", "-ac", "2",
       "-c:a", "libvorbis", "-q:a", "5", os.path.join(out, "Music", name + ".ogg"))
    print("music", name)

def load(path, start=None, length=None, mono=True, rate=44100):
    """Decode any file to float samples (via a temporary wav)."""
    tmp = tempfile.mktemp(suffix=".wav")
    a = ["-i", path]
    if start is not None: a = ["-ss", str(start)] + a
    if length is not None: a += ["-t", str(length)]
    ff(*a, "-ac", "1" if mono else "2", "-ar", str(rate), "-c:a", "pcm_s16le", tmp)
    with wave.open(tmp) as w:
        ch = w.getnchannels()
        x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
    os.remove(tmp)
    return x.reshape(-1, ch)

def save(name, x, rate=44100):
    peak = np.max(np.abs(x)) + 1e-9
    x = x / peak * 0.89                                  # normalised to -1 dB peak
    with wave.open(os.path.join(out, "Sfx", name + ".wav"), "wb") as w:
        w.setnchannels(x.shape[1]); w.setsampwidth(2); w.setframerate(rate)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    print("sfx", name, f"{len(x) / rate:.1f} s")

def loop(x, fade_s, rate=44100):
    """Seamless loop: the last fade_s seconds are crossfaded (equal power) into the start."""
    f = int(fade_s * rate)
    head, body, tail = x[:f], x[f:-f], x[-f:]
    t = np.linspace(0, 1, f, dtype=np.float32)[:, None]
    mixed = tail * np.cos(t * np.pi / 2) + head * np.sin(t * np.pi / 2)
    return np.concatenate([mixed, body])

def trim(x, rate=44100, thresh=0.01):
    """One-shots: drop leading silence, short fade at the end."""
    a = np.max(np.abs(x), axis=1)
    i = np.argmax(a > thresh * a.max())
    x = x[max(0, i - int(0.005 * rate)):]
    n = min(len(x), int(0.05 * rate))
    x[-n:] *= np.linspace(1, 0, n)[:, None]
    return x

S = lambda p: os.path.join(src, p)
# Engine: a deep rocket rumble, and the muffled engine you hear through the hull in vacuum
save("engine_low", loop(load(S("rocket-engine/rocket_engine.001.wav")), 1.2))
save("engine_vac", loop(load(S("underwater-or-space-engine-rumble/underwater_or_space_engine.ogg"), 10, 22), 2.0))
save("ignition", trim(load(S("rocket-launch/l.mp3"))))
# Air: wind over the hull (also the roar of reentry, pitched up), RCS hiss and a puff
save("wind", loop(load(S("space-winds/space-wind.mp3"), 5, 24), 3.0))
save("rcs", loop(load(S("thruster/space_ship.ogg")), 1.0))
save("rcs_puff", trim(load(S("boost-or-launch-or-thruster-sound-effect/boost.mp3"))))
# Bangs and clanks
save("explosion1", trim(load(S("2-high-quality-explosions/exp.mp3"))))
save("explosion2", trim(load(S("big-explosion/tnt.mp3"))))
save("clank", trim(load(S("metal-impact-sounds/bong1.wav"))))
save("clink", trim(load(S("metal-impact-sounds/clink3.wav"))))
save("thud", trim(load(S("metal-impact-sounds/thud3.wav"))))
# Places: the assembly building's hum, the space centre at night, a breeze by day
save("hangar_amb", loop(load(S("background-rumble-noise/background_ambience.ogg"), mono=False), 3.0))
save("crickets", loop(load(S("crickets-ambient-noise-loopable/crickets-oneloop.mp3"), mono=False), 1.5))
save("breeze", loop(load(S("short-wind-sound/short wind sound.wav")), 1.2))
# Interface (Kenney, CC0)
K = "interface-sounds/x/Audio/"
save("ui_click", trim(load(S(K + "click_002.ogg"))))
save("ui_drop", trim(load(S(K + "drop_002.ogg"))))
save("ui_confirm", trim(load(S(K + "confirmation_002.ogg"))))
save("ui_error", trim(load(S(K + "error_004.ogg"))))
