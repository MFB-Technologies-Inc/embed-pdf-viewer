import re,sys
# usage: panics.py cargo.log -> "binary :: test :: first message line" per failure
bin=None; cur=None; out={}; grab=0
for line in open(sys.argv[1],errors="replace"):
    line=line.rstrip("\n")
    m=re.match(r"^\s+Running (\S+)",line)
    if m: bin=re.sub(r"-[0-9a-f]{16}$","",m.group(1)); continue
    m=re.match(r"^---- (.*) stdout ----$",line)
    if m: cur=m.group(1); grab=1; continue
    if cur and grab:
        if "panicked at" in line: continue
        if line.strip()=="" : continue
        out[f"{bin} :: {cur}"]=re.sub(r"\d+(\.\d+)?(ms|s)\b","",line)[:300]; cur=None
for k in sorted(out): print(k,"::",out[k])
