#!/bin/bash
# Builds a small LGPL FFmpeg with only what AULA Studio uses: reading images and videos, its filters, and
# raw/PNG output. No audio and no video encoders. Run in an MSYS2 UCRT64 shell:
#   pacman -S --needed make nasm diffutils mingw-w64-ucrt-x86_64-{gcc,dav1d,zlib,pkgconf}
#   bash portable/build-ffmpeg.sh <work folder>
# The result is <work folder>/install: bin\ffmpeg.exe, bin\ffprobe.exe and LICENSE.txt, as build.ps1 expects.
set -euo pipefail
version=8.1
work=$(realpath "$1")
mkdir -p "$work"
cd "$work"
curl -fsSL --retry 5 --retry-all-errors -C - -o "ffmpeg-$version.tar.xz" "https://ffmpeg.org/releases/ffmpeg-$version.tar.xz"
rm -rf "ffmpeg-$version"
tar xJf "ffmpeg-$version.tar.xz"
cd "ffmpeg-$version"

./configure --prefix="$work/install" \
    --disable-everything --disable-autodetect --disable-doc --disable-debug --disable-network \
    --disable-swresample --disable-avdevice --disable-ffplay \
    --enable-small --enable-w32threads --enable-zlib --enable-libdav1d \
    --enable-static --disable-shared --pkg-config-flags=--static --extra-ldflags=-static \
    --enable-protocol=file,pipe \
    --enable-demuxer=gif,mov,matroska,avi,asf,flv,mpegts,mpegps,mpegvideo,m4v,h264,hevc,vc1,image2,apng,png_pipe,jpeg_pipe,webp_pipe,bmp_pipe,tiff_pipe,gif_pipe \
    --enable-decoder=gif,png,apng,mjpeg,bmp,webp,tiff,h264,hevc,vp8,vp9,libdav1d,mpeg1video,mpeg2video,mpeg4,msmpeg4v1,msmpeg4v2,msmpeg4v3,wmv1,wmv2,wmv3,vc1,flv,h263 \
    --enable-parser=h264,hevc,vp8,vp9,av1,mpeg4video,mpegvideo,mjpeg,png,gif,webp,bmp,vc1,h263 \
    --enable-bsf=vp9_superframe_split,av1_frame_merge,av1_metadata,h264_mp4toannexb,hevc_mp4toannexb,extract_extradata \
    --enable-filter=fps,crop,scale,pad,format,premultiply,premultiply_dynamic,unpremultiply,unpremultiply_dynamic,showinfo,null \
    --enable-encoder=png,rawvideo \
    --enable-muxer=rawvideo,image2pipe
make -j"$(nproc)"
make install
strip "$work/install/bin/"*.exe
{
    cat COPYING.LGPLv3
    printf '\n\n--- dav1d (AV1 decoder, linked statically) ---\n\n'
    cat /ucrt64/share/licenses/dav1d/COPYING
} > "$work/install/LICENSE.txt"
ls -l "$work/install/bin"
