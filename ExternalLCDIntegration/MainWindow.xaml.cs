﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Threading;
using System.Windows;
using ExternalLCDIntegration.Extensions;
using ExternalLCDIntegration.Models;
using ExternalLCDIntegration.Services;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace ExternalLCDIntegration
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isRunning = false;
        private int _horizontalLedCountTop = 5;
        private int _horizontalLedCountBottom = 5;
        private int _verticalLedCountLeft = 5;
        private int _verticalLedCountRight = 5;
        private SerialPort _port;
        private readonly BackgroundWorker _backgroundWorker;
        private readonly string _portMessage = "Please choose a comport before starting background job.";
        private readonly string _ledMessage = "The Led Count is invalid.";

        public MainWindow()
        {
            InitializeComponent();
            _backgroundWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _backgroundWorker.DoWork += BackgroundWorkerOnDoWork;

            Closing += OnWindowClosing;

            string[] portNames = SerialPort.GetPortNames();
            foreach (var port in portNames)
            {
                comList.Items.Add(port);
            }
            _horizontalLedCountTop = int.Parse(HorizontalLedCountTop.Text);

        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _port?.Close();
        }

        private void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_port == null)
            {
                MessageBox.Show(_portMessage);
                return;
            }
            if (!GetLedCount())
            {
                MessageBox.Show(_ledMessage);
                return;
            }
            if (_isRunning)
            {
                _backgroundWorker.CancelAsync();
            }
            else
            {
                _backgroundWorker.RunWorkerAsync();
            }

            _isRunning = !_isRunning;
        }

        private void WaitMilliseconds(int milliseconds)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < milliseconds)
            {
                Thread.Sleep(25);
            }
        }
        private void GetAverageColor()
        {
            ScreenService.GetScreenResolution(out var screenWidth, out var screenHeight);

            var size = new Size(screenWidth, screenHeight);
            var screenBitmap = ScreenService.CreateBitmap(screenWidth, screenHeight);
            do
            {
                var ledCount = 0;
                WaitMilliseconds(100);
                screenBitmap = ScreenService.CopyFromTheScreen(screenBitmap, size);
                var format = screenBitmap.PixelFormat;
                var bppModifier =
                    format == PixelFormat.Format24bppRgb
                        ? 3
                        : 4; // cutting corners, will fail on anything else but 32 and 24 bit images
                var sourceData = screenBitmap.LockBits(new Rectangle(0, 0, screenWidth, screenHeight),
                    ImageLockMode.ReadOnly,
                    format);
                var stride = sourceData.Stride;
                var scan = sourceData.Scan0;

                var requestModel = new SideLedReadingRequest
                {
                    Y = screenHeight,
                    X = screenWidth,
                    Depth = 5,
                    SideLedCount = _verticalLedCountRight,
                CurrentLedCount = ledCount,
                    ScreenPointer = scan,
                    StartFromZero = true,
                    BPPModifier = bppModifier,
                    Stride = stride,
                    ColourArray = ArrayService.CreateByteArray(_horizontalLedCountTop, _horizontalLedCountBottom, _verticalLedCountLeft, _verticalLedCountRight)
                };

                #region VerticalRight

                requestModel.IsIncremental = false;
                requestModel.StartFromZero = false;
                requestModel.ColourArray = ScreenService.GetSideLED(requestModel);
                ledCount += _verticalLedCountRight;

                #endregion

                #region HorizonalTop

                requestModel.CurrentLedCount = ledCount;
                requestModel.StartFromZero = true;
                requestModel.SideLedCount = _horizontalLedCountTop;
                requestModel.ColourArray = ScreenService.GetSideLED(requestModel);
                ledCount += _horizontalLedCountTop;

                #endregion

                #region VerticalLeft
                requestModel.SideLedCount = _verticalLedCountRight;
                requestModel.CurrentLedCount = ledCount;
                requestModel.IsIncremental = true;
                requestModel.ColourArray = ScreenService.GetSideLED(requestModel);
                ledCount += _verticalLedCountLeft;

                #endregion


                #region HorizonalBottom
                requestModel.StartFromZero = false;
                requestModel.CurrentLedCount = ledCount;
                requestModel.SideLedCount = _horizontalLedCountBottom;
                requestModel.ColourArray = ScreenService.GetSideLED(requestModel);

                #endregion

                screenBitmap.UnlockBits(sourceData);
                SendDataToSerialPort(requestModel.ColourArray, requestModel.ColourArray.Length);
                //Dispatcher.BeginInvoke(new Action(() => { PrintRGB(avgB, avgG, avgR); }));
            } while (_isRunning);
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            GetAverageColor();
        }

        private bool GetLedCount()
        {
            try
            {

                _horizontalLedCountTop = int.Parse(HorizontalLedCountTop.Text);
                _horizontalLedCountBottom = int.Parse(HorizontalLedCountBottom.Text);
                _verticalLedCountLeft = int.Parse(VerticalLedCountLeft.Text);
                _verticalLedCountRight = int.Parse(VerticalLedCountRight.Text);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        private void ConnectingBT_Click(object sender, RoutedEventArgs e)
        {
            if (_port == null)
            {
                if (comList.SelectedItem == null) MessageBox.Show(_portMessage);
                else
                {
                    var comPortName = comList.SelectedItem.ToString();
                    _port = new SerialPort(comPortName, 115200, System.IO.Ports.Parity.None, 8,
                        System.IO.Ports.StopBits.One) {ReadTimeout = 500, WriteTimeout = 500};
                    _port.Open();
                    ConnectingBT.Content = "Disconnect";
                }
            }
            else
            {
                _port.Close();
                _port = null;
                ConnectingBT.Content = "Connect";
            }
        }

        private void SendDataToSerialPort(byte[] data, int len)//array length should be 3 times bigger than LEDCount
        {
            char[] StopByte = {'c'};
            _port.Write(data, 0, len);
            _port.Write(StopByte,0,1);
        }

    }
}
