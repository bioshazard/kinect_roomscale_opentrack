# kinect_roomscale_opentrack

Pulls X,Y,Z position from Kinect and submits as UDP for OpenTrack. Based on [SkeletonBasics-WPF](https://msdn.microsoft.com/en-us/library/hh855381.aspx). Just hijacked the bone render function, dumped the head point X,Y,Z,Orientation to OpenTrack over UDP.

## Usage

- Connect Xbox 360 Kinect to PC
- Install Kinect drivers
- Start OpenTrack (TODO: link to my configuration in wiki)
- Run `bin/Debug/SkeletonBasics-WPF.exe`
- Walk your area perimeter, squat, and jump to average the room center
