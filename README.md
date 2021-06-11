 
# Overview

This is an utility for working with game files of Higurashi no Naku Koro ni visual novel released for Nintendo Switch.

This is very much work in progress, so don't expect much.

## Usage

`dotnet run subcommand subcommand-args`

Subcommands:

- `rom-list romname` - list files in a rom

- `rom-extract-all [--regex=filename_regex] romname destination-directory` - extract raw files (optionally filtered by regex) from rom

- `rom-extract-all-with-decode [--regex=filename_regex] romname destination-directory` - extract files (optionally filtered by regex) from rom, decoding known formats

- `[rom-]pic-decode [romname] picname outname` - decode a PIC file (optionally from rom) to png

- `[rom-]sound-remux [romname] nxaname outname` - remux a NXA file (optionally from rom)to opus

- `[rom-]font-extract [romname] fntname outname` - extract an FNT file (optionally from rom) to a bunch of png files

- `[rom-]txa-extract [romname] txaname outname` - extract an TXA file (optionally from rom) to a bunch of png files

- `[rom-]sysse-extract [romname] syssename outname` - extract a sysse.bin file (optionally from rom) to a bunch of wav files

- `[rom-]bustup-extract [romname] bustupname outname` - extract a BUP file (optionally from rom) to a bunch of png files

- `[rom-]mask-extract [romname] mskname outname` - extract a MSK file (optionally from rom) to a png file

- `[rom-]scenario-decompile [romname] snrname outname` - disassemble an SNR file (optionally from rom) to a listing, a json file containg head data and raw dump of code section

- `scenario-build asmdir outname` - build an SNR file from a listing and json file containing head data (same structure as decompiler)

- `rom-replace-file origrom srcfile targetname outrom` - quickly replace a file in ROM by another one. Does not remove previous file contents, so avoid using this repeatedly

- `rom-build outrom [infile targetname]...` - build a ROM from scratch from specified pairs of source files and their target names

- `rom-build-from-dir outrom indir` - build a ROM from scratch from specified root directory. All files in the directory will be put to the rom with the same name

- `txa-encode srcdir outtxa` - build a TXA from the specified directory (needs an `index.txt` file to function)

- `scenario-layout [--ignore-logset] fntfile asmfile outasmfile` - transform the specified scenario asm file, doing word wrapping according to mixed English/Japanese rules. Specify `--ignore-logset` to make the layouter not modify messages of `LOGSET`. Useful, as the original game script has a typo there (`r@` vs `@r`).