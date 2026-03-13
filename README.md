# Vegas-yt-dlp
Simple script for importing youtube videos to Vegas
# Usage
Dl cs script file, place in Vegas 14 scriptmenu dir, Execute via scripts drop-down, the script should automatically import the url from your clipboard, or you can re-copy a new one before submitting it in the pop-up window, it will then execute yt-dlp and ffmpeg if installed and auto-import the footage and audio at the cursor.
# Requirements
Vegas 14 is the only software I've tested this on
Yt-dlp added to path via env var or sys32 location.
FFmpeg is a bonus but may not be needed depending on YTs available formats.
