Showtime
========
Different language implementations have been moved to seperate Git
repositories.

Download
--------
***Option A - Individual languages:***
 - [C#](https://github.com/Mystfit/Showtime-Csharp)
 - [Java](https://github.com/Mystfit/Showtime-java])
 - [Processing](https://github.com/Mystfit/Showtime-Processing)
 - [Python](https://github.com/Mystfit/Showtime-Python)
 
**Plugins for programs**
 - [Ableton Live](https://github.com/Mystfit/Showtime-Live)


***Option B - All languages***

- Run `git clone https://github.com/Mystfit/Showtime.git`
- Run `git submodule init` followed by `git submodule update`

What is this?
-------------

Showtime is designed to let multiple programs running in multiple languages talk to each other whilst trying to cut down on the clutter required in setting up connections and discovering each other. It has been designed to help create meaningful links between programs used in live performance.

The project originated from the hassles I underwent trying to hook the music software Ableton Live up to Unity using OSC. I wrote the first version of this library using Python and C# to let Unity control Ableton Live through its underlying Python API, without needing to use any MIDI or OSC whatsoever, and that eventually evolved into the Java and Processing ports as well.

### Examples ###
Processing <-> Live  
[![Example of Showtime-Processing talking to Ableton Live](http://img.youtube.com/vi/0-5mLBCJWJk/0.jpg)](http://www.youtube.com/watch?v=0-5mLBCJWJk)  

Python <-> Live  
[![Example of Python talking to Ableton Live](http://img.youtube.com/vi/QV27wt76ZgY/0.jpg)](http://www.youtube.com/watch?v=QV27wt76ZgY)


#### Languages Supported ###
- Python
- C#
- Java
- Processing
 
### Programs Supported ###
- Ableton Live
- Unity3D
