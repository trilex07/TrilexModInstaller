"""Extract existing log measurements without treating wall spans as GPU Busy."""
import argparse,csv,hashlib,json,re,statistics,math
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--singularity',type=Path,required=True)
parser.add_argument('--darkness',type=Path,required=True)
parser.add_argument('--out',type=Path,required=True)
args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=True)
def read(path):
    raw=path.read_bytes()
    return raw.decode('utf-8-sig',errors='replace').splitlines(),hashlib.sha256(raw).hexdigest()
s,sh=read(args.singularity);d,dh=read(args.darkness)
rows=[];mode='Unknown'
for number,line in enumerate(s,1):
    m=re.search(r'SceneMode: \w+ -> (\w+)',line)
    if m:mode=m[1]
    m=re.search(r'perf: ([\d.]+) fps \(([\d.]+) ms/frame\).*?peak (\d+), split (\d+)',line)
    if m:rows.append(dict(line=number,mode_at_report=mode,fps=float(m[1]),frame_ms=float(m[2]),peak_draws=int(m[3]),split_draws=int(m[4])))
    if 'frame budget:' in line and rows:
        for field,pattern in [('copy_ms',r'copy ([\d.]+)'),('gameplay_hooks_ms',r'gameplay hooks ([\d.]+)'),('residual_ms',r'our work ([\d.]+)')]:
            m=re.search(pattern,line)
            if m:rows[-1][field]=float(m[1])
with (args.out/'singularity-windows.csv').open('w',newline='') as f:
    w=csv.DictWriter(f,fieldnames=['line','mode_at_report','fps','frame_ms','peak_draws','split_draws','copy_ms','gameplay_hooks_ms','residual_ms']);w.writeheader();w.writerows(rows)
cadence=[]
for number,line in enumerate(d,1):
    m=re.search(r'\[XRCadence\] layers=([\d.]+)/s game-boundaries=([\d.]+)/s world-bearing=([\d.]+)/s',line)
    if m:cadence.append(dict(line=number,layers_s=float(m[1]),boundaries_s=float(m[2]),world_s=float(m[3])))
with (args.out/'darkness-cadence.csv').open('w',newline='') as f:
    w=csv.DictWriter(f,fieldnames=['line','layers_s','boundaries_s','world_s']);w.writeheader();w.writerows(cadence)
def stats(values):
    if not values:return {'count':0}
    ordered=sorted(values)
    return dict(count=len(values),min=min(values),median=statistics.median(values),nearest_rank_p95=ordered[math.ceil(len(values)*.95)-1],max=max(values))
summary={'note':'Unweighted log-window summaries; modes can change within a window. No compositor-drop, GPU Busy, before/after or causal inference.',
    'input_sha256':{'singularity':sh,'darkness':dh},'singularity_windows':len(rows),
    'singularity_by_mode':{mode:stats([r['fps'] for r in rows if r['mode_at_report']==mode]) for mode in sorted({r['mode_at_report'] for r in rows})},
    'darkness_world_only_windows':stats([r['world_s'] for r in cadence if r['world_s']>0 and r['world_s']==r['layers_s']])}
for field,pattern in [('right_restore_wall_ms',r'right\+restore=([\d.]+)'),('transfer_join_wall_ms_per_job',r'mainJoinMean=([\d.]+)ms'),('nis_gpu_pair_ms',r'GPU pair dispatch sample=([\d.]+)ms')]:
    summary[field]=stats([float(m[1]) for line in d if (m:=re.search(pattern,line))])
(args.out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary,indent=2))
