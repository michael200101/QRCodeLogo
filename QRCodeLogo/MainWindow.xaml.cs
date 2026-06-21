using QRCoder;
using Svg;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelFormat = System.Windows.Media.PixelFormat;


namespace QRCodeLogo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public enum Types
        {
            Normal,
            Contact,
            Wifi
        }
        public enum Technologia
        {
            NONE,
            WEP,
            WPA
        }
        public bool contact {  get; set; }
        public bool wifi { get; set; }

        public Types Type { get; set; }
        public Technologia Technologi { get; set; }

        private readonly DispatcherTimer mPreviewTimer;
        private bool mReady;
        private bool mIsDark = true;

        // Output resolution in pixels per QR module, controlled by the quality slider.
        // Drives both the live preview and the saved PNG, so the preview is WYSIWYG.
        private int mQuality = 30;

        public MainWindow()
        {
            InitializeComponent();
            ProjectPath = AppContext.BaseDirectory;

            // Make sure the Logo and QR folders exist next to the executable,
            // wherever it is run from (important for the single-file build).
            Directory.CreateDirectory(LogoFolder);
            Directory.CreateDirectory(Path.Combine(ProjectPath, "QR"));

            LogoGallery.ItemsSource = Logos;
            LoadLogos();

            // Debounce live-preview rendering so typing stays responsive.
            mPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            mPreviewTimer.Tick += (s, e) => { mPreviewTimer.Stop(); RenderPreview(); };

            mReady = true;
            RenderPreview();
        }

        // ---- Live preview ----

        private void OnInputChanged(object sender, TextChangedEventArgs e) => SchedulePreview();

        private void SchedulePreview()
        {
            if (!mReady) return;
            mPreviewTimer.Stop();
            mPreviewTimer.Start();
        }

        private byte[]? mLastPng;

        private void RenderPreview()
        {
            try
            {
                using Bitmap? composite = BuildComposite(mQuality, warnIfLogoMissing: false);
                if (composite == null)
                {
                    mLastPng = null;
                    SaveInfoLabel.Text = "Kein Inhalt";
                    return;
                }

                Output.Source = BitmapToBitmapSource(composite);

                // Encode once so the preview shows the real file size and Save can reuse it.
                using (MemoryStream ms = new MemoryStream())
                {
                    composite.Save(ms, ImageFormat.Png);
                    mLastPng = ms.ToArray();
                }
                SaveInfoLabel.Text = $"{composite.Width} × {composite.Height} px  ·  {FormatBytes(mLastPng.Length)}";
            }
            catch
            {
                // Ignore transient errors from incomplete input; keep the last good preview.
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024.0):0.##} MB";
        }

        // ---- Theme ----

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
            => ApplyTheme(!mIsDark);

        private void ApplyTheme(bool dark)
        {
            mIsDark = dark;
            var newTheme = new ResourceDictionary
            {
                Source = new Uri($"Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative)
            };

            // Replace the active palette dictionary (the one defining WindowBg).
            var dicts = Application.Current.Resources.MergedDictionaries;
            for (int i = 0; i < dicts.Count; i++)
            {
                if (dicts[i].Contains("WindowBg"))
                {
                    dicts[i] = newTheme;
                    break;
                }
            }

            //ThemeToggleButton.Content = dark ? "☀  Hell" : "🌙  Dunkel";
        }

        public bool ContactToggle { get; set; }

        QRCodeGenerator mQrGenerator = new QRCodeGenerator();

        public string ProjectPath { get; set; }

        public string mName {  get; set; } = "";
        public string mSsid {  get; set; } = "";

        // Logo edge length as a percentage of the QR code width (set by the slider).
        private double LogoSizePercent = 20;

        public ObservableCollection<LogoItem> Logos { get; } = new ObservableCollection<LogoItem>();

        private LogoItem? mSelectedLogo;

        private static readonly string[] LogoExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg" };

        private string LogoFolder => Path.Combine(ProjectPath, "Logo");

        private void LoadLogos()
        {
            Logos.Clear();
            mSelectedLogo = null;

            if (!Directory.Exists(LogoFolder))
                return;

            var files = Directory.EnumerateFiles(LogoFolder)
                                  .Where(f => LogoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                  .OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
                Logos.Add(new LogoItem(file));

            // Pre-select logo.png if present, otherwise the first logo found.
            var initial = Logos.FirstOrDefault(l => string.Equals(l.Name, "logo.png", StringComparison.OrdinalIgnoreCase))
                          ?? Logos.FirstOrDefault();
            if (initial != null)
                SelectLogo(initial);
        }

        private void SelectLogo(LogoItem item)
        {
            foreach (var logo in Logos)
                logo.IsSelected = ReferenceEquals(logo, item);
            mSelectedLogo = item;
        }

        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            var item = (LogoItem)((Button)sender).DataContext;
            SelectLogo(item);
            LogoCheckBox.IsChecked = true; // picking a logo implies you want to use it
            SchedulePreview();
        }

        private void RefreshLogos_Click(object sender, RoutedEventArgs e)
        {
            LoadLogos();
            SchedulePreview();
        }

        // True only if a logo is wanted but none is usable; shows a message and returns true.
        private bool LogoRequiredButMissing()
        {
            if (mLogo && (mSelectedLogo == null || !File.Exists(mSelectedLogo.Path)))
            {
                MessageBox.Show(
                    "Bitte zuerst ein Logo aus der Galerie auswählen.",
                    "Kein Logo ausgewählt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }
            return false;
        }

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        // Builds the (extension-less) output file name from the current type and inputs.
        // Assumes BuildPayload has run so mName/mSsid are current.
        private string BuildFileName()
        {
            var date = DateTime.Now.ToString().Replace(" ", "_").Replace(".", "_").Replace(":", "_");
            string name = Type switch
            {
                Types.Contact => "Contact_" + mName + date,
                Types.Wifi => "Wifi_" + mSsid + date,
                _ => date,
            };
            return string.Join("_", name.Split(InvalidFileNameChars));
        }

        private static void NothingToSave() =>
            MessageBox.Show(
                "Es gibt noch keinen Inhalt zum Speichern.",
                "Nichts zu speichern",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

        // Save the current QR code as a raster PNG (uses the live, WYSIWYG preview pipeline).
        private void SavePng_Click(object sender, RoutedEventArgs e)
        {
            if (LogoRequiredButMissing()) return;

            using Bitmap? composite = BuildComposite(mQuality, warnIfLogoMissing: true);
            if (composite == null) { NothingToSave(); return; }

            byte[] byteArray;
            using (MemoryStream ms = new MemoryStream())
            {
                composite.Save(ms, ImageFormat.Png);
                byteArray = ms.ToArray();
            }

            string qrFolder = Path.Combine(ProjectPath, "QR");
            Directory.CreateDirectory(qrFolder);
            File.WriteAllBytes(Path.Combine(qrFolder, BuildFileName() + ".png"), byteArray);

            Output.Source = BitmapToBitmapSource(composite);
        }

        // Save the current QR code as a true vector SVG (infinitely scalable).
        private void SaveSvg_Click(object sender, RoutedEventArgs e)
        {
            if (LogoRequiredButMissing()) return;

            string svg = BuildSvg();
            if (string.IsNullOrEmpty(svg)) { NothingToSave(); return; }

            string qrFolder = Path.Combine(ProjectPath, "QR");
            Directory.CreateDirectory(qrFolder);
            File.WriteAllText(Path.Combine(qrFolder, BuildFileName() + ".svg"), svg);
        }

        // Produces a vector SVG of the QR code. The logo is injected manually (rather than via
        // QRCoder's square logo feature) so it keeps its aspect ratio with a tight backing.
        private string BuildSvg()
        {
            string payload = BuildPayload();
            if (string.IsNullOrWhiteSpace(payload))
                return "";

            QRCodeData qrCodeData = mQrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.H, true);
            var svgQr = new SvgQRCode(qrCodeData);

            // 1000-unit viewBox; the SVG scales to any size without quality loss.
            string lightColorHex = mTransparent ? "none" : "#ffffff";
            string svg = svgQr.GetGraphic(
                new System.Drawing.Size(1000, 1000),
                "#000000",
                lightColorHex,
                drawQuietZones: true,
                sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute,
                logo: null);

            if (mLogo && mSelectedLogo != null && File.Exists(mSelectedLogo.Path))
            {
                string injection = BuildSvgLogoMarkup(mSelectedLogo.Path);
                if (!string.IsNullOrEmpty(injection))
                {
                    if (injection.Contains("xlink:"))
                        svg = EnsureXlinkNamespace(svg);
                    int idx = svg.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                        svg = svg.Insert(idx, injection);
                }
            }

            return svg;
        }

        // Builds the SVG fragment for the logo: a tight white backing plus the logo itself,
        // fitted into the logo box while preserving its aspect ratio (coords in the 0..1000 viewBox).
        private string BuildSvgLogoMarkup(string path)
        {
            bool isSvg = path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

            double srcW, srcH;
            if (isSvg)
            {
                var dim = SvgDocument.Open(path).GetDimensions();
                srcW = dim.Width > 0 ? dim.Width : 100;
                srcH = dim.Height > 0 ? dim.Height : 100;
            }
            else
            {
                using var probe = new Bitmap(path);
                srcW = probe.Width;
                srcH = probe.Height;
            }

            double box = 1000.0 * (LogoSizePercent / 100.0);
            double ratio = Math.Min(box / srcW, box / srcH);
            double w = srcW * ratio, h = srcH * ratio;
            double x = (1000 - w) / 2, y = (1000 - h) / 2;
            double pad = box * 0.06;

            // White backing clears the modules behind the logo and keeps it scannable.
            string rect = FormattableString.Invariant(
                $"<rect x=\"{x - pad:0.##}\" y=\"{y - pad:0.##}\" width=\"{w + 2 * pad:0.##}\" height=\"{h + 2 * pad:0.##}\" fill=\"#ffffff\" />");

            string logoMarkup = isSvg
                ? BuildInlineSvgLogo(path, x, y, w, h, srcW, srcH)
                : BuildRasterImageLogo(path, x, y, w, h);

            return rect + logoMarkup;
        }

        // Inlines a vector logo as a nested <svg> so it renders everywhere and stays vector.
        private static string BuildInlineSvgLogo(string path, double x, double y, double w, double h, double srcW, double srcH)
        {
            string raw = File.ReadAllText(path);
            int open = raw.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            int openEnd = open >= 0 ? raw.IndexOf('>', open) : -1;
            int close = raw.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
            if (open < 0 || openEnd < 0 || close <= openEnd || raw[openEnd - 1] == '/')
                return ""; // malformed or self-closing root

            string openTag = raw.Substring(open, openEnd - open + 1);
            string inner = raw.Substring(openEnd + 1, close - (openEnd + 1));
            string viewBox = ExtractAttribute(openTag, "viewBox")
                             ?? FormattableString.Invariant($"0 0 {srcW} {srcH}");

            return FormattableString.Invariant(
                $"<svg x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{w:0.##}\" height=\"{h:0.##}\" viewBox=\"{viewBox}\" preserveAspectRatio=\"xMidYMid meet\">{inner}</svg>");
        }

        // Embeds a raster logo as a base64 PNG <image>; xlink:href + href for broad compatibility.
        private static string BuildRasterImageLogo(string path, double x, double y, double w, double h)
        {
            string base64;
            using (var bmp = new Bitmap(path))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                base64 = Convert.ToBase64String(ms.ToArray());
            }
            string uri = "data:image/png;base64," + base64;
            return FormattableString.Invariant(
                $"<image x=\"{x:0.##}\" y=\"{y:0.##}\" width=\"{w:0.##}\" height=\"{h:0.##}\" preserveAspectRatio=\"xMidYMid meet\" xlink:href=\"{uri}\" href=\"{uri}\" />");
        }

        private static string? ExtractAttribute(string tag, string attribute)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                tag, attribute + @"\s*=\s*""([^""]*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string EnsureXlinkNamespace(string svg)
        {
            if (svg.Contains("xmlns:xlink")) return svg;
            int si = svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            return si < 0 ? svg : svg.Insert(si + 4, " xmlns:xlink=\"http://www.w3.org/1999/xlink\"");
        }

        // Builds the payload string to encode from the current inputs and selected type.
        private string BuildPayload()
        {
            if (Type == Types.Contact)
            {
                string vorname = TextVorname.Text;
                string nachname = TextNachname.Text;
                mName = $"{vorname} {nachname}";
                string firma = TextFirma.Text;
                string titel = TextTitel.Text;
                string telefonMobil = TextTelefonnummer.Text;
                string telefonFestnetz = TextTelefonnummerFest.Text;
                string email = TextEMail.Text;
                string adresse = $";;{TextStraße.Text};{TextStadt.Text};;{TextPLZ.Text};{TextLand.Text}";

                return $"BEGIN:VCARD\r\n" +
                       $"VERSION:3.0\r\n" +
                       $"N:{nachname};{vorname};;;\r\n" +
                       $"FN:{mName}\r\n" +
                       $"ORG:{firma}\r\n" +
                       $"TITLE:{titel}\r\n" +
                       $"TEL;TYPE=CELL:{telefonMobil}\r\n" +
                       $"TEL;TYPE=HOME:{telefonFestnetz}\r\n" +
                       $"EMAIL:{email}\r\n" +
                       $"ADR;TYPE=WORK:{adresse}\r\n" +
                       $"END:VCARD";
            }
            if (Type == Types.Wifi)
            {
                mSsid = SSID.Text;
                string password = Passwort.Text;
                return $"WIFI:T:{Technologi};S:{mSsid};P:{password};";
            }
            return Input.Text;
        }

        // Renders the QR code (plus optional logo) into a Bitmap. Returns null when there is
        // nothing to encode, or when a logo is required but missing and the caller asked to bail.
        private Bitmap? BuildComposite(int pixelsPerModule, bool warnIfLogoMissing)
        {
            string payload = BuildPayload();
            if (string.IsNullOrWhiteSpace(payload))
                return null;

            // Resolve the logo source and its intrinsic aspect ratio. SVGs stay as a
            // document so we can rasterize them at the exact target size (always crisp).
            SvgDocument? svgDoc = null;
            Bitmap? rasterLogo = null;
            int srcW = 0, srcH = 0;
            bool haveLogo = false;
            if (mLogo)
            {
                string? logoPath = mSelectedLogo?.Path;
                if (string.IsNullOrEmpty(logoPath) || !File.Exists(logoPath))
                {
                    if (warnIfLogoMissing)
                        return null; // caller is responsible for telling the user
                    // preview: simply render without a logo
                }
                else if (logoPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    svgDoc = SvgDocument.Open(logoPath);
                    var dim = svgDoc.GetDimensions();
                    srcW = (int)Math.Ceiling(dim.Width);
                    srcH = (int)Math.Ceiling(dim.Height);
                    if (srcW <= 0 || srcH <= 0) { srcW = 100; srcH = 100; }
                    haveLogo = true;
                }
                else
                {
                    rasterLogo = new Bitmap(logoPath);
                    srcW = rasterLogo.Width;
                    srcH = rasterLogo.Height;
                    haveLogo = true;
                }
            }

            QRCodeData qrCodeData = mQrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.H, true);
            QRCode qrCode = new QRCode(qrCodeData);

            Bitmap qrBase = qrCode.GetGraphic(
                pixelsPerModule,
                System.Drawing.Color.Black,
                System.Drawing.Color.White,
                drawQuietZones: true);

            if (mTransparent)
                qrBase.MakeTransparent(System.Drawing.Color.White);

            // Square box reserved for the logo, centered on the QR code (slider drives the size).
            int logoSize = (int)(qrBase.Width * (LogoSizePercent / 100.0));
            int pos = (qrBase.Width - logoSize) / 2;
            int border = Math.Max(2, pixelsPerModule / 10);

            // Fit the logo inside the box while keeping its aspect ratio (no stretching).
            int drawX = pos, drawY = pos, drawW = logoSize, drawH = logoSize;
            if (haveLogo)
            {
                float ratio = Math.Min((float)logoSize / srcW, (float)logoSize / srcH);
                drawW = Math.Max(1, (int)Math.Round(srcW * ratio));
                drawH = Math.Max(1, (int)Math.Round(srcH * ratio));
                drawX = pos + (logoSize - drawW) / 2;
                drawY = pos + (logoSize - drawH) / 2;
            }

            // Rasterize SVG at the exact target size; raster logos are scaled at draw time.
            Bitmap? logo = svgDoc != null ? svgDoc.Draw(drawW, drawH) : rasterLogo;

            // Punch a transparent hole behind the logo (fast block copy, not per-pixel).
            if (mTransparent && haveLogo)
                using (Graphics gq = Graphics.FromImage(qrBase))
                {
                    gq.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    using var clearBrush = new SolidBrush(System.Drawing.Color.Transparent);
                    gq.FillRectangle(clearBrush, drawX - border, drawY - border, drawW + 2 * border, drawH + 2 * border);
                }

            Bitmap final = new Bitmap(qrBase.Width, qrBase.Height);
            using (Graphics g = Graphics.FromImage(final))
            {
                // Draw the QR 1:1 with no resampling so its modules stay crisp.
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(qrBase, new Rectangle(0, 0, qrBase.Width, qrBase.Height));

                if (logo != null)
                {
                    if (!mTransparent)
                        using (System.Drawing.Brush whiteBrush = new SolidBrush(System.Drawing.Color.White))
                        {
                            g.FillRectangle(whiteBrush, drawX - border, drawY - border, drawW + 2 * border, drawH + 2 * border);
                        }

                    // Highest-quality resampling for the logo; clamp edges to avoid a faint border.
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    var destRect = new Rectangle(drawX, drawY, drawW, drawH);
                    using (var attr = new ImageAttributes())
                    {
                        attr.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                        g.DrawImage(logo, destRect, 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, attr);
                    }
                }
            }

            qrBase.Dispose();
            logo?.Dispose();
            return final;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string folderPath = Path.Combine(ProjectPath, "Logo");
            Directory.CreateDirectory(folderPath);
            Process.Start("explorer.exe", folderPath);
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            string folderPath = Path.Combine(ProjectPath, "QR");
            Directory.CreateDirectory(folderPath);
            Process.Start("explorer.exe", folderPath);
        }

        private void Normal_Checked(object sender, RoutedEventArgs e)
        {
            // Fires during XAML parse (Normal IsChecked="True") before the panels exist;
            // the initial visibility is already set in XAML, so skip until loaded.
            if (!IsInitialized) return;

            var radio = (RadioButton)sender;

            if (radio.Content.ToString() == "Normal")
            {
                Type = Types.Normal;
                Input.Visibility = Visibility.Visible;
                Contact.Visibility = Visibility.Collapsed;
                Wifi.Visibility = Visibility.Collapsed;
                contact = false;
                wifi = false;
            }
            else if (radio.Content.ToString() == "Contact")
            {
                Type = Types.Contact;
                Input.Visibility = Visibility.Collapsed;
                Contact.Visibility = Visibility.Visible;
                Wifi.Visibility = Visibility.Collapsed;
                contact = true;
                wifi = false;
            }
            else if (radio.Content.ToString() == "Wi-Fi")
            {
                Type = Types.Wifi;
                Input.Visibility = Visibility.Collapsed;
                Contact.Visibility = Visibility.Collapsed;
                Wifi.Visibility = Visibility.Visible;
                contact = false;
                wifi = true;
            }
            SchedulePreview();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var radio = (RadioButton)sender;

            if (radio.Content.ToString() == "WPA/WPA2/WPA3")
                Technologi = Technologia.WPA;
            else if (radio.Content.ToString() == "WEP")
                Technologi = Technologia.WEP;
            else if (radio.Content.ToString() == "None")
                Technologi = Technologia.NONE;

            SchedulePreview();
        }


        public static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);

            try
            {
                PixelFormat format;

                switch (bitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                        format = PixelFormats.Bgra32;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        format = PixelFormats.Bgr24;
                        break;
                    default:
                        throw new NotSupportedException("Unsupported pixel format");
                }

                var bitmapSource = BitmapSource.Create(
                    bitmap.Width,
                    bitmap.Height,
                    96, // DPI X
                    96, // DPI Y
                    format,
                    null,
                    data.Scan0,
                    data.Stride * bitmap.Height,
                    data.Stride
                );

                bitmapSource.Freeze(); // Optional: makes it thread-safe
                return bitmapSource;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        bool mTransparent= false;

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            mTransparent = ((CheckBox)sender).IsChecked == true;
            SchedulePreview();
        }

        private void LogoSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LogoSizePercent = e.NewValue;
            // The label is declared after the slider in XAML, so it may not exist
            // yet during the initial value assignment in InitializeComponent.
            if (LogoSizeLabel != null)
                LogoSizeLabel.Text = $"{Math.Round(LogoSizePercent)} % der QR-Breite";
            SchedulePreview();
        }

        private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mQuality = (int)Math.Round(e.NewValue);
            if (QualityValueLabel != null)
                QualityValueLabel.Text = $"{mQuality} px/Modul";
            SchedulePreview();
        }
        bool mLogo = false;

        private void CheckBox_Checked_1(object sender, RoutedEventArgs e)
        {
            mLogo = ((CheckBox)sender).IsChecked == true;
            SchedulePreview();
        }
    }

    /// <summary>
    /// One selectable logo shown in the gallery. <see cref="IsSelected"/> drives the
    /// highlight in the UI; <see cref="Thumbnail"/> is decoded small and without locking
    /// the file so the full-size image can still be opened during generation.
    /// </summary>
    public class LogoItem : INotifyPropertyChanged
    {
        private bool mIsSelected;

        public LogoItem(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Thumbnail = LoadThumbnail(path);
        }

        public string Path { get; }
        public string Name { get; }
        public ImageSource? Thumbnail { get; }

        public bool IsSelected
        {
            get => mIsSelected;
            set
            {
                if (mIsSelected == value) return;
                mIsSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static ImageSource? LoadThumbnail(string path)
        {
            try
            {
                if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    return LoadSvgThumbnail(path);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // read fully, then release the file
                bitmap.DecodePixelWidth = 96;                  // small thumbnail keeps memory low
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null; // skip unreadable/corrupt images rather than crash the gallery
            }
        }

        private static ImageSource LoadSvgThumbnail(string path)
        {
            var doc = SvgDocument.Open(path);
            var dim = doc.GetDimensions();
            int w = 96;
            int h = dim.Width > 0 ? Math.Max(1, (int)Math.Round(96 * dim.Height / dim.Width)) : 96;

            using var bmp = doc.Draw(w, h);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
