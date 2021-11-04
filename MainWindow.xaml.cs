using ATL;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SubaruFileOrganizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Threading.SynchronizationContext _uiContext = System.Threading.SynchronizationContext.Current;
        public ObservableCollection<BindingClass> Log { get; set; }
        int curDone = 0;
        ScrollViewer scroller;
        public MainWindow()
        {
            Log = new ObservableCollection<BindingClass>();
            InitializeComponent();
            outputLog.ItemsSource = Log;
            if (outputLog.Items.Count > 0)
            {
                var border = VisualTreeHelper.GetChild(outputLog, 0) as Decorator;
                if (border != null)
                {
                    scroller = border.Child as ScrollViewer;
                }
            }
            //mainThread = System.Threading.Thread.CurrentThread.tas
        }

        private void inputButton_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    inputLabel.Text = fbd.SelectedPath;
                }
            }
        }

        private void outputButton_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    outputLabel.Text = fbd.SelectedPath;
                }
            }
        }

        private void start_Click(object sender, RoutedEventArgs e)
        {
            this.TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo() { ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal };
            var input = inputLabel.Text;
            var output = outputLabel.Text;
            var checkedFLAC = flacCheck.IsChecked.Value;
            if (!Directory.Exists(output))
            {
                var ou = Directory.CreateDirectory(output);
            }
            if (Directory.Exists(input))
            {
                inputButton.IsEnabled = false;
                outputButton.IsEnabled = false;
                start.IsEnabled = false;
                inputLabel.IsEnabled = false;
                outputLabel.IsEnabled = false;
                Task.Run(() =>
                {
                    try
                    {
                        ConcurrentBag<Tuple<Track, string>> allData = new ConcurrentBag<Tuple<Track, string>>();
                        var all = Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories).ToList();
                        var total = all.Count;
                        this.Dispatcher.Invoke(() =>
                        {
                            totalLabel.Content = total;
                        });
                        curDone = 0;
                        Timer t = new Timer() { Interval = 100 };
                        t.Elapsed += ((object sender2, ElapsedEventArgs e2) =>
                        {
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                curLabel.Content = curDone;
                                if (curDone == total)
                                {
                                    t.Stop();
                                }
                            }));
                        });
                        t.Start();
                        Parallel.ForEach(all, (item) =>
                        {
                            try
                            {
                                AudioFileReader afr = new AudioFileReader(item);
                                var tag = new Track(item);
                                if (tag.Title != null && tag.Year != 0 && tag.Album != null && (String.IsNullOrWhiteSpace(tag.AlbumArtist)== false || String.IsNullOrWhiteSpace(tag.Artist) == false)) // checking for any metadata at all
                                {
                                    allData.Add(new Tuple<Track, string>(tag, item));
                                }
                                else
                                {
                                    PrintLog(item, "Missing metadata", "Reading");
                                }
                            }
                            catch (Exception ee)
                            {
                                if (item.Contains(".mp3"))
                                {
                                    var tag = new Track(item);
                                    if (tag.Title != null && tag.Year != 0 && tag.Album != null && (String.IsNullOrWhiteSpace(tag.AlbumArtist) == false || String.IsNullOrWhiteSpace(tag.Artist) == false)) // checking for any metadata at all
                                    {
                                        allData.Add(new Tuple<Track, string>(tag, item));
                                    }
                                    else
                                    {
                                        PrintLog(item, "Missing metadata", "Reading");
                                    }
                                }
                                else
                                {
                                    PrintLog(item, "Failed to parse as audio", "Reading");
                                }
                            }
                            curDone++;
                            this.Dispatcher.Invoke(() =>
                            {
                                this.TaskbarItemInfo.ProgressValue = (double)curDone / total;
                            });
                        });
                        t.Stop();
                        curDone = 0;
                        List<string> artists = new List<string>();
                        foreach (var x in allData)
                        {
                            if (String.IsNullOrWhiteSpace(x.Item1.AlbumArtist) == false)
                            {
                                artists.Add(x.Item1.AlbumArtist);
                            }
                            else
                            {
                                artists.Add(x.Item1.Artist);
                            }
                        }
                        artists = artists.Distinct().ToList();
                        artists.Sort();
                        Dictionary<string, List<DateTime>> artistAlbums = new Dictionary<string, List<DateTime>>();
                        foreach (var artist in artists)
                        {
                            var artistTracks = allData.Where((x) =>
                            {
                                if (String.IsNullOrWhiteSpace(x.Item1.AlbumArtist) == false)
                                {
                                    if (x.Item1.AlbumArtist.Equals(artist))
                                    {
                                        return true;
                                    }
                                }
                                else
                                {
                                    if (x.Item1.Artist.Equals(artist))
                                    {
                                        return true;
                                    }
                                }
                                return false;
                            }).ToList();
                            var years = artistTracks.Select(x => x.Item1.Year).Distinct().ToList();
                            for (int i = 0; i < years.Count; i++)
                            {
                                var albumsInYear = artistTracks.Where(x => x.Item1.Year == years[i]).Select(x => x.Item1.Album).Distinct().ToList();
                                var anyMin = artistTracks.Where(x => x.Item1.Year == years[i] && albumsInYear.Contains(x.Item1.Album)).Any(x => x.Item1.Date == DateTime.MinValue);
                                var albumDates = artistTracks.Where(x => x.Item1.Year == years[i] && albumsInYear.Contains(x.Item1.Album)).GroupBy(x => x.Item1.Album).Select(groups => groups.First()).Select(xx => xx.Item1.Date).ToList();
                                bool notSame = albumDates.Distinct().Count() != albumDates.Count;
                                if (notSame)
                                {
                                    ;
                                }
                                // if ANY are bad, we have to redo them all...
                                if (albumsInYear.Count > 1 && (anyMin || notSame))
                                {
                                    for (int j = 0; j < albumsInYear.Count; j++)
                                    {
                                        var prop = j / albumsInYear.Count;
                                        var dayProp = (int)(365 * prop);
                                        DateTime date0 = new DateTime(years[i], 1, 1);
                                        var date = date0.AddDays(dayProp);
                                        var subTracks = artistTracks.Where(x => x.Item1.Year == years[i] && x.Item1.Album == albumsInYear[j]).ToList();
                                        foreach (var item in subTracks)
                                        {
                                            allData.Where(x => x.Item1 == item.Item1).FirstOrDefault().Item1.Date = date;
                                        }
                                    }
                                }
                                for (int j = 0; j < albumsInYear.Count; j++)
                                {
                                    var subTracks = artistTracks.Where(x => x.Item1.Year == years[i] && x.Item1.Album == albumsInYear[j]).ToList();
                                    var dates = subTracks.Select(x => x.Item1.Date).Distinct().ToList();
                                    if (dates.Count > 1)
                                    {
                                        dates.Sort();
                                        foreach (var item in subTracks)
                                        {
                                            allData.Where(x => x == item).FirstOrDefault().Item1.Date = dates.First();
                                        }

                                    }
                                }
                            }

                            var albums = artistTracks.Select(x => x.Item1.Date).Distinct().ToList();
                            //albums = albums.OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
                            artistAlbums.Add(artist, albums);
                        }
                        int curFolder = 0;
                        int curFolderCount = 1;
                        total = allData.Count;
                        t.Start();
                        this.Dispatcher.Invoke(() =>
                        {
                            descriptionLabel.Content = "Copying files: ";
                            totalLabel.Content = total;
                            SetFullAccessPermissionsForEveryone(output);
                            Directory.CreateDirectory(output + "/" + curFolderCount.ToString());
                        });
                        Random rng = new Random();
                        for (int i = 0; i < artists.Count; i++)
                        {
                            for (int j = 0; j < artistAlbums[artists[i]].Count; j++)
                            {
                                var songs = allData.Where(x =>
                                {
                                    if (x.Item1.Date.Equals(artistAlbums[artists[i]][j]))
                                    {
                                        if (String.IsNullOrWhiteSpace(x.Item1.AlbumArtist) == false)
                                        {
                                            if (x.Item1.AlbumArtist.Equals(artists[i]))
                                            {
                                                return true;
                                            }
                                        }
                                        else
                                        {
                                            if (x.Item1.Artist.Equals(artists[i]))
                                            {
                                                return true;
                                            }
                                        }
                                        return false;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }).ToList();
                                if ((curFolder + songs.Count) > 256)
                                {
                                    curFolderCount++;
                                    curFolder = 0;
                                    if (songs.Count > 256)
                                    {
                                        foreach (var item in songs)
                                        {
                                            PrintLog(item.Item2, "Album too large - handle personally", "Writing");
                                        }
                                        continue;
                                    }
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        Directory.CreateDirectory(output + "/" + curFolderCount.ToString());
                                    });
                                }
                                songs = songs.OrderBy(x => x.Item1.DiscNumber).ThenBy(x => x.Item1.TrackNumber).ToList();

                                for (int k = 0; k < songs.Count; k++)
                                {
                                    var item = songs[k];
                                    bool flac = false;
                                    string title = item.Item1.Title;
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                                        {
                                            title = title.Replace(c, '_');
                                        }
                                    });
                                    string newName = StringFromNumber(curFolder) + " " + title + "." + item.Item2.Split('.').Last();
                                    string fullName = output + "/" + curFolderCount + "/" + newName;
                                    if (fullName.ToLower().Contains(".flac"))
                                    {
                                        flac = true;
                                        string[] array = fullName.Split('.');
                                        fullName = String.Join(".", array.ToList().GetRange(0, array.Length - 1)) + ".mp3";
                                    }

                                    if (!File.Exists(fullName))
                                    {
                                        if (flac && checkedFLAC)
                                        {
                                            using (var reader = new AudioFileReader(item.Item2))
                                            {
                                                using (var writer = new NAudio.Lame.LameMP3FileWriter(fullName, reader.WaveFormat, 320))
                                                {
                                                    reader.CopyTo(writer);
                                                    Track newTrack = new Track(fullName);
                                                    newTrack.AdditionalFields = item.Item1.AdditionalFields;
                                                    newTrack.Album = item.Item1.Album;
                                                    newTrack.AlbumArtist = item.Item1.AlbumArtist;
                                                    newTrack.Artist = item.Item1.Artist;
                                                    newTrack.Chapters = item.Item1.Chapters;
                                                    newTrack.ChaptersTableDescription = item.Item1.ChaptersTableDescription;
                                                    newTrack.Comment = item.Item1.Comment;
                                                    newTrack.Composer = item.Item1.Composer;
                                                    newTrack.Conductor = item.Item1.Conductor;
                                                    newTrack.Copyright = item.Item1.Copyright;
                                                    newTrack.Date = item.Item1.Date;
                                                    newTrack.Description = item.Item1.Description;
                                                    newTrack.DiscNumber = item.Item1.DiscNumber;
                                                    newTrack.DiscTotal = item.Item1.DiscTotal;
                                                    newTrack.Genre = item.Item1.Genre;
                                                    newTrack.Lyrics = item.Item1.Lyrics;
                                                    newTrack.OriginalAlbum = item.Item1.OriginalAlbum;
                                                    newTrack.OriginalArtist = item.Item1.OriginalArtist;
                                                    newTrack.PictureTokens = item.Item1.PictureTokens;
                                                    newTrack.Popularity = item.Item1.Popularity;
                                                    newTrack.Publisher = item.Item1.Publisher;
                                                    newTrack.PublishingDate = item.Item1.PublishingDate;
                                                    newTrack.Title = item.Item1.Title;
                                                    newTrack.TrackNumber = item.Item1.TrackNumber;
                                                    newTrack.TrackTotal = item.Item1.TrackTotal;
                                                    newTrack.Year = item.Item1.Year;
                                                    newTrack.Save();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            File.Copy(songs[k].Item2, fullName);
                                        }
                                    }
                                    else
                                    {
                                        PrintLog(item.Item2, "File exists", "Writing");
                                    }
                                    FileAttributes attributes = File.GetAttributes(fullName);
                                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                                    {
                                        File.SetAttributes(fullName, attributes & ~FileAttributes.ReadOnly);
                                    }
                                    curDone++;
                                    curFolder++;
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        this.TaskbarItemInfo.ProgressValue = (double)curDone / total;
                                    });
                                };
                            }
                        }
                        this.Dispatcher.Invoke(() =>
                        {
                            this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
                        });
                        this.Dispatcher.Invoke(() =>
                        {
                            inputButton.IsEnabled = true;
                            outputButton.IsEnabled = true;
                            start.IsEnabled = true;
                            inputLabel.IsEnabled = true;
                            outputLabel.IsEnabled = true;
                        });
                    }
                    catch (Exception ee)
                    {
                        var stack = ee.StackTrace;
                        ;
                    }
                });
            }
        }

        string StringFromNumber(int number)
        {
            string output = "";
            int zs = number / 26;
            output += alphabet[zs];
            int inc = number % 26;
            output += alphabet[inc];
            return output;
        }

        static Dictionary<int, string> alphabet = new Dictionary<int, string>
        {
            {0, "A" },
            {1, "B" },
            {2, "C" },
            {3, "D" },
            {4, "E" },
            {5, "F" },
            {6, "G" },
            {7, "H" },
            {8, "I" },
            {9, "J" },
            {10, "K" },
            {11, "L" },
            {12, "M" },
            {13, "N" },
            {14, "O" },
            {15, "P" },
            {16, "Q" },
            {17, "R" },
            {18, "S" },
            {19, "T" },
            {20, "U" },
            {21, "V" },
            {22, "W" },
            {23, "X" },
            {24, "Y" },
            {25, "Z" },
        };

        void PrintLog(string file, string reason, string step)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Log.Add(new BindingClass() { FileName = file, Reason = reason, Step = step });
                if (scroller != null)
                {
                    scroller.ScrollToEnd();
                }
                else
                {
                    if (outputLog.Items.Count > 0)
                    {
                        var border = VisualTreeHelper.GetChild(outputLog, 0) as Decorator;
                        if (border != null)
                        {
                            scroller = border.Child as ScrollViewer;
                        }
                    }
                }
            });
        }

        public static void SetFullAccessPermissionsForEveryone(string directoryPath)
        {
            IdentityReference everyoneIdentity = new SecurityIdentifier(WellKnownSidType.WorldSid,
                                                       null);

            DirectorySecurity dir_security = Directory.GetAccessControl(directoryPath);

            FileSystemAccessRule full_access_rule = new FileSystemAccessRule(everyoneIdentity,
                            FileSystemRights.FullControl, InheritanceFlags.ContainerInherit |
                             InheritanceFlags.ObjectInherit, PropagationFlags.None,
                             AccessControlType.Allow);
            dir_security.AddAccessRule(full_access_rule);

            Directory.SetAccessControl(directoryPath, dir_security);
        }

        public class BindingClass
        {
            public string Step { get; set; }
            public string Reason { get; set; }
            public string FileName { get; set; }
        }
    }
}
