#!/bin/bash
SP=/tmp/claude-1000/-home-rex-Claude-Player-Count/14a1566d-e774-4a84-b792-bd2c9557aa10/scratchpad
D=/home/rex/Claude/WorldAlteringEditor/src/TSMapEditor/bin/Debug/net8.0
GAMEDIR="/home/rex/snap/cncra2yr/common/.wine/drive_c/Westwood/RA2"
cp -rf "$SP/yrcfg/Config/." "$D/Config/"
perl -i -pe 's/ExpectedClientExecutableName=ra2md\.exe/ExpectedClientExecutableName=ra2md.exe,gamemd-spawn.exe,gamemd.exe/' "$D/Config/Default/Constants.ini"
cp "$SP/yrcfg/Config/DefaultSettings.ini" "$D/MapEditorSettings.ini"
python3 - "$GAMEDIR" "$D/MapEditorSettings.ini" <<'PY'
import sys
gd,p=sys.argv[1],sys.argv[2]
s=open(p,encoding="utf-8",errors="replace").read().splitlines()
out=[];done=False
for ln in s:
    out.append(ln)
    if ln.strip()=="[General]" and not done:
        out.append("GameDirectory="+gd); done=True
if not done: out=["[General]","GameDirectory="+gd]+out
open(p,"w",encoding="utf-8").write("\n".join(out))
PY
echo "YR setup applied"
grep -a "ExpectedClientExecutableName" "$D/Config/Default/Constants.ini"
