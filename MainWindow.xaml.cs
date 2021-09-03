using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                        ConcurrentBag<Tuple<TagLib.File, string>> allData = new ConcurrentBag<Tuple<TagLib.File, string>>();
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
                                var tag = TagLib.File.Create(item);
                                if (tag.Tag.Title != null && tag.Tag.Year != 0 && tag.Tag.Album != null && (tag.Tag.JoinedAlbumArtists != null || tag.Tag.FirstArtist != null)) // checking for any metadata at all
                                {
                                    allData.Add(new Tuple<TagLib.File, string>(tag, item));
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
                                    var tag = TagLib.File.Create(item);
                                    if (tag.Tag.Title != null && tag.Tag.Year != 0 && tag.Tag.Album != null && (tag.Tag.JoinedAlbumArtists != null || tag.Tag.FirstArtist != null)) // checking for any metadata at all
                                    {
                                        allData.Add(new Tuple<TagLib.File, string>(tag, item));
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
                            if (x.Item1.Tag.JoinedAlbumArtists != null)
                            {
                                artists.Add(x.Item1.Tag.JoinedAlbumArtists);
                            }
                            else
                            {
                                artists.Add(x.Item1.Tag.FirstArtist);
                            }
                        }
                        artists = artists.Distinct().ToList();
                        artists.Sort();
                        Dictionary<string, List<Tuple<uint, string>>> artistAlbums = new Dictionary<string, List<Tuple<uint, string>>>();
                        foreach (var artist in artists)
                        {
                            var albums = allData.Where((x) =>
                            {
                                if (x.Item1.Tag.JoinedAlbumArtists != null)
                                {
                                    if (x.Item1.Tag.JoinedAlbumArtists.Equals(artist))
                                    {
                                        return true;
                                    }
                                }
                                else
                                {
                                    if (x.Item1.Tag.FirstArtist.Equals(artist))
                                    {
                                        return true;
                                    }
                                }
                                return false;
                            }).Select(x => new Tuple<uint, string>(x.Item1.Tag.Year, x.Item1.Tag.Album)).Distinct().ToList();
                            albums = albums.OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
                            artistAlbums.Add(artist, albums);
                        }
                        int curFolder = 0;
                        int curFolderCount = 0;
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
                                    if (x.Item1.Tag.Year.Equals(artistAlbums[artists[i]][j].Item1) && x.Item1.Tag.Album.Equals(artistAlbums[artists[i]][j].Item2))
                                    {
                                        if (x.Item1.Tag.JoinedAlbumArtists != null)
                                        {
                                            if (x.Item1.Tag.JoinedAlbumArtists.Equals(artists[i]))
                                            {
                                                return true;
                                            }
                                        }
                                        else
                                        {
                                            if (x.Item1.Tag.FirstArtist.Equals(artists[i]))
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
                                songs = songs.OrderBy(x => x.Item1.Tag.Disc).ThenBy(x => x.Item1.Tag.Track).ToList();

                                for (int k = 0; k < songs.Count; k++)
                                {
                                    var item = songs[k];
                                    bool flac = false;
                                    string title = item.Item1.Tag.Title;
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
                                            var tag = new NAudio.Lame.ID3TagData
                                            {
                                                Title = item.Item1.Tag.Title,
                                                Track = item.Item1.Tag.Track.ToString(),
                                                Album = item.Item1.Tag.Album,
                                                AlbumArtist = item.Item1.Tag.FirstAlbumArtist,
                                                Artist = item.Item1.Tag.FirstArtist,
                                                Year = item.Item1.Tag.Year.ToString(),
                                                Genre = item.Item1.Tag.JoinedGenres,
                                                Comment = item.Item1.Tag.Comment,
                                                Subtitle = item.Item1.Tag.Subtitle
                                            };
                                            if (item.Item1.Tag.Pictures.Length > 0)
                                            {
                                                tag.AlbumArt = item.Item1.Tag.Pictures.First().Data.Data;
                                            }
                                            using (var reader = new AudioFileReader(item.Item2))
                                            {
                                                using (var writer = new NAudio.Lame.LameMP3FileWriter(fullName, reader.WaveFormat, 320, tag))
                                                {
                                                    reader.CopyTo(writer);
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
