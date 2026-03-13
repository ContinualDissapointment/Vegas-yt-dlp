/**
 * YtDlpImport.cs — Vegas Pro 14 Script
 *
 * SETUP:
 *   1. Install yt-dlp: https://github.com/yt-dlp/yt-dlp/releases
 *      Place yt-dlp.exe on your PATH, or set YT_DLP_PATH below.
 *   2. (Optional) Install ffmpeg on PATH so yt-dlp can merge streams.
 *   3. In Vegas: Tools > Scripting > Run Script... > select this file.
 */

// Assembly references required by Vegas Pro 14
//css_reference ScriptPortal.Vegas.dll;
//css_reference System.Windows.Forms.dll;

using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint
{
    // ── CONFIG ────────────────────────────────────────────────────────────────
    // Full path to yt-dlp.exe — leave as "yt-dlp" if it's already on your PATH
    private const string YT_DLP_PATH = "yt-dlp";

    // Where downloaded files land. Defaults to your Videos folder.
    private static readonly string DEFAULT_OUTPUT_DIR =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VegasYtDlp");

    // yt-dlp format: best mp4 up to 1080p, fallback to best available
    private const string FORMAT = "bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/best[ext=mp4]/best";
    // ─────────────────────────────────────────────────────────────────────────

    public void FromVegas(Vegas vegas)
    {
        try
        {
            // 1. Show input dialog
            string url = PromptUrl();
            if (string.IsNullOrWhiteSpace(url)) return;

            // 2. Resolve / create output directory
            Directory.CreateDirectory(DEFAULT_OUTPUT_DIR);

            // 3. Output template — yt-dlp fills in title + ext
            string outputTemplate = Path.Combine(DEFAULT_OUTPUT_DIR, "%(title)s.%(ext)s");

            // 4. Run yt-dlp and capture the actual filename
            string downloadedFile = RunYtDlp(url, outputTemplate, DEFAULT_OUTPUT_DIR);
            if (string.IsNullOrEmpty(downloadedFile))
            {
                MessageBox.Show(
                    "yt-dlp finished but no output file was found.\n\n" +
                    "Make sure yt-dlp.exe is accessible and ffmpeg is installed.",
                    "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 5. Import file into Vegas at the current cursor position
            ImportFile(vegas, downloadedFile);

            MessageBox.Show(
                "Imported:\n" + Path.GetFileName(downloadedFile),
                "YtDlp Import — Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error: " + ex.Message, "YtDlp Import", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string PromptUrl()
    {
        Form form = new Form
        {
            Text            = "YtDlp Import — Paste YouTube URL",
            Width           = 500,
            Height          = 130,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterScreen,
            MaximizeBox     = false,
            MinimizeBox     = false
        };

        Label label  = new Label  { Left = 12, Top = 12, Width = 460, Text = "YouTube URL:" };
        TextBox box  = new TextBox { Left = 12, Top = 30, Width = 460 };

        // Auto-paste clipboard if it looks like a URL
        string clip = Clipboard.GetText();
        if (clip.StartsWith("http")) box.Text = clip;

        Button ok     = new Button { Text = "Download & Import", Left = 260, Width = 130, Top = 58, DialogResult = DialogResult.OK };
        Button cancel = new Button { Text = "Cancel",            Left = 396, Width =  76, Top = 58, DialogResult = DialogResult.Cancel };

        form.Controls.AddRange(new Control[] { label, box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() == DialogResult.OK ? box.Text.Trim() : null;
    }

    private static string RunYtDlp(string url, string outputTemplate, string outputDir)
    {
        string args = string.Format(
            "--no-playlist -f \"{0}\" --merge-output-format mp4 -o \"{1}\" --print after_move:filepath \"{2}\"",
            FORMAT, outputTemplate, url);

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName               = YT_DLP_PATH,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        string lastLine    = null;
        string errorOutput = null;

        using (Process proc = new Process { StartInfo = psi })
        {
            proc.Start();

            Form progress = BuildProgressForm(url);
            progress.Show();
            Application.DoEvents();

            string line;
            while ((line = proc.StandardOutput.ReadLine()) != null)
            {
                line = line.Trim();
                if (!string.IsNullOrEmpty(line)) lastLine = line;
                Application.DoEvents();
            }

            errorOutput = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            progress.Close();
            progress.Dispose();

            if (proc.ExitCode != 0)
                throw new Exception("yt-dlp exited with code " + proc.ExitCode + ".\n\n" + errorOutput);
        }

        // --print after_move:filepath should give us the exact path
        if (lastLine != null && File.Exists(lastLine)) return lastLine;

        // Fallback: newest file in output dir
        string[] files = Directory.GetFiles(outputDir, "*.mp4");
        if (files.Length == 0) files = Directory.GetFiles(outputDir, "*.*");
        if (files.Length == 0) return null;

        Array.Sort(files, (a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));
        return files[files.Length - 1];
    }

    private static Form BuildProgressForm(string url)
    {
        Form f = new Form
        {
            Text            = "Downloading...",
            Width           = 420,
            Height          = 90,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition   = FormStartPosition.CenterScreen,
            ControlBox      = false
        };
        Label lbl = new Label
        {
            Left  = 12,
            Top   = 16,
            Width = 390,
            Text  = "yt-dlp is downloading:\n" + (url.Length > 60 ? url.Substring(0, 60) + "..." : url)
        };
        f.Controls.Add(lbl);
        return f;
    }

    private static void ImportFile(Vegas vegas, string filePath)
    {
        VideoTrack vTrack = null;
        AudioTrack aTrack = null;

        foreach (Track t in vegas.Project.Tracks)
        {
            if (t is VideoTrack && vTrack == null) vTrack = (VideoTrack)t;
            if (t is AudioTrack && aTrack == null) aTrack = (AudioTrack)t;
        }

        if (vTrack == null) { vTrack = new VideoTrack(0, "Video"); vegas.Project.Tracks.Add(vTrack); }
        if (aTrack == null) { aTrack = new AudioTrack(1, "Audio"); vegas.Project.Tracks.Add(aTrack); }

        Timecode insertPos = vegas.Transport.CursorPosition;
        Media    media     = new Media(filePath);

        if (media.HasVideo())
        {
            VideoEvent ve = vTrack.AddVideoEvent(insertPos, media.Length);
            ve.AddTake(media.GetVideoStreamByIndex(0));
        }

        if (media.HasAudio())
        {
            AudioEvent ae = aTrack.AddAudioEvent(insertPos, media.Length);
            ae.AddTake(media.GetAudioStreamByIndex(0));
        }
    }
}
