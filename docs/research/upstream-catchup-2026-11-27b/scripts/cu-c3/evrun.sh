#!/usr/bin/env bash
# CU-C3 (2026-11-27), from 2026-11-18 cu-c3/CU-C3-evrun.sh.
# CU-C3 (2026-11-27b): copied from 2026-11-27 scripts/cu-c3/evrun.sh.
# usage: evrun.sh <checkout> <label>  every plugin-conformance evidence test file, one result line each
cd "$1/tooling/plugin-conformance" || exit 2
OUT=/workspaces/logs/CU-C3/ev-$2; mkdir -p $OUT; : > $OUT/summary.txt
export CI=true TZ=UTC
for f in test/*.test.ts test/*.test.mjs; do
  n=$(basename $f)
  timeout 1200 node --import=tsx --conditions=development --test $f > $OUT/$n.log 2>&1; e=$?
  p=$(grep -E "^ℹ pass" $OUT/$n.log | awk '{print $3}'); x=$(grep -E "^ℹ fail" $OUT/$n.log | awk '{print $3}')
  echo "$n exit=$e pass=$p fail=$x" >> $OUT/summary.txt
done
awk '{for(i=1;i<=NF;i++){if($i~/^pass=/){split($i,a,"=");P+=a[2]} if($i~/^fail=/){split($i,b,"=");F+=b[2]}}} END{print "TOTAL pass="P" fail="F}' $OUT/summary.txt >> $OUT/summary.txt
echo EVDONE
