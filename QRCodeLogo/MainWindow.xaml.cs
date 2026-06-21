using QRCoder;
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
        private const int PreviewPixelsPerModule = 30;
        private const int SavePixelsPerModule = 100;

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

        private void RenderPreview()
        {
            try
            {
                using Bitmap? composite = BuildComposite(PreviewPixelsPerModule, warnIfLogoMissing: false);
                if (composite != null)
                    Output.Source = BitmapToBitmapSource(composite);
            }
            catch
            {
                // Ignore transient errors from incomplete input; keep the last good preview.
            }
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

        private static readonly string[] LogoExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

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

        // Save the current QR code as a PNG. The preview is always live; this only writes a file.
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (mLogo && (mSelectedLogo == null || !File.Exists(mSelectedLogo.Path)))
            {
                MessageBox.Show(
                    "Bitte zuerst ein Logo aus der Galerie auswählen.",
                    "Kein Logo ausgewählt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            using Bitmap? composite = BuildComposite(SavePixelsPerModule, warnIfLogoMissing: true);
            if (composite == null)
            {
                MessageBox.Show(
                    "Es gibt noch keinen Inhalt zum Speichern.",
                    "Nichts zu speichern",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            byte[] byteArray;
            using (MemoryStream ms = new MemoryStream())
            {
                composite.Save(ms, ImageFormat.Png);
                byteArray = ms.ToArray();
            }

            var date = DateTime.Now.ToString().Replace(" ", "_").Replace(".", "_").Replace(":", "_");
            string fileName = Type switch
            {
                Types.Contact => "Contact_" + mName + date,
                Types.Wifi => "Wifi_" + mSsid + date,
                _ => date,
            };
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

            string qrFolder = Path.Combine(ProjectPath, "QR");
            Directory.CreateDirectory(qrFolder);
            File.WriteAllBytes(Path.Combine(qrFolder, fileName + ".png"), byteArray);

            Output.Source = BitmapToBitmapSource(composite);
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

            Bitmap? logo = null;
            if (mLogo)
            {
                string? logoPath = mSelectedLogo?.Path;
                if (string.IsNullOrEmpty(logoPath) || !File.Exists(logoPath))
                {
                    if (warnIfLogoMissing)
                        return null; // caller is responsible for telling the user
                    // preview: simply render without a logo
                }
                else
                {
                    logo = new Bitmap(logoPath);
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
            if (logo != null)
            {
                float ratio = Math.Min((float)logoSize / logo.Width, (float)logoSize / logo.Height);
                drawW = Math.Max(1, (int)Math.Round(logo.Width * ratio));
                drawH = Math.Max(1, (int)Math.Round(logo.Height * ratio));
                drawX = pos + (logoSize - drawW) / 2;
                drawY = pos + (logoSize - drawH) / 2;
            }

            if (mTransparent && logo != null)
                for (int y = drawY - border; y < drawY + drawH + border; y++)
                {
                    for (int x = drawX - border; x < drawX + drawW + border; x++)
                    {
                        if (x >= 0 && x < qrBase.Width && y >= 0 && y < qrBase.Height)
                            qrBase.SetPixel(x, y, System.Drawing.Color.Transparent);
                    }
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
    }
}
