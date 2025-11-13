using QRCoder;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;


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
            none,
            Wep,
            Wpa
        }
        public bool contact {  get; set; }
        public bool wifi { get; set; }

        public Types Type { get; set; }
        public Technologia Technologi { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            string patte = AppDomain.CurrentDomain.BaseDirectory;
            ProjectPath = AppContext.BaseDirectory;
        }
        public bool ContactToggle { get; set; }

        public string ProjectPath { get; set; }
        private void Button_Click(object sender, RoutedEventArgs e)
        {



            string vorname = TextVorname.Text;
            string nachname = TextNachname.Text;
            string name = $"{vorname} {nachname}";
            string firma = TextFirma.Text;
            string titel = TextTitel.Text;
            string telefonMobil = TextTelefonnummer.Text;
            string telefonFestnetz = TextTelefonnummerFest.Text;
            string email = TextEMail.Text;
            string adresse = $";;{TextStraße.Text};{TextStadt.Text};;{TextPLZ.Text};{TextLand.Text}";

            string vCard = $"BEGIN:VCARD\r\n" +
                           $"VERSION:3.0\r\n" +
                           $"N:{nachname};{vorname};;;\r\n" +
                           $"FN:{name}\r\n" +
                           $"ORG:{firma}\r\n" +
                           $"TITLE:{titel}\r\n" +
                           $"TEL;TYPE=CELL:{telefonMobil}\r\n" +
                           $"TEL;TYPE=HOME:{telefonFestnetz}\r\n" +
                           $"EMAIL:{email}\r\n" +
                           $"ADR;TYPE=WORK:{adresse}\r\n" +
                           $"END:VCARD";


            string ssid= SSID.Text;
            string password = Passwort.Text;

            string Wifi= $"WIFI:T: {Technologi.ToString()}; S: {ssid}; P: {password};" ;
            Output.Source = null;
            string logoFilePath = $"{ProjectPath}Logo\\logo.png";
            string QrText = Input.Text;
            var size = 500;

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(QrText, QRCodeGenerator.ECCLevel.H);

            if (Type == Types.Contact)
            {
                 qrCodeData = qrGenerator.CreateQrCode(vCard, QRCodeGenerator.ECCLevel.H, true);
            }
            else if (Type == Types.Normal)
            {
                 qrCodeData = qrGenerator.CreateQrCode(QrText, QRCodeGenerator.ECCLevel.H, true);
            }
            else if (Type == Types.Wifi)
            {
                 qrCodeData = qrGenerator.CreateQrCode(Wifi, QRCodeGenerator.ECCLevel.H, true);
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


            // Convert the QR code with the logo to a Base64 string
            byte[] byteArray;
            using (MemoryStream ms = new MemoryStream())
            {
                qrCodeImage.Save(ms, ImageFormat.Png);
                byteArray = ms.ToArray();
            }
            string base64String = Convert.ToBase64String(byteArray);



            BitmapImage bitmap = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(byteArray))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
            }

            // Image-Source setzen

            
            var Date = DateTime.Now.ToString().Replace(" ", "_").Replace(".","_").Replace(":","_");
            string fileName="";

            if (contact == true)
            {
                fileName = "Contact_" + name + Date.ToString();
            }
            else if (wifi == true)
            {
                fileName = "Wifi_" + ssid + Date.ToString();
            }
            else
                fileName = Date;

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

            if (radio.Content == "WPA/WPA2/WPA3")
                Technologi = Technologia.Wpa;
            else if (radio.Content == "WEP")
                Technologi = Technologia.Wep;
            else if (radio.Content == "None")
                Technologi = Technologia.none;

        }
    }
}