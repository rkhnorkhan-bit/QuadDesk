"""Checks shipped JSON/XML assets, NOT the C# implementation or Windows behavior."""
import json, math, pathlib, xml.etree.ElementTree as ET
root = pathlib.Path(__file__).resolve().parents[1]
checks = 0

def calculate(node, rect, ids):
    x,y,w,h = rect
    assert w>0 and h>0
    if node['type'] == 'zone':
        assert node['id'] not in ids and node['name']
        ids.add(node['id']); return [(node['id'],rect)]
    assert node['type']=='split' and node['orientation'] in ('vertical','horizontal')
    ratio=node['ratio']; assert 0<ratio<1 and math.isfinite(ratio)
    vertical=node['orientation']=='vertical'; cut=math.floor((w if vertical else h)*ratio+.5)
    a,b=((x,y,cut,h),(x+cut,y,w-cut,h)) if vertical else ((x,y,w,cut),(x,y+cut,w,h-cut))
    return calculate(node['first'],a,ids)+calculate(node['second'],b,ids)

def overlap(a,b):
    x,y,w,h=a;X,Y,W,H=b
    return max(0,min(x+w,X+W)-max(x,X))*max(0,min(y+h,Y+H)-max(y,Y))

layouts=[]
for path in sorted((root/'layouts').glob('*.json')):
    data=json.loads(path.read_text()); assert data['id']==path.stem; layouts.append(data)
    for area in [(0,0,3840,2160),(2560,0,3839,2159),(-3840,0,3840,2160),(0,-2160,3840,2160),(0,0,2160,3840)]:
        zones=calculate(data['root'],area,set())
        assert sum(w*h for _,(_,_,w,h) in zones)==area[2]*area[3]
        for i,(_,a) in enumerate(zones):
            assert overlap(a,area)==a[2]*a[3]
            for _,b in zones[i+1:]:assert overlap(a,b)==0
        checks+=1
main=next(l for l in layouts if l['id']=='main-plus-3')
assert [r for _,r in calculate(main['root'],(0,0,3840,2160),set())]==[(0,0,2496,2160),(2496,0,1344,720),(2496,720,1344,720),(2496,1440,1344,720)]
quad=next(l for l in layouts if l['id']=='quad')
assert all(r[2:]==(1920,1080) for _,r in calculate(quad['root'],(0,0,3840,2160),set()))
config_path = root/'config.json' if (root/'config.json').is_file() else root/'config.default.json'
config=json.loads(config_path.read_text())
assert config['targetDevicePath'] is None and config['activeLayoutId'] in {l['id'] for l in layouts}
assert config['enabled'] is True and config['minimumZoneWidth']==200
for path in list(root.rglob('*.csproj'))+[root/'Directory.Build.props',root/'src/QuadDesk/app.manifest']:
    ET.parse(path)
for required in ['src/QuadDesk/Program.cs','src/QuadDesk/Assets/QuadDesk.ico','src/QuadDesk.Core/LayoutEngine.cs','tests/QuadDesk.Tests/LayoutTests.cs','build.ps1','publish.ps1','README.md','BUILD.md','LICENSE']:
    assert (root/required).is_file(), required
print(json.dumps({'status':'PASS','scope':'JSON layout assets + XML/project presence only; not C# execution','layouts':len(layouts),'partition_cases':checks,'main_plus_3_exact':True,'quad_exact':True,'config_target_unselected':True},indent=2))

for script in ['build.ps1','publish.ps1','Update-Portable.ps1']:
    (root/script).read_text(encoding='ascii')
