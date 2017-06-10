# kinect_roomscale_opentrack

Pulls X,Y,Z position from Kinect and submits as UDP for OpenTrack. Based on [SkeletonBasics-WPF](https://msdn.microsoft.com/en-us/library/hh855381.aspx). Just hijacked the [bone render function](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/MainWindow.xaml.cs#L283), dumped the head point X,Y,Z,Orientation to OpenTrack over UDP.

## Usage

- Connect Xbox 360 Kinect to PC (not tested on newer Xbox One Kinect)
- Install Kinect drivers
- Download and start [OpenTrack](https://github.com/opentrack/opentrack). My example configuration is detailed below.
- Run `bin/Debug/SkeletonBasics-WPF.exe` or recompile from source with Visual Studio 2015
- Walk your area perimeter, squat, and jump to average the room center
- Install [VRidge](https://riftcat.com/vridge) to your mobile device
- Install [RiftCat](https://riftcat.com/) to your PC
- Configure RiftCat settings to use Freetrack for positional tracking.

## RiftCat Tracking Options

![](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/riftcat_tracking.PNG?raw=true)

## OpenTrack Settings

### Main

![](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/opentrack_settings/opentrack_main.PNG?raw=true)

### Options

Be sure to set a button for centering OpenTrack after you calibrate your 3d Perimeter with the kinect app.

![](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/opentrack_settings/opentrack_options_shortcuts.PNG?raw=true)

### OpenTrack Profile:

You can just import this and it will apply all the position and orientation mappings

https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/opentrack_settings/opentrack_profile.ini

### Input: UDP Sender

![](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/opentrack_settings/opentrack_input_udpsender.PNG?raw=true)

### Output: Freetrack

![](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/opentrack_settings/opentrack_output_freetrack.PNG?raw=true)

### Filter: Accela

![](https://github.com/bioshazard/kinect_roomscale_opentrack/blob/master/doc/opentrack_settings/opentrack_filter_accela.PNG?raw=true)
