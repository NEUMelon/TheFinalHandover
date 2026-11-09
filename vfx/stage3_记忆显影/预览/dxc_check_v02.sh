#!/bin/bash
# 用 DXC 对 .shader 文件里的 HLSLPROGRAM 块做语法 / 类型检查（模拟 Unity D3D11 平台宏）。不是 Unity 编译器，只作旁证。
SH="$1"; T=/tmp/tmp.2SzTbppNSs; OUT=$T/sc
python3 -I - "$SH" "$OUT/body.hlsl" <<'PY'
import sys,re
s=open(sys.argv[1],encoding='utf-8').read()
m=re.search(r'HLSLPROGRAM(.*?)ENDHLSL',s,re.S)
open(sys.argv[2],'w').write(m.group(1))
PY
cd $T/inc
for stage in vert:vs_6_0:SHADER_STAGE_VERTEX frag:ps_6_0:SHADER_STAGE_FRAGMENT; do
  IFS=: read E P S <<< "$stage"
  echo "== $E ($P)"
  LD_LIBRARY_PATH=$T/dxc/lib $T/dxc/bin/dxc -T $P -E $E -HV 2018 -I $T/inc \
    -D SHADER_API_D3D11=1 -D SHADER_TARGET=45 -D UNITY_VERSION=202230 -D $S=1 -D UNITY_COMPILER_DXC=1 \
    -Wno-unknown-pragmas -Wno-ignored-attributes $OUT/body.hlsl -Fo $OUT/out_$E.bin 2>&1 | grep -v "^$" | head -40
  echo "exit=${PIPESTATUS[0]}"
done
