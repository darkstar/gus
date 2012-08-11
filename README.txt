GUS - General Unpack Shell

This aims to be a small, extensible tool to unpack (and pack) various archives, data files
from games, etc.

The name is inspired by an old DOS utility which did something similar with archive files.

GUS' syntax is quite simple. It has 3 major modes, list, unpack and pack.

Listing files:
gus list [-t type] [-lf listfile] filename.ext

This lists the files contained in the given archive file (if supported). The format is
usually autodetected but you can specify it via the [-t type] option if autodetect doesn't
work correctly. See "gus -ll" for a list of all supported formats.
The [-lf listfile] command, if specified, writes a list of filenames to the specified
listfile, so that you can later repack the file with all entries in the same order as in
the original (required for some game data files)


Unpacking files:
gus unpack [-t type] [-lf listfile] [-d directory] filename.ext

This unpacks the specified file filename.ext either into the current directory or in the
destination directory specified with [-d directory]. If [-lf listfile] is specified, a
list of files unpacked is written into that file for repacking later on.
Again, [-t type] can be used if the autodetection does not work (right)


Packing files:
gus pack -t type [-lf listfile] [-d directory] filename.ext [filespec]

Packing is not supported for all unpacker modules (the program is mainly intended for
unpacking anyway ;-)
You have to explicitly specify a file format with [-t type] here, of course. If the
files you want to pack are in a different directory, use [-d directory] to specify
a base directory. If you have a listfile (and you should, at least for now) you can
specify it with [-lf listfile]. All file names in the list file are interpreted relative
to the base directory specified with -d. If you don't specify a listfile, you have to
at least give some [filespec] after the filename, like *.txt or something like that.
NOTE: This is not yet implemented, you MUST use a listfile for now


Other options
gus -h shows some help

gus -ll shows all available unpacker modules, along with some flags:

C:\>gus -ll
[GUS] - General Unpack Shell  v1.0
(C) 2012 by Darkstar <darkstar@drueing.de>

Registered Data Transformers:
zlib_dec             zlib decompressor                        v1.0
zlib_cmp             zlib compressor                          v1.0
xor                  xor data transformer                     v1.0

Registered Unpackers:
ethervapor.pac       EtherVapor PAC file                      v1.0 [PS-N-]
elsword.kom          ElSword KOM file                         v1.0 [---NX]
ys.ysf               Y's Online YSF file                      v1.0 [-S-NX]
pvz.pak              Plats vs. Zombies PAK file               v1.0 [-S-NX]
grandia2.afs         Grandia II AFS file                      v1.0 [----X]
falcom.pac           FALCOM Farland Symphony PAC file         v1.0 [---NX]
zip                  generic ZIP file                         v1.0 [PSTN-]
falcom.fs2.dat       FALCOM Farland Symphony 2 DAT file       v1.0 [PSTN-]
falcom.zwei.dat      FALCOM Zwei/Zwei2 DAT file               v1.0 [---NX]
grimrock.dat         Grimrock DAT file                        v1.0 [P---X]

The Data Transformers are internal only (they are used to pack, unpack or otherwise
transform data during pack or unpack).

The interestin part is the "Registered unpackers" list. It shows (in that order)
- The "type" string to be used for each (un)packer (specified with -t)
- A cleartext description of the (un)packer module / file format
- A version number
- Some flags:
  P  - files in this format can be re-packed (with "gus pack")
  S  - the unpacker supports unpacking (and packing, if P is also set) of
       subdirectory structures
  T  - the unpacker module supports timestamps on files (NOTE: this is
       currently unimplemented)
  N  - the unpacker module supports file names (if not set, files are indexed
       by some ID or hash)
  X  - the unpacker module is experimental

A note to the "N" flag and packing:
if this flag is set, you can generally pack arbitrary files into the archive. But if
it is not set, then you really should use a listfile, because the order of the files in
the archive might be important, and only listfiles guarantee the files are written back
in the same order as when they were initially unpacked.
Also, if the flag ist NOT set, you should NOT alter the filenames of the unpacked files
(you may, however, alter the contents), simply because the file name is under the
control of the unpacker module and might contain information needed on repacking. For
instance, if the files are indexed via a hash of their filename, that hash will be the
filename. If you change the filename, the file will not be found by the game/program
later on. Also there might be additional flags that the unpacker stores in the filename
(e.g. if the file was originally packed or not, or if it was saved in a specific format)

Other than that, have fun!
-Darkstar