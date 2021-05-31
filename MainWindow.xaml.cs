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
            var input = inputLabel.Text;
            var output = outputLabel.Text;
            if(!Directory.Exists(output))
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
                    
                    ConcurrentBag< Tuple<TagLib.File, string>> allData = new ConcurrentBag<Tuple<TagLib.File, string>>();
                    var all = Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories).ToList();
                    this.Dispatcher.Invoke(() =>
                    {
                        totalLabel.Content = all.Count;
                    });
                    int cur = 0;
                    Timer t = new Timer() { Interval = 100 };
                    t.Elapsed += ((object sender2, ElapsedEventArgs e2) =>
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            curLabel.Content = cur;
                        }));
                    });
                    t.Start();
                    Parallel.ForEach(all, (item) =>
                    {
                        try
                        {
                            AudioFileReader afr = new AudioFileReader(item);
                            var tag = TagLib.File.Create(item);
                            if(tag.Tag.Title != null && tag.Tag.Year != 0 && tag.Tag.Album != null && (tag.Tag.JoinedAlbumArtists != null || tag.Tag.FirstArtist != null)) // checking for any metadata at all
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
                        cur++;
                    });
                    t.Stop();
                    cur = 0;
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
                        albums.Sort();
                        artistAlbums.Add(artist, albums);
                    }
                    int curFolder = 0;
                    int curFolderCount = 0;
                    t.Start();
                    this.Dispatcher.Invoke(() =>
                    {
                        descriptionLabel.Content = "Copying files: ";
                        totalLabel.Content = allData.Count;
                        SetFullAccessPermissionsForEveryone(output);
                        Directory.CreateDirectory(output + "/" + curFolderCount.ToString());
                    });
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
                            if((curFolder + songs.Count) > 256)
                            {
                                curFolderCount++;
                                curFolder = 0;
                                if(songs.Count > 256)
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
                            Parallel.For(0, songs.Count, (k) =>
                            {
                                var item = songs[k];
                                string title = item.Item1.Tag.Title;
                                this.Dispatcher.Invoke(() =>
                                {
                                    foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                                    {
                                        title = title.Replace(c, '_');
                                    }
                                });
                                string newName = i + "-" + j + " " + item.Item1.Tag.Disc + "-" + item.Item1.Tag.Track + " " + title + "." + item.Item2.Split('.').Last();
                                string fullName = output + "/" + curFolderCount + "/" + newName;
                                this.Dispatcher.Invoke(() =>
                                {
                                    if(!File.Exists(fullName))
                                    {
                                        File.Copy(songs[k].Item2, fullName);
                                    }
                                    else
                                    {
                                        PrintLog(item.Item2, "File exists", "Writing");
                                    }
                                });
                                cur++;
                                curFolder++;
                            });
                        }
                    }
                    t.Stop();
                    this.Dispatcher.Invoke(() =>
                    {
                        inputButton.IsEnabled = true;
                        outputButton.IsEnabled = true;
                        start.IsEnabled = true;
                        inputLabel.IsEnabled = true;
                        outputLabel.IsEnabled = true;
                    });
                });
            }
        }

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
            //Everyone Identity
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
