using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace YouTube_Downloadr
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    /// <summary>
    /// The View Model for download link combo box
    /// </summary>
    public class DownloadLinksViewModel : INotifyPropertyChanged
    {
      // used for representing the quality of the source file
      private string _title;
      public string Title
      {
        get
        {
          return _title;
        }
        set
        {
          if (value != _title)
          {
            _title = value;
            NotifyPropertyChanged("Title");
          }
        }
      }

      // stored the real download link
      private string _link;
      public string Link
      {
        get { return _link; }
        set 
        {
          if (value != _link)
          {
            _link = value;
            NotifyPropertyChanged("Link");
          }
        }
      }

      // store the file type
      private string _type;
      public string Type
      {
        get
        {
          return _type;
        }
        set
        {
          if (value != _type)
          {
            _type = value;
            NotifyPropertyChanged("Type");
          }
        }
      }

      public event PropertyChangedEventHandler PropertyChanged;
      private void NotifyPropertyChanged(String propertyName)
      {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (null != handler)
        {
          handler(this, new PropertyChangedEventArgs(propertyName));
        }
      }

    }

    // view model for download list
    public class DownloadListViewModel : INotifyPropertyChanged
    {
      // represent the downloading file
      private string _title;
      public string Title
      {
        get
        {
          return _title;
        }
        set
        {
          if (value != _title)
          {
            _title = value;
            NotifyPropertyChanged("Title");
          }
        }
      }

      // represent the downloading progress
      private int _progress;
      public int Progress
      {
        get
        {
          return _progress;
        }
        set
        {
          if (value != _progress)
          {
            _progress = value;
            NotifyPropertyChanged("Progress");
          }
        }
      }

      private int _tag;
      public int Tag
      {
        get
        {
          return _tag;
        }
        set
        {
          if (value != _tag)
          {
            _tag = value;
            NotifyPropertyChanged("Tag");
          }
        }
      }

      public event PropertyChangedEventHandler PropertyChanged;
      private void NotifyPropertyChanged(String propertyName)
      {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (null != handler)
        {
          handler(this, new PropertyChangedEventArgs(propertyName));
        }
      }
    }


    // the collection data used for combo box binding
    public ObservableCollection<DownloadLinksViewModel> Links { get; private set; }
    public ObservableCollection<DownloadListViewModel> DownloadItems { get; private set; }

    public List<Task> DownloadTasks { get; set; }

    public MainWindow()
    {
      InitializeComponent();

      // Initialize the view model and data binding
      Links = new ObservableCollection<DownloadLinksViewModel>();
      Files.ItemsSource = Links;

      DownloadItems = new ObservableCollection<DownloadListViewModel>();
      DownloadList.ItemsSource = DownloadItems;

      // initialize the task pool
      DownloadTasks = new List<Task>();

      Url.Focus();
    }

    // toggle the enabled state of controls
    private void ToggleControls(bool isEnabled)
    {
      Url.IsEnabled = Fetch.IsEnabled = Download.IsEnabled = isEnabled;
    }

    // While the fetch button is clicked
    private void Fetch_Click(object sender, RoutedEventArgs args)
    {
      var content = Url.Text;

      // clear the download links
      Links.Clear();

      ToggleControls(false);

      WebClient client = new WebClient();
      client.DownloadStringCompleted += (s, e) => 
      {
        if (e.Error == null)
        {
          var text = e.Result;

          // ready to parse the links maps
          Regex regex = new Regex("url_encoded_fmt_stream_map\": \"([^\"]*)?\"");
          MatchCollection mc = regex.Matches(text);
          if (mc.Count > 0)
          {
            Match m = mc[0];
            string streamMaps = m.Groups[1].Value;

            string[] urls = streamMaps.Split(new char[] { ',' });
            foreach (string u in urls)
            {
              string[] parameters = u.Split(new string[] { "\\u0026" }, StringSplitOptions.RemoveEmptyEntries);

              Dictionary<string, string> paramMap = new Dictionary<string, string>();

              foreach (string p in parameters)
              {
                int eqPos = p.IndexOf('=');
                paramMap.Add(p.Substring(0, eqPos), WebUtility.UrlDecode(p.Substring(eqPos + 1)));
              }

              // extract the file type
              int typePos = paramMap["type"].IndexOf(';');
              int typeLen = typePos - 6; // 6 is the length of 'video/' +1
              if (typePos == -1)  // there's no semicolon here.
              {
                typeLen = paramMap["type"].Length - 6;
              }
              string fileType = paramMap["type"].Substring(6, typeLen);
              if (fileType == "x-flv")
              {
                fileType = "flv";
              }

              // Insert a links into the view model
              Links.Add(new DownloadLinksViewModel() 
              { 
                Title = paramMap["quality"] + " (" + paramMap["type"] + ")",
                Type = fileType, 
                Link = paramMap["url"] + "&signature=" + paramMap["sig"] 
              });

            }
          }

          if (Links.Count > 0)
          {
            Files.SelectedIndex = 0;
          }

          ToggleControls(true);
        }
        else
        {
          MessageBox.Show("There's something wrong! Please try again later.");
        }
      };

      // start to fetch and parse the download links
      client.DownloadStringAsync(new Uri(content));
    }

    // while download button is clicked
    private void Download_Click(object sender, RoutedEventArgs args)
    {
      int index = Files.SelectedIndex;
      DownloadLinksViewModel item = Links[index];

      // open a SaveFileDialog to choose where to save the file
      SaveFileDialog sfd = new SaveFileDialog();
      sfd.FileName = "video." + item.Type;
      sfd.Filter = "All Files (*.*) | *.*";
      if ((bool) sfd.ShowDialog())
      {
        //ToggleControls(false);

        // determine the filename set by user.
        string fileName = sfd.FileName;
        int taskIndex = DownloadTasks.Count;
        DownloadItems.Add(new DownloadListViewModel() 
        { 
          Title = fileName,
          Progress = 0,
          Tag = taskIndex
        });


        WebClient client = new WebClient();
        client.DownloadProgressChanged += (s, e) =>
        {
          // update the progress bar
          DownloadItems[taskIndex].Progress = e.ProgressPercentage;
        };

        client.DownloadFileCompleted += (s, e) => {

        };

        // start to download file.
        Task task = client.DownloadFileTaskAsync(new Uri(Links[index].Link), fileName);
        DownloadTasks.Add(task);

        // clear the url and combobox
        Url.Text = "";
        Url.Focus();
        Links.Clear();
      }
    }

  }
}
