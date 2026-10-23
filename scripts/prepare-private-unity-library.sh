#!/bin/sh
# Exported personal room geometry never enters Git or CI; run only on developer Mac.
set -eu
: "${QPS_UNITY_LIBRARY_DIR:?Set to private HefuVR UnityLibraryExport/unityLibrary path}"
ROOT="$QPS_UNITY_LIBRARY_DIR"
test -s "$ROOT/build.gradle"
test -s "$ROOT/src/main/AndroidManifest.xml"
# Unity 2022 exports its own LAUNCHER intent-filter. The Kotlin QPS MainActivity
# must be the sole launcher; UnityPlayerActivity is entered from our Home button.
python3 - "$ROOT/src/main/AndroidManifest.xml" <<'PY'
from pathlib import Path
import sys,xml.etree.ElementTree as ET
p=Path(sys.argv[1]);t=p.read_text()
name='{http://schemas.android.com/apk/res/android}name'
root=ET.fromstring(t)
for act in root.findall('.//activity'):
 if act.get(name)=='com.unity3d.player.UnityPlayerActivity':
  for f in list(act.findall('intent-filter')):
   act.remove(f)
  act.set('{http://schemas.android.com/apk/res/android}exported','false')
ET.register_namespace('android','http://schemas.android.com/apk/res/android')
ET.register_namespace('tools','http://schemas.android.com/tools')
p.write_text(ET.tostring(root,encoding='unicode'))
print('Private Unity manifest: UnityPlayerActivity internal-only, no second launcher')
PY
# Exported photo picker has resources, so AGP 8.x requires it as a resolved AAR
# dependency. Keep its miniature Maven repository in PRIVATE generated output.
python3 - "$ROOT/build.gradle" "$ROOT/libs/HefuPhotoPicker.aar" "$ROOT/../private-maven" <<'PYGRADLE'
from pathlib import Path
import sys,shutil,os,re
p=Path(sys.argv[1]);aar=Path(sys.argv[2]);repo=Path(sys.argv[3]);assert aar.is_file()
target=repo/'com/hefu/photopicker/1.0';target.mkdir(parents=True,exist_ok=True)
shutil.copy2(aar,target/'photopicker-1.0.aar')
(target/'photopicker-1.0.pom').write_text('''<?xml version="1.0" encoding="UTF-8"?>
<project xmlns="http://maven.apache.org/POM/4.0.0"><modelVersion>4.0.0</modelVersion>
<groupId>com.hefu</groupId><artifactId>photopicker</artifactId><version>1.0</version><packaging>aar</packaging>
</project>
''')
s=p.read_text()
s=s.replace("implementation(name: 'HefuPhotoPicker', ext:'aar')", "implementation 'com.hefu:photopicker:1.0'")
s=s.replace("implementation files('libs/HefuPhotoPicker.aar')", "implementation 'com.hefu:photopicker:1.0'")
# Idempotently normalize the local NDK path without storing user-specific paths in Git.
version='23.1.7779620'
sdk=Path(os.environ.get('ANDROID_HOME',str(Path.home()/'Library/Android/sdk')))
ndk=sdk/'ndk'/version
assert ndk.is_dir(),f'Missing Unity IL2CPP Android NDK {ndk}'
s=re.sub(r'(?m)^\s*ndkVersion [^\n]*\n','\n',s)
s=re.sub(r'(?m)^\s*ndkPath [^\n]*\n','\n',s)
s=s.replace('    namespace "com.unity3d.player"',
            '    namespace "com.unity3d.player"\n    ndkVersion "'+version+'"\n    ndkPath "'+str(ndk)+'"',1)
p.write_text(s)
print('PRIVATE_PHOTOPICKER_MAVEN_READY')
PYGRADLE
echo "PRIVATE_UNITY_PREPARE_PASS $ROOT"
