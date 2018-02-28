using System;

namespace TapTrack.Demo
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Imaging;
    using System.Collections.ObjectModel;
    using System.Threading;
    using WpfAnimatedGif;
    using System.Diagnostics;
    using Tcmp.Communication;
    using Tcmp.CommandFamilies;
    using Tcmp.CommandFamilies.BasicNfc;
    using Tcmp.CommandFamilies.Type4;
    using Ndef;
    using Tcmp.Communication.Exceptions;
    using Tcmp;
    using NdefLibrary.Ndef;
    using System.Text;
    using Tcmp.CommandFamilies.System;	
    using System.Management;
    using System.Text.RegularExpressions;

	using Tcmp.CommandFamilies.StandaloneCheckin;
	using TapTrack.Demo;
	using Microsoft.Win32;
	using FileHelpers;
	using System.Data;


	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
    {
        TappyReader tappy;
        private ObservableCollection<Row> table;
        GridLength zeroHeight = new GridLength(0);
		private int currentStationCode;
		private string currentStationName; 
        ushort numCheckinsStoredInReader = 0;
        ushort numCheckinsRemainingToDownload = 0;
        ushort nextCheckinToDownload = 0;
        ushort numCheckinsToDownloadAtOnce = 100;
     


        public MainWindow()
        {
            InitializeComponent();
            tappy = new TappyReader(CommunicationProtocol.Usb);
            table = new ObservableCollection<Row>();
            records.ItemsSource = table;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            tappy.Disconnect();
        }

		/// <summary>
		/// Helper function to convert a hex string into a UTF string
		/// </summary>
		public static string HexarrayToString(string hexStr)
		{
			hexStr = hexStr.Replace("-", "");
			byte[] utf = HexStringToByteArray(hexStr);
			string result = new string(' ', utf.Length);
			int charNum = 0;
			foreach (byte utf8Char in utf)
			{
				result = result.Insert(charNum++, Convert.ToChar(utf8Char).ToString());
			}
			return result;

		}

		/// <summary>
		/// Helper function to convert a hex string into a byte array
		/// </summary>
		public static byte[] HexStringToByteArray(String hex)
		{
			int NumberChars = hex.Length;
			byte[] bytes = new byte[NumberChars / 2];
			for (int i = 0; i < NumberChars; i += 2)
				bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
			return bytes;
		}

        //
		// Reset checkins
		//

		private void ResetCheckins_Click(object sender, RoutedEventArgs e)
        {

			Callback confirmResetCheckins = (ResponseFrame frame, Exception exc) =>
			{
				if (CheckForErrorsOrTimeout(frame, exc))
					return;	
				ShowSuccessStatus("Checkins Reset");

			};
			Command cmd = new resetCheckins();
			tappy.SendCommand(cmd, confirmResetCheckins);

        }

        private void GetNumCheckins_Click(object sender, RoutedEventArgs e)
        {
			Callback showNumCheckins = (ResponseFrame frame, Exception exc) =>
			{
				if (CheckForErrorsOrTimeout(frame, exc))
					return;
				UInt32 numCheckins = BitConverter.ToUInt32(frame.Data, 0);
				ShowSuccessStatus($"Number of Checkins is {numCheckins} ");

			};
			Command cmd = new getNumCheckins();
			tappy.SendCommand(cmd, showNumCheckins);			

		}

        //
        // Export checkins to CSV
        //

        private void ExportCSV_Click(object sender, RoutedEventArgs e)
        {
			SaveFileDialog dialog = new SaveFileDialog();
			dialog.Filter = "CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt";
			List<CheckinRecord> export = DatabaseUtility.SelectDownloadedCheckins();


			if (dialog.ShowDialog() == true)
			{
				FileHelperEngine<CheckinRecord> engine = new FileHelperEngine<CheckinRecord>();
				engine.HeaderText = engine.GetFileHeader();
				engine.WriteFile(dialog.FileName, export);
			}
		}
     

        private void CountRowsByTagCodeButton_Click(object sender, RoutedEventArgs e)
        {
			DataTable dt = DatabaseUtility.TagCodeCountWithCheckinTags();
			dgTagCodeCount.DataContext = dt;
		}

        private void storeDownloadedCheckin(ResponseFrame downloadedCheckinsFrame, Exception downloadCheckinsExc)
        {
            if (CheckForErrorsOrTimeout(downloadedCheckinsFrame, downloadCheckinsExc))
                return;

            if (downloadedCheckinsFrame.Data == null)
            {
                ShowFailStatus("There are no checkins stored in the Tappy to download");
                return;

            }


            if (downloadedCheckinsFrame.Data != null && (downloadedCheckinsFrame.Data.Length % 12) != 0)
            {
                ShowFailStatus("Checkin Records are not a multiple of 12 bytes, perhaps a checkin record was fragmented when downloaded from the Tappy");
                return;
            }

            List<CheckinRecord> downloadedCheckins = new List<CheckinRecord>();
            UInt32 numCheckinRecordsDownloaded = (UInt32)downloadedCheckinsFrame.Data.Length / 12;
            CheckinRecord currentRecord;
            byte[] tagCode = new byte[7];
            int year;
            byte month, monthDay, hour, min;
            UInt32 i;
            bool failedDateValidation = false;

            for (i = 0; i + 12 <= downloadedCheckinsFrame.Data.Length; i += 12)
            {
               currentRecord = new CheckinRecord();
               currentRecord.id = Guid.NewGuid();

                try
                {

                    tagCode[0] = downloadedCheckinsFrame.Data[i];
                    tagCode[1] = downloadedCheckinsFrame.Data[i + 1];
                    tagCode[2] = downloadedCheckinsFrame.Data[i + 2];
                    tagCode[3] = downloadedCheckinsFrame.Data[i + 3];
                    tagCode[4] = downloadedCheckinsFrame.Data[i + 4];
                    tagCode[5] = downloadedCheckinsFrame.Data[i + 5];
                    tagCode[6] = downloadedCheckinsFrame.Data[i + 6];
                    year = 2000 + downloadedCheckinsFrame.Data[i + 7];
                    month = downloadedCheckinsFrame.Data[i + 8];
                    monthDay = downloadedCheckinsFrame.Data[i + 9];
                    hour = downloadedCheckinsFrame.Data[i + 10];
                    min = downloadedCheckinsFrame.Data[i + 11];
                    currentRecord.tagCode = BitConverter.ToString(tagCode);
                    currentRecord.timestamp = new DateTime(year, (int)month, (int)monthDay, (int)hour, (int)min, 0);
                    currentRecord.stationCode = currentStationCode;
                    currentRecord.stationName = currentStationName;

                    downloadedCheckins.Add(currentRecord);                    
                }
                catch (ArgumentOutOfRangeException timeExc)
                {
                    currentRecord.tagCode = BitConverter.ToString(tagCode);
                    currentRecord.timestamp = new DateTime(1970, 1, 1);
                    currentRecord.stationCode = currentStationCode;
                    currentRecord.stationName = currentStationName;
                    downloadedCheckins.Add(currentRecord);
                    failedDateValidation = true;

                }
                catch (Exception ge)
                {
                    ShowFailStatus("The downloaded checkins could not be stored");
                    return;
                }
            }
            if(failedDateValidation == true)
            {
                Debug.WriteLine($"{downloadedCheckins.Count} have been downloaded from the Tappy, but one or more failed the date validation");
            }
            else
            {
                Debug.WriteLine($"{downloadedCheckins.Count} have been downloaded from the Tappy");
            }            
            writeCheckinsToDb(downloadedCheckins, failedDateValidation);

        }

        void writeCheckinsToDb(List<CheckinRecord> downloadedCheckins, bool failedDateValidation)
        {
            try
            {
                DatabaseUtility.Connect();
                if (DatabaseUtility.InsertCheckinRecord(downloadedCheckins))
                {
                    Debug.WriteLine($"{downloadedCheckins.Count} have been inserted into the DB");

                    numCheckinsRemainingToDownload -= (ushort)downloadedCheckins.Count;
                                
                    if (numCheckinsRemainingToDownload != 0)
                    {
                        if(numCheckinsToDownloadAtOnce <= numCheckinsRemainingToDownload)
                        {
                            nextCheckinToDownload = (ushort)(numCheckinsStoredInReader - numCheckinsRemainingToDownload);
                            Command cmdDownloadCheckins = new downloadCheckins(nextCheckinToDownload, (ushort)(nextCheckinToDownload + numCheckinsToDownloadAtOnce - 1));
                            Debug.WriteLine($" Checkin numbers {nextCheckinToDownload + 1}  through {nextCheckinToDownload + 1 + numCheckinsToDownloadAtOnce} are being downloaded from the Tappy");
                            tappy.SendCommand(cmdDownloadCheckins, storeDownloadedCheckin);
                        }
                        else
                        {
                            nextCheckinToDownload = (ushort)(numCheckinsStoredInReader - numCheckinsRemainingToDownload);
                            Command cmdDownloadCheckins = new downloadCheckins(nextCheckinToDownload, (ushort)(numCheckinsStoredInReader - 1));
                            Debug.WriteLine($" Checkin numbers {nextCheckinToDownload + 1}  through {numCheckinsStoredInReader} are being downloaded from the Tappy");
                            tappy.SendCommand(cmdDownloadCheckins, storeDownloadedCheckin);
                        }                           

                    }
                    else
                    {
                        if (failedDateValidation == true)
                        {
                            ShowSuccessStatus($"{downloadedCheckins.Count} have been downloaded from the Tappy, but one or more failed the date validation and have been inserted as 1-Jan-1970",3000);
                        }
                        else
                        {
                            ShowSuccessStatus($"{numCheckinsStoredInReader} have been downloaded from the Tappy and stored in the database");
                        }
                       
                            return;
                    }
                                                               
                }
                else
                {
                    ShowFailStatus("The downloaded checkins could not be stored");
                    return;
                }
            }
            catch (Exception dbExc)
            {
                ShowFailStatus("An error occurred when storing downloaded checkins");
                return;
            }

        }

        //
        // Download checkins
        //


        private void DownloadCheckinsClick(object sender, RoutedEventArgs e)
        {

            Callback storeNumCheckins = (ResponseFrame frame, Exception exc) =>
			{
				if (CheckForErrorsOrTimeout(frame, exc))
					return;

                numCheckinsStoredInReader = BitConverter.ToUInt16(frame.Data, 0);
                if (numCheckinsStoredInReader == 0)
				{
					ShowSuccessStatus("There are no checkins to download from the Tappy");
					return;
				}

                /* Step 3 - download the checkins currently stored in the Tappy*/          
                numCheckinsRemainingToDownload = numCheckinsStoredInReader;
                    Command cmdDownloadCheckins = new downloadCheckins(0, (ushort)(numCheckinsToDownloadAtOnce-1));
                Debug.WriteLine($" Checkin numbers 1 through {numCheckinsToDownloadAtOnce} are being downloaded from the Tappy");
                tappy.SendCommand(cmdDownloadCheckins, storeDownloadedCheckin);

            };

			Callback storeCurrentStationInfo = (stationNumFrame, stationNumExc) =>
			{
				if (CheckForErrorsOrTimeout(stationNumFrame, stationNumExc))
					return;

                byte[] stationCodeLittleEndian = new byte[2];
                Array.Copy(stationNumFrame.Data, stationCodeLittleEndian, 2);
                Array.Reverse(stationCodeLittleEndian);
				currentStationCode = BitConverter.ToUInt16(stationCodeLittleEndian, 0);				
                
				currentStationName = HexarrayToString(BitConverter.ToString(stationNumFrame.Data, 2));
				currentStationName.TrimEnd();

				/*Step 2: get the number of checkins currently stored in the Tappy*/
				Command cmdGetNumCheckins = new getNumCheckins();          
				tappy.SendCommand(cmdGetNumCheckins, storeNumCheckins);


			};

			ShowPendingStatus("Downloading Checkins...");
			/*Step 1 get the station name and code from the Tappy*/
			Command cmdGetStationInfo = new GetStationInfo();
			tappy.SendCommand(cmdGetStationInfo, storeCurrentStationInfo);
		}

        //
        // Delete stored checkins
        //

        private void DeleteStoredCheckins_Click(object sender, RoutedEventArgs e)
        {
            if(DatabaseUtility.ClearCheckinTable() == true)
            {
                dgCheckins.DataContext = DatabaseUtility.SelectDownloadedCheckins();
                ShowSuccessStatus($"Dowloaded Checkins have been cleared from the local database");
            }
            else
            {
                ShowFailStatus($"There was a problem when attempting to clear the downloaded checkins from the local datbase");
            }
        }

        //
        // Stop
        //

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            HideStatus();
            tappy.SendCommand<Stop>();
        }

        private void AutoDetectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectWindow window = new ConnectWindow();

            if (window.ShowDialog() == true)
            {
                if (window.Protocol == CommunicationProtocol.Usb)
                    batteryTab.Visibility = Visibility.Hidden;
                else if (window.Protocol == CommunicationProtocol.Bluetooth)
                {
                    batteryTab.Visibility = Visibility.Visible;
                    if (GetBluegigaDevice() == null)
                    {
                        ShowFailStatus("Please insert BLED112 dongle");
                        return;
                    }
                }

                tappy.SwitchProtocol(window.Protocol);

                ShowPendingStatus("Searching for a Tappy");

                Task.Run(() =>
                {
                    if (tappy.AutoDetect())
                    {
                        ShowSuccessStatus($"Connected to {tappy.DeviceName}");
                        if (window.Protocol == CommunicationProtocol.Bluetooth)
                        {
                            try
                            {
                                Command cmd = new EnableDataThrottling(10, 5);
                                tappy.SendCommand(cmd);
                            }
                            catch
                            {

                            }
                        }
                    }
                    else
                        ShowFailStatus("No Tappy found");
                });
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (settingsContainer.Height.IsAuto)
                settingsContainer.Height = zeroHeight;
            else
                settingsContainer.Height = GridLength.Auto;
        }

        private void ShowPendingStatus(string message)
        {
            Action show = () =>
            {
                statusPopup.IsOpen = true;
                statusText.Content = "Pending";
                statusMessage.Content = message;
                ImageBehavior.SetAnimatedSource(statusImage, (BitmapImage)FindResource("Pending"));
            };

            Dispatcher.BeginInvoke(show);
        }

        private void ShowSuccessStatus(string message = "", int delay=1500)
        {
            Action show = () =>
            {
                statusPopup.IsOpen = true;
                statusText.Content = "Success";
                statusMessage.Content = message;
                ImageBehavior.SetAnimatedSource(statusImage, (BitmapImage)FindResource("Success"));

                Task.Run(() =>
                {
                    Thread.Sleep(delay);
                    HideStatus();
                });
            };

            Dispatcher.BeginInvoke(show);
        }

        private void ShowFailStatus(string message)
        {
            Action show = () =>
            {
                dismissButtonContainer.Height = new GridLength(50);
                dismissButton.Visibility = Visibility.Visible;
                statusPopup.IsOpen = true;
                statusText.Content = "Fail";
                statusMessage.Content = message;
                ImageBehavior.SetAnimatedSource(statusImage, (BitmapImage)FindResource("Error"));
            };

            Dispatcher.BeginInvoke(show);
        }

        private void HideStatus()
        {
            Action hide = () =>
            {
                statusPopup.IsOpen = false;
            };

            Dispatcher.Invoke(hide);
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            HideStatus();
            dismissButton.Visibility = Visibility.Hidden;
            dismissButtonContainer.Height = zeroHeight;
        }

        private void ResponseCallback(ResponseFrame frame, Exception e)
        {
            if (CheckForErrorsOrTimeout(frame, e))
                return;
            ShowSuccessStatus();
        }

        private bool CheckForErrorsOrTimeout(ResponseFrame frame, Exception e)
        {
            if (e != null)
            {
                if (e.GetType() == typeof(HardwareException))
                    ShowFailStatus("Tappy is not connected");
                else
                    ShowFailStatus("An error occured");

                return true;
            }
            else if (!TcmpFrame.IsValidFrame(frame))
            {
                ShowFailStatus("An error occured");

                return true;
            }
            else if (frame.IsApplicationErrorFrame())
            {
                ApplicationErrorFrame errorFrame = (ApplicationErrorFrame)frame;
                ShowFailStatus(errorFrame.ErrorString);
                return true;
            }
            else if (frame.CommandFamily0 == 0 && frame.CommandFamily1 == 0 && frame.ResponseCode < 0x05)
            {
                ShowFailStatus(TappyError.LookUp(frame.CommandFamily, frame.ResponseCode));
                return true;
            }
            else if (frame.ResponseCode == 0x03)
            {
                ShowFailStatus("No tag detected");
                return true;
            }
            else
            {
                return false;
            }
        }

        private void importStringMappingButton_Click(object sender, RoutedEventArgs e)
        {
			OpenFileDialog dialog = new OpenFileDialog();
			dialog.Filter = "CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt";
			List<CheckinTag> import = new List<CheckinTag>();

			if (dialog.ShowDialog() == true)
			{
				int count = 0;
				try
				{
					FileHelperEngine<CheckinTag> engine = new FileHelperEngine<CheckinTag>();
					import.AddRange(engine.ReadFile(dialog.FileName));
				}
				catch (Exception exc)
				{
					ShowFailStatus($"There is a problem with the csv. Import has been aborted.");
					return;
				}

				for (int i = 0; i < import.Count; i++)
				{
					try
					{
						DatabaseUtility.InsertCheckinTag(import[i]);
						count++;
					}
					catch (Exception exc)
					{
						ShowFailStatus($"Trouble importing row {i + 2} from {dialog.FileName}");
					}
				}				
				ShowSuccessStatus($"Imported {count}/{import.Count} rows");
				UpdateCheckinTagDg();
			}
		}

        private void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                tappy.Disconnect();
                ShowSuccessStatus("Disconnect was successful");
            }
            catch
            {
                ShowFailStatus("Disconnect was unsuccessful");
            }
        }

        private void firmwareVersionButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPendingStatus("");
            Command cmd = new GetFirmwareVersion();

            Action<string> update = (string text) =>
            {
                firmwareTextBox.Text = text;
            };

            Callback callback = (ResponseFrame frame, Exception exc) =>
            {
                if (CheckForErrorsOrTimeout(frame, exc))
                    return;

                if (frame.ResponseCode == 0x06)
                {
                    byte[] data = frame.Data;

                    Dispatcher.BeginInvoke(update, $"{data[0]}.{data[1]}");
                }
                ShowSuccessStatus();
            };

            tappy.SendCommand(cmd, callback);
        }

        private void hardwareVersionButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPendingStatus("");
            Command cmd = new GetHardwareVersion();

            Action<string> update = (string text) =>
            {
                hardwareTextBox.Text = text;
            };

            Callback callback = (ResponseFrame frame, Exception exc) =>
            {
                if (CheckForErrorsOrTimeout(frame, exc))
                    return;

                if (frame.ResponseCode == 0x05)
                {
                    byte[] data = frame.Data;

                    Dispatcher.BeginInvoke(update, $"{data[0]}.{data[1]}");
                }
                ShowSuccessStatus();
            };

            tappy.SendCommand(cmd, callback);
        }

        private void batteryButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPendingStatus("");
            Command cmd = new GetBatteryLevel();

            Action<string> update = (string text) =>
            {
                batteryTextBox.Text = text;
            };

            Callback callback = (ResponseFrame frame, Exception exc) =>
            {
                if (CheckForErrorsOrTimeout(frame, exc))
                    return;


                if (frame.ResponseCode == 0x08)
                {
                    byte[] data = frame.Data;

                    Dispatcher.BeginInvoke(update, $"{data[0]}%");
                }
                ShowSuccessStatus();
            };

            tappy.SendCommand(cmd, callback);
        }   

        private string Search(string searchLocation)
        {
            ManagementObjectCollection comPortDevices;
            ManagementObjectSearcher searcher = new ManagementObjectSearcher($"Select * From {searchLocation}");
            comPortDevices = searcher.Get();

            foreach (ManagementObject device in comPortDevices)
            {
                string name = device["Name"] as string;

                if (name?.Contains("Bluegiga Bluetooth Low Energy") ?? false)
                {
                    Debug.WriteLine($"Found {device["Name"]}");
                    Match match = Regex.Match(name, @"\(([^)]*)\)");
                    if (match.Groups.Count > 1)
                        return match.Groups[1].Value;
                }
            }

            return null;
        }

        private string GetBluegigaDevice()
        {
            return Search("Win32_SerialPort") ?? Search("Win32_pnpEntity");
        }

		private void TabItem_Loaded(object sender, RoutedEventArgs e)
		{

		}

		private void tiCheckinView_Loaded(object sender, RoutedEventArgs e)
		{
			dgCheckins.DataContext = DatabaseUtility.SelectDownloadedCheckins();
		}

		private void UpdateCheckinTagDg()
		{
			dgCheckinTags.DataContext = DatabaseUtility.SelectCheckinTags();
		}

		private void tiViewCheckinTags_Loaded(object sender, RoutedEventArgs e)
		{
			UpdateCheckinTagDg();
		}

   
    }
}
