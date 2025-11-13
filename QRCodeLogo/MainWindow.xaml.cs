using QRCoder;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        }
        public bool ContactToggle { get; set; }

        QRCodeGenerator mQrGenerator = new QRCodeGenerator();

        public string ProjectPath { get; set; }

        public string mName {  get; set; }
        public string mSsid {  get; set; }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Output.Source = null;
            string logoFilePath = $"{ProjectPath}Logo\\logo.png";
            string QrText = Input.Text;
            var size = 500;

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

            Bitmap logo = new Bitmap(logoFilePath);

            Bitmap qrCodeImage = qrCode.GetGraphic(
                100,                      // Pixel pro Modul
                System.Drawing.Color.Black, // Vordergrundfarbe
                System.Drawing.Color.White, // Hintergrundfarbe
                logo,                    // Dein Logo-Bitmap
                iconSizePercent: 20,     // Logo-Größe in % des QR-Codes
                iconBorderWidth: 1,      // Weißer Rand um das Logo
                drawQuietZones: true     // Ruhezone um den QR-Code
            );


            byte[] byteArray;
            using (MemoryStream ms = new MemoryStream())
            {
                qrCodeImage.Save(ms, ImageFormat.Bmp);
                byteArray = ms.ToArray();
            }


            var bitmap=BitmapToBitmapSource(qrCodeImage);


            var Date = DateTime.Now.ToString().Replace(" ", "_").Replace(".","_").Replace(":","_");
            string fileName="";

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
            var radio = sender as RadioButton;

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
            var radio = sender as RadioButton;

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
                int bytesPerPixel;

                switch (bitmap.PixelFormat)
                {
                    case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                        format = PixelFormats.Bgra32;
                        bytesPerPixel = 4;
                        break;
                    case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                        format = PixelFormats.Bgr24;
                        bytesPerPixel = 3;
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
    }
}