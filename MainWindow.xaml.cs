//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Net.Sockets;
    using System.Net;
    using System;
    using System.Globalization;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        // @Kinect udpclient
        private UdpClient udpClient;

        // [minX, maxX, minY, maxY, minZ, maxZ]
        // keep track of the biggest value we have seen, and use it to offset 0,0,0 by the average point of the max pairs
        private float[] minMaxXYZ = { 0, 0, 0, 0, 1000, 0 };

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }
        
        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Prep UDP Client
            this.udpClient = new UdpClient("127.0.0.1", 4242);

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        private void processMaxPosition(float X, float Y, float Z)
        {
            if (X < minMaxXYZ[0]) { minMaxXYZ[0] = X; }
            if (X > minMaxXYZ[1]) { minMaxXYZ[1] = X; }
            if (Y < minMaxXYZ[2]) { minMaxXYZ[2] = Y; }
            if (Y > minMaxXYZ[3]) { minMaxXYZ[3] = Y; }
            if (Z < minMaxXYZ[4]) { minMaxXYZ[4] = Z; }
            if (Z > minMaxXYZ[5]) { minMaxXYZ[5] = Z; }
        }

        // calculate center
        private float averageX() { return (minMaxXYZ[0] + minMaxXYZ[1]) / 2; }
        private float averageY() { return (minMaxXYZ[2] + minMaxXYZ[3]) / 2; }
        private float averageZ() { return (minMaxXYZ[4] + minMaxXYZ[5]) / 2; }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // stream to free track (but start off with the console text!
            // 

            // OpenTrack Settings
            // 
            // Filter: Accela
            // Low sensitivity and smoothing seems to be the key to reducing latency.
            // * General Smoothing: 0
            // * Rotation Sens: 0.17deg
            // * Deadzone: 0.03deg
            // * Position Sens: 0.07m
            // * Deadzone: 0.1mm
            //
            // Options
            // * Relative Translation: Disable
            // * Invert Yaw/Pitch/Roll
            // * 

            // mapping had to be changed to scale amounts

            // Get head joint
            Joint head = skeleton.Joints[JointType.Head];
            SkeletonPoint headPoint = head.Position;

            // Get raw XYZ position
            float X = headPoint.X;
            float Y = headPoint.Y;
            float Z = headPoint.Z;

            // auto detect min/max x,y,z reached for this runtime
            this.processMaxPosition(X, Y, Z);

            // positional scale
            float MX = 30;
            float MY = 30;
            float MZ = 30;

            // offset (should rather happen before we scale, but we can fix that later :P)
            float DX = 0;
            float DY = 0;
            float DZ = 50;

            // offset your current raw position by the average of the extreme (min/max) positions reached in the duration of the runtime
            // center is the average of the extreme recorded positions, normalized is the offset from that center
            double normalizedX = (X - averageX()) * MX - DX;
            double normalizedY = (Y - averageY()) * MY - DY;
            double normalizedZ = (Z - averageZ()) * MZ - DZ;

            // Orientation
            String statusText = "orientation: ";
            Vector4 headQuaternion = skeleton.BoneOrientations[JointType.Head].AbsoluteRotation.Quaternion;

            float qW = headQuaternion.W;
            float qX = headQuaternion.X;
            float qY = headQuaternion.Y;
            float qZ = headQuaternion.Z;
            statusText += "(QUAT) W: " + qW.ToString("F1");
            statusText += ", X: " + qX.ToString("F1");
            statusText += ", Y: " + qY.ToString("F1");
            statusText += ", Z: " + qZ.ToString("F1");

            // to euler. ty http://www.dreamincode.net/forums/topic/349917-convert-from-quaternion-to-euler-angles-vector3/page__view__findpost__p__2028742
            double radY = (double)Math.Atan2(2 * (qW * qY + qX * qZ), 1 - 2 * (Math.Pow(qY, 2) + Math.Pow(qX, 2)));
            double radX = (double)Math.Asin(2 * (qW * qX - qZ * qY));
            double radZ = (double)Math.Atan2(2 * (qW * qZ + qY * qX), 1 - 2 * (Math.Pow(qX, 2) + Math.Pow(qZ, 2)));

            // normalize Y rotate. goes up to 3 when turning left to face the camera. then BAM -3 and falling after you pass halfway...
            // this will invert that range and have the left rotation past the camera continue going up to 2xPI
            if(radY < 0)
            {
                radY = 2 * Math.PI + radY;
            }

            double radScale = 25;
            double radXScale = 25;

            radX = radX * radXScale;
            radY = radY * radScale;
            radZ = radZ * radScale;


            statusText += "(RAD) X: " + radX.ToString("F1");
            statusText += ", Y: " + radY.ToString("F5");
            statusText += ", Z: " + radZ.ToString("F1");





            // Generate UDP packet byte buffer of position data
            MemoryStream byteBuffer = new MemoryStream(48);
            // position
            byte[] txBytes = BitConverter.GetBytes(normalizedX); 
            byte[] tyBytes = BitConverter.GetBytes(normalizedY);
            byte[] tzBytes = BitConverter.GetBytes(normalizedZ);
            byteBuffer.Write(txBytes, 0, txBytes.Length);
            byteBuffer.Write(tyBytes, 0, tyBytes.Length);
            byteBuffer.Write(tzBytes, 0, tzBytes.Length);
            // // dont serve meaningful pitch/yaw/rotate, phone orientation takes care of that
            //byte[] zero = BitConverter.GetBytes(0);
            // rotation
            byte[] tYaw = BitConverter.GetBytes(radY);
            byte[] tPitch = BitConverter.GetBytes(radX);
            byte[] tRoll = BitConverter.GetBytes(radZ);
            byteBuffer.Write(tYaw, 0, tYaw.Length);
            byteBuffer.Write(tPitch, 0, tPitch.Length);
            byteBuffer.Write(tRoll, 0, tRoll.Length);

            this.udpClient.Send(byteBuffer.ToArray(), (int)byteBuffer.Length);

            int memberTotal = txBytes.Length + tyBytes.Length + tzBytes.Length + tYaw.Length + tPitch.Length + tRoll.Length;

            statusText += ", " + memberTotal.ToString();
            this.statusBarText.Text = statusText;
            //this.statusBarText.Text = X.ToString("F3") + ", " + Y.ToString("F3") + ", " + Z.ToString("F3") + " :: " + minMaxXYZ[0].ToString("F3") + ", " + minMaxXYZ[1].ToString("F3") + ", " + minMaxXYZ[2].ToString("F3") + ", " + minMaxXYZ[3].ToString("F3") + ", " + minMaxXYZ[4].ToString("F3") + ", " + minMaxXYZ[5].ToString("F3") + " :: " + normalizedX.ToString("F3") + ", " + normalizedY.ToString("F3") + ", " + normalizedZ.ToString("F3");




            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
 

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
    }
}