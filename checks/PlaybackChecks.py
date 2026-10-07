"""Compare saved real-game PulseAudio captures with shipped audio, not listening quality."""
import json
import subprocess
from pathlib import Path
import numpy as np

root = Path(__file__).resolve().parents[1]
qa = root/'artifacts/qa'
rate = 22050

def decode(path):
    return np.frombuffer(subprocess.run(['/usr/bin/ffmpeg','-v','error','-i',str(path),
        '-ac','1','-ar',str(rate),'-f','f32le','-'],capture_output=True,check=True).stdout,dtype='<f4').astype(float)

def match(haystack, needle):
    needle = needle - needle.mean()
    width = len(needle)
    assert len(haystack) >= width and np.dot(needle,needle) > 0
    n = 1 << (len(haystack)+width-1).bit_length()
    dot = np.fft.irfft(np.fft.rfft(haystack,n)*np.conj(np.fft.rfft(needle,n)),n)[:len(haystack)-width+1]
    sums = np.r_[0.,np.cumsum(haystack)]
    squares = np.r_[0.,np.cumsum(haystack*haystack)]
    energy = squares[width:]-squares[:-width]-(sums[width:]-sums[:-width])**2/width
    scores = dot/np.sqrt(np.maximum(1e-15,energy*np.dot(needle,needle)))
    i = int(np.argmax(scores))
    return dict(correlation=float(scores[i]),offsetSeconds=i/rate)

def main():
    state = json.loads((qa/'music-60fps-state.json').read_text())
    mapping = json.loads((root/'assets/music-map.json').read_text())
    theme = next(t['theme'] for t in mapping['tracks'] if t['id']==state['music']['curMusic'])
    capture = np.fromfile(qa/'music-game-60fps.f32',dtype='<f4').astype(float)
    music = match(decode(root/mapping['themes'][theme]['audio']),capture[2*rate:4*rate])
    assert music['correlation'] > .99, music
    voice = match(decode(qa/'emote-game.wav'),decode(root/'Riakawa/Assets/Sounds/chiikawa-emote.wav'))
    assert voice['correlation'] > .9, voice
    (qa/'playback-checks.json').write_text(json.dumps(dict(music=music,voice=voice,listeningReviewed=False),indent=2))
    print(f"PASS: captured music correlation {music['correlation']:.4f}, emote {voice['correlation']:.4f}")


if __name__ == '__main__':
    main()
