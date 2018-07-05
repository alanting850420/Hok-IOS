using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using System.Security;
using System.Security.Cryptography;
using System.IO;
using Utility.ModifyRegistry;
using Microsoft.Win32;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using CSharpAnalytics;
using System.Reflection;

namespace BeanfunLogin
{
    enum LoginMethod : int {
        Regular = 0,
        Keypasco = 1,
        PlaySafe = 2,
        QRCode = 3
    };

    public partial class main : Form
    {
        private AccountManager accountManager = null;

        public BeanfunClient bfClient;

        public BeanfunClient.GamaotpClass gamaotpClass;
        public BeanfunClient.QRCodeClass qrcodeClass;

        private string service_code = "610074" , service_region = "T9";

        public List<GameService> gameList = new List<GameService>();

        private CSharpAnalytics.Activities.AutoTimedEventActivity timedActivity = null;

        private string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public main()
        {
            currentVersion = currentVersion.Remove(currentVersion.Length - 2);

            if (Properties.Settings.Default.GAEnabled)
            {
                try
                {
                    AutoMeasurement.Instance = new WinFormAutoMeasurement();
                    AutoMeasurement.DebugWriter = d => Debug.WriteLine(d);
                    AutoMeasurement.Start(new MeasurementConfiguration("UA-75983216-4", Assembly.GetExecutingAssembly().GetName().Name, currentVersion));
                }
                catch
                {
                    this.timedActivity = null;
                    Properties.Settings.Default.GAEnabled = false;
                    Properties.Settings.Default.Save();
                }
            }

            timedActivity = new CSharpAnalytics.Activities.AutoTimedEventActivity("FormLoad", Properties.Settings.Default.loginMethod.ToString());
            InitializeComponent();
            init();
            CheckForUpdate();

            //string[] lines = System.IO.File.ReadAllLines(@"C:\POP\12.txt");
            //string[] lines = System.IO.File.ReadAllLines(@"\\vmware-host\Shared Folders\POP\2.txt");
            //string[] words = lines[0].Split((char)9);
            //this.accountInput.Text = words[0];
            //this.passwdInput.Text = words[1];
            this.accountInput.Text = "alanting0004";
            this.passwdInput.Text = "sf7778101";
            /*if (words[0] == "" || words[1] == "")
            {
                this.loginButton.Enabled = true;
            }
            else {*/
                this.loginButton.Enabled = false;
                this.loginWorker.RunWorkerAsync(0);
            //}
        }

        public void ShowToolTip(IWin32Window ui, string title, string des, int iniDelay = 2000, bool repeat = false)
        {
            if (Properties.Settings.Default.showTip || repeat)
            {
                ToolTip toolTip = new ToolTip();
                toolTip.ToolTipTitle = title;
                toolTip.UseFading = true;
                toolTip.UseAnimation = true;
                toolTip.IsBalloon = true;
                toolTip.InitialDelay = iniDelay;

                toolTip.Show(string.Empty, ui, 3000);
                toolTip.Show(des, ui);
            }
        }

        public bool errexit(string msg, int method, string title = null)
        {
            string originalMsg = msg;
            if (Properties.Settings.Default.GAEnabled) 
                AutoMeasurement.Client.TrackException(msg);

            switch (msg)
            {
                case "LoginNoResponse":
                    msg = "初始化失敗，請檢查網路連線。";
                    method = 0;
                    break;
                case "LoginNoSkey":
                    method = 0;
                    break;
                case "LoginNoAkey":
                    msg = "登入失敗，帳號或密碼錯誤。";
                    break;
                case "LoginNoAccountMatch":
                    msg = "登入失敗，無法取得帳號列表。";
                    break;
                case "LoginNoAccount":
                    msg = "登入失敗，找不到遊戲帳號。";
                    break;
                case "LoginNoResponseVakten":
                    msg = "登入失敗，與伺服器驗證失敗，請檢查是否安裝且已執行vakten程式。";
                    break;
                case "LoginUnknown":
                    msg = "登入失敗，請稍後再試";
                    method = 0;
                    break;
                case "OTPNoLongPollingKey":
                    if (Properties.Settings.Default.loginMethod == (int)LoginMethod.PlaySafe)
                        msg = "密碼獲取失敗，請檢查晶片卡是否插入讀卡機，且讀卡機運作正常。\n若仍出現此訊息，請嘗試重新登入。";
                    else
                    {
                        msg = "已從伺服器斷線，請重新登入。";
                        method = 1;
                    }
                    break;
                case "LoginNoReaderName":
                    msg = "登入失敗，找不到晶片卡或讀卡機，請檢查晶片卡是否插入讀卡機，且讀卡機運作正常。\n若還是發生此情形，請嘗試重新登入。";
                    break;
                case "LoginNoCardType":
                    msg = "登入失敗，晶片卡讀取失敗。";
                    break;
                case "LoginNoCardId":
                    msg = "登入失敗，找不到讀卡機。";
                    break;
                case "LoginNoOpInfo":
                    msg = "登入失敗，讀卡機讀取失敗。";
                    break;
                case "LoginNoEncryptedData":
                    msg = "登入失敗，晶片卡讀取失敗。";
                    break;
                case "OTPUnknown":
                    msg = "獲取密碼失敗，請嘗試重新登入。";
                    break;
                case "LoginNoPSDriver":
                    msg = "PlaySafe驅動初始化失敗，請檢查PlaySafe元件是否已正確安裝。";
                    break;
                default:
                    break;
            }
            MessageBox.Show("有錯誤，請重新開啟", "KartRiderLogin");
            Application.Exit();

            return false;
        }

        public void BackToLogin()
        {
            this.Size = new System.Drawing.Size(459, 290);
            panel1.SendToBack();
            panel2.BringToFront();
            Properties.Settings.Default.autoLogin = false;
            init();
            comboBox1_SelectedIndexChanged(null, null);
        }

        public bool init()
        {
            try
            {
                //this.Text = "BeanfunLogin - v" + currentVersion;
                this.AcceptButton = this.loginButton;
                this.bfClient = null;
                this.accountManager = new AccountManager();
                //string[] lines = System.IO.File.ReadAllLines(@"C:\POP\12.txt");
                string[] lines = System.IO.File.ReadAllLines(@"\\vmware-host\Shared Folders\ C#POP\2.txt");
                string[] words = lines[0].Split((char)9);
                this.accountInput.Text = words[0];
                this.passwdInput.Text = words[1];
                this.accountInput.Text = "alanting0004";
                this.passwdInput.Text = "sf7778101";

                bool res = accountManager.init();
                if (res == false)
                    errexit("帳號記錄初始化失敗，未知的錯誤。", 0);
                // Properties.Settings.Default.Reset(); //SetToDefault.                  

                this.loginMethodInput.SelectedIndex = 0;
                this.textBox3.Text = "";

                if (this.accountInput.Text == "")
                    this.ActiveControl = this.accountInput;
                else if (this.passwdInput.Text == "")
                    this.ActiveControl = this.passwdInput;

                // .NET textbox full mode bug.
                this.accountInput.ImeMode = ImeMode.OnHalf;
                this.passwdInput.ImeMode = ImeMode.OnHalf;
                return true;
            }
            catch (Exception e)
            { 
                return errexit("初始化失敗，未知的錯誤。" + e.Message, 0); 
            }
        }

        public class GameService
        {
            public string name { get; set; }
            public string service_code { get; set; }
            public string service_region { get; set; }

            public GameService(string name, string service_code, string service_region)
            {
                this.name = name;
                this.service_code = service_code;
                this.service_region = service_region;
            }
        }

        public void CheckForUpdate()
        {
            try
            {
                WebClient wc = new WebClient();
                string res = Encoding.UTF8.GetString(wc.DownloadData("http://tw.beanfun.com/game_zone/"));
                Regex reg = new Regex("Services.ServiceList = (.*);");
                if (reg.IsMatch(res))
                {
                    string json = reg.Match(res).Groups[1].Value;
                    JObject o = JObject.Parse(json);
                    foreach (var game in o["Rows"])
                    {
                        if ((string)game["ServiceFamilyName"] == "跑跑卡丁車")
                        {
                            Debug.Write(game["serviceCode"]);
                            this.comboBox2.Items.Add((string)game["ServiceFamilyName"]);
                            gameList.Add(new GameService((string)game["ServiceFamilyName"], (string)game["ServiceCode"], (string)game["ServiceRegion"]));
                        }
                    }
                }
                this.comboBox2.SelectedIndex = 0;
                this.comboBox2.Enabled = false;
            }
            catch { return; }
        }

        // The login botton.
        private void loginButton_Click(object sender, EventArgs e)
        {
            if (this.pingWorker.IsBusy)
            {
                this.pingWorker.CancelAsync();
            }


            this.UseWaitCursor = true;
            this.panel2.Enabled = false;

            this.loginButton.Text = "請稍後...";
            this.loginButton.Enabled = false;
            this.loginWorker.RunWorkerAsync(0);
        }    

        // The get OTP button.
        private void getOtpButton_Click(object sender, EventArgs e)
        {
            if (this.pingWorker.IsBusy)
            {
                this.pingWorker.CancelAsync();
            }
            if (listView1.SelectedItems.Count <= 0 || this.loginWorker.IsBusy) return;
           
            this.getOtpWorker.RunWorkerAsync(listView1.SelectedItems[0].Index);
        }

        // Building ciphertext by 3DES.
        private byte[] ciphertext(string plaintext, string key)
        {
            byte[] plainByte = Encoding.UTF8.GetBytes(plaintext);
            byte[] entropy = Encoding.UTF8.GetBytes(key);
            return ProtectedData.Protect(plainByte, entropy, DataProtectionScope.CurrentUser);
        }


        /* Handle other elements' statements. */
        private void BackToLogin_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BackToLogin();
        }

        private void SetGamePath_ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "新楓之谷主程式 (MapleStory.exe)|MapleStory.exe|All files (*.*)|*.*";
            openFileDialog.Title = "Set MapleStory.exe Path.";
            openFileDialog.InitialDirectory = Properties.Settings.Default.gamePath;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string file = openFileDialog.FileName;
                Properties.Settings.Default.gamePath = file;
                Properties.Settings.Default.Save();
            }

            if (Properties.Settings.Default.GAEnabled)
            {
                AutoMeasurement.Client.TrackEvent("set game path", "set game path");
            }
        }


        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                if (this.checkBox4.Checked == true && this.listView1.SelectedItems[0].Index != -1 && this.listView1.SelectedItems[0].Index <= this.bfClient.accountList.Count())
                {
                    Properties.Settings.Default.autoSelectIndex = this.listView1.SelectedItems[0].Index;
                    Properties.Settings.Default.autoSelect = true;
                }
                else
                    Properties.Settings.Default.autoSelect = false;
            }
            Properties.Settings.Default.Save();

            if (Properties.Settings.Default.GAEnabled)
            {
                AutoMeasurement.Client.TrackEvent(this.checkBox4.Checked ? "autoSelectOn" : "autoSelectOff", "autoSelectCheckbox");
            }
        }

        private void textBox3_OnClick(object sender, EventArgs e)
        {
            if (textBox3.Text == "" || textBox3.Text == "獲取失敗") return;
            Clipboard.SetText(textBox3.Text);
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                Clipboard.SetText(this.bfClient.accountList[this.listView1.SelectedItems[0].Index].sacc);
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
                this.getOtpButton.Text = "獲取密碼";
        }

        // login method changed event
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            qrCheckLogin.Enabled = false;

            accountInput.Visible = true;
            accountLabel.Visible = true;

            passLabel.Visible = true;
            passwdInput.Visible = true;
            loginButton.Visible = true;

            Properties.Settings.Default.loginMethod = this.loginMethodInput.SelectedIndex;

            if (Properties.Settings.Default.loginMethod == (int)LoginMethod.PlaySafe)
            {
                this.passLabel.Text = "PIN碼";
            }
            else if (Properties.Settings.Default.loginMethod == (int)LoginMethod.QRCode)
            {
                accountInput.Visible = false;
                accountLabel.Visible = false;

                passLabel.Visible = false;
                passwdInput.Visible = false;

                loginButton.Visible = false;

                this.qrWorker.RunWorkerAsync(null);
                this.loginMethodInput.Enabled = false;
            }
            else
            {
                this.passLabel.Text = "密碼";
            }
        }

        private void keepLogged_CheckedChanged(object sender, EventArgs e)
        {
            if (keepLogged.Checked)
                if (!this.pingWorker.IsBusy)
                    this.pingWorker.RunWorkerAsync();
            else
                    if (this.pingWorker.IsBusy)
                    {
                        this.pingWorker.CancelAsync();
                    }
            Properties.Settings.Default.Save();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.loginGame = this.comboBox2.SelectedIndex;
            try
            {
                service_code = gameList[this.comboBox2.SelectedIndex].service_code;
                service_region = gameList[this.comboBox2.SelectedIndex].service_region;
            }
            catch
            {

            }
            
            //Properties.Settings.Default.Save();
       }

        private void autoPaste_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();

            if (Properties.Settings.Default.GAEnabled)
            {
                AutoMeasurement.Client.TrackEvent(this.autoPaste.Checked ? "autoPasteOn" : "autoPasteOff", "autoPasteCheckbox");
            }
        }

        private void main_Load(object sender, EventArgs e)
        {

        }
    }
}
