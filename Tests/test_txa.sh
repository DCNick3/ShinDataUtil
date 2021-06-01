
set -e

#DATADIR="/home/dcnick3/trash/switch-reverse/higurashi/romfs/data/"
DATARAWDIR="/home/dcnick3/trash/switch-reverse/higurashi/romfs/data-raw/"

TDIR="$(mktemp -d)"

while IFS= read -r -d '' txa
do
  rm -rf "${TDIR:?}"/*
  sdu txa-extract "$txa" "$TDIR/orig_txa"
  T="$(/usr/bin/time -f "%e" sdu txa-encode "$TDIR/orig_txa" "$TDIR/reenc.txa" 2>&1 1>/dev/null)"
  sdu txa-extract "$TDIR/reenc.txa" "$TDIR/reenc_txa"
  
  T="$(numfmt --padding=7 "$T")"
  
  OS="$(stat --printf="%s" "$txa")"
  RS="$(stat --printf="%s" "$TDIR/reenc.txa")"
  SD="$((RS-OS))"
  PERC="$(echo "scale=4; $SD/$OS*100" | bc)"
  
  printf '/%-40s %ss %10sB (%s%%)\n' "$(realpath --relative-to="$DATARAWDIR" "$txa")" "$T" "$SD" "$PERC"
  
  diff <(find "$TDIR/orig_txa" -type f -exec md5sum {} + | sort -k 2 | cut -f1 -d" ") \
    <(find "$TDIR/reenc_txa" -type f -exec md5sum {} + | sort -k 2 | cut -f1 -d" ")
done <   <(find "$DATARAWDIR" -name '*.txa' -xtype f -print0)

rm -rf "$TDIR"
