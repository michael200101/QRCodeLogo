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
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        public MainWindow()
        {
            InitializeComponent();
            ProjectPath = AppContext.BaseDirectory;
            LogoGallery.ItemsSource = Logos;
            LoadLogos();
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
        }

        private void RefreshLogos_Click(object sender, RoutedEventArgs e)
        {
            LoadLogos();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Output.Source = null;
            string QrText = Input.Text;

            QRCodeData qrCodeData = mQrGenerator.CreateQrCode(QrText, QRCodeGenerator.ECCLevel.H);

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

                string vCard = $"BEGIN:VCARD\r\n" +
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
                qrCodeData = mQrGenerator.CreateQrCode(vCard, QRCodeGenerator.ECCLevel.H, true);
            }
            else if (Type == Types.Normal)
            {
                qrCodeData = mQrGenerator.CreateQrCode(QrText, QRCodeGenerator.ECCLevel.H, true);
            }
            else if (Type == Types.Wifi)
            {
                mSsid = SSID.Text;
                string password = Passwort.Text;

                string Wifi = $"WIFI:T:{Technologi.ToString()};S:{mSsid};P:{password};";
                qrCodeData = mQrGenerator.CreateQrCode(Wifi, QRCodeGenerator.ECCLevel.H, true);
            }
            QRCode qrCode = new QRCode(qrCodeData);

            // Load the chosen logo only if the user actually wants one.
            Bitmap? logo = null;
            if (mLogo)
            {
                string? logoPath = mSelectedLogo?.Path;
                if (string.IsNullOrEmpty(logoPath) || !File.Exists(logoPath))
                {
                    MessageBox.Show(
                        "Bitte zuerst ein Logo aus der Galerie auswählen.",
                        "Kein Logo ausgewählt",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                logo = new Bitmap(logoPath);
            }

            Bitmap qrBase = qrCode.GetGraphic(
                100,
                System.Drawing.Color.Black,
                System.Drawing.Color.White,
                drawQuietZones: true
                );

            if(mTransparent)
                qrBase.MakeTransparent(System.Drawing.Color.White);

            // Square box reserved for the logo, centered on the QR code.
            // Driven by the slider: a percentage of the QR width.
            int logoSize = (int)(qrBase.Width * (LogoSizePercent / 100.0));
            int pos = (qrBase.Width - logoSize) / 2;
            int border = 10;

            // Fit the logo inside the square box while keeping its aspect ratio,
            // so non-square logos are no longer stretched or squished.
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
                        {
                            qrBase.SetPixel(x, y, System.Drawing.Color.Transparent);
                        }
                    }
                }
            Bitmap final = new Bitmap(qrBase.Width, qrBase.Height);
                using (Graphics g = Graphics.FromImage(final))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(qrBase, 0, 0);

                    if (logo != null)
                    {
                        if (!mTransparent)
                            using (System.Drawing.Brush WhiteBrush = new SolidBrush(System.Drawing.Color.White))
                            {
                                g.FillRectangle(WhiteBrush, drawX - border, drawY - border, drawW + 2 * border, drawH + 2 * border);
                            }

                        g.DrawImage(logo, drawX, drawY, drawW, drawH);
                    }
                }
            logo?.Dispose();



            byte[] byteArray;

            using (MemoryStream ms = new MemoryStream())
            {
                final.Save(ms, ImageFormat.Png);
                byteArray = ms.ToArray();
            }


            var bitmap = BitmapToBitmapSource(final);


            var Date = DateTime.Now.ToString().Replace(" ", "_").Replace(".", "_").Replace(":", "_");
            string fileName = "";

            if (contact == true)
            {
                fileName = "Contact_" + mName + Date.ToString();
            }
            else if (wifi == true)
            {
                fileName = "Wifi_" + mSsid + Date.ToString();
            }
            else
                fileName = Date;


            //SaveBitmapSourceAsPng(bitmap, $"{ProjectPath}QR\\{fileName}.png");
            File.WriteAllBytes($"{ProjectPath}QR\\{fileName}.png", byteArray);
            Output.Source = bitmap;





        }

        private void MyToggle_Checked(object sender, RoutedEventArgs e)
        {
            var Toggle = sender as ToggleButton;
            if (Toggle != null)
                if (Toggle.IsChecked == true)
                {
                    ContactToggle = true;
                    Input.Visibility = Visibility.Hidden;
                    Contact.Visibility = Visibility.Visible;
                }
                else
                {
                    ContactToggle = false;
                    Input.Visibility = Visibility.Visible;
                    Contact.Visibility = Visibility.Hidden;

                }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string folderPath = ProjectPath + "Logo";
            Process.Start("explorer.exe", folderPath);
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            string folderPath = ProjectPath + "QR";
            Process.Start("explorer.exe", folderPath);
        }

        private void Normal_Checked(object sender, RoutedEventArgs e)
        {
            var radio = (RadioButton)sender;

            if (radio.Content.ToString() == "Normal")
            {
                Type=Types.Normal;
                Input.Visibility= Visibility.Visible;
                Contact.Visibility= Visibility.Hidden;
                Wifi.Visibility= Visibility.Hidden;
                contact = false;
                wifi = false;
            }
            else if (radio.Content.ToString() == "Contact")
            {
                Type=Types.Contact;
                Input.Visibility = Visibility.Hidden;
                Contact.Visibility = Visibility.Visible;
                Wifi.Visibility = Visibility.Hidden;
                contact = true;
                wifi = false;
            }
            else if (radio.Content.ToString() == "Wi-Fi")
            {
                Type=Types.Wifi;
                Input.Visibility = Visibility.Hidden;
                Contact.Visibility = Visibility.Hidden;
                Wifi.Visibility = Visibility.Visible;
                contact = false;
                wifi = true;
            }
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {

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
        }

        private void LogoSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LogoSizePercent = e.NewValue;
            // The label is declared after the slider in XAML, so it may not exist
            // yet during the initial value assignment in InitializeComponent.
            if (LogoSizeLabel != null)
                LogoSizeLabel.Text = $"{Math.Round(LogoSizePercent)} % der QR-Breite";
        }
        bool mLogo = false;

        private void CheckBox_Checked_1(object sender, RoutedEventArgs e)
        {
            mLogo = ((CheckBox)sender).IsChecked == true;
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