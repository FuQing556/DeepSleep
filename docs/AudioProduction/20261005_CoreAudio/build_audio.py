"""Reproducible, sample-based first-pass DeepSleep cues. No Unity file edits.

Requires numpy, scipy and soundfile. Sources are the two preserved Kenney CC0
archives in sources/. WAV is 44.1 kHz / 24-bit: source bandwidth is not upgraded.
The manifest is a production ledger, not a claim of listening approval.
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import sys
from fractions import Fraction

ROOT = Path(__file__).resolve().parents[3]
WORK = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "Temp/CoreAudioPython"))
import numpy as np
from scipy import signal
import soundfile as sf

SR = 44100
OUT = ROOT / "Assets/_Project/Audio"
RNG = np.random.default_rng(20261005)
SOURCES = {}
CUES = []
CLIPS = {}


def db(value):
    return 10 ** (value / 20)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fade(x, attack=.0015, release=.012):
    x = np.asarray(x, dtype=np.float64).copy()
    a, r = min(len(x), int(attack * SR)), min(len(x), int(release * SR))
    if a: x[:a] *= np.linspace(0, 1, a) ** 1.4
    if r: x[-r:] *= np.linspace(1, 0, r) ** 1.4
    return x


def filt(x, low=100, high=5500):
    if low:
        x = signal.sosfilt(signal.butter(2, low, 'highpass', fs=SR, output='sos'), x)
    if high:
        x = signal.sosfilt(signal.butter(3, high, 'lowpass', fs=SR, output='sos'), x)
    return x


def source(name):
    if name not in SOURCES:
        paths = list((WORK / 'sources').glob(f'*/Audio/{name}.ogg'))
        if len(paths) != 1: raise ValueError(f'Unknown/ambiguous source {name}')
        path = paths[0]
        x, rate = sf.read(path, always_2d=True, dtype='float64')
        mono = x.mean(axis=1)
        if rate != SR:
            ratio = Fraction(SR, rate)
            mono = signal.resample_poly(mono, ratio.numerator, ratio.denominator)
        mono -= np.mean(mono)
        threshold = np.max(np.abs(mono)) * .008
        active = np.flatnonzero(np.abs(mono) >= threshold)
        start = max(0, int(active[0]) - int(.002 * SR))
        end = min(len(mono), int(active[-1]) + int(.012 * SR))
        mono = mono[start:end]
        mono /= max(np.max(np.abs(mono)), 1e-6)
        SOURCES[name] = dict(path=path.relative_to(WORK).as_posix(), sha256=sha(path),
                             sample_rate=rate, channels=x.shape[1], decoded_seconds=len(x)/rate,
                             trim_samples=[start, end], signal=mono)
    return SOURCES[name]['signal'].copy()


def material(name, duration=None, rate=1., low=100, high=5500,
             reverse=False, attack=.0015, release=.012, offset=0.):
    x = source(name)
    if offset: x = x[int(offset * SR):]
    if reverse: x = x[::-1]
    if rate != 1:
        q = Fraction(1/rate).limit_denominator(1000)
        x = signal.resample_poly(x, q.numerator, q.denominator)
    if duration is not None:
        n = int(duration * SR)
        x = np.pad(x[:n], (0, max(0, n-len(x))))
    return fade(filt(x, low, high), attack, release)


def texture(name, duration, low=300, high=4000, grain=.045, seed=0, swell=.45):
    """Overlap short source-sample grains; used for air/cloth, not an oscillator."""
    rnd = np.random.default_rng(20261005 + seed)
    raw = source(name)
    n, width = int(duration * SR), int(grain * SR)
    result = np.zeros(n)
    window = np.hanning(width)
    for t in range(-width//2, n, max(1, width//3)):
        start = rnd.integers(0, max(1, len(raw)-width))
        piece = raw[start:start+width]
        piece = np.pad(piece, (0, width-len(piece))) * window
        a, b = max(0, t), min(n, t+width)
        result[a:b] += piece[a-t:b-t] * rnd.uniform(.65, 1)
    env_t = np.linspace(0, 1, n)
    envelope = np.where(env_t < swell, env_t/max(.01,swell),
                        (1-env_t)/max(.01,1-swell)) ** .85
    result = filt(result, low, high) * envelope
    result /= max(np.max(np.abs(result)), 1e-6)
    return fade(result, .007, .018)


def mix(*layers, duration=None):
    # layer = (mono samples, delay seconds, gain)
    n = int(duration * SR) if duration else max(int(t*SR)+len(x) for x,t,g in layers)
    out = np.zeros(n)
    for x,t,g in layers:
        start = int(t*SR)
        count = min(len(x), n-start)
        if count > 0: out[start:start+count] += x[:count]*g
    return out


def master(x, rms_db=-25, peak_db=-5, tail=.01):
    x = np.asarray(x, dtype=np.float64)
    x -= np.mean(x)
    x = fade(x, .0015, tail)
    rms = np.sqrt(np.mean(x*x))
    gain = min(db(rms_db) / max(rms, 1e-8), db(peak_db) / max(np.max(np.abs(x)), 1e-8))
    return x * gain


def write(cue, x, folder, sources, recipe, variant=1, theme=None, rms=-25, peak=-5):
    filename = f'SFX_{cue}_{theme+"_" if theme else ""}{variant:02}.wav'
    path = OUT / 'SFX' / folder / filename
    path.parent.mkdir(parents=True, exist_ok=True)
    x = master(x, rms, peak)
    sf.write(path, x, SR, subtype='PCM_24')
    record(path, cue, x, sources, recipe, variant, theme)
    CLIPS.setdefault((cue,theme), []).append(x)
    return x


def record(path, cue, x, sources, recipe, variant=1, theme=None, loop=False):
    if not np.all(np.isfinite(x)): raise ValueError(f'Non-finite samples in {cue}')
    for name in sources: source(name)
    rms = float(np.sqrt(np.mean(x*x)))
    spectrum = abs(np.fft.rfft(x if x.ndim==1 else x.mean(axis=1)))**2
    freqs = np.fft.rfftfreq(len(x), 1/SR)
    CUES.append(dict(cue=cue, theme=theme, variant=variant, path=path.relative_to(ROOT).as_posix(),
        duration_seconds=round(len(x)/SR, 6), sample_rate=SR, channels=1 if x.ndim==1 else x.shape[1],
        format='PCM_24', loop=loop, peak_dbfs=round(20*np.log10(max(1e-10,np.max(abs(x)))),3),
        rms_dbfs=round(20*np.log10(max(1e-10,rms)),3), dc_offset=float(np.mean(x)),
        energy_above_8khz_fraction=float(spectrum[freqs>=8000].sum()/max(1e-10,spectrum.sum())),
        first_last_peak=float(max(np.max(abs(x[:1])),np.max(abs(x[-1:])))),
        sources=sources, recipe=recipe, sha256=sha(path), status='first_pass_pending_human_listening'))


def make_combat():
    for v in range(3):
        cloth=f'footstep_carpet_{v:03}'
        grain=f'footstep_grass_{v:03}'
        # Soft launch retains a tactile attack; grain layer is subordinate, no pitched chirp.
        x=mix((material('pluck_001',.071,rate=1.03+v*.013,high=2800),0,.22),
              (material(cloth,.08,rate=1.10,high=4300),0,.9),
              (material(grain,.063,offset=.028+v*.01,low=650,high=4200),.026,.16),duration=.108)
        write('DsShot',x,'DeepSeek',['pluck_001',cloth,grain],
              'Soft carpet impulse + muted pluck + short dry-grass grain; lowpass, no reverb.',v+1,rms=-27)
    for v in range(2):
        soft=f'impactSoft_medium_{v:03}'
        grain=f'footstep_snow_{v:03}'
        hit=mix((material(soft,.072,rate=1.14,low=100,high=4600),0,1),
                (material(grain,.044,offset=.012,low=1200,high=5600),.008,.18),duration=.09)
        write('DsHit',hit,'DeepSeek',[soft,grain],'Short soft-material contact, dry grain accent.',v+1,rms=-28)
        splash=mix((hit,0,.62),
            (material(grain,.075,rate=1.12,low=650,high=4400),.019,.34),
            (material(f'footstep_grass_{v+2:03}',.085,offset=.03,low=900,high=4900),.047,.18),duration=.155)
        write('DsSplash',splash,'DeepSeek',[soft,grain,f'footstep_grass_{v+2:03}'],
              'One core contact, two quieter staggered particulate layers; no explosion bass.',v+1,rms=-28.5)
    charge=mix((texture('scratch_002',.43,450,2800,seed=41,swell=.82),0,.26),
               (material('switch_001',.045,low=250,high=3200),0,.26),
               (material('impactMetal_light_001',.045,low=800,high=4200),.405,.15),duration=.48)
    write('HsCharge',charge,'Harness',['scratch_002','switch_001','impactMetal_light_001'],
          'Mechanical latch and sampled friction gathering; finite 480ms, cancel by runtime fade.',rms=-32)
    for v in range(3):
        tin=f'impactTin_medium_{v:03}'
        metal=f'impactMetal_light_{v:03}'
        x=mix((material(tin,.065,rate=1.08,low=170,high=4800),0,.7),
              (texture('scratch_001',.115,700,5400,seed=51+v,swell=.14),.005,.20),
              (material(metal,.12,rate=.95+v*.014,low=520,high=4100),.02,.20),duration=.158)
        write('HsFire',x,'Harness',[tin,'scratch_001',metal],
              'Compact tin release + narrow sampled air + damped light metal; tail <180ms.',v+1,rms=-25.5)
    for v in range(2):
        metal=f'impactMetal_medium_{v:03}'
        x=mix((material(metal,.075,low=190,high=4600),0,.57),
              (material(f'impactSoft_medium_{v:03}',.071,low=100,high=3500),0,.52),duration=.098)
        write('HsHit',x,'Harness',[metal,f'impactSoft_medium_{v:03}'],
              'Damped metal skin and compact soft core; only on actual hit.',v+1,rms=-27)
    slash_specs=[('HsSlashUp',.155,.40,650,5300,.12),('HsSlashDown',.17,.22,250,4500,.28),
                 ('HsSlashSweep',.235,.53,400,4700,.18)]
    for i,(cue,dur,swell,low,high,weight) in enumerate(slash_specs):
        x=mix((texture('scratch_001',dur,low,high,seed=60+i,swell=swell),0,.60),
              (material('footstep_carpet_003',.048,low=150,high=2300),0,.20),
              (material('impactMetal_light_003',.055,low=500,high=3800),dur*.67,weight),duration=dur+.025)
        write(cue,x,'Harness',['scratch_001','footstep_carpet_003','impactMetal_light_003'],
              f'Sampled cloth onset, granular friction air with peak at {swell:.0%}, damped handle return; no hit impact.',rms=-27)
    wave=texture('scratch_002',.22,900,4400,seed=71,swell=.22)
    write('HsWave',wave,'Harness',['scratch_002'],'Subordinate sampled-air tail, no second heavy attack.',rms=-33)
    for role,soft,metal in [('Ds','impactSoft_medium_003',None),('Hs','impactSoft_medium_004','impactMetal_medium_003')]:
        layers=[(material(soft,.125,rate=.94,low=100,high=3500),0,.9),
                (material('footstep_carpet_004',.072,low=300,high=3200),.02,.23)]
        sources=[soft,'footstep_carpet_004']
        if metal:
            layers.append((material(metal,.087,low=300,high=3300),.011,.22)); sources.append(metal)
        write('PlayerHurt'+role,mix(*layers,duration=.165),'Players',sources,
              'Actual damage: compact body impact with cloth; HS has a short damped casing layer.',rms=-23.5,peak=-4.5)
    guard=mix((material('impactSoft_medium_002',.09,rate=1.13,low=180,high=3600),0,.60),
              (material('impactPlate_light_001',.10,low=700,high=4300),.005,.28),
              (material('pluck_002',.06,low=320,high=2500),.018,.17),duration=.132)
    write('GuardBlock',guard,'DeepSeek',['impactSoft_medium_002','impactPlate_light_001','pluck_002'],
          'Elastic short plate/soft rebound; distinct from cloth/body hurt.',rms=-25)
    start=mix((texture('footstep_grass_004',.25,350,3300,seed=81,swell=.4),0,.28),
              (material('pluck_002',.10,low=250,high=2700),.03,.25),
              (material('impactPlate_light_001',.15,low=500,high=3500),.11,.13),duration=.32)
    write('GuardStart',start,'DeepSeek',['footstep_grass_004','pluck_002','impactPlate_light_001'],
          'Short sampled-grain unfurl and light elastic landing, no sustained drone.',rms=-29)
    end=mix((material('footstep_grass_003',.105,low=650,high=3300),0,.5),
            (material('click_003',.048,rate=.85,high=2200),.074,.25),duration=.16)
    write('GuardEnd',end,'DeepSeek',['footstep_grass_003','click_003'],
          'Natural-expiry withdrawal only; forced teardown must remain silent.',rms=-32)
    for v in range(3):
        generic=f'impactGeneric_light_{v:03}'
        snow=f'footstep_snow_{v+1:03}'
        pop=mix((material(generic,.028,rate=1.1,low=500,high=4500),0,.6),
                (material(snow,.061,offset=.026,low=900,high=4800),.007,.3),duration=.09)
        write('BubblePop',pop,'Enemies',[generic,snow],
              'Dry film-like short fracture plus tiny particulate air; no water/drop source.',v+1,rms=-30)
    write('BubbleImpact',material('impactSoft_medium_001',.077,rate=.92,low=220,high=2300),
          'Enemies',['impactSoft_medium_001'],'Muted absorption, not player hurt.',rms=-31)
    reveal=mix((texture('footstep_carpet_002',.32,200,2700,seed=91,swell=.64),0,.32),
               (material('impactSoft_medium_002',.12,low=100,high=3000),.29,.34),duration=.44)
    write('DoubaoReveal',reveal,'Enemies',['footstep_carpet_002','impactSoft_medium_002'],
          'Cloth-like unfolding and soft landing; no voice or boss-horror drone.',rms=-28)
    defeat=mix((texture('footstep_grass_003',.46,450,3900,seed=92,swell=.18),0,.35),
               (material('footstep_snow_004',.16,low=700,high=4200),.18,.22),
               (material('footstep_snow_002',.18,rate=.91,low=850,high=3400),.33,.14),duration=.62)
    write('DoubaoDefeat',defeat,'Enemies',['footstep_grass_003','footstep_snow_004','footstep_snow_002'],
          'One designed collective dissolution, not N simultaneous bubble clips.',rms=-29)
    enemy=mix((material('impactWood_light_002',.06,rate=1.2,low=200,high=4000),0,.35),
              (material('footstep_snow_003',.12,low=650,high=4400),.025,.45),duration=.175)
    write('EnemyDefeat',enemy,'Enemies',['impactWood_light_002','footstep_snow_003'],
          'Small casing fragments; defeated-only, no explosion.',rms=-30)
    snake=mix((material('click_004',.023,high=3300),0,.32),
              (texture('scratch_002',.073,650,3700,seed=95,swell=.15),.005,.40),duration=.095)
    write('SnakeShot',snake,'Enemies',['click_004','scratch_002'],'Small dull mechanical air release.',rms=-31)


def make_ui_and_flow():
    for theme in ['DS','HS']:
        soft=theme=='DS'
        tactile='footstep_carpet_001' if soft else 'impactMetal_light_002'
        click='click_003' if soft else 'click_005'
        tone=material(tactile,.065,low=180 if soft else 450,high=2800 if soft else 3500)
        core=mix((material(click,.045,low=220,high=3000),0,.58),
                 (tone,.013,.22 if soft else .16),duration=.095)
        write('UiConfirm',core,'UI',[click,tactile],'Small tactile click + subordinate short rebound.',theme=theme,rms=-32)
        focus=material(click,.042,rate=1.015,low=450,high=2600)
        write('UiFocus',focus,'UI',[click],'Low-level focus tick; runtime rate limited, no touch hover.',theme=theme,rms=-39)
        cancel=mix((material(click,.047,rate=.87,low=150,high=2300),0,.45),
                   (material(tactile,.045,rate=.93,low=250,high=2400),.022,.15),duration=.085)
        write('UiCancel',cancel,'UI',[click,tactile],'Lower/shorter return of the same tactile family.',theme=theme,rms=-33)
        opening=mix((texture('scratch_001' if soft else 'scratch_002',.13,350,3000,seed=110+(not soft),swell=.4),0,.24),
                    (core,.051,.28),duration=.16)
        write('UiOpen',opening,'UI',['scratch_001' if soft else 'scratch_002',click,tactile],
              'Brief sampled-material pass and quiet seated latch.',theme=theme,rms=-34)
        reject=mix((cancel,0,.7),(cancel,.073,.5),duration=.165)
        write('UiReject',reject,'UI',[click,tactile],'Two dull contacts, no buzzer or sharp alarm.',theme=theme,rms=-31)
    # Shared semantic prompts are small edits of the established materials, not new melodic songs.
    tap=material('impactWood_light_001',.07,low=200,high=3000)
    pluck=material('pluck_002',.12,rate=.95,low=220,high=2800)
    glass=material('impactGlass_light_002',.20,low=400,high=2900)
    fabric=texture('footstep_carpet_003',.34,150,2600,seed=123,swell=.5)
    defs={
      'Ready': (mix((tap,0,.32),(pluck,.038,.16),duration=.14), ['impactWood_light_001','pluck_002'], -31),
      'AiToggle': (mix((material('switch_002',.052,high=2800),0,.35),(tap,.036,.13),duration=.115), ['switch_002','impactWood_light_001'], -33),
      'Connected': (mix((tap,0,.22),(pluck,.105,.25),(glass,.135,.12),duration=.32), ['impactWood_light_001','pluck_002','impactGlass_light_002'], -30),
      'Disconnected': (mix((material('impactSoft_medium_003',.11,rate=.84,high=2300),0,.4),(tap,.16,.14),duration=.31), ['impactSoft_medium_003','impactWood_light_001'], -27.5),
      'Upgrade': (mix((tap,0,.24),(pluck,.07,.20),(glass,.14,.19),duration=.39), ['impactWood_light_001','pluck_002','impactGlass_light_002'], -28),
      'Refresh': (mix((texture('footstep_grass_002',.18,700,3400,seed=124,swell=.25),0,.28),(tap,.15,.18),duration=.23), ['footstep_grass_002','impactWood_light_001'], -32),
      'NodeOpen': (mix((fabric,0,.30),(pluck,.24,.14),(glass,.41,.13),duration=.73), ['footstep_carpet_003','pluck_002','impactGlass_light_002'], -30),
      'Depart': (mix((texture('scratch_002',.40,300,3600,seed=125,swell=.65),0,.32),(tap,.35,.17),(glass,.38,.13),duration=.59), ['scratch_002','impactWood_light_001','impactGlass_light_002'], -28.5),
      'PlayerDown': (mix((material('impactSoft_heavy_002',.24,rate=.8,low=90,high=2700),0,.55),(material('footstep_carpet_003',.12,rate=.83,high=1900),.20,.24),duration=.43), ['impactSoft_heavy_002','footstep_carpet_003'], -25),
      'ReviveStart': (mix((fabric,0,.23),(pluck,.02,.16),duration=.36), ['footstep_carpet_003','pluck_002'], -32),
      'ReviveDone': (mix((pluck,0,.28),(glass,.12,.20),(material('impactGlass_light_003',.22,rate=1.05,low=400,high=3200),.23,.14),duration=.53), ['pluck_002','impactGlass_light_002','impactGlass_light_003'], -27),
      'Victory': (mix((tap,0,.2),(pluck,.09,.21),(glass,.26,.22),(material('impactGlass_light_003',.36,rate=.88,low=250,high=3000),.50,.17),(fabric,.65,.1),duration=1.12), ['impactWood_light_001','pluck_002','impactGlass_light_002','impactGlass_light_003','footstep_carpet_003'], -27),
      'Defeat': (mix((material('impactSoft_heavy_001',.25,rate=.8,low=75,high=2200),0,.45),(material('impactWood_light_001',.12,rate=.75,low=120,high=2000),.20,.16),(fabric,.31,.13),duration=.72), ['impactSoft_heavy_001','impactWood_light_001','footstep_carpet_003'], -27.5),
    }
    for cue,(x,names,rms) in defs.items():
        folder='Players' if cue in ['PlayerDown','ReviveStart','ReviveDone'] else 'Flow'
        write(cue,x,folder,names,'Shared prompt derived from short tactile materials; fixed semantic rhythm, no long reverb.',rms=rms)


def make_ambience():
    n=SR*20
    t=np.arange(n)/SR
    for index,(cue,low,high,level) in enumerate([
      ('AmbienceDusk',110,3500,-39),('AmbienceSky',140,2500,-40),('AmbienceRest',95,2600,-42)]):
        # Explicit synthetic approximation. Periodic FFT noise and slow integer-cycle
        # modulation make a continuous loop without a silent dip at the boundary.
        rnd=np.random.default_rng(20261005+index)
        freq=np.fft.rfftfreq(n,1/SR)
        shape=np.zeros_like(freq)
        mask=(freq>low)&(freq<high)
        shape[mask]=(freq[mask]/max(low,1))**(-.8)
        shape *= 1/(1+(freq/high)**8)
        base=rnd.normal(size=len(freq))+1j*rnd.normal(size=len(freq))
        base[0]=0
        wind=np.fft.irfft(base*shape,n)
        wind/=max(np.std(wind),1e-8)
        env=.62+.13*np.sin(2*np.pi*t/20+.4)+.09*np.sin(2*np.pi*t*3/20+index)
        left=wind*env
        right=np.roll(wind,int(.024*SR))*(.61+.11*np.sin(2*np.pi*t/20+.7)+.08*np.sin(2*np.pi*t*3/20+.5+index))
        stereo=np.stack([left,right],axis=1)
        names=[]
        if cue!='AmbienceSky':
            names=['footstep_grass_001','footstep_grass_004']
            for j,at in enumerate([1.7,5.3,10.2,14.8,18.]):
                grain=texture(names[j%2],.43,700,3200,seed=170+j+index*10,swell=.5)
                a=int(at*SR); b=a+len(grain)
                stereo[a:b,0]+=grain*.065
                stereo[a:b,1]+=grain*.045
        stereo*=db(level)/max(np.sqrt(np.mean(stereo*stereo)),1e-8)
        # Match last sample to the periodic next first sample over 128 samples only.
        correction=stereo[0]-stereo[-1]
        stereo[-128:]+=np.linspace(0,1,128)[:,None]*correction
        path=OUT/'Ambience'/f'AMB_{cue}_01.wav'
        path.parent.mkdir(parents=True,exist_ok=True)
        sf.write(path,stereo,SR,subtype='PCM_24')
        record(path,cue,stereo,names,
               'Synthetic filtered periodic wind with slow modulation; dusk/rest add quiet processed grass-footstep texture. NOT river/city/field recording. Pending ambience replacement/listening approval.',loop=True)
        CLIPS[(cue,None)]=[stereo]


def preview(name, events, duration, notes):
    output=np.zeros((int(duration*SR),2))
    ledger=[]
    for cue,theme,at,gain,pan,variant in events:
        x=CLIPS[(cue,theme)][variant%len(CLIPS[(cue,theme)])]
        if cue.startswith('Ambience'):
            x=x[:6*SR].copy()
            for channel in range(x.shape[1]):
                x[:,channel]=fade(x[:,channel], .25, .4)
        if x.ndim==1:
            x=np.stack([x*np.sqrt((1-pan)/2),x*np.sqrt((1+pan)/2)],axis=1)
        a=int(at*SR); count=min(len(x),len(output)-a)
        if count<=0: continue
        output[a:a+count]+=x[:count]*gain
        ledger.append(dict(cue=cue,theme=theme,start=round(at,4),gain=gain,pan=pan,variant=variant+1))
    peak=float(np.max(abs(output)))
    gain=min(1.,db(-4)/max(peak,1e-8))
    output*=gain
    path=WORK/'previews'/f'{name}.wav'
    path.parent.mkdir(parents=True,exist_ok=True)
    sf.write(path,output,SR,subtype='PCM_24')
    (path.with_suffix('.json')).write_text(json.dumps(dict(notes=notes,normalization_gain=gain,
        peak_dbfs=float(20*np.log10(max(np.max(abs(output)),1e-10))),events=ledger),ensure_ascii=False,indent=2),encoding='utf-8')


def make_previews():
    groups=[('01_DS_Shot',[('DsShot',None)]),('02_DS_Impact',[('DsHit',None),('DsSplash',None)]),
      ('03_HS_Laser',[('HsCharge',None),('HsFire',None),('HsHit',None)]),
      ('04_HS_Melee',[('HsSlashUp',None),('HsSlashDown',None),('HsSlashSweep',None),('HsWave',None)]),
      ('05_Hurt_Guard',[('PlayerHurtDs',None),('PlayerHurtHs',None),('GuardBlock',None),('GuardStart',None),('GuardEnd',None)]),
      ('06_Bubble_Doubao',[('BubblePop',None),('BubbleImpact',None),('DoubaoReveal',None),('DoubaoDefeat',None)]),
      ('07_UI_TwoThemes',[(c,t) for t in ['DS','HS'] for c in ['UiFocus','UiConfirm','UiCancel','UiOpen','UiReject']])]
    for name,cues in groups:
        events=[]; cursor=.3
        for cue,theme in cues:
            for v,x in enumerate(CLIPS[(cue,theme)]):
                events.append((cue,theme,cursor,1.,0.,v)); cursor+=max(.75,len(x)/SR+.45)
        preview(name,events,cursor+.4,'Sequential samples, no loudness normalization between cues. See event list for order. Human listening pending.')
    preview('08_Ambience_Candidates',[(c,None,i*7,1,0,0) for i,c in enumerate(['AmbienceDusk','AmbienceSky','AmbienceRest'])],
            21,'Six-second excerpts at 0/7/14s: Dusk, Sky, Rest. One second silence between excerpts, no overlap. Individual 20s loop files are in Assets/_Project/Audio/Ambience; NOT field recordings.')
    events=[]
    # 0-5s normal DS; 5-10s max 9 volleys/s, one shot per volley not per pellet.
    for start,stop,hz in [(0.3,5,3),(5.2,10,9)]:
        for i,at in enumerate(np.arange(start,stop,1/hz)):
            events.append(('DsShot',None,float(at),.7,-.25,i%3))
            if i%2==0: events.append(('DsHit',None,float(at+.06),.45,.1,i%2))
            if i%5==0: events.append(('DsSplash',None,float(at+.09),.4,.16,i%2))
    # 10.5-17s laser bursts; 0.18s intervals, one cue per volley not per array branch.
    for b,at in enumerate([10.5,12.7,14.9]):
        events.append(('HsCharge',None,at,.75,.2,0))
        for j in range(5):
            events.append(('HsFire',None,at+.5+j*.18,.64,.2,j%3))
            events.append(('HsHit',None,at+.54+j*.18,.34,0,j%2))
    for i,at in enumerate([17.3,17.7,18.15,18.8,19.2,19.65]):
        events.append((['HsSlashUp','HsSlashDown','HsSlashSweep'][i%3],None,at,.7,.18,0))
        events.append(('HsHit',None,at+.10,.4,0,i%2))
        if i%3==2: events.append(('HsWave',None,at+.09,.45,-.1,0))
    events += [('GuardBlock',None,20.4,.8,0,0),('PlayerHurtDs',None,21.05,.75,0,0)]
    for i,at in enumerate([21.8,21.88,22.15]): events.append(('BubblePop',None,at,.45,(i-1)*.15,i))
    events += [('DoubaoDefeat',None,22.7,.8,0,0),('NodeOpen',None,23.6,.85,0,0),
               ('UiConfirm','DS',24.6,.85,0,0),('UiConfirm','HS',25.2,.85,0,0),('Upgrade',None,25.9,.8,0,0)]
    preview('09_Density_28s',events,28,'OFFLINE audition montage, not a Unity loopback/performance or multiplayer verification. DS 3→9 volleys/s; HS 0.18s burst; melee; block/hurt; grouped bubbles; UI. No per-branch duplication. Runtime mixer may differ.')


def main():
    make_combat(); make_ui_and_flow(); make_ambience(); make_previews()
    manifest=dict(batch='20261005_CoreAudio',status='first_pass_pending_human_listening',
      sample_rate=SR,output_subtype='PCM_24',source_quality_note='Kenney Vorbis sources are 44.1kHz; decoding to 24-bit does not restore lost original information.',
      sources={name:{k:v for k,v in info.items() if k!='signal'} for name,info in SOURCES.items()},
      clips=CUES,total_clips=len(CUES),assets_total_bytes=sum((ROOT/r['path']).stat().st_size for r in CUES),
      validation=dict(finite_samples=True,no_clipping=all(r['peak_dbfs']<=-3 for r in CUES),
        short_clip_boundaries_zero=all(r['first_last_peak']<1e-5 for r in CUES if not r['loop']),
        listening_approved=False,in_game_verified=False,mobile_verified=False))
    assert manifest['validation']['no_clipping']
    assert manifest['validation']['short_clip_boundaries_zero']
    (WORK/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:manifest[k] for k in ['total_clips','assets_total_bytes','validation']},indent=2))


if __name__=='__main__': main()
