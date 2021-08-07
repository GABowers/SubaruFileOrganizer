# SubaruFileOrganizer
Small program to organize music optimally for USB playback on the Subaru "Starlink" OS.

To my understanding Starlink works best when there are as few folders as possible, and yet an individual folder should not have more than 256 files.

That's all fine, but the car has a couple of different methods of sorting/playing music: by individual songs, by albums, or by folders. And when the car starts with a USB in, it starts playing by file name (or at least, this has been my experience). As a result, playing music by album gets out of order.

This program reads all audio files from a given folder (and all subfolders) that have tag data - Album Artist, Album, Year, Track number, and Title (so these are all required). It sorts first by artist, then year and album name combined (for albums with the same name or multiple albums in a year), then disc number and track list. It adds a tracking "code" to the filenames (AA - ZZ). It will then add songs from an album to an individual folder as long as there are less than 256 songs in the folder, and room for all of the album's songs. Once that threshold is reached, it will create a new folder and repeat the process.

So just to say again: this requires that the music be tagged with Album Artist, Album, Year, Track number, and Title (Disc number will default to 0 if not set, which should be fine).

2021-08-07: Updated program to allow one to convert FLAC files to MP3s (320 bitrate).
