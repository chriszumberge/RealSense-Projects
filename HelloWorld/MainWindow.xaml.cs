//--------------------------------------------------------------------------------------
// Copyright 2014-2015 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.
//--------------------------------------------------------------------------------------
using System;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using System.Drawing;

namespace HelloWorld
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Thread _processingThread;
        private PXCMSenseManager _senseManager;
        private PXCMHandModule _hand;
        private PXCMHandConfiguration _handConfig;
        private PXCMHandData _handData;
        private PXCMHandData.GestureData _gestureData;
        private bool _handWaving = false;
        private bool _handTrigger = false;
        private int _msgTimer = 0;
 
        public MainWindow()
        {
            InitializeComponent();

            // Instantiate and initialize the SenseManager
            _senseManager = PXCMSenseManager.CreateInstance();
            _senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);
            _senseManager.EnableHand();
            _senseManager.Init();

            // Configure the Hand Module
            _hand = _senseManager.QueryHand();
            _handConfig = _hand.CreateActiveConfiguration();
            _handConfig.EnableGesture("wave");
            _handConfig.EnableAllAlerts();
            _handConfig.ApplyChanges();

            // Start the worker thread
            _processingThread = new Thread(new ThreadStart(ProcessingThread));
            _processingThread.Start();
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lblMessage.Content = "(Wave Your Hand)";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _processingThread.Abort();
            if (_handData != null) _handData.Dispose();
            _handConfig.Dispose();
            _senseManager.Dispose();
        }

        private void ProcessingThread()
        {
            // Start AcquireFrame/ReleaseFrame loop
            while (_senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                PXCMCapture.Sample sample = _senseManager.QuerySample();
                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;

                // Get color image data
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Retrieve gesture data
                _hand = _senseManager.QueryHand();

                if (_hand != null)
                {
                    // Retrieve the most recent processed data
                    _handData = _hand.CreateOutput();
                    _handData.Update();
                    _handWaving = _handData.IsGestureFired("wave", out _gestureData);
                }
                                
                // Update the user interface
                UpdateUI(colorBitmap);

                // Release the frame
                if (_handData != null) _handData.Dispose();
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                _senseManager.ReleaseFrame();
            }
        }

        bool _reverse = false;

        private void UpdateUI(Bitmap bitmap)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate()
            {
                if (bitmap != null)
                {
                    // Mirror the color stream Image control
                    imgColorStream.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    ScaleTransform mainTransform = new ScaleTransform();
                    mainTransform.ScaleX = _reverse ? -1 : 1;
                    mainTransform.ScaleY = 1;
                    imgColorStream.RenderTransform = mainTransform;
                    
                    // Display the color stream
                    imgColorStream.Source = ConvertBitmap.BitmapToBitmapSource(bitmap);

                    // Update the screen message
                    if (_handWaving)
                    {
                        lblMessage.Content = "Hello World!";
                        _handTrigger = true;
                    }

                    // Reset the screen message after ~50 frames
                    if (_handTrigger)
                    {
                        _msgTimer++;

                        if (_msgTimer >= 50)
                        {
                            lblMessage.Content = "(Wave Your Hand)";
                            _msgTimer = 0;
                            _handTrigger = false;
                        }
                    }
                }
            }));
        }

        private void btnReverse_Click(object sender, RoutedEventArgs e) => _reverse = !_reverse;
    }
}
