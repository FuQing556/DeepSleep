"""Content cues from existing CC0 recordings plus explicitly synthetic breath-flute.
No AI audio generator; preserve source credits from 20261005_CoreAudio/SOURCES.md.
"""
import importlib.util
import json
from pathlib import Path

WORK = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('core', WORK.parent/'20261005_CoreAudio/build_audio.py')
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
np = b.np

def glass(duration=.25, rate=1):
    return b.material('glass_001',duration,rate=rate,low=400,high=6000,release=.06)

def air(duration, high=3800, seed=1):
    return b.texture('footstep_carpet_000',duration,low=500,high=high,seed=seed,grain=.03)

def flute(duration=2.4):
    t=np.arange(int(duration*b.SR))/b.SR
    # A gentle physically-inspired breath tone, not claimed to be a recorded flute.
    phase=2*np.pi*523.25*t + .045*np.sin(2*np.pi*4.7*t)
    tone=np.sin(phase)+.16*np.sin(2*phase)+.035*np.sin(3*phase)
    breath=air(duration,2100,17)
    return b.fade(tone*np.sin(np.pi*t/duration)**.65*.2+breath*.045,.12,.2)

def emit(name, signal, sources, recipe, rms=-27, variant=1):
    return b.write(name,signal,'Encounters',sources,recipe,variant=variant,rms=rms,peak=-6)

def main():
    # Discrete material attack, airy body, short dark tail: no sirens or excessive pitch sweeps.
    bell=b.material('confirmation_001',.7,rate=.72,low=250,high=3200,release=.18)
    soft=b.material('impactSoft_medium_000',.25,rate=.8,high=2200)
    for name,duration,gain in [('KimiReveal',1.5,.5),('KimiPhase',1.1,.35),('KimiPrism',.7,.28),('KimiDefeat',1.8,.45)]:
        emit(name,b.mix((air(duration,3200),0,.22),(bell,.05,gain),(glass(.45,.75),.15,.14),duration=duration),
             ['footstep_carpet_000','confirmation_001','glass_001'],'Soft air + muted glass + quiet resonant confirmation tail; no electronic riser.')
    for name,duration in [('KimiMoonWarn',.23),('KimiMoonFire',.32),('KimiLaserCharge',1.15),('KimiLaserFire',.5),('KimiTide',.95)]:
        emit(name,b.mix((air(duration,4500 if name=='KimiMoonFire' else 3000),0,.7),(soft,0,.16),
                       (glass(min(.25,duration),1.1),.01,.12),duration=duration),
             ['footstep_carpet_000','impactSoft_medium_000','glass_001'],'Cloth-derived directional air, soft body and restrained crystalline edge.',rms=-25 if name in ['KimiLaserFire','KimiTide'] else -29)
    emit('KimiFlute',flute(),['footstep_carpet_000'],'Synthetic breath-flute A/B harmonic body, slow vibrato, source-grain breath; faded loop boundaries.',rms=-29)
    for name in ['KimiOrb','KimiReflect','KimiMirrorBreak','KimiInterrupt','KimiHit']:
        for v in range(1,3):
            long=name in ['KimiMirrorBreak','KimiInterrupt']
            duration=.45 if long else .14
            emit(name,b.mix((glass(duration,.8+v*.07),0,.6),(soft,0,.1),
                (air(duration,3600,v),.01,.15),duration=duration),
                ['glass_001','impactSoft_medium_000','footstep_carpet_000'],'Small glass material tick; break/interrupt spread fragments with air tail.',rms=-26 if long else -32,variant=v)
    for name in ['DownloadCharge','DownloadDash','DownloadDefeat','DownloadImpact','GuardBlockMetal','GuardDefeat','GuardImpact']:
        duration=.62 if name.endswith('Charge') else .42 if name.endswith('Defeat') else .22
        metal=b.material('impactMetal_light_000',duration,rate=.85,low=150,high=3500,release=.05)
        emit(name,b.mix((metal,0,.65),(air(duration,2800),0,.6 if 'Dash' in name else .1),(soft,.025,.25),duration=duration),
             ['impactMetal_light_000','footstep_carpet_000','impactSoft_medium_000'],'Dry softened metal/chassis impulse; dash uses stronger cloth-air, no alarm beeps.',rms=-28)
    assert all(x['peak_dbfs']<=-6 and x['first_last_peak']<1e-5 for x in b.CUES)
    (WORK/'manifest.json').write_text(json.dumps({'clips':b.CUES,'human_listening_approved':False,'source_note':__doc__},indent=2,ensure_ascii=False),encoding='utf-8')
    preview=np.concatenate([np.concatenate([b.CLIPS[(n,None)][0],np.zeros(int(.35*b.SR))]) for n in dict.fromkeys(x['cue'] for x in b.CUES)])
    b.sf.write(WORK/'audition.wav',preview,b.SR,subtype='PCM_24')
    print('Produced',len(b.CUES),'clips; finite, boundary fade and -6dBFS peak verified.')

if __name__=='__main__': main()
