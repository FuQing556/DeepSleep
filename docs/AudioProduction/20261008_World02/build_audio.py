"""World02 sample-based sound design. Reuses preserved Kenney CC0 recordings.
No generative audio, speech, sirens, or musical risers. Output is not listening approval.
"""
import importlib.util
import json
from pathlib import Path

WORK = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('core', WORK.parent/'20261005_CoreAudio/build_audio.py')
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
np = b.np

def mat(name, duration, rate=1, high=4000):
    return b.material(name, duration, rate=rate, low=130, high=high, release=.045)

def air(duration, seed=0, high=3300):
    return b.texture('footstep_carpet_000', duration, high=high, seed=seed, grain=.035)

def emit(name, layers, duration, sources, recipe, rms=-28, variants=1):
    for v in range(variants):
        x = b.mix(*layers, duration=duration)
        # Small material-rate variation, not random high pitch chirps.
        if v:
            x = b.signal.resample(x, int(len(x) * (1+.025*v)))
        b.write(name, x, 'World02', sources, recipe, variant=v+1, rms=rms, peak=-6)

def main():
    soft = lambda t: mat('impactSoft_medium_000', t, .72, 1900)
    rubber = lambda t: mat('impactSoft_medium_001', t, .82, 2700)
    glass = lambda t: mat('glass_001', t, .88, 5500)
    paper = lambda t: b.texture('scratch_002', t, low=450, high=3700, grain=.026, seed=17)
    for name, duration, split in [('RecursiveHit',.16,False),('RecursiveSplit',.38,True),
                                 ('RecursiveDefeat',.22,True),('RecursiveImpact',.23,False)]:
        emit(name, [(soft(duration),0,.65),(rubber(duration),.016,.3),
                    (rubber(.12),.10 if split else .03,.18)], duration,
             ['impactSoft_medium_000','impactSoft_medium_001'],
             'Low rubber/soft-body pop; split adds staggered smaller droplets. Dry, no cartoon boing.',
             rms=-31 if name=='RecursiveHit' else -28, variants=2)
    for name, duration in [('QuickAppImpact',.21),('QuickAppDefeat',.38)]:
        emit(name,[(mat('impactPlate_light_000',duration,.95,4000),0,.5),
                   (paper(duration),.025,.25),(soft(.18),0,.2)],duration,
             ['impactPlate_light_000','scratch_002','impactSoft_medium_000'],
             'Light plastic/card fragments, short chassis contact, no advertising voice or notification spam.',variants=2)
    for name, duration in [('ClaudeReveal',1.4),('ClaudePhase',.8),('ClaudeDefeat',1.5)]:
        emit(name,[(air(duration),0,.3),(glass(.6),.1,.18),(soft(.3),.05,.18),
                   (mat('confirmation_001',duration,.67,2600),.1,.17)],duration,
             ['footstep_carpet_000','glass_001','impactSoft_medium_000','confirmation_001'],
             'Muted glass geometry with soft air and dark resonant tail; defeat recedes without fanfare.',rms=-26)
    emit('ClaudeCutWarning',[(paper(1.15),0,.3),(glass(.15),0,.12)],1.2,
         ['scratch_002','glass_001'],'A restrained drawing/scribing line precedes the cut.',rms=-30)
    for name, duration in [('ClaudeCutFire',.55),('ClaudeTrackingFire',.28)]:
        emit(name,[(air(duration,2,5300),0,.6),(glass(.22),0,.35),(soft(.15),.015,.23)],duration,
             ['footstep_carpet_000','glass_001','impactSoft_medium_000'],
             'Sharp glass edge plus broad directional cloth-air. Fullscreen is wider/longer, not louder per line.',
             rms=-24 if name=='ClaudeCutFire' else -27,variants=2)
    emit('ClaudeTrackingLock',[(mat('tick_001',.14,1,3500),0,.6),(glass(.15),.03,.12)],.2,
         ['tick_001','glass_001'],'One short geometric lock click; no repeating alarm.',rms=-29)
    for name, duration in [('ClaudeEnergyCharge',1.8),('ClaudeEnergyFire',.55),('ClaudeEnergyBurst',1.1)]:
        emit(name,[(air(duration,5,2800),0,.6),(soft(min(duration,.3)),0,.3),
                   (glass(min(duration,.4)),.04,.17)],duration,
             ['footstep_carpet_000','impactSoft_medium_000','glass_001'],
             'Dense air pressure and softened glass particles; explosion has dark body and finite dissipation tail.',
             rms=-25 if name=='ClaudeEnergyBurst' else -28)
    for name, duration in [('ClaudeBookOpen',.42),('ClaudeBookSeal',.18),('ClaudeBookBreak',.42)]:
        emit(name,[(paper(duration),0,.65),(glass(min(duration,.2)),.02,.16)],duration,
             ['scratch_002','glass_001'],'Dry paper unfurl/pressed seal/torn pages; only one cue for a simultaneous batch.',rms=-29)
    for name, duration in [('ClaudeHit',.14),('ClaudeImpact',.24)]:
        emit(name,[(glass(duration),0,.22),(soft(duration),0,.55)],duration,
             ['glass_001','impactSoft_medium_000'],'Small geometric shield tap or player contact, subordinate to weapon feedback.',
             rms=-32 if name=='ClaudeHit' else -28,variants=2)
    for name, reverse in [('SceneStateWarning',False),('SceneStateStart',False),('SceneStateEnd',True)]:
        emit(name,[(mat('switch_002',.15,.9,3000),0,.4),
                   (b.material('confirmation_001',.25,rate=.85,reverse=reverse,high=2600),.03,.15)],.3,
             ['switch_002','confirmation_001'],'One dry state edge, quiet confirmation/reversed release. No sound per warning corner.',rms=-31)
    for name, high, seed in [('AmbienceCyber',1800,80),('AmbienceRain',4800,81),('AmbienceArcade',1200,82)]:
        duration=12
        x=b.texture('footstep_carpet_000',duration,low=150,high=high,grain=.06,seed=seed,swell=.5)
        if name=='AmbienceRain':
            x=b.mix((x,0,.7),(b.texture('footstep_grass_000',duration,low=1800,high=6000,seed=seed),0,.16),duration=duration)
        x=b.master(x,-38,-14,tail=.08)
        path=b.OUT/'Ambience'/('AMB_'+name+'_01.wav');path.parent.mkdir(parents=True,exist_ok=True)
        b.sf.write(path,x,b.SR,subtype='PCM_24')
        b.record(path,name,x,['footstep_carpet_000']+(['footstep_grass_000'] if name=='AmbienceRain' else []),
                 'Quiet filtered source-grain room/air/rain bed; zero endpoints and crossfaded transitions, no invented recording claim.',loop=True)
    assert all(x['peak_dbfs']<=-6 and x['first_last_peak']<1e-5 for x in b.CUES)
    (WORK/'manifest.json').write_text(json.dumps({'clips':b.CUES,'human_listening_approved':False,
        'source_credits':'../20261005_CoreAudio/SOURCES.md','source_note':__doc__},indent=2,ensure_ascii=False),encoding='utf8')
    preview=np.concatenate([np.concatenate([b.CLIPS[(n,None)][0],np.zeros(int(.3*b.SR))])
                            for n in dict.fromkeys(x['cue'] for x in b.CUES if not x['loop'])])
    b.sf.write(WORK/'audition.wav',preview,b.SR,subtype='PCM_24')
    print('Produced',len(b.CUES),'World02 clips; finite samples / zero boundaries / peak ceilings passed.')

if __name__=='__main__': main()
